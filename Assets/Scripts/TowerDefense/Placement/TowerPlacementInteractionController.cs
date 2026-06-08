using System;
using UnityEngine;

/// <summary>
/// `TowerPlacementInteractionController` 负责“玩家怎么进入放置流程、怎么更新流程、怎么结束流程”。
///
/// 这一轮拆它出来，目的不是为了把代码拆碎，而是为了把三个边界彻底讲清楚：
/// 1. `TowerPlacementRules` 负责回答“这里能不能放”
/// 2. `TowerPlacementVisualController` 负责回答“拖拽时看见什么”
/// 3. 本控制器负责回答“玩家当前正处于哪一个放置交互阶段”
///
/// 这样后面如果要继续改：
/// - 点击选中手感
/// - 拖拽开始时机
/// - 快速点击放置
/// - 取消部署
/// - 预览提示面板何时显示
/// 就不需要再回到 `TowerDefenseGame` 这个总导演脚本里翻整局流程。
///
/// 这里故意保持它是一个纯 C# 类，而不是新的 MonoBehaviour，原因有三个：
/// - 它没有自己的独立 GameObject 生命周期需求
/// - 它更像“交互状态机 / 流程控制器”，天然适合由总控显式创建与注入依赖
/// - 纯 C# 类更容易继续单测或做后续拆分
/// </summary>
public sealed class TowerPlacementInteractionController
{
    /// <summary>
    /// 用一个专门的 delegate 把“放置规则校验”边界显式写出来。
    ///
    /// 这里不用 `Func<..., out ...>` 这种不直观的签名，
    /// 是为了让调用方一眼看懂：规则层既会返回是否合法，也会给出失败原因。
    /// </summary>
    public delegate bool PlacementValidator(Vector3 worldPosition, TowerType towerType, out string invalidReason);

    private readonly Func<bool> _isGameOverQuery;
    private readonly Func<int> _currentScrapQuery;
    private readonly Func<TowerType, bool> _canAffordTower;
    private readonly Func<TowerType, GameObject> _getPrototype;
    private readonly Func<TowerType, string> _getTowerDisplayName;
    private readonly Func<Vector2, Vector3> _screenToWorldPosition;
    private readonly PlacementValidator _validatePlacementPosition;
    private readonly Func<TowerType, Bounds> _getPlacementOverlayWorldBounds;
    private readonly Func<TowerType, Func<Vector3, bool>> _buildPlacementOverlayValidator;
    private readonly Func<Vector3, TowerType, bool> _tryPlaceTowerAt;
    private readonly Action _refreshHud;
    private readonly Action<string> _setStatusMessage;
    private readonly Action<string> _logPlacementDiagnostic;

    private TowerPlacementVisualController _placementVisualController;
    private TowerDefenseHudPresenter _hudPresenter;
    private TowerCatalog _towerCatalog;

    /// <summary>
    /// 这些字段原本都堆在 `TowerDefenseGame` 里。
    ///
    /// 现在把它们挪到这里，等于明确宣布：
    /// 它们属于“放置交互流程状态”，而不是“整局游戏全局状态”。
    /// </summary>
    private TowerType _selectedTowerType = TowerType.None;
    private bool _isPlacementDragActive;
    private TowerType _dragTowerType = TowerType.None;
    private Vector3 _previewWorldPosition;
    private bool _previewPositionIsValid;
    private string _previewInvalidReason = string.Empty;

