using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// `HudNoticeTone` 描述一条 HUD 反馈在视觉上应该偏向什么语气。
///
/// 这样做的意义是：
/// - 玩法层继续只关心“发生了什么事”
/// - HUD 层可以根据语气统一决定颜色层级
///
/// 以后如果要把同一套事件再接到别的 UI 元素上，
/// 也能复用这一层“语气信息”，而不是重新猜每句文字该染成什么色。
/// </summary>
public enum HudNoticeTone
{
    Auto,
    Neutral,
    Positive,
    Spending,
    Warning,
    Danger
}

/// <summary>
/// `HudNoticeEntry` 是一条可被 HUD 展示的反馈记录。
///
/// 它同时带上：
/// - 文案本身
/// - 建议的视觉语气
///
/// 这让“事件是什么”和“怎么显示它”之间仍然保持一个很轻的解耦层。
/// </summary>
public readonly struct HudNoticeEntry
{
    public HudNoticeEntry(string message, HudNoticeTone tone)
    {
        Message = message ?? string.Empty;
        Tone = ResolveTone(Message, tone);
    }

    public string Message { get; }

    public HudNoticeTone Tone { get; }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    private static HudNoticeTone ResolveTone(string message, HudNoticeTone requestedTone)
    {
        if (requestedTone != HudNoticeTone.Auto)
        {
            return requestedTone;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return HudNoticeTone.Neutral;
        }

        if (message.StartsWith("+", StringComparison.Ordinal))
        {
            return HudNoticeTone.Positive;
        }

        if (message.StartsWith("-", StringComparison.Ordinal))
        {
            return HudNoticeTone.Spending;
        }

        string normalized = message.ToLowerInvariant();
        if (normalized.Contains("offline") || normalized.Contains("failed") || normalized.Contains("depleted"))
        {
            return HudNoticeTone.Danger;
        }

        if (normalized.Contains("blocked") || normalized.Contains("warning") || normalized.Contains("incoming"))
        {
            return HudNoticeTone.Warning;
        }

        return HudNoticeTone.Neutral;
    }
}

/// <summary>
/// `TowerDefenseHudState` 是 HUD 每次刷新时真正需要的最小状态快照。
///
/// 这里刻意不把整个 `TowerDefenseGame` 直接暴露给 HUD，
/// 而是只传表现层真正关心的结果：
/// - 当前资源。
/// - 当前基地血量。
/// - 当前波次。
/// - 当前选中了什么建筑。
/// - 当前是否正在拖拽部署。
///
/// 这样做的核心好处是：
/// HUD 只依赖“结果”，不依赖总控内部的实现细节。
/// 以后即使玩法代码继续拆分，HUD 也更容易保持稳定。
/// </summary>
public readonly struct TowerDefenseHudState
{
    public TowerDefenseHudState(
        int currentScrap,
        int currentBaseHealth,
        int currentWave,
        int totalWaves,
        TowerType selectedTowerType,
        bool isPlacementDragActive,
        TowerType dragTowerType,
        PlacedStructureHudState placedStructureState,
        PowerGridHudSnapshot powerGridSnapshot,
        string currentStatusMessage,
        HudNoticeEntry transientNotice,
        HudNoticeEntry[] recentHudNotices)
    {
        CurrentScrap = currentScrap;
        CurrentBaseHealth = currentBaseHealth;
        CurrentWave = currentWave;
        TotalWaves = totalWaves;
        SelectedTowerType = selectedTowerType;
        IsPlacementDragActive = isPlacementDragActive;
        DragTowerType = dragTowerType;
        PlacedStructureState = placedStructureState;
        PowerGridSnapshot = powerGridSnapshot;
        CurrentStatusMessage = currentStatusMessage ?? string.Empty;
        TransientNotice = transientNotice;
        RecentHudNotices = recentHudNotices ?? Array.Empty<HudNoticeEntry>();
    }

    public int CurrentScrap { get; }

    public int CurrentBaseHealth { get; }

    public int CurrentWave { get; }

    public int TotalWaves { get; }

    public TowerType SelectedTowerType { get; }

    public bool IsPlacementDragActive { get; }

    public TowerType DragTowerType { get; }

    public PlacedStructureHudState PlacedStructureState { get; }

    public PowerGridHudSnapshot PowerGridSnapshot { get; }

    public string CurrentStatusMessage { get; }

    public HudNoticeEntry TransientNotice { get; }

    public HudNoticeEntry[] RecentHudNotices { get; }
}

public readonly struct PlacedStructureHudState
{
    public PlacedStructureHudState(bool hasSelection, string title, string details)
    {
        HasSelection = hasSelection;
        Title = title ?? string.Empty;
        Details = details ?? string.Empty;
    }

    public bool HasSelection { get; }
    public string Title { get; }
    public string Details { get; }
}

/// <summary>
/// `TowerDragPreviewState` 是拖拽提示面板需要看到的局部状态。
///
/// 它只关心三件事：
/// - 现在拖的是哪种建筑。
/// - 当前鼠标落点是否合法。
/// - 如果不合法，失败原因是什么。
///
/// 这份状态之所以单独拆出来，
/// 是因为拖拽提示刷新频率很高，没必要每次都整包带上完整 HUD 状态。
/// </summary>
public readonly struct TowerDragPreviewState
{
    public TowerDragPreviewState(TowerType towerType, bool isValid, string invalidReason)
    {
        TowerType = towerType;
        IsValid = isValid;
        InvalidReason = invalidReason ?? string.Empty;
    }

    public TowerType TowerType { get; }

    public bool IsValid { get; }

    public string InvalidReason { get; }
}

