using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
    public PlacedStructureHudState(
        bool hasSelection,
        string title,
        string details,
        int currentLevel = 1,
        int maxLevel = 4,
        bool canUpgrade = false,
        int upgradeCost = 0,
        bool hasMechanicalUpgrade = false,
        string nextMechanicalUpgradeDescription = "",
        string currentMechanicalUpgradeDescription = "")
    {
        HasSelection = hasSelection;
        Title = title ?? string.Empty;
        Details = details ?? string.Empty;
        CurrentLevel = currentLevel;
        MaxLevel = maxLevel;
        CanUpgrade = canUpgrade;
        UpgradeCost = upgradeCost;
        HasMechanicalUpgrade = hasMechanicalUpgrade;
        NextMechanicalUpgradeDescription = nextMechanicalUpgradeDescription ?? string.Empty;
        CurrentMechanicalUpgradeDescription = currentMechanicalUpgradeDescription ?? string.Empty;
    }

    public bool HasSelection { get; }
    public string Title { get; }
    public string Details { get; }
    public int CurrentLevel { get; }
    public int MaxLevel { get; }
    public bool CanUpgrade { get; }
    public int UpgradeCost { get; }
    public bool HasMechanicalUpgrade { get; }
    public string NextMechanicalUpgradeDescription { get; }
    public string CurrentMechanicalUpgradeDescription { get; }
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
    private bool _showPrimaryOperationSection = true;
    private bool _showPowerGridSection = true;

    private TMP_Text _scrapText;
    private TMP_Text _baseHealthText;
    private TMP_Text _waveText;
    private TMP_Text _selectionText;
    private TMP_Text _structureStatusText;
    private string _scrapTextTemplate;
    private string _baseHealthTextTemplate;
    private string _waveTextTemplate;
    private string _selectionTextTemplate;
    private string _structureStatusTextTemplate;

    private TMP_Text _gameOverTitle;
    private TMP_Text _gameOverHint;
    private TMP_Text _relayTowerButtonText;
    private TMP_Text _defenseTowerButtonText;
    private TMP_Text _slowFieldTowerButtonText;
    private TMP_Text _bombardTowerButtonText;
    private TMP_Text _clearSelectionButtonText;
    private TMP_Text _demolishSelectedStructureButtonText;
    private TMP_Text _upgradeSelectedStructureButtonText;
    private TMP_Text _dragPreviewLabel;

    private Button _relayTowerButton;
    private Button _defenseTowerButton;
    private Button _slowFieldTowerButton;
    private Button _bombardTowerButton;
    private Button _clearSelectionButton;
    private Button _demolishSelectedStructureButton;
    private Button _upgradeSelectedStructureButton;
    private TowerInfoPopup _towerInfoPopup;
    private bool _infoPopupOnlyMode;
    private GameObject _gameOverPanel;
    private GameObject _dragPreviewPanel;
    private GameObject _topBarRoot;
    private GameObject _bottomBarRoot;

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

    public void ConfigureSelectionSections(bool showPrimaryOperationSection, bool showPowerGridSection)
    {
        _showPrimaryOperationSection = showPrimaryOperationSection;
        _showPowerGridSection = showPowerGridSection;
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
        TMP_Text structureStatusText,
        Button relayTowerButton,
        Button defenseTowerButton,
        Button slowFieldTowerButton,
        Button bombardTowerButton,
        Button clearSelectionButton,
        Button demolishSelectedStructureButton,
        GameObject gameOverPanel,
        TMP_Text gameOverTitle,
        TMP_Text gameOverHint,
        GameObject dragPreviewPanel,
        TMP_Text dragPreviewLabel,
        Button upgradeSelectedStructureButton,
        TowerInfoPopup towerInfoPopup)
    {
        _scrapText = scrapText;
        _baseHealthText = baseHealthText;
        _waveText = waveText;
        _selectionText = selectionText;
        _structureStatusText = structureStatusText;
        _relayTowerButton = relayTowerButton;
        _defenseTowerButton = defenseTowerButton;
        _slowFieldTowerButton = slowFieldTowerButton;
        _bombardTowerButton = bombardTowerButton;
        _clearSelectionButton = clearSelectionButton;
        _demolishSelectedStructureButton = demolishSelectedStructureButton;
        _upgradeSelectedStructureButton = upgradeSelectedStructureButton;
        _towerInfoPopup = towerInfoPopup;
        _gameOverPanel = gameOverPanel;
        _gameOverTitle = gameOverTitle;
        _gameOverHint = gameOverHint;
        _dragPreviewPanel = dragPreviewPanel;
        _dragPreviewLabel = dragPreviewLabel;

        EnsureDragPreviewDoesNotBlockRaycasts();
        CacheSceneAuthoredTextTemplates();
        CaptureOptionalHudRoots();

        _relayTowerButtonText = _relayTowerButton != null ? _relayTowerButton.GetComponentInChildren<TMP_Text>(true) : null;
        _defenseTowerButtonText = _defenseTowerButton != null ? _defenseTowerButton.GetComponentInChildren<TMP_Text>(true) : null;
        _slowFieldTowerButtonText = _slowFieldTowerButton != null ? _slowFieldTowerButton.GetComponentInChildren<TMP_Text>(true) : null;
        _bombardTowerButtonText = _bombardTowerButton != null ? _bombardTowerButton.GetComponentInChildren<TMP_Text>(true) : null;
        _clearSelectionButtonText = _clearSelectionButton != null ? _clearSelectionButton.GetComponentInChildren<TMP_Text>(true) : null;
        _demolishSelectedStructureButtonText = _demolishSelectedStructureButton != null ? _demolishSelectedStructureButton.GetComponentInChildren<TMP_Text>(true) : null;
        _upgradeSelectedStructureButtonText = _upgradeSelectedStructureButton != null ? _upgradeSelectedStructureButton.GetComponentInChildren<TMP_Text>(true) : null;
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
        if (_selectionText == null)
        {
            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < texts.Length; index++)
            {
                if (texts[index] != null && texts[index].name == "SelectionText")
                {
                    _selectionText = texts[index];
                    break;
                }
            }
        }

        if (_structureStatusText == null)
        {
            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < texts.Length; index++)
            {
                if (texts[index] != null && texts[index].name == "StructureStatusText")
                {
                    _structureStatusText = texts[index];
                    break;
                }
            }
        }

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

        if (_demolishSelectedStructureButton == null)
        {
            _demolishSelectedStructureButton = FindButtonByName("DeleteSelectedStructureButton");
        }

        if (_demolishSelectedStructureButtonText == null && _demolishSelectedStructureButton != null)
        {
            _demolishSelectedStructureButtonText = _demolishSelectedStructureButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (_upgradeSelectedStructureButton == null)
        {
            _upgradeSelectedStructureButton = FindButtonByName("UpgradeSelectedStructureButton");
        }

        if (_upgradeSelectedStructureButtonText == null && _upgradeSelectedStructureButton != null)
        {
            _upgradeSelectedStructureButtonText = _upgradeSelectedStructureButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (_towerInfoPopup == null)
        {
            _towerInfoPopup = Object.FindFirstObjectByType<TowerInfoPopup>(FindObjectsInactive.Include);
        }

        EnsureDragPreviewDoesNotBlockRaycasts();
        CacheSceneAuthoredTextTemplates();
        CaptureOptionalHudRoots();

        WarnIfMissing(_scrapText, "ScrapText");
        WarnIfMissing(_baseHealthText, "BaseHealthText");
        WarnIfMissing(_waveText, "WaveText");
        WarnIfMissing(_selectionText, "SelectionText");
        WarnIfMissing(_structureStatusText, "StructureStatusText");
        WarnIfMissing(_relayTowerButton, "RelayTowerButton");
        WarnIfMissing(_defenseTowerButton, "DefenseTowerButton");
        WarnIfMissing(_slowFieldTowerButton, "SlowFieldTowerButton");
        WarnIfMissing(_bombardTowerButton, "BombardTowerButton");
        WarnIfMissing(_clearSelectionButton, "ClearSelectionButton");
        WarnIfMissing(_demolishSelectedStructureButton, "DeleteSelectedStructureButton");
        WarnIfMissing(_upgradeSelectedStructureButton, "UpgradeSelectedStructureButton");
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

    private void CacheSceneAuthoredTextTemplates()
    {
        if (_scrapText != null && !string.IsNullOrWhiteSpace(_scrapText.text))
        {
            _scrapTextTemplate = _scrapText.text;
        }

        if (_baseHealthText != null && !string.IsNullOrWhiteSpace(_baseHealthText.text))
        {
            _baseHealthTextTemplate = _baseHealthText.text;
        }

        if (_waveText != null && !string.IsNullOrWhiteSpace(_waveText.text))
        {
            _waveTextTemplate = _waveText.text;
        }

        if (_selectionText != null && !string.IsNullOrWhiteSpace(_selectionText.text))
        {
            _selectionTextTemplate = _selectionText.text;
        }

        if (_structureStatusText != null && !string.IsNullOrWhiteSpace(_structureStatusText.text))
        {
            _structureStatusTextTemplate = _structureStatusText.text;
        }
    }

    private static void WarnIfMissing(UnityEngine.Object reference, string expectedName)
    {
        if (reference == null)
        {
            Debug.LogWarning($"TowerDefenseHudPresenter is missing HUD reference: {expectedName}. Check the scene wiring.");
        }
    }

    private static void SetActiveIfPresent(Component component, bool visible)
    {
        if (component != null)
        {
            component.gameObject.SetActive(visible);
        }
    }

    private static void SetActiveIfPresent(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    private static Button FindButtonByName(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
        {
            return null;
        }

        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button != null && string.Equals(button.name, buttonName, StringComparison.Ordinal))
            {
                return button;
            }
        }

        return null;
    }

    private static string BuildSceneMetricText(string template, string fallbackLabel, string value)
    {
        string label = fallbackLabel;
        if (!string.IsNullOrWhiteSpace(template))
        {
            int colonIndex = template.IndexOf(':');
            if (colonIndex > 0)
            {
                label = template.Substring(0, colonIndex).Trim();
            }
            else
            {
                label = template.Trim();
            }
        }

        return $"{label}: {value}";
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
        ApplyTowerCardVisualsOnly(_relayTowerButton, towerCatalog.GetDefinition(TowerType.Relay));
        ApplyTowerCardVisualsOnly(_defenseTowerButton, towerCatalog.GetDefinition(TowerType.SingleTarget));
        ApplyTowerCardVisualsOnly(_slowFieldTowerButton, towerCatalog.GetDefinition(TowerType.SlowField));
        ApplyTowerCardVisualsOnly(_bombardTowerButton, towerCatalog.GetDefinition(TowerType.Bombard));
    }

    /// <summary>
    /// 刷新常驻 HUD。
    ///
    /// 这里更新的是“值”和“状态”，不是“版式骨架”。
    /// 所以你在场景里调好的布局会被保留下来；
    /// 脚本只负责把当前游戏状态填进对应文本里。
    /// </summary>
    public void Refresh(
        TowerDefenseHudState state,
        TowerCatalog towerCatalog,
        Func<TowerType, bool> canAffordTower,
        Func<TowerType, TowerTutorialAvailability> tutorialTowerAvailabilityQuery = null)
    {
        if (_scrapText != null)
        {
            _scrapText.text = BuildSceneMetricText(_scrapTextTemplate, "Scrap", state.CurrentScrap.ToString());
        }

        if (_baseHealthText != null)
        {
            _baseHealthText.text = BuildSceneMetricText(_baseHealthTextTemplate, "Base HP", state.CurrentBaseHealth.ToString());
        }

        if (_waveText != null)
        {
            string waveDisplay = state.TotalWaves > 0 ? $"{state.CurrentWave}/{state.TotalWaves}" : "0/0";
            _waveText.text = BuildSceneMetricText(_waveTextTemplate, "Wave", waveDisplay);
        }

        if (_selectionText != null && !_infoPopupOnlyMode)
        {
            _selectionText.text = BuildSelectionBlock(state, towerCatalog);
        }

        if (_structureStatusText != null && !_infoPopupOnlyMode)
        {
            _structureStatusText.text = BuildStructureStatusBlock(state);
        }

        UpdateButtonInteractableState(canAffordTower, tutorialTowerAvailabilityQuery);
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
            $"<color=#{infoHex}>DEPLOY TRACE</color>\n" +
            $"{definition.DisplayName.ToUpperInvariant()}\n" +
            $"<color=#{accentHex}>{definition.BuildCostLabel}</color>  <color=#{infoHex}>GRID {definition.ExpansionSquareSize:0.0}</color>\n" +
            "Cyan sectors show exact legal drop zones\n" +
            stateLine;
    }

    /// <summary>
    /// 显示 `Game Over` 面板并填入文案。
    /// </summary>
    public void ShowGameOver(string title, string hint)
    {
        ShowResultPanel(title, hint);
    }

    /// <summary>
    /// 当前项目还没有独立的 VictoryPanel 结构，
    /// 所以先复用同一块结果面板来承载胜利结算。
    ///
    /// 这样做的重点不是长期 UI 命名有多优雅，
    /// 而是先把“胜利后停住 -> 玩家点击继续 -> 再切剧情”这条体验链接通，
    /// 同时不强迫现有关卡场景立刻重接一整套新引用。
    /// </summary>
    public void ShowVictory(string title, string hint)
    {
        ShowResultPanel(title, hint);
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
    /// 把“删除当前选中建筑”的按钮显示控制也统一交给 HUD Presenter。
    ///
    /// 这里故意不把判定逻辑塞进 UI 层，
    /// Presenter 只接受上层告诉它“现在该不该显示”，避免 UI 反向依赖总控内部状态。
    /// </summary>
    public void SetDemolishSelectedStructureButtonVisible(bool visible)
    {
        if (_demolishSelectedStructureButton != null)
        {
            _demolishSelectedStructureButton.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 统一给删除按钮注册点击事件。
    ///
    /// 用脚本绑定而不是依赖场景 YAML 里的 onClick，
    /// 可以让运行时兜底创建出来的按钮和场景里作者化的按钮走同一条接线逻辑。
    /// </summary>
    public void BindDemolishSelectedStructureButton(Action onClick)
    {
        if (_demolishSelectedStructureButton == null)
        {
            return;
        }

        _demolishSelectedStructureButton.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            _demolishSelectedStructureButton.onClick.AddListener(() => onClick());
        }
    }

    public void SetUpgradeSelectedStructureButtonVisible(bool visible)
    {
        if (_upgradeSelectedStructureButton != null)
        {
            _upgradeSelectedStructureButton.gameObject.SetActive(visible);
        }
    }

    public void SetUpgradeSelectedStructureButtonInteractable(bool interactable)
    {
        if (_upgradeSelectedStructureButton != null)
        {
            _upgradeSelectedStructureButton.interactable = interactable;
        }
    }

    public void SetUpgradeSelectedStructureButtonLabel(string label)
    {
        if (_upgradeSelectedStructureButtonText != null)
        {
            _upgradeSelectedStructureButtonText.text = label ?? string.Empty;
        }
    }

    public void BindUpgradeSelectedStructureButton(Action onClick)
    {
        if (_upgradeSelectedStructureButton == null)
        {
            return;
        }

        _upgradeSelectedStructureButton.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            _upgradeSelectedStructureButton.onClick.AddListener(() => onClick());
        }
    }

    // ────────────────────────────
    //  Tower Info Popup
    // ────────────────────────────

    public void ShowPlacedTowerInfoPopup(
        string title,
        string stats,
        string extra,
        Vector3 worldPosition)
    {
        if (_towerInfoPopup == null) return;
        _towerInfoPopup.ShowAtWorldPosition(worldPosition, title, stats, extra);
    }

    public void ShowShopCardInfoPopup(
        string title,
        string stats,
        string extra,
        RectTransform cardRect)
    {
        if (_towerInfoPopup == null) return;
        _towerInfoPopup.ShowAboveRect(cardRect, title, stats, extra);
    }

    public void HideTowerInfoPopup()
    {
        if (_towerInfoPopup == null) return;
        _towerInfoPopup.Hide();
    }

    /// <summary>
    /// Returns the RectTransform of the shop card button for the given tower type,
    /// so the info popup can be positioned above the card.
    /// </summary>
    public RectTransform GetShopCardRect(TowerType towerType)
    {
        Button cardButton = null;
        switch (towerType)
        {
            case TowerType.Relay: cardButton = _relayTowerButton; break;
            case TowerType.SingleTarget: cardButton = _defenseTowerButton; break;
            case TowerType.SlowField: cardButton = _slowFieldTowerButton; break;
            case TowerType.Bombard: cardButton = _bombardTowerButton; break;
        }

        return cardButton != null ? cardButton.GetComponent<RectTransform>() : null;
    }

    /// <summary>
    /// Hide the popup and also stop the top-bar text fields from showing selection
    /// info. The popup is now the only place where detailed tower info lives.
    /// </summary>
    public void ApplyInfoPopupOnlyMode()
    {
        _infoPopupOnlyMode = true;

        if (_selectionText != null)
        {
            _selectionText.text = string.Empty;
        }

        if (_structureStatusText != null)
        {
            _structureStatusText.text = string.Empty;
        }
    }

    /// <summary>
    /// 在真正跨场景过渡前，把塔防 HUD 与结果面板统一隐藏。
    ///
    /// 这样黑场压上来时，玩家不会再看到旧的胜利界面、HUD 或拖拽提示残留一帧。
    /// </summary>
    public void HideAllGameplayPresentationForSceneTransition()
    {
        SetActiveIfPresent(_topBarRoot, false);
        SetActiveIfPresent(_bottomBarRoot, false);
        SetActiveIfPresent(_scrapText, false);
        SetActiveIfPresent(_baseHealthText, false);
        SetActiveIfPresent(_waveText, false);
        SetActiveIfPresent(_selectionText, false);
        SetActiveIfPresent(_structureStatusText, false);
        SetActiveIfPresent(_relayTowerButton, false);
        SetActiveIfPresent(_defenseTowerButton, false);
        SetActiveIfPresent(_slowFieldTowerButton, false);
        SetActiveIfPresent(_bombardTowerButton, false);
        SetActiveIfPresent(_clearSelectionButton, false);
        SetActiveIfPresent(_demolishSelectedStructureButton, false);
        SetActiveIfPresent(_upgradeSelectedStructureButton, false);
        SetActiveIfPresent(_dragPreviewPanel, false);
        HideTowerInfoPopup();
        SetActiveIfPresent(_gameOverPanel, false);
    }

    /// <summary>
    /// The authored HUD keeps some decorative and layout-only elements grouped under larger roots
    /// such as `TopBar` and `BottomBar`. Hiding only the child texts and buttons still leaves those
    /// larger bars visible behind the formal victory page, which is why the user could still see
    /// the normal HUD framing after clearing a level.
    ///
    /// We treat these roots as optional because older scenes may not have exactly the same
    /// structure. When they exist, they join the transition-hide group together with the gameplay
    /// widgets above.
    /// </summary>
    private void CaptureOptionalHudRoots()
    {
        Canvas hudCanvas = ResolveHudCanvas();
        if (hudCanvas == null)
        {
            return;
        }

        if (_topBarRoot == null)
        {
            _topBarRoot = FindChildGameObject(hudCanvas.transform, "TopBar");
        }

        if (_bottomBarRoot == null)
        {
            _bottomBarRoot = FindChildGameObject(hudCanvas.transform, "BottomBar");
        }
    }

    private void ShowResultPanel(string title, string hint)
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

    private static Canvas ResolveHudCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < canvases.Length; index++)
        {
            Canvas canvas = canvases[index];
            if (canvas != null && string.Equals(canvas.name, "HUDCanvas", StringComparison.Ordinal))
            {
                return canvas;
            }
        }

        return null;
    }

    private static GameObject FindChildGameObject(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform child = parent.Find(childName);
        return child != null ? child.gameObject : null;
    }

    /// <summary>
    /// 配置单张部署卡的文案。
    ///
    /// 这里保留少量文本排版控制，
    /// 只是为了确保多行卡片文案在当前卡片里能稳定读清楚。
    /// 但它不会再去改按钮位置、父物体布局或整个右侧区结构。
    /// </summary>
    private void ApplyTowerCardVisualsOnly(Button button, TowerDefinition definition)
    {
        if (button != null)
        {
            TowerShopCard towerShopCard = button.GetComponent<TowerShopCard>();
            if (towerShopCard != null)
            {
                towerShopCard.ApplyDefinitionVisuals(definition);
            }
        }
    }

    /// <summary>
    /// 组装“当前选中 / 当前拖拽”的说明文案。
    ///
    /// 这部分仍然由脚本生成，
    /// 因为它本质上就是当前状态的动态摘要，而不是固定装饰性文本。
    /// </summary>

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
    private void UpdateButtonInteractableState(
        Func<TowerType, bool> canAffordTower,
        Func<TowerType, TowerTutorialAvailability> tutorialTowerAvailabilityQuery)
    {
        ApplyTowerButtonState(_relayTowerButton, TowerType.Relay, canAffordTower, tutorialTowerAvailabilityQuery);
        ApplyTowerButtonState(_defenseTowerButton, TowerType.SingleTarget, canAffordTower, tutorialTowerAvailabilityQuery);
        ApplyTowerButtonState(_slowFieldTowerButton, TowerType.SlowField, canAffordTower, tutorialTowerAvailabilityQuery);
        ApplyTowerButtonState(_bombardTowerButton, TowerType.Bombard, canAffordTower, tutorialTowerAvailabilityQuery);

        if (_clearSelectionButton != null)
        {
            _clearSelectionButton.interactable = true;
        }
    }

    private static void ApplyTowerButtonState(
        Button button,
        TowerType towerType,
        Func<TowerType, bool> canAffordTower,
        Func<TowerType, TowerTutorialAvailability> tutorialTowerAvailabilityQuery)
    {
        if (button == null)
        {
            return;
        }

        TowerTutorialAvailability tutorialAvailability = tutorialTowerAvailabilityQuery != null
            ? tutorialTowerAvailabilityQuery(towerType)
            : TowerTutorialAvailability.Default;
        bool isTutorialLocked = tutorialAvailability == TowerTutorialAvailability.Locked;
        button.interactable = !isTutorialLocked && canAffordTower(towerType);

        TowerShopCard towerShopCard = button.GetComponent<TowerShopCard>();
        if (towerShopCard == null)
        {
            return;
        }

        towerShopCard.ApplyInteractionVisualState(ResolveTowerShopCardInteractionState(tutorialAvailability));
    }

    private static TowerShopCardInteractionState ResolveTowerShopCardInteractionState(TowerTutorialAvailability availability)
    {
        switch (availability)
        {
            case TowerTutorialAvailability.Locked:
                return TowerShopCardInteractionState.Locked;
            case TowerTutorialAvailability.Available:
                return TowerShopCardInteractionState.Available;
            case TowerTutorialAvailability.Recommended:
                return TowerShopCardInteractionState.Recommended;
            default:
                return TowerShopCardInteractionState.Default;
        }
    }

    private string BuildSelectionBlock(TowerDefenseHudState state, TowerCatalog towerCatalog)
    {
        return !string.IsNullOrWhiteSpace(_selectionTextTemplate)
            ? _selectionTextTemplate
            : string.Empty;
    }

    private string BuildStructureStatusBlock(TowerDefenseHudState state)
    {
        if (state.PlacedStructureState.HasSelection)
        {
            return state.PlacedStructureState.Title + "  |  " + state.PlacedStructureState.Details;
        }

        return !string.IsNullOrWhiteSpace(_structureStatusTextTemplate)
            ? _structureStatusTextTemplate
            : string.Empty;
    }

}