    public TowerPlacementInteractionController(
        Func<bool> isGameOverQuery,
        Func<int> currentScrapQuery,
        Func<TowerType, bool> canAffordTower,
        Func<TowerType, GameObject> getPrototype,
        Func<TowerType, string> getTowerDisplayName,
        Func<Vector2, Vector3> screenToWorldPosition,
        PlacementValidator validatePlacementPosition,
        Func<TowerType, Bounds> getPlacementOverlayWorldBounds,
        Func<TowerType, Func<Vector3, bool>> buildPlacementOverlayValidator,
        Func<Vector3, TowerType, bool> tryPlaceTowerAt,
        Action refreshHud,
        Action<string> setStatusMessage,
        Action<string> logPlacementDiagnostic)
    {
        _isGameOverQuery = isGameOverQuery;
        _currentScrapQuery = currentScrapQuery;
        _canAffordTower = canAffordTower;
        _getPrototype = getPrototype;
        _getTowerDisplayName = getTowerDisplayName;
        _screenToWorldPosition = screenToWorldPosition;
        _validatePlacementPosition = validatePlacementPosition;
        _getPlacementOverlayWorldBounds = getPlacementOverlayWorldBounds;
        _buildPlacementOverlayValidator = buildPlacementOverlayValidator;
        _tryPlaceTowerAt = tryPlaceTowerAt;
        _refreshHud = refreshHud;
        _setStatusMessage = setStatusMessage;
        _logPlacementDiagnostic = logPlacementDiagnostic;
    }

    /// <summary>
    /// 这一步专门绑定“展示层依赖”。
    ///
    /// 之所以不放进构造函数，是因为：
    /// - `TowerPlacementVisualController` 是运行时初始化后才创建好的
    /// - HUD presenter 也依赖场景引用与运行时装配
    /// 所以这组对象比规则 / 数据 / 回调更晚就绪。
    /// </summary>
    public void BindPresentation(
        TowerPlacementVisualController placementVisualController,
        TowerDefenseHudPresenter hudPresenter,
        TowerCatalog towerCatalog)
    {
        _placementVisualController = placementVisualController;
        _hudPresenter = hudPresenter;
        _towerCatalog = towerCatalog;
    }

    public TowerType SelectedTowerType => _selectedTowerType;
    public bool IsPlacementDragActive => _isPlacementDragActive;
    public TowerType DragTowerType => _dragTowerType;

    /// <summary>
    /// 让 HUD 继续只读“交互流程快照”，而不是反向依赖控制器内部细节。
    /// </summary>
    public TowerDragPreviewState CurrentDragPreviewState =>
        new TowerDragPreviewState(_dragTowerType, _previewPositionIsValid, _previewInvalidReason);

    public void SelectRelayTower()
    {
        SelectTower(TowerType.Relay);
    }

    public void SelectDefenseTower()
    {
        SelectTower(TowerType.SingleTarget);
    }

    public void SelectSingleTargetTower()
    {
        SelectTower(TowerType.SingleTarget);
    }

    public void SelectSlowFieldTower()
    {
        SelectTower(TowerType.SlowField);
    }

    public void SelectBombardTower()
    {
        SelectTower(TowerType.Bombard);
    }

    /// <summary>
    /// `ClearSelection()` 的职责是“把整个部署交互流退回空闲态”。
    ///
    /// 它不仅仅是取消拖拽，
    /// 还要把“当前选中了什么塔型”一起清掉。
    /// </summary>
    public void ClearSelection()
    {
        CancelPlacementDragInternal();
        SelectTower(TowerType.None);
    }

    public void CancelPlacementDrag()
    {
        CancelPlacementDragInternal();
        _refreshHud?.Invoke();
    }

    /// <summary>
    /// 给外部的“点击地图快速放置”入口用。
    ///
    /// 这里把原来总控里“当前有无选中塔型 / 当前是否拖拽中”的交互前置判断一起收进来，
    /// 让总控的 `Update()` 不用再知道这么多放置态细节。
    /// </summary>
    public bool TryQuickPlacementAt(Vector3 worldPosition)
    {
        if (_isGameOverQuery != null && _isGameOverQuery())
        {
            return false;
        }

        if (_isPlacementDragActive || _selectedTowerType == TowerType.None)
        {
            return false;
        }

        return _tryPlaceTowerAt != null && _tryPlaceTowerAt(worldPosition, _selectedTowerType);
    }