/// <summary>
/// `TowerDefenseHudTheme` 是 HUD 运行时使用的轻量样式快照。
///
/// 这里刻意不把整个 UI 样式系统做得很重，
/// 而是先把当前最常改、最容易写死在代码里的颜色入口收口起来。
/// 这样后续你替换正式美术时：
/// - 可以继续在 Scene 里改布局
/// - 也可以通过 Inspector 改这一层的语义配色
/// - 不需要去 Presenter 里翻很多硬编码字符串
/// </summary>
public readonly struct TowerDefenseHudTheme
{
    public TowerDefenseHudTheme(
        Color metricLabelColor,
        Color scrapValueColor,
        Color baseValueColor,
        Color waveValueColor,
        Color cardTextColor,
        Color secondaryInfoColor,
        Color statusTextColor,
        Color neutralNoticeColor,
        Color positiveNoticeColor,
        Color spendingNoticeColor,
        Color warningNoticeColor,
        Color dangerNoticeColor,
        Color dragPreviewInfoColor,
        Color dragPreviewValidColor,
        Color dragPreviewInvalidColor,
        Vector4 cardLabelMargin,
        float cardLabelCharacterSpacing,
        float cardLabelLineSpacing,
        Vector2 dragPreviewPanelOffset)
    {
        MetricLabelColor = metricLabelColor;
        ScrapValueColor = scrapValueColor;
        BaseValueColor = baseValueColor;
        WaveValueColor = waveValueColor;
        CardTextColor = cardTextColor;
        SecondaryInfoColor = secondaryInfoColor;
        StatusTextColor = statusTextColor;
        NeutralNoticeColor = neutralNoticeColor;
        PositiveNoticeColor = positiveNoticeColor;
        SpendingNoticeColor = spendingNoticeColor;
        WarningNoticeColor = warningNoticeColor;
        DangerNoticeColor = dangerNoticeColor;
        DragPreviewInfoColor = dragPreviewInfoColor;
        DragPreviewValidColor = dragPreviewValidColor;
        DragPreviewInvalidColor = dragPreviewInvalidColor;
        CardLabelMargin = cardLabelMargin;
        CardLabelCharacterSpacing = cardLabelCharacterSpacing;
        CardLabelLineSpacing = cardLabelLineSpacing;
        DragPreviewPanelOffset = dragPreviewPanelOffset;
    }

    public Color MetricLabelColor { get; }
    public Color ScrapValueColor { get; }
    public Color BaseValueColor { get; }
    public Color WaveValueColor { get; }
    public Color CardTextColor { get; }
    public Color SecondaryInfoColor { get; }
    public Color StatusTextColor { get; }
    public Color NeutralNoticeColor { get; }
    public Color PositiveNoticeColor { get; }
    public Color SpendingNoticeColor { get; }
    public Color WarningNoticeColor { get; }
    public Color DangerNoticeColor { get; }
    public Color DragPreviewInfoColor { get; }
    public Color DragPreviewValidColor { get; }
    public Color DragPreviewInvalidColor { get; }
    public Vector4 CardLabelMargin { get; }
    public float CardLabelCharacterSpacing { get; }
    public float CardLabelLineSpacing { get; }
    public Vector2 DragPreviewPanelOffset { get; }

    public static TowerDefenseHudTheme Default => new TowerDefenseHudTheme(
        metricLabelColor: new Color(0.56f, 0.66f, 0.75f, 1f),
        scrapValueColor: new Color(1f, 0.71f, 0.4f, 1f),
        baseValueColor: new Color(0.45f, 0.91f, 1f, 1f),
        waveValueColor: new Color(1f, 0.85f, 0.47f, 1f),
        cardTextColor: new Color(0.96f, 0.98f, 1f, 1f),
        secondaryInfoColor: new Color(0.54f, 0.65f, 0.75f, 1f),
        statusTextColor: new Color(0.84f, 0.9f, 0.94f, 1f),
        neutralNoticeColor: new Color(0.81f, 0.88f, 0.92f, 1f),
        positiveNoticeColor: new Color(0.49f, 0.95f, 0.69f, 1f),
        spendingNoticeColor: new Color(1f, 0.85f, 0.47f, 1f),
        warningNoticeColor: new Color(1f, 0.72f, 0.44f, 1f),
        dangerNoticeColor: new Color(1f, 0.55f, 0.5f, 1f),
        dragPreviewInfoColor: new Color(0.53f, 0.65f, 0.74f, 1f),
        dragPreviewValidColor: new Color(0.47f, 0.95f, 0.85f, 1f),
        dragPreviewInvalidColor: new Color(1f, 0.45f, 0.51f, 1f),
        cardLabelMargin: new Vector4(108f, 18f, 24f, 18f),
        cardLabelCharacterSpacing: 1.2f,
        cardLabelLineSpacing: -10f,
        dragPreviewPanelOffset: new Vector2(142f, -92f));
}

/// <summary>
/// `TowerDefenseHudPresenter` 负责把玩法层结果写进当前场景里的 HUD。
///
/// 这个类现在遵循“场景主导布局、脚本主导动态内容”的边界：
/// - 场景决定卡片放哪、面板长什么样、字体排版怎么摆。
/// - Presenter 负责把动态数字、动态说明和拖拽提示填进去。
///
/// 这样做以后，HUD 在 Scene / Inspector 里更容易直接调整，
/// 也不会一进 Play 就又被脚本整套摆回去。
/// </summary>
public sealed class TowerDefenseHudPresenter
{
    private TowerDefenseHudTheme _theme = TowerDefenseHudTheme.Default;
    private TowerDefenseHudCopyAsset _copyAsset;