    /// <summary>
    /// 悬停部署卡时的预热也收口到交互控制器里。
    ///
    /// 这样 `TowerShopCard` 只需要说“我悬停了某种塔型”，
    /// 不需要知道底下到底会预热什么可视化资源。
    /// </summary>
    public void PrewarmPlacementAreaOverlay(TowerType towerType)
    {
        if (_placementVisualController == null || _getPlacementOverlayWorldBounds == null || _validatePlacementPosition == null)
        {
            return;
        }

        // 这一步除了预热合法区覆盖层，也顺手把“当前塔型的预览实例”提前准备好。
        //
        // 玩家体感上的“点卡片开始拖拽会顿一下”，往往不只来自覆盖层扫描，
        // 还可能来自第一次 `Instantiate` 预览塔。
        // 所以现在把这两件事一起前移到 hover / pointer down 阶段，
        // 真正开始拖的时候尽量只做位置更新，而不是首次建对象。
        _placementVisualController.PrewarmPlacementPreviewInstance(towerType);

        _placementVisualController.PrewarmPlacementAreaOverlay(
            towerType,
            _isGameOverQuery != null && _isGameOverQuery(),
            _getPlacementOverlayWorldBounds(towerType),
            _buildPlacementOverlayValidator != null
                ? _buildPlacementOverlayValidator(towerType)
                : (worldPosition => _validatePlacementPosition(worldPosition, towerType, out _)));
    }

    /// <summary>
    /// 真正开始拖拽部署。
    ///
    /// 这里既负责检查前置条件，也负责把“规则态 + 可视化态 + HUD 提示态”
    /// 一起切到拖拽中。
    /// </summary>
    public bool BeginPlacementDrag(TowerType towerType, Vector2 screenPosition)
    {
        if ((_isGameOverQuery != null && _isGameOverQuery()) || towerType == TowerType.None)
        {
            return false;
        }

        if (_canAffordTower != null && !_canAffordTower(towerType))
        {
            _selectedTowerType = towerType;
            _refreshHud?.Invoke();
            int currentScrap = _currentScrapQuery != null ? _currentScrapQuery() : 0;
            _setStatusMessage?.Invoke($"Not enough scrap. You currently have {currentScrap} SCRAP.");
            _logPlacementDiagnostic?.Invoke($"Begin drag rejected: insufficient scrap for {towerType}.");
            return false;
        }

        if (_getPrototype == null || _getPrototype(towerType) == null)
        {
            _setStatusMessage?.Invoke("Card prototype is missing. Check the scene setup.");
            _logPlacementDiagnostic?.Invoke($"Begin drag rejected: missing prototype for {towerType}.");
            return false;
        }

        CancelPlacementDragInternal();

        _selectedTowerType = towerType;
        _dragTowerType = towerType;
        _isPlacementDragActive = true;
        _refreshHud?.Invoke();

        Vector3 initialPreviewWorldPosition = _screenToWorldPosition != null
            ? _screenToWorldPosition(screenPosition)
            : Vector3.zero;

        _placementVisualController?.EnsurePlacementPreviewInstance(towerType, initialPreviewWorldPosition);
        _hudPresenter?.SetDragPreviewVisible(true);
        ShowPreparedPlacementAreaOverlay(towerType);
        UpdatePlacementDrag(screenPosition);
        _setStatusMessage?.Invoke("Drag the Generator or Turret into a highlighted legal area, then release to deploy.");
        _logPlacementDiagnostic?.Invoke($"Begin drag accepted: tower={towerType} screen={screenPosition} previewWorld={_previewWorldPosition} previewValid={_previewPositionIsValid} reason={_previewInvalidReason}");
        return true;
    }

    /// <summary>
    /// 拖拽过程中持续刷新预览世界坐标、合法性和提示面板。
    /// </summary>
    public void UpdatePlacementDrag(Vector2 screenPosition)
    {
        if (!_isPlacementDragActive)
        {
            return;
        }

        _previewWorldPosition = _screenToWorldPosition != null
            ? _screenToWorldPosition(screenPosition)
            : Vector3.zero;

        if (_validatePlacementPosition != null)
        {
            GameObject prototype = _getPrototype?.Invoke(_dragTowerType);
            float visualOffsetY = TowerPlacementVisualController.ComputeVisualBottomOffset(prototype);
            Vector3 validatePosition = new Vector3(_previewWorldPosition.x, _previewWorldPosition.y + visualOffsetY, _previewWorldPosition.z);
            _previewPositionIsValid = _validatePlacementPosition(validatePosition, _dragTowerType, out _previewInvalidReason);
        }
        else
        {
            _previewPositionIsValid = false;
            _previewInvalidReason = "Placement validation is unavailable.";
        }

        _placementVisualController?.SetPreviewPosition(_previewWorldPosition);
        _placementVisualController?.UpdatePlacementPreviewVisual(_previewPositionIsValid);
        _hudPresenter?.UpdateDragPreviewPanel(screenPosition, CurrentDragPreviewState, _towerCatalog);
    }

    /// <summary>
    /// 结束拖拽时，这里统一决定：
    /// - 成功落塔
    /// - 取消部署
    /// - 保留失败原因提示
    /// </summary>
    public void EndPlacementDrag(Vector2 screenPosition, bool releasedOverUserInterface)
    {
        if (!_isPlacementDragActive)
        {
            return;
        }

        UpdatePlacementDrag(screenPosition);

        TowerType towerType = _dragTowerType;
        Vector3 worldPosition = _previewWorldPosition;
        bool canPlace = _previewPositionIsValid && !releasedOverUserInterface;
        string invalidReason = _previewInvalidReason;

        _logPlacementDiagnostic?.Invoke($"End drag: tower={towerType} screen={screenPosition} world={worldPosition} previewValid={_previewPositionIsValid} releasedOverUi={releasedOverUserInterface} reason={invalidReason}");

        CancelPlacementDragInternal();

        if (canPlace)
        {
            _tryPlaceTowerAt?.Invoke(worldPosition, towerType);
            return;
        }

        _refreshHud?.Invoke();

        if (releasedOverUserInterface)
        {
            _setStatusMessage?.Invoke("Deployment cancelled.");
        }
        else if (!string.IsNullOrEmpty(invalidReason))
        {
            _setStatusMessage?.Invoke(invalidReason);
        }
    }

    /// <summary>
    /// 供总控在进入 Game Over 或别的强制切态时调用。
    ///
    /// 它和 `CancelPlacementDrag()` 的区别是：
    /// - 这里是内部/强制流程切换
    /// - 外部不需要额外刷新一次 HUD 文案时，可直接走它
    /// </summary>
    public void ForceCancelPlacementDrag()
    {
        CancelPlacementDragInternal();
    }

    public void SetSelectionSilently(TowerType towerType)
    {
        _selectedTowerType = towerType;
    }

    private void SelectTower(TowerType towerType)
    {
        if (_isGameOverQuery != null && _isGameOverQuery())
        {
            return;
        }

        _selectedTowerType = towerType;

        if (towerType == TowerType.None)
        {
            _setStatusMessage?.Invoke("Deployment selection cleared.");
        }
        else
        {
            string towerDisplayName = _getTowerDisplayName != null ? _getTowerDisplayName(towerType) : towerType.ToString();
            _setStatusMessage?.Invoke($"Selected: {towerDisplayName}. Drag the card to preview exact legal areas.");
            PrewarmPlacementAreaOverlay(towerType);
        }

        _refreshHud?.Invoke();
    }

    private void ShowPreparedPlacementAreaOverlay(TowerType towerType)
    {
        if (_placementVisualController == null || _getPlacementOverlayWorldBounds == null || _validatePlacementPosition == null)
        {
            return;
        }

        _placementVisualController.ShowPreparedPlacementAreaOverlay(
            towerType,
            _getPlacementOverlayWorldBounds(towerType),
            _buildPlacementOverlayValidator != null
                ? _buildPlacementOverlayValidator(towerType)
                : (worldPosition => _validatePlacementPosition(worldPosition, towerType, out _)));
    }

    private void CancelPlacementDragInternal()
    {
        _isPlacementDragActive = false;
        _dragTowerType = TowerType.None;
        _previewPositionIsValid = false;
        _previewInvalidReason = string.Empty;
        _hudPresenter?.SetDragPreviewVisible(false);
        _placementVisualController?.HidePlacementAreaOverlay();
        _placementVisualController?.DeactivatePlacementPreview();
    }
}