    private TMP_Text _scrapText;
    private TMP_Text _baseHealthText;
    private TMP_Text _waveText;
    private TMP_Text _selectionText;
    private TMP_Text _operationText;
    private TMP_Text _liveStatusText;
    private TMP_Text _powerGridText;
    private TMP_Text _latestEventText;
    private TMP_Text _recentLogText;

    private TMP_Text _gameOverTitle;
    private TMP_Text _gameOverHint;
    private TMP_Text _relayTowerButtonText;
    private TMP_Text _defenseTowerButtonText;
    private TMP_Text _slowFieldTowerButtonText;
    private TMP_Text _bombardTowerButtonText;
    private TMP_Text _clearSelectionButtonText;
    private TMP_Text _dragPreviewLabel;

    private Button _relayTowerButton;
    private Button _defenseTowerButton;
    private Button _slowFieldTowerButton;
    private Button _bombardTowerButton;
    private Button _clearSelectionButton;
    private GameObject _gameOverPanel;
    private GameObject _dragPreviewPanel;

    /// <summary>
    /// 由总控把 HUD 主题快照注入进来。
    ///
    /// 这样 Presenter 继续只负责“如何显示”，
    /// 而主题长什么样则回到更适合作者调整的 Inspector 入口。
    /// </summary>
    public void SetTheme(TowerDefenseHudTheme theme)
    {
        _theme = theme;
    }

    /// <summary>
    /// 注入 HUD 固定文案资产。
    /// </summary>
    public void SetCopy(TowerDefenseHudCopyAsset copyAsset)
    {
        _copyAsset = copyAsset;
    }

    /// <summary>
    /// 由外部把已经在 Inspector 里拖好的 HUD 引用直接注入进来。
    ///
    /// 这是项目从“按名字查找场景对象”逐步迁移到“显式 Inspector 引用”的关键步骤：
    /// - 如果场景作者已经把引用拖好，Presenter 就直接使用这些确定对象。
    /// - 如果某些引用暂时还没拖，后续仍然可以给出缺项告警。
    ///
    /// 这样做的好处是迁移可以分阶段进行，
    /// 我们不需要一次性把所有场景重做完，
    /// 但新补好的场景已经能立刻摆脱“改名就炸”的脆弱模式。
    /// </summary>
    public void BindSceneReferences(
        TMP_Text scrapText,
        TMP_Text baseHealthText,
        TMP_Text waveText,
        TMP_Text selectionText,
        TMP_Text operationText,
        TMP_Text liveStatusText,
        TMP_Text powerGridText,
        TMP_Text latestEventText,
        TMP_Text recentLogText,
        Button relayTowerButton,
        Button defenseTowerButton,
        Button slowFieldTowerButton,
        Button bombardTowerButton,
        Button clearSelectionButton,
        GameObject gameOverPanel,
        TMP_Text gameOverTitle,
        TMP_Text gameOverHint,
        GameObject dragPreviewPanel,
        TMP_Text dragPreviewLabel)
    {
        _scrapText = scrapText;
        _baseHealthText = baseHealthText;
        _waveText = waveText;
        _selectionText = selectionText;
        _operationText = operationText;
        _liveStatusText = liveStatusText;
        _powerGridText = powerGridText;
        _latestEventText = latestEventText;
        _recentLogText = recentLogText;
        _relayTowerButton = relayTowerButton;
        _defenseTowerButton = defenseTowerButton;
        _slowFieldTowerButton = slowFieldTowerButton;
        _bombardTowerButton = bombardTowerButton;
        _clearSelectionButton = clearSelectionButton;
        _gameOverPanel = gameOverPanel;
        _gameOverTitle = gameOverTitle;
        _gameOverHint = gameOverHint;
        _dragPreviewPanel = dragPreviewPanel;
        _dragPreviewLabel = dragPreviewLabel;

        EnsureDragPreviewDoesNotBlockRaycasts();

        _relayTowerButtonText = _relayTowerButton != null ? _relayTowerButton.GetComponentInChildren<TMP_Text>(true) : null;
        _defenseTowerButtonText = _defenseTowerButton != null ? _defenseTowerButton.GetComponentInChildren<TMP_Text>(true) : null;
        _slowFieldTowerButtonText = _slowFieldTowerButton != null ? _slowFieldTowerButton.GetComponentInChildren<TMP_Text>(true) : null;
        _bombardTowerButtonText = _bombardTowerButton != null ? _bombardTowerButton.GetComponentInChildren<TMP_Text>(true) : null;
        _clearSelectionButtonText = _clearSelectionButton != null ? _clearSelectionButton.GetComponentInChildren<TMP_Text>(true) : null;
    }

    /// <summary>
    /// 对当前 HUD 引用做一次补齐与告警检查。
    ///
    /// 现在它不再主动按名字回捞整套 HUD，
    /// 这里只负责：
    /// - 补按钮内部文字缓存。
    /// - 纠正拖拽提示面板的射线设置。
    /// - 对缺失引用输出明确告警。
    /// </summary>
    public void FindSceneReferences()
    {
        if (_relayTowerButtonText == null && _relayTowerButton != null)
        {
            _relayTowerButtonText = _relayTowerButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (_defenseTowerButtonText == null && _defenseTowerButton != null)
        {
            _defenseTowerButtonText = _defenseTowerButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (_slowFieldTowerButtonText == null && _slowFieldTowerButton != null)
        {
            _slowFieldTowerButtonText = _slowFieldTowerButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (_bombardTowerButtonText == null && _bombardTowerButton != null)
        {
            _bombardTowerButtonText = _bombardTowerButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (_clearSelectionButtonText == null && _clearSelectionButton != null)
        {
            _clearSelectionButtonText = _clearSelectionButton.GetComponentInChildren<TMP_Text>(true);
        }

        EnsureDragPreviewDoesNotBlockRaycasts();

        WarnIfMissing(_scrapText, "ScrapText");
        WarnIfMissing(_baseHealthText, "BaseHealthText");
        WarnIfMissing(_waveText, "WaveText");
        if (_selectionText == null && _operationText == null)
        {
            WarnIfMissing(_selectionText, "SelectionText / OperationText");
        }

        WarnIfMissing(_liveStatusText, "LiveStatusText");
        WarnIfMissing(_powerGridText, "PowerGridText");
        WarnIfMissing(_latestEventText, "LatestEventText");
        WarnIfMissing(_recentLogText, "RecentLogText");
        WarnIfMissing(_relayTowerButton, "RelayTowerButton");
        WarnIfMissing(_defenseTowerButton, "DefenseTowerButton");
        WarnIfMissing(_slowFieldTowerButton, "SlowFieldTowerButton");
        WarnIfMissing(_bombardTowerButton, "BombardTowerButton");
        WarnIfMissing(_clearSelectionButton, "ClearSelectionButton");
        WarnIfMissing(_gameOverPanel, "GameOverPanel");
        WarnIfMissing(_gameOverTitle, "GameOverTitle");
        WarnIfMissing(_gameOverHint, "GameOverHint");
        WarnIfMissing(_dragPreviewPanel, "DragPreviewPanel");
        WarnIfMissing(_dragPreviewLabel, "DragPreviewLabel");
    }

    /// <summary>
    /// 拖拽提示面板只是跟随鼠标的视觉说明，不应该拦截任何鼠标释放事件。
    ///
    /// 否则玩家把塔拖到地图上时，鼠标下方其实压着这个提示面板本身，
    /// `EventSystem` 就会误以为“这次释放仍然发生在 UI 上”，
    /// 从而把一次本来合法的放塔当成取消操作。
    ///
    /// 这里同时把：
    /// - 面板背景 `Graphic` 的 `RaycastTarget` 关掉。
    /// - 文本本身的 `RaycastTarget` 关掉。
    /// - 整个面板挂一个 `CanvasGroup` 并关闭 `blocksRaycasts`。
    ///
    /// 这样就算场景里谁又手滑把某个 UI 组件的勾重新点上了，
    /// 运行时也会在 Presenter 绑定阶段把它纠正回“纯提示、不拦鼠标”的状态。
    /// </summary>
    private void EnsureDragPreviewDoesNotBlockRaycasts()
    {
        if (_dragPreviewPanel != null)
        {
            Graphic panelGraphic = _dragPreviewPanel.GetComponent<Graphic>();
            if (panelGraphic != null)
            {
                panelGraphic.raycastTarget = false;
            }

            CanvasGroup canvasGroup = _dragPreviewPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = _dragPreviewPanel.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (_dragPreviewLabel != null)
        {
            _dragPreviewLabel.raycastTarget = false;
        }
    }

    private static void WarnIfMissing(UnityEngine.Object reference, string expectedName)
    {
        if (reference == null)
        {
            Debug.LogWarning($"TowerDefenseHudPresenter is missing HUD reference: {expectedName}. Check the scene wiring.");
        }
    }

    /// <summary>
    /// 根据塔目录统一配置两张部署卡的静态文案。
    ///
    /// 这里仍然保留少量格式控制，因为部署卡文案本身是由玩法数据驱动的：
    /// - 展示名会变。
    /// - 成本会变。
    /// - 扩张方格说明会变。
    ///
    /// 但这里不会再接管卡片位置、底板样式和整个右侧区布局，
    /// 那些内容现在应该主要由场景来控制。
    /// </summary>
    public void ConfigureCardLabels(TowerCatalog towerCatalog)
    {
        ConfigureTowerCard(_relayTowerButton, _relayTowerButtonText, towerCatalog.GetDefinition(TowerType.Relay));
        ConfigureTowerCard(_defenseTowerButton, _defenseTowerButtonText, towerCatalog.GetDefinition(TowerType.SingleTarget));
        ConfigureTowerCard(_slowFieldTowerButton, _slowFieldTowerButtonText, towerCatalog.GetDefinition(TowerType.SlowField));
        ConfigureTowerCard(_bombardTowerButton, _bombardTowerButtonText, towerCatalog.GetDefinition(TowerType.Bombard));

        if (_clearSelectionButtonText != null)
        {
            string secondaryHex = ColorUtility.ToHtmlStringRGB(_theme.SecondaryInfoColor);
            _clearSelectionButtonText.text = $"{GetCopyText(_copyAsset != null ? _copyAsset.CancelDeployPrimary : null, "CANCEL DEPLOY")}\n<size=20><color=#{secondaryHex}>{GetCopyText(_copyAsset != null ? _copyAsset.CancelDeploySecondary : null, "Esc / RMB")}</color></size>";
            _clearSelectionButtonText.alignment = TextAlignmentOptions.Center;
        }
    }

    /// <summary>
    /// 刷新常驻 HUD。
    ///
    /// 这里更新的是“值”和“状态”，不是“版式骨架”。
    /// 所以你在场景里调好的布局会被保留下来；
    /// 脚本只负责把当前游戏状态填进对应文本里。
    /// </summary>
    public void Refresh(TowerDefenseHudState state, TowerCatalog towerCatalog, Func<TowerType, bool> canAffordTower)
    {
        if (_scrapText != null)
        {
            _scrapText.text = BuildMetricText(GetCopyText(_copyAsset != null ? _copyAsset.ScrapMetricLabel : null, "SCRAP STOCK"), state.CurrentScrap.ToString(), _theme.ScrapValueColor);
        }

        if (_baseHealthText != null)
        {
            _baseHealthText.text = BuildMetricText(GetCopyText(_copyAsset != null ? _copyAsset.BaseMetricLabel : null, "BASE CORE"), state.CurrentBaseHealth.ToString(), _theme.BaseValueColor);
        }

        if (_waveText != null)
        {
            string waveDisplay = state.TotalWaves > 0 ? $"{state.CurrentWave}/{state.TotalWaves}" : "0/0";
            _waveText.text = BuildMetricText(GetCopyText(_copyAsset != null ? _copyAsset.WaveMetricLabel : null, "WAVE CLOCK"), waveDisplay, _theme.WaveValueColor);
        }

        if (_selectionText != null)
        {
            _selectionText.text = HasSplitSelectionBlocks() ? string.Empty : BuildSelectionText(state, towerCatalog);
        }

        RefreshSplitSelectionBlocks(state, towerCatalog);
        UpdateButtonInteractableState(canAffordTower);
    }

    /// <summary>
    /// 接收玩法层发来的状态消息。
    ///
    /// 现在项目已经移除了常驻 `StatusStrip`，
    /// 所以这里保留接口但不再显示固定状态栏。
    /// 这样可以避免调用链断裂，同时把表现权交给别的提示 UI。
    /// </summary>
    public void SetStatusMessage(string message)
    {
    }

    /// <summary>
    /// 控制拖拽提示面板显隐。
    /// </summary>
    public void SetDragPreviewVisible(bool visible)
    {
        if (_dragPreviewPanel != null)
        {
            _dragPreviewPanel.SetActive(visible);
        }
    }

    /// <summary>
    /// 更新跟随鼠标的拖拽提示面板。
    ///
    /// 这里仍然保留“跟着鼠标走”的行为，
    /// 因为它本来就属于交互期动态反馈，而不是应该由场景固定摆死的界面。
    ///
    /// 同时这里会根据当前塔型和合法性结果，
    /// 实时刷新提示文案，帮助玩家理解：
    /// - 当前拖的是发电机还是炮塔。
    /// - 这次落点为什么能放或不能放。
    /// </summary>
    public void UpdateDragPreviewPanel(Vector2 screenPosition, TowerDragPreviewState previewState, TowerCatalog towerCatalog)
    {
        if (_dragPreviewPanel == null || _dragPreviewLabel == null)
        {
            return;
        }

        RectTransform parentRect = _dragPreviewPanel.transform.parent as RectTransform;
        RectTransform panelRect = _dragPreviewPanel.GetComponent<RectTransform>();
        if (parentRect != null && panelRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition + _theme.DragPreviewPanelOffset,
                null,
                out Vector2 localPoint);
            panelRect.anchoredPosition = localPoint;
        }

        TowerDefinition definition = towerCatalog.GetDefinition(previewState.TowerType);
        if (definition == null)
        {
            _dragPreviewLabel.text = string.Empty;
            return;
        }

        string accentHex = ColorUtility.ToHtmlStringRGB(definition.AccentColor);
        string infoHex = ColorUtility.ToHtmlStringRGB(_theme.DragPreviewInfoColor);
        string validHex = ColorUtility.ToHtmlStringRGB(_theme.DragPreviewValidColor);
        string invalidHex = ColorUtility.ToHtmlStringRGB(_theme.DragPreviewInvalidColor);
        string stateLine = previewState.IsValid
            ? $"<color=#{validHex}>DROP POINT CONFIRMED</color>"
            : $"<color=#{invalidHex}>{previewState.InvalidReason}</color>";

        _dragPreviewLabel.text =
            $"<size=20><color=#{infoHex}>{GetCopyText(_copyAsset != null ? _copyAsset.DeployTraceTitle : null, "DEPLOY TRACE")}</color></size>\n" +
            $"<size=34>{definition.DisplayName.ToUpperInvariant()}</size>\n" +
            $"<size=20><color=#{accentHex}>{definition.BuildCostLabel}</color>  <color=#{infoHex}>{GetCopyText(_copyAsset != null ? _copyAsset.DragGridLabel : null, "GRID")} {definition.ExpansionSquareSize:0.0}</color></size>\n" +
            $"<size=18><color=#{infoHex}>{GetCopyText(_copyAsset != null ? _copyAsset.DragLegalHint : null, "Cyan sectors show exact legal drop zones")}</color></size>\n" +
            $"<size=18>{stateLine}</size>";
    }

    /// <summary>
    /// 显示 `Game Over` 面板并填入文案。
    /// </summary>
    public void ShowGameOver(string title, string hint)
    {
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(true);
        }

        if (_gameOverTitle != null)
        {
            _gameOverTitle.text = title;
        }

        if (_gameOverHint != null)
        {
            _gameOverHint.text = hint;
        }
    }

    /// <summary>
    /// 单独控制 `Game Over` 面板显隐。
    /// </summary>
    public void SetGameOverVisible(bool visible)
    {
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(visible);
        }
    }

    /// <summary>
    /// 配置单张部署卡的文案。
    ///
    /// 这里保留少量文本排版控制，
    /// 只是为了确保多行卡片文案在当前卡片里能稳定读清楚。
    /// 但它不会再去改按钮位置、父物体布局或整个右侧区结构。
    /// </summary>
    private void ConfigureTowerCard(Button button, TMP_Text label, TowerDefinition definition)
    {
        if (button != null)
        {
            TowerShopCard towerShopCard = button.GetComponent<TowerShopCard>();
            if (towerShopCard != null)
            {
                towerShopCard.ApplyDefinitionVisuals(definition);
            }
        }

        if (label == null || definition == null)
        {
            return;
        }

        label.text = definition.BuildCardLabelMarkup(_theme.SecondaryInfoColor);
        label.alignment = TextAlignmentOptions.Left;
        label.margin = _theme.CardLabelMargin;
        label.enableWordWrapping = false;
        label.characterSpacing = _theme.CardLabelCharacterSpacing;
        label.lineSpacing = _theme.CardLabelLineSpacing;
        label.color = _theme.CardTextColor;
    }

    /// <summary>
    /// 组装“当前选中 / 当前拖拽”的说明文案。
    ///
    /// 这部分仍然由脚本生成，
    /// 因为它本质上就是当前状态的动态摘要，而不是固定装饰性文本。
    /// </summary>
    private string BuildSelectionText(TowerDefenseHudState state, TowerCatalog towerCatalog)
    {
        string composedText = BuildPrimaryOperationBlock(state, towerCatalog);

        AppendSection(ref composedText, BuildStatusBlock(state.CurrentStatusMessage));
        AppendSection(ref composedText, BuildPowerGridBlock(state.PowerGridSnapshot));
        AppendSection(ref composedText, BuildTransientNoticeBlock(state.TransientNotice));
        AppendSection(ref composedText, BuildRecentNoticeBlock(state.RecentHudNotices, state.TransientNotice));

        return composedText;
    }

    /// <summary>
    /// 如果场景里已经显式拆出了独立 HUD 文本块，
    /// 就优先分别写入这些节点，而不是继续把所有区块拼成一段大富文本。
    /// </summary>
    private void RefreshSplitSelectionBlocks(TowerDefenseHudState state, TowerCatalog towerCatalog)
    {
        if (!HasSplitSelectionBlocks())
        {
            return;
        }

        ApplyBlockText(_operationText, BuildPrimaryOperationBlock(state, towerCatalog));
        ApplyBlockText(_liveStatusText, BuildStatusBlock(state.CurrentStatusMessage));
        ApplyBlockText(_powerGridText, BuildPowerGridBlock(state.PowerGridSnapshot));
        ApplyBlockText(_latestEventText, BuildTransientNoticeBlock(state.TransientNotice));
        ApplyBlockText(_recentLogText, BuildRecentNoticeBlock(state.RecentHudNotices, state.TransientNotice));
    }

    private bool HasSplitSelectionBlocks()
    {
        return _operationText != null
            || _liveStatusText != null
            || _powerGridText != null
            || _latestEventText != null
            || _recentLogText != null;
    }

    private static void ApplyBlockText(TMP_Text label, string content)
    {
        if (label == null)
        {
            return;
        }

        label.text = string.IsNullOrWhiteSpace(content) ? string.Empty : content;
        label.gameObject.SetActive(true);
    }

    /// <summary>
    /// 组装操作区的主说明块。
    ///
    /// 这里继续只表达“玩家眼前主要在做什么”：
    /// - 正在拖拽哪种建筑
    /// - 当前选中了哪张卡
    /// - 当前选中了哪座已放下的结构
    /// - 或者当前处于默认待命态
    ///
    /// 后面新增的供电摘要、实时状态和事件流，会作为独立区块拼接在后面，
    /// 让信息层级比之前更清楚。
    /// </summary>
    private string BuildPrimaryOperationBlock(TowerDefenseHudState state, TowerCatalog towerCatalog)
    {
        if (state.IsPlacementDragActive)
        {
            TowerDefinition draggingDefinition = towerCatalog.GetDefinition(state.DragTowerType);
            if (draggingDefinition != null)
            {
                string accentHex = ColorUtility.ToHtmlStringRGB(draggingDefinition.AccentColor);
                string secondaryHex = ColorUtility.ToHtmlStringRGB(_theme.SecondaryInfoColor);
                return
                    $"{GetCopyText(_copyAsset != null ? _copyAsset.DeployTraceTitle : null, "DEPLOY TRACE")}\n" +
                    $"<size=30>{draggingDefinition.DisplayName}</size>\n" +
                    $"<size=20><color=#{accentHex}>{draggingDefinition.BuildCostLabel}</color>  <color=#{secondaryHex}>{GetCopyText(_copyAsset != null ? _copyAsset.DragSelectedLegalHint : null, "Cyan sectors = exact legal zone")}</color></size>";
            }
        }

        if (state.SelectedTowerType != TowerType.None)
        {
            TowerDefinition selectedDefinition = towerCatalog.GetDefinition(state.SelectedTowerType);
            if (selectedDefinition != null)
            {
                string accentHex = ColorUtility.ToHtmlStringRGB(selectedDefinition.AccentColor);
                string secondaryHex = ColorUtility.ToHtmlStringRGB(_theme.SecondaryInfoColor);
                string economyLine = BuildSelectionEconomyLine(state.CurrentScrap, selectedDefinition);
                return
                    $"{GetCopyText(_copyAsset != null ? _copyAsset.TacticalReadyTitle : null, "TACTICAL READY")}\n" +
                    $"<size=30>{selectedDefinition.DisplayName}</size>\n" +
                    $"<size=20><color=#{accentHex}>{selectedDefinition.SelectionHint}</color></size>\n" +
                    $"<size=18><color=#{secondaryHex}>{selectedDefinition.UpgradeFocusSummary}</color></size>\n" +
                    $"<size=18>{economyLine}</size>";
            }
        }

        if (state.PlacedStructureState.HasSelection)
        {
            string secondaryHex = ColorUtility.ToHtmlStringRGB(_theme.SecondaryInfoColor);
            return
                $"{GetCopyText(_copyAsset != null ? _copyAsset.StructureLinkTitle : null, "STRUCTURE LINK")}\n" +
                $"<size=30>{state.PlacedStructureState.Title}</size>\n" +
                $"<size=18><color=#{secondaryHex}>{state.PlacedStructureState.Details}</color></size>";
        }

        string operationHintHex = ColorUtility.ToHtmlStringRGB(_theme.SecondaryInfoColor);
        return
            $"{GetCopyText(_copyAsset != null ? _copyAsset.OperationLinkTitle : null, "OPERATION LINK")}\n" +
            $"<size=28>{GetCopyText(_copyAsset != null ? _copyAsset.IdleOperationSummary : null, "Click or drag a tower card to project legal sectors")}</size>\n" +
            $"<size=20><color=#{operationHintHex}>{GetCopyText(_copyAsset != null ? _copyAsset.IdleOperationHotkeys : null, "1 Relay / 2 Single / 3 Slow / 4 Bomb / Esc Cancel")}</color></size>";
    }

    /// <summary>
    /// 当前状态行负责承接原来已经存在的 `SetStatusMessage()` 调用链。
    ///
    /// 这样一来：
    /// - 旧代码不需要为了“没有 StatusStrip 了”而改得支离破碎
    /// - 这些状态消息也不再丢失，而是正式落进操作区里
    /// </summary>
    private string BuildStatusBlock(string statusMessage)
    {
        if (string.IsNullOrWhiteSpace(statusMessage))
        {
            return string.Empty;
        }

        string statusHex = ColorUtility.ToHtmlStringRGB(_theme.StatusTextColor);
        return
            $"{GetCopyText(_copyAsset != null ? _copyAsset.LiveStatusTitle : null, "LIVE STATUS")}\n" +
            $"<size=18><color=#{statusHex}>{EscapeRichText(statusMessage)}</color></size>";
    }

    /// <summary>
    /// 把供电系统当前已经生效的结果翻译成一段可读摘要。
    ///
    /// 这样玩家在准备放塔、升级或排查断电时，
    /// 不需要先点中特定继电器，也能先看到整局供电态势。
    /// </summary>
    private string BuildPowerGridBlock(PowerGridHudSnapshot snapshot)
    {
        string relayLimitText = snapshot.RelayLimit == int.MaxValue
            ? "∞"
            : snapshot.RelayLimit.ToString();

        Color statusColor = snapshot.OfflineTowerCount > 0
            ? _theme.DangerNoticeColor
            : snapshot.RelayCount == 0
                ? _theme.WarningNoticeColor
                : _theme.PositiveNoticeColor;
        string statusHex = ColorUtility.ToHtmlStringRGB(statusColor);
        string infoHex = ColorUtility.ToHtmlStringRGB(_theme.SecondaryInfoColor);

        return
            $"{GetCopyText(_copyAsset != null ? _copyAsset.PowerGridTitle : null, "POWER GRID")}\n" +
            $"<size=18><color=#{infoHex}>{GetCopyText(_copyAsset != null ? _copyAsset.RelayCountLabel : null, "Relays")} {snapshot.RelayCount}/{relayLimitText}  {GetCopyText(_copyAsset != null ? _copyAsset.OnlineTowerCountLabel : null, "Towers")} {snapshot.PoweredTowerCount}/{snapshot.TotalTowerCount} {GetCopyText(_copyAsset != null ? _copyAsset.OnlineTowerSuffix : null, "online")}</color></size>\n" +
            $"<size=18><color=#{infoHex}>{GetCopyText(_copyAsset != null ? _copyAsset.LoadLabel : null, "Load")} {snapshot.AssignedLoad}/{snapshot.TotalCapacity}</color></size>\n" +
            $"<size=18><color=#{statusHex}>{EscapeRichText(snapshot.StatusMessage)}</color></size>";
    }

    /// <summary>
    /// 最新一条瞬时提示会以更醒目的方式单独展示。
    ///
    /// 这样资源增减、关键警告或波次提示不会淹没在长说明文本里，
    /// 玩家扫一眼就能知道刚刚发生了什么。
    /// </summary>
    private string BuildTransientNoticeBlock(HudNoticeEntry transientNotice)
    {
        if (!transientNotice.HasMessage)
        {
            return string.Empty;
        }

        return
            $"{GetCopyText(_copyAsset != null ? _copyAsset.LatestEventTitle : null, "LATEST EVENT")}\n" +
            $"<size=18><color={GetNoticeColor(transientNotice.Tone)}>{EscapeRichText(transientNotice.Message)}</color></size>";
    }

    /// <summary>
    /// 最近事件流会保留最近几条重要反馈，
    /// 解决“瞬时提示一闪而过，玩家没看清就丢失”的问题。
    /// </summary>
    private string BuildRecentNoticeBlock(HudNoticeEntry[] notices, HudNoticeEntry transientNotice)
    {
        if (notices == null || notices.Length == 0)
        {
            return string.Empty;
        }

        string lines = string.Empty;
        int visibleCount = 0;

        for (int i = 0; i < notices.Length; i++)
        {
            HudNoticeEntry notice = notices[i];
            if (!notice.HasMessage)
            {
                continue;
            }

            if (transientNotice.HasMessage &&
                notice.Message == transientNotice.Message &&
                notice.Tone == transientNotice.Tone)
            {
                continue;
            }

            if (visibleCount > 0)
            {
                lines += "\n";
            }

            lines += $"<color={GetNoticeColor(notice.Tone)}>• {EscapeRichText(notice.Message)}</color>";
            visibleCount++;
        }

        if (visibleCount == 0)
        {
            return string.Empty;
        }

        return
            $"{GetCopyText(_copyAsset != null ? _copyAsset.RecentLogTitle : null, "RECENT LOG")}\n" +
            $"<size=16>{lines}</size>";
    }

    private static void AppendSection(ref string composedText, string section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(composedText))
        {
            composedText += "\n\n";
        }

        composedText += section;
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static string GetCopyText(string configuredValue, string fallbackValue)
    {
        return string.IsNullOrWhiteSpace(configuredValue) ? fallbackValue : configuredValue;
    }

    private string GetNoticeColor(HudNoticeTone tone)
    {
        switch (tone)
        {
            case HudNoticeTone.Positive:
                return $"#{ColorUtility.ToHtmlStringRGB(_theme.PositiveNoticeColor)}";
            case HudNoticeTone.Spending:
                return $"#{ColorUtility.ToHtmlStringRGB(_theme.SpendingNoticeColor)}";
            case HudNoticeTone.Warning:
                return $"#{ColorUtility.ToHtmlStringRGB(_theme.WarningNoticeColor)}";
            case HudNoticeTone.Danger:
                return $"#{ColorUtility.ToHtmlStringRGB(_theme.DangerNoticeColor)}";
            default:
                return $"#{ColorUtility.ToHtmlStringRGB(_theme.NeutralNoticeColor)}";
        }
    }

    private string BuildSelectionEconomyLine(int currentScrap, TowerDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        string positiveHex = $"#{ColorUtility.ToHtmlStringRGB(_theme.PositiveNoticeColor)}";
        string dangerHex = $"#{ColorUtility.ToHtmlStringRGB(_theme.DangerNoticeColor)}";
        if (definition.BuildCost <= 0)
        {
            return $"<color={positiveHex}>{GetCopyText(_copyAsset != null ? _copyAsset.FreeDeployLine : null, "FREE deploy. Scrap remains unchanged.")}</color>";
        }

        int remainingAfterBuild = currentScrap - definition.BuildCost;
        if (remainingAfterBuild >= 0)
        {
            return $"<color={positiveHex}>{remainingAfterBuild} {GetCopyText(_copyAsset != null ? _copyAsset.ScrapLeftSuffix : null, "SCRAP left after deploy.")}</color>";
        }

        return $"<color={dangerHex}>{GetCopyText(_copyAsset != null ? _copyAsset.NeedMoreScrapPrefix : null, "Need")} {-remainingAfterBuild} {GetCopyText(_copyAsset != null ? _copyAsset.NeedMoreScrapSuffix : null, "more SCRAP to deploy.")}</color>";
    }

    /// <summary>
    /// 在“场景主导 HUD”的模式下，只维护按钮的可交互状态。
    ///
    /// 也就是说：
    /// - 场景负责这些按钮长什么样。
    /// - 脚本只负责告诉它们当前能不能点。
    ///
    /// 如果你想修改不可购买时的颜色、选中时的高亮、按下时的过渡，
    /// 现在更推荐直接去 Button 的 `Transition / ColorBlock` 里改。
    /// </summary>
    private void UpdateButtonInteractableState(Func<TowerType, bool> canAffordTower)
    {
        if (_relayTowerButton != null)
        {
            _relayTowerButton.interactable = canAffordTower(TowerType.Relay);
        }

        if (_defenseTowerButton != null)
        {
            _defenseTowerButton.interactable = canAffordTower(TowerType.SingleTarget);
        }

        if (_slowFieldTowerButton != null)
        {
            _slowFieldTowerButton.interactable = canAffordTower(TowerType.SlowField);
        }

        if (_bombardTowerButton != null)
        {
            _bombardTowerButton.interactable = canAffordTower(TowerType.Bombard);
        }

        if (_clearSelectionButton != null)
        {
            _clearSelectionButton.interactable = true;
        }
    }

    /// <summary>
    /// 组装顶部资源卡的富文本。
    ///
    /// 这里返回的是“内容格式”，不是布局格式。
    /// 卡片放哪、字号多大、外边距多少，应该主要由场景控制；
    /// 但每张卡内部标签和数值的层级关系，仍然适合由代码统一生成。
    /// </summary>
    private string BuildMetricText(string label, string value, Color accentColor)
    {
        string labelHex = ColorUtility.ToHtmlStringRGB(_theme.MetricLabelColor);
        string accentHex = ColorUtility.ToHtmlStringRGB(accentColor);
        return
            $"<size=18><color=#{labelHex}>{label}</color></size>\n" +
            $"<size=56><color=#{accentHex}>{value}</color></size>";
    }
}
