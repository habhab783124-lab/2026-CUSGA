using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// `TowerType` 描述当前原型里玩家可部署的建筑类型。
/// `None` 表示未选择，`Relay` 表示发电机，`Defense` 表示防御塔。
/// 这个枚举会贯穿部署卡、拖拽预览、放置校验、扣费和真正建塔等整条链路。
/// </summary>
public enum TowerType
{
    None,
    Relay,
    SingleTarget,
    SlowField,
    Bombard
}

public enum TowerTutorialAvailability
{
    Default,
    Locked,
    Available,
    Recommended
}

/// <summary>
/// `TowerDefenseGame` 是当前塔防原型的整局总协调器。
/// 它负责把“运行状态、放置规则、放置交互、建塔执行、HUD 表现”这些子模块装配成一条完整主链。
/// 需要注意的是：资源/基地/波次状态、放置交互和真正建塔执行都已经继续下沉到独立组件里，
/// 所以这个类越来越像一个编排层，而不是继续把所有细节都塞进一个上帝脚本。
/// </summary>
public class TowerDefenseGame : MonoBehaviour
{
    /// <summary>
    /// `TowerPresentationAuthoring` 把“某种塔在 UI 和文案层该怎样被表现”收口成一组 Inspector 配置。
    ///
    /// 这样做以后，商店卡、HUD 操作区和后续更多界面都可以从同一份配置读样式，
    /// 而不是继续把名字、摘要、强调色和图标散落在不同脚本里。
    /// </summary>
    [Serializable]
    private sealed class TowerPresentationAuthoring
    {
        public string displayName = "Tower";
        public string cardRoleSummary = "Role Summary";
        public string selectionHint = "Selection hint.";
        public string upgradeFocusSummary = "Upgrade summary.";
        public Color accentColor = Color.white;
        public Sprite cardIconSprite = null;
        public Color cardIconTint = Color.white;
        public Color cardBackgroundTint = new Color(0.08f, 0.11f, 0.16f, 0.96f);
        public Color cardAccentTint = Color.white;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Tower" : displayName;
        public string CardRoleSummary => string.IsNullOrWhiteSpace(cardRoleSummary) ? DisplayName : cardRoleSummary;
        public string SelectionHint => string.IsNullOrWhiteSpace(selectionHint) ? CardRoleSummary : selectionHint;
        public string UpgradeFocusSummary => string.IsNullOrWhiteSpace(upgradeFocusSummary) ? "Upgrade improves this structure." : upgradeFocusSummary;
        public Color AccentColor => accentColor;
        public Sprite CardIconSprite => cardIconSprite;
        public Color CardIconTint => cardIconTint;
        public Color CardBackgroundTint => cardBackgroundTint;
        public Color CardAccentTint => cardAccentTint;
    }

    /// <summary>
    /// `HudThemeAuthoring` 把当前 HUD 仍然写死在代码里的主要配色收口到 Inspector。
    ///
    /// 这一步很重要，因为后面你替换正式美术时，
    /// 最常改的往往就是这些“语义配色”和“文本层级颜色”，
    /// 而不是 HUD 刷新逻辑本身。
    /// </summary>
    [Serializable]
    private sealed class HudThemeAuthoring
    {
        [SerializeField] private Color metricLabelColor = new Color(0.56f, 0.66f, 0.75f, 1f);
        [SerializeField] private Color scrapValueColor = new Color(1f, 0.71f, 0.4f, 1f);
        [SerializeField] private Color baseValueColor = new Color(0.45f, 0.91f, 1f, 1f);
        [SerializeField] private Color waveValueColor = new Color(1f, 0.85f, 0.47f, 1f);
        [SerializeField] private Color cardTextColor = new Color(0.96f, 0.98f, 1f, 1f);
        [SerializeField] private Color secondaryInfoColor = new Color(0.54f, 0.65f, 0.75f, 1f);
        [SerializeField] private Color statusTextColor = new Color(0.84f, 0.9f, 0.94f, 1f);
        [SerializeField] private Color neutralNoticeColor = new Color(0.81f, 0.88f, 0.92f, 1f);
        [SerializeField] private Color positiveNoticeColor = new Color(0.49f, 0.95f, 0.69f, 1f);
        [SerializeField] private Color spendingNoticeColor = new Color(1f, 0.85f, 0.47f, 1f);
        [SerializeField] private Color warningNoticeColor = new Color(1f, 0.72f, 0.44f, 1f);
        [SerializeField] private Color dangerNoticeColor = new Color(1f, 0.55f, 0.5f, 1f);
        [SerializeField] private Color dragPreviewInfoColor = new Color(0.53f, 0.65f, 0.74f, 1f);
        [SerializeField] private Color dragPreviewValidColor = new Color(0.47f, 0.95f, 0.85f, 1f);
        [SerializeField] private Color dragPreviewInvalidColor = new Color(1f, 0.45f, 0.51f, 1f);
        [SerializeField] private Vector4 cardLabelMargin = new Vector4(108f, 18f, 24f, 18f);
        [SerializeField] private float cardLabelCharacterSpacing = 1.2f;
        [SerializeField] private float cardLabelLineSpacing = -10f;
        [SerializeField] private Vector2 dragPreviewPanelOffset = new Vector2(142f, -92f);

        public TowerDefenseHudTheme ToRuntimeTheme()
        {
            return new TowerDefenseHudTheme(
                metricLabelColor,
                scrapValueColor,
                baseValueColor,
                waveValueColor,
                cardTextColor,
                secondaryInfoColor,
                statusTextColor,
                neutralNoticeColor,
                positiveNoticeColor,
                spendingNoticeColor,
                warningNoticeColor,
                dangerNoticeColor,
                dragPreviewInfoColor,
                dragPreviewValidColor,
                dragPreviewInvalidColor,
                cardLabelMargin,
                cardLabelCharacterSpacing,
                cardLabelLineSpacing,
                dragPreviewPanelOffset);
        }
    }

    /// <summary>
    /// 当前场景中的总控单例。部署卡、旧版 BuildPad 兼容桥和部分运行时对象会通过它拿到统一入口。
    /// </summary>
    public static TowerDefenseGame Instance { get; private set; }

    [Header("Core Rules")]
    [FormerlySerializedAs("startingEnergy")]
    [SerializeField] private int startingScrap = 80;
    [SerializeField] private int startingBaseHealth = 10;
    [SerializeField] private int relayTowerCost = 0;
    [SerializeField] private int singleTargetTowerCost = 38;
    [SerializeField] private int slowFieldTowerCost = 50;
    [SerializeField] private int bombardTowerCost = 62;

    [Header("Placement Rules (Legacy Compatibility)")]

    /// <summary>
    /// Legacy compatibility field.
    ///
    /// This value is no longer the authoritative source for relay footprint / placement blocking.
    /// The current authoritative source is the `CircleCollider2D` configured directly on the
    /// relay runtime prefab.
    ///
    /// We intentionally keep this serialized field for one transition period so old scenes do not
    /// immediately lose data or break deserialization, but new tuning work should not use it.
    /// </summary>
    [SerializeField] private float relayPlacementRadius = 0.52f;

    /// <summary>
    /// Legacy compatibility field.
    ///
    /// This value is no longer the authoritative source for combat-tower footprint / placement
    /// blocking. The current authoritative source is the `CircleCollider2D` configured directly on
    /// each combat-tower runtime prefab.
    ///
    /// We intentionally keep this serialized field for one transition period so old scenes do not
    /// immediately lose data or break deserialization, but new tuning work should not use it.
    /// </summary>
    [SerializeField] private float defensePlacementRadius = 0.58f;

    [Header("Placement Expansion")]
    [SerializeField] private float relayExpansionSquareSize = 4.5f;
    [SerializeField] private float defenseExpansionSquareSize = 4.5f;
    [SerializeField] private Vector2 initialPlacementSquareCenter = new Vector2(-6.5f, -2.25f);
    [SerializeField] private float initialPlacementSquareSize = 3f;

    [Header("Placement Grid")]
    [SerializeField] private float placementGridCellSize = 0.5f;
    [SerializeField] private Vector2 placementGridOrigin;
    [SerializeField] private bool snapPlacementToCellCenter = true;
    [SerializeField] private Vector2Int relayFootprintCells = new Vector2Int(2, 2);
    [SerializeField] private Vector2Int defenseFootprintCells = new Vector2Int(2, 2);

    [Header("Placement Preview")]
    [SerializeField] private Color validPreviewColor = new Color(0.26f, 0.95f, 0.78f, 0.72f);
    [SerializeField] private Color invalidPreviewColor = new Color(1f, 0.32f, 0.38f, 0.72f);
    [SerializeField] private Sprite placementRingSpriteReference;
    [SerializeField] private string placementRingResourcePath = "UI/placement-ring";

    [Header("Placement Overlay")]
    [SerializeField] private float placementAreaOverlayPixelsPerUnit = 20f;
    [SerializeField] private Color placementAreaOverlayFillColor = new Color(0.18f, 0.82f, 0.86f, 0.16f);
    [SerializeField] private Color placementAreaOverlayEdgeColor = new Color(0.72f, 1f, 0.97f, 0.52f);
    [SerializeField] private int placementAreaOverlaySortingOrder = 12;

    [Header("Starter Zone Marker")]
    [SerializeField] private Color starterZoneMarkerFillColor = new Color(0.22f, 0.82f, 0.88f, 0.22f);
    [SerializeField] private Color starterZoneMarkerEdgeColor = new Color(0.9f, 1f, 0.98f, 1f);
    [SerializeField] private int starterZoneMarkerSortingOrder = 10;

    [Header("Diagnostics")]
    [SerializeField] private bool enablePlacementDiagnostics;

    [Header("Scene Flow")]
    [SerializeField] private bool restartCurrentSceneOnGameOverClick = true;
    [SerializeField] private float gameOverRestartClickDelaySeconds = 0.2f;
    [SerializeField] private float levelClearContinueClickDelaySeconds = 0.2f;

    [Header("Tower Presentation")]
    [SerializeField] private TowerPresentationAuthoring relayPresentation = new TowerPresentationAuthoring
    {
        displayName = "Relay Generator",
        cardRoleSummary = "Relay Node / Supply Grid",
        selectionHint = "Anchor the power grid first, then expand tower coverage from there.",
        upgradeFocusSummary = "Upgrades add more supply capacity without changing relay coverage radius.",
        accentColor = new Color(1f, 0.55f, 0.22f, 1f),
        cardIconTint = new Color(1f, 0.66f, 0.3f, 1f),
        cardBackgroundTint = new Color(0.14f, 0.1f, 0.08f, 0.96f),
        cardAccentTint = new Color(1f, 0.55f, 0.22f, 1f)
    };
    [SerializeField] private TowerPresentationAuthoring singleTargetPresentation = new TowerPresentationAuthoring
    {
        displayName = "Defense Turret",
        cardRoleSummary = "Focus Fire / Frontline",
        selectionHint = "Reliable direct damage for finishing one target at a time.",
        upgradeFocusSummary = "Upgrades push faster fire, longer reach, and steadier single-target DPS.",
        accentColor = new Color(0.28f, 0.78f, 1f, 1f),
        cardIconTint = new Color(0.55f, 0.88f, 1f, 1f),
        cardBackgroundTint = new Color(0.07f, 0.11f, 0.16f, 0.96f),
        cardAccentTint = new Color(0.28f, 0.78f, 1f, 1f)
    };
    [SerializeField] private TowerPresentationAuthoring slowFieldPresentation = new TowerPresentationAuthoring
    {
        displayName = "Slow Field Tower",
        cardRoleSummary = "Area Control / Slow",
        selectionHint = "Controls lanes by slowing every enemy inside the field.",
        upgradeFocusSummary = "Upgrades strengthen the slow, extend control time, and improve area denial.",
        accentColor = new Color(0.36f, 0.95f, 0.84f, 1f),
        cardIconTint = new Color(0.66f, 1f, 0.91f, 1f),
        cardBackgroundTint = new Color(0.07f, 0.14f, 0.14f, 0.96f),
        cardAccentTint = new Color(0.36f, 0.95f, 0.84f, 1f)
    };
    [SerializeField] private TowerPresentationAuthoring bombardPresentation = new TowerPresentationAuthoring
    {
        displayName = "Bombard Tower",
        cardRoleSummary = "Burst Splash / Delayed",
        selectionHint = "Delayed splash damage that punishes clustered enemies at range.",
        upgradeFocusSummary = "Upgrades widen the blast, shorten bomb travel, and raise burst damage.",
        accentColor = new Color(1f, 0.62f, 0.26f, 1f),
        cardIconTint = new Color(1f, 0.78f, 0.46f, 1f),
        cardBackgroundTint = new Color(0.16f, 0.1f, 0.08f, 0.96f),
        cardAccentTint = new Color(1f, 0.62f, 0.26f, 1f)
    };

    [Header("HUD Theme")]
    [SerializeField] private HudThemeAuthoring hudTheme = new HudThemeAuthoring();

    [Header("Audio")]
    [SerializeField] private TowerDefenseAudioProfileAsset audioProfileReference;
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField] private AudioClip placeStructureClip;
    [SerializeField] private AudioClip singleTargetImpactClip;
    [SerializeField] private AudioClip slowFieldImpactClip;
    [SerializeField] private AudioClip bombardImpactClip;
    [SerializeField] private float backgroundMusicVolume = 0.55f;
    [SerializeField] private float soundEffectVolume = 0.85f;

    [Header("Scene References (Preferred)")]

    /// <summary>
    /// 这一组是玩法主链路优先使用的显式场景引用。
    /// 包括主相机、塔原型、运行时根节点和 `BuildZone`。如果这些引用已经在 Inspector 里配好，
    /// 运行时就不应该再依赖对象名查找；名字字段只保留给过渡期兜底或运行时容器命名。
    /// </summary>
    [SerializeField] private Camera mainCameraReference;
    [SerializeField] private GameObject relayTowerPrototypeReference;
    [FormerlySerializedAs("defenseTowerPrototypeReference")]
    [SerializeField] private GameObject singleTargetTowerPrototypeReference;
    [SerializeField] private GameObject slowFieldTowerPrototypeReference;
    [SerializeField] private GameObject bombardTowerPrototypeReference;
    [SerializeField] private Transform placedTowerRootReference;
    [SerializeField] private Transform placementPreviewRootReference;
    [SerializeField] private BuildZone buildZoneReference;
    [SerializeField] private PlacementGrid placementGridReference;
    [SerializeField] private BattlefieldMapDefinition battlefieldMapReference;

    [Header("Scene Object Names")]
    [SerializeField] private string placedTowerRootName = "PlacedTowers";
    [SerializeField] private string placementPreviewRootName = "PlacementPreviewRoot";
    [SerializeField] private string buildZoneName = "BuildZone";
    [SerializeField] private string placementGridName = "PlacementGrid";

    [Header("HUD References (Preferred)")]

    /// <summary>
    /// 这一组是玩法 HUD 的显式场景引用。
    /// 当前策略是优先直接拖 Inspector，引导项目逐步摆脱按名字查找 UI 对象的旧做法。
    /// </summary>
    [FormerlySerializedAs("energyTextReference")]
    [SerializeField] private TMP_Text scrapTextReference;
    [SerializeField] private TMP_Text baseHealthTextReference;
    [SerializeField] private TMP_Text waveTextReference;
    [SerializeField] private TMP_Text selectionTextReference;
    [SerializeField] private TMP_Text structureStatusTextReference;

    [SerializeField] private Button relayTowerButtonReference;
    [SerializeField] private Button defenseTowerButtonReference;
    [SerializeField] private Button slowFieldTowerButtonReference;
    [SerializeField] private Button bombardTowerButtonReference;
    [SerializeField] private Button clearSelectionButtonReference;
    [SerializeField] private Button demolishSelectedStructureButtonReference;
    [SerializeField] private GameObject gameOverPanelReference;
    [SerializeField] private TMP_Text gameOverTitleReference;
    [SerializeField] private TMP_Text gameOverHintReference;
    [SerializeField] private GameObject dragPreviewPanelReference;
    [SerializeField] private TMP_Text dragPreviewLabelReference;
    [SerializeField] private VictoryResultPageView victoryResultPageViewReference;

    /// <summary>
    /// `_sessionState` 负责保存这一局的资源、基地、波次和结算状态。
    /// 它是当前总控最核心的一份“局内运行状态源”。
    /// </summary>
    private TowerDefenseSessionState _sessionState;

    private GameObject _relayTowerPrototype;
    private GameObject _singleTargetTowerPrototype;
    private GameObject _slowFieldTowerPrototype;
    private GameObject _bombardTowerPrototype;
    private Camera _mainCamera;
    private BuildZone _buildZone;
    private PlacementGrid _placementGrid;
    private BattlefieldMapDefinition _battlefieldMapDefinition;
    private Transform _placedTowerRoot;
    private Transform _placementPreviewRoot;
    private TowerPlacementRules _placementRules;
    private TowerPowerGridCoordinator _powerGridCoordinator;

    /// <summary>
    /// `_placementVisualController` 负责放置阶段的可视化反馈。
    /// 它会统一管理预览塔、合法区域覆盖层和首塔起手区标记，让 `TowerDefenseGame` 只保留调度职责。
    /// </summary>
    private TowerPlacementVisualController _placementVisualController;

    /// <summary>
    /// `_placementInteractionController` 负责“玩家怎样进入放置流程、怎样更新流程、怎样结束流程”。
    /// 这一轮把交互状态从总控里迁出去后，
    /// `TowerDefenseGame` 更明确地退回到“整局编排 + 真正建塔 + HUD 刷新入口”的职责边界。
    /// </summary>
    private TowerPlacementInteractionController _placementInteractionController;

    /// <summary>
    /// `_placementBuildExecutor` 负责真正建塔这一段执行链。
    /// 也就是：最终校验、实例化塔、兼容旧 BuildPad、补碰撞体、扣费和放置成功后的收尾刷新。
    /// 这样总控就不用再同时承担“整局状态管理”和“建塔流水线细节”两种职责。
    /// </summary>
    private TowerPlacementBuildExecutor _placementBuildExecutor;

    /// <summary>
    /// `_presentationCoordinator` 负责 HUD 广播与结算表现收尾。
    /// 它把 HUD 快照组装、状态消息转发、Game Over 面板显示和敌人血条隐藏这些表现层协调逻辑
    /// 从总控中继续收口出去。
    /// </summary>
    private TowerDefensePresentationCoordinator _presentationCoordinator;

    /// <summary>
    /// `_sceneBootstrapper` 负责把当前关卡里的显式引用、运行时根节点和兜底对象装配成可用状态。
    /// 这样总控就不必继续内联整段“场景怎么接线、根节点怎么补、BuildZone 怎么兜底”的启动代码。
    /// </summary>
    private TowerDefenseSceneBootstrapper _sceneBootstrapper;

    /// <summary>
    /// `_inputCoordinator` 负责输入轮询、快速点击放置、屏幕坐标换算和 UI 阻挡判断。
    /// 这样总控就不再自己持有这一组输入工具层细节。
    /// </summary>
    private TowerDefenseInputCoordinator _inputCoordinator;

    /// <summary>
    /// `_placementSupportCoordinator` 负责放置链里剩下的支持型能力，
    /// 例如：起手区标记、合法区预热、塔静态定义查询、规则桥接与起手区自检。
    /// 这是让总控在最后一轮尽量收敛成“装配层”的关键一步。
    /// </summary>
    private TowerPlacementSupportCoordinator _placementSupportCoordinator;
    private RelayTower _selectedRelayTower;
    private DefenseTower _selectedDefenseTower;
    private TowerDefenseAudioCoordinator _audioCoordinator;
    private AudioSource _backgroundMusicSource;
    private AudioSource _soundEffectSource;

    /// <summary>
    /// `_towerCatalog` 提供塔的静态定义，例如显示名、造价、占地半径和扩张方格边长。
    /// 总控通过它读配置，而不是把这些常量散落在很多 `switch` 里。
    /// </summary>
    private TowerCatalog _towerCatalog;

    /// <summary>
    /// `_hudPresenter` 是 HUD 表现层适配器。
    /// 它只负责把当前状态刷到界面上，并同步拖拽提示、按钮可用性与结算面板。
    /// 这样做的目的，是把“状态计算”和“界面呈现”分开，减少总控脚本继续膨胀。
    /// </summary>
    private TowerDefenseHudPresenter _hudPresenter;
    private StructureSelectionRangeVisualizer _selectionRangeVisualizer;
    private bool _gameOverRestartTriggered;
    private float _gameOverRestartAllowedAtUnscaledTime;
    private bool _levelClearPresentationActive;
    private bool _levelClearContinueTriggered;
    private float _levelClearContinueAllowedAtUnscaledTime;
    private bool _continueCampaignAfterLevelClear;
    private string _fallbackNextSceneNameAfterLevelClear = string.Empty;
    private float _levelClearFadeOutDuration = 0.75f;
    private float _levelClearFadeInDuration = 0.75f;
    private bool _levelClearStartOpaqueOnContinue;
    private const float DemolishRefundRatio = 0.5f;
    private const string VictoryResultPagePrefabResourcePath = "TowerDefense/UI/VictoryResultPage";
    private const string DefaultTutorialLockedTowerStatusMessage = "指挥中心：当前阶段尚未批准部署该塔型，先完成当前指引。";
    private const string FailureResultPagePrefabResourcePath = "TowerDefense/UI/FailureResultPage";
    private readonly Dictionary<TowerType, TowerTutorialAvailability> _tutorialTowerAvailability = new Dictionary<TowerType, TowerTutorialAvailability>(4);
    private bool _hasTutorialTowerAvailabilityOverrides;
    private string _tutorialLockedTowerStatusMessage = DefaultTutorialLockedTowerStatusMessage;
    private string _tutorialFailureCommanderLineOverride = string.Empty;
    private string _tutorialFailureFollowUpLineOverride = string.Empty;
    private bool _tutorialPaused;
    private float _tutorialResumeAllowedAtUnscaledTime;
    private Action _tutorialResumeCallback;

    /// <summary>
    /// 对外暴露只读的结算状态，方便 HUD、敌人和其他运行时对象判断当前是否已经 Game Over。
    /// </summary>
    public bool IsGameOver => _sessionState != null && _sessionState.IsGameOver;
    public int CurrentScrap => _sessionState != null ? _sessionState.CurrentScrap : 0;
    public int CurrentBaseHealth => _sessionState != null ? _sessionState.CurrentBaseHealth : 0;
    public int CurrentWave => _sessionState != null ? _sessionState.CurrentWave : 0;
    public int TotalWaves => _sessionState != null ? _sessionState.TotalWaves : 0;
    public Transform PlacedTowerRoot => _placedTowerRoot;

    public TowerTutorialAvailability GetTutorialTowerAvailability(TowerType towerType)
    {
        if (!_hasTutorialTowerAvailabilityOverrides || towerType == TowerType.None)
        {
            return TowerTutorialAvailability.Default;
        }

        return _tutorialTowerAvailability.TryGetValue(towerType, out TowerTutorialAvailability availability)
            ? availability
            : TowerTutorialAvailability.Default;
    }

    public bool CanPreviewTowerCard(TowerType towerType)
    {
        return towerType != TowerType.None && !IsGameOver && !IsTowerLockedByTutorial(towerType);
    }

    private bool IsTowerLockedByTutorial(TowerType towerType)
    {
        return GetTutorialTowerAvailability(towerType) == TowerTutorialAvailability.Locked;
    }

    public void ApplyTutorialTowerAvailability(
        TowerTutorialAvailability relayAvailability,
        TowerTutorialAvailability singleTargetAvailability,
        TowerTutorialAvailability slowFieldAvailability,
        TowerTutorialAvailability bombardAvailability)
    {
        _tutorialTowerAvailability.Clear();
        _tutorialTowerAvailability[TowerType.Relay] = relayAvailability;
        _tutorialTowerAvailability[TowerType.SingleTarget] = singleTargetAvailability;
        _tutorialTowerAvailability[TowerType.SlowField] = slowFieldAvailability;
        _tutorialTowerAvailability[TowerType.Bombard] = bombardAvailability;
        _hasTutorialTowerAvailabilityOverrides = true;
        RefreshHud();
    }

    public void ClearTutorialTowerAvailability()
    {
        if (!_hasTutorialTowerAvailabilityOverrides && _tutorialTowerAvailability.Count == 0)
        {
            return;
        }

        _tutorialTowerAvailability.Clear();
        _hasTutorialTowerAvailabilityOverrides = false;
        RefreshHud();
    }

    public void SetTutorialLockedTowerStatusMessage(string message)
    {
        _tutorialLockedTowerStatusMessage = string.IsNullOrWhiteSpace(message)
            ? DefaultTutorialLockedTowerStatusMessage
            : message;
    }

    public void SetTutorialFailureDialogueOverride(string commanderLine, string followUpLine)
    {
        _tutorialFailureCommanderLineOverride = commanderLine ?? string.Empty;
        _tutorialFailureFollowUpLineOverride = followUpLine ?? string.Empty;
    }

    public void ClearTutorialFailureDialogueOverride()
    {
        _tutorialFailureCommanderLineOverride = string.Empty;
        _tutorialFailureFollowUpLineOverride = string.Empty;
    }

    public void SetTutorialStatusMessage(string message)
    {
        _presentationCoordinator?.SetPinnedStatusMessage(message);
    }

    public void ClearTutorialStatusMessage()
    {
        _presentationCoordinator?.ClearPinnedStatusMessage();
    }

    public void ShowTutorialHudNotice(string message, HudNoticeTone tone = HudNoticeTone.Auto)
    {
        _presentationCoordinator?.SetPinnedTransientNotice(message, tone);
    }

    public void ClearTutorialHudNotice()
    {
        _presentationCoordinator?.ClearPinnedTransientNotice();
    }

    public bool IsTutorialPaused => _tutorialPaused;

    public void SetTutorialPaused(bool paused, Action onResumeCallback = null)
    {
        if (paused)
        {
            _tutorialPaused = true;
            _tutorialResumeCallback = onResumeCallback;
            _tutorialResumeAllowedAtUnscaledTime = Time.unscaledTime + 0.4f;
            Time.timeScale = 0f;
        }
        else
        {
            _tutorialPaused = false;
            Time.timeScale = 1f;
            Action cb = _tutorialResumeCallback;
            _tutorialResumeCallback = null;
            cb?.Invoke();
        }
    }

    public void ConfigureTutorialSelectionHudSections(bool showPrimaryOperationSection, bool showPowerGridSection)
    {
        _hudPresenter?.ConfigureSelectionSections(showPrimaryOperationSection, showPowerGridSection);
        RefreshHud();
    }

    /// <summary>
    /// `Awake()` 负责建立单例、锁定基础运行参数，并把场景引用与协作模块先装配起来。
    /// 之所以把这些初始化尽量前置，是为了避免部署卡、刷怪器或 HUD 在 `Start()` 前访问到半初始化状态。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        Application.runInBackground = true;

        _sessionState = new TowerDefenseSessionState(startingScrap, startingBaseHealth);
        InitializeArchitectureModules();
    }

    /// <summary>
    /// 释放放置可视化控制器，并在对象销毁时安全清理单例引用。
    /// 这样可以避免场景重载或脚本重编译后残留旧实例状态。
    /// </summary>
    private void OnDestroy()
    {
        _selectionRangeVisualizer?.Dispose();
        _selectionRangeVisualizer = null;

        _placementVisualController?.Dispose();
        _placementVisualController = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// `Start()` 负责完成进入关卡后的首轮就绪工作。
    /// 包括补齐场景引用、建立运行时根节点、配置 HUD、隐藏初始面板、刷新首塔标记，
    /// 以及执行一次起手区自检，确保第一时间就能发现“首塔放不下”到底是交互问题还是规则问题。
    /// </summary>
    private void Start()
    {
        FindSceneReferences();
        EnsureRuntimeRoots();
        _placementSupportCoordinator?.RefreshPlacementRuleContext();
        InitializePlacementVisuals();
        _presentationCoordinator?.InitializePresentation("Place a relay on any empty ground, then deploy towers inside relay coverage. You can drag the deploy cards or use hotkeys 1 / 2 / 3 / 4.");
        _placementSupportCoordinator?.HidePlacementAreaOverlay();
        _powerGridCoordinator?.RecalculatePowerDistribution();
        _audioCoordinator?.StartBackgroundMusic();
    }

    /// <summary>
    /// `Update()` 现在只负责驱动输入协调器。
    /// 这样总控不再自己轮询热键、快速点击放置和 UI 阻挡判断，
    /// 而是把这些输入层细节统一交给 `_inputCoordinator`。
    /// </summary>
    private void Update()
    {
        HandleTutorialResumeInput();

        if (HandleLevelClearContinueInput())
        {
            return;
        }

        _inputCoordinator?.Tick();
        HandleGameOverRestartInput();
    }

    private void HandleTutorialResumeInput()
    {
        if (!_tutorialPaused) return;
        if (Time.unscaledTime < _tutorialResumeAllowedAtUnscaledTime) return;
        if (!DidPressTutorialContinue()) return;
        SetTutorialPaused(false);
    }

    private static bool DidPressTutorialContinue()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }

    /// <summary>
    /// 旧版 `BuildPad` 入口的兼容桥。
    /// 虽然当前主玩法已经改成自由拖拽部署，但这个方法还能把固定塔位请求转发到统一的真正建塔逻辑里。
    /// </summary>
    public bool TryPlaceTower(BuildPad pad)
    {
        if (pad == null)
        {
            return false;
        }

        TowerType selectedTowerType = _placementInteractionController != null
            ? _placementInteractionController.SelectedTowerType
            : TowerType.None;
        return TryPlaceTowerAt(pad.GetBuildPosition(), selectedTowerType, pad);
    }

    /// <summary>
    /// 开始一次新的拖拽部署流程。
    /// 这里会先检查塔型、资源和原型体是否有效，再进入拖拽状态并生成对应的预览反馈。
    /// </summary>
    public bool BeginPlacementDrag(TowerType towerType, Vector2 screenPosition)
    {
        return _placementInteractionController != null &&
               _placementInteractionController.BeginPlacementDrag(towerType, screenPosition);
    }

    /// <summary>
    /// 在拖拽过程中持续更新预览塔。
    /// 每一帧都会把屏幕坐标换算到世界坐标，重跑放置校验，然后同步更新预览表现和拖拽提示面板。
    /// </summary>
    public void UpdatePlacementDrag(Vector2 screenPosition)
    {
        _placementInteractionController?.UpdatePlacementDrag(screenPosition);
    }

    /// <summary>
    /// 在玩家松手时结束拖拽部署。
    /// 这里会先拿到最终鼠标位置和合法性结果，再决定是正式建塔、提示失败，还是仅仅取消本次拖拽。
    /// </summary>
    public void EndPlacementDrag(Vector2 screenPosition, bool releasedOverUserInterface)
    {
        _placementInteractionController?.EndPlacementDrag(screenPosition, releasedOverUserInterface);
    }


    /// <summary>
    /// 对外暴露的取消拖拽入口。
    /// 按钮、快捷键和其他外部调用都可以走这里，统一回收预览与临时状态。
    /// </summary>
    public void CancelPlacementDrag()
    {
        _placementInteractionController?.CancelPlacementDrag();
    }

    /// <summary>
    /// 增加废料。
    /// 这里只接受正数收入，并且在 Game Over 后不再改动局内资源。
    /// </summary>
    public void AddScrap(int amount)
    {
        if (_sessionState == null || !_sessionState.TryAddScrap(amount))
        {
            return;
        }

        ShowTransientHudNotice($"+{amount} SCRAP recovered.", tone: HudNoticeTone.Positive);
        RefreshHud();
    }

    /// <summary>
    /// 兼容旧的能量命名入口。
    /// </summary>
    public void AddEnergy(int amount)
    {
        AddScrap(amount);
    }

    /// <summary>
    /// 让基地承受一次伤害。
    /// 方法会扣血、刷新 HUD、推送提示，并在生命降到零时切到 Game Over。
    /// </summary>
    public void DamageBase(int amount)
    {
        if (_sessionState == null || !_sessionState.TryApplyBaseDamage(amount, out int actualDamage, out bool baseDepleted))
        {
            return;
        }

        RefreshHud();
        SetStatusMessage($"An enemy slipped through. Base lost {actualDamage} HP.");
        ShowTransientHudNotice($"-{actualDamage} CORE integrity.", duration: 3f, tone: HudNoticeTone.Danger);

        if (baseDepleted)
        {
            ShowGameOver();
        }
    }

    /// <summary>
    /// 同步当前波次进度，并刷新顶部 HUD 的波次显示。
    /// </summary>
    public void SetWaveProgress(int currentWave, int totalWaves)
    {
        _sessionState?.SetWaveProgress(currentWave, totalWaves);
        RefreshHud();
    }

    /// <summary>
    /// 向 HUD 层发送状态消息。
    /// 虽然当前常驻 `StatusStrip` 已移除，但保留这个入口仍然有价值，
    /// 因为它让结算、放置失败和调试提示继续有统一出口。
    /// </summary>
    public void SetStatusMessage(string message)
    {
        _presentationCoordinator?.SetStatusMessage(message);
    }

    public void ContinueAfterVictoryPresentation()
    {
        if (!_levelClearPresentationActive)
        {
            return;
        }

        HandleLevelClearContinueInputFromUi();
    }

    public void RetryAfterFailurePresentation()
    {
        if (!IsGameOver || _gameOverRestartTriggered)
        {
            return;
        }

        RestartCurrentScene();
    }

    public void ShowTransientHudNotice(string message, float duration = 2.5f, HudNoticeTone tone = HudNoticeTone.Auto)
    {
        _presentationCoordinator?.ShowTransientHudNotice(message, duration, tone);
    }

    public void PlayPlaceStructureSound()
    {
        _audioCoordinator?.PlayPlaceStructure();
    }

    public void PlaySingleTargetImpactSound()
    {
        _audioCoordinator?.PlaySingleTargetImpact();
    }

    public void PlaySlowFieldImpactSound()
    {
        _audioCoordinator?.PlaySlowFieldImpact();
    }

    public void PlayBombardImpactSound()
    {
        _audioCoordinator?.PlayBombardImpact();
    }

    /// <summary>
    /// 选中发电机，供按钮事件或快捷键直接调用。
    /// </summary>
    public void SelectRelayTower()
    {
        ClearPlacedStructureSelection();
        _placementInteractionController?.SelectRelayTower();
        RefreshHud();
    }

    /// <summary>
    /// 选中防御塔，供按钮事件或快捷键直接调用。
    /// </summary>
    public void SelectDefenseTower()
    {
        ClearPlacedStructureSelection();
        _placementInteractionController?.SelectSingleTargetTower();
        RefreshHud();
    }

    public void SelectSlowFieldTower()
    {
        ClearPlacedStructureSelection();
        _placementInteractionController?.SelectSlowFieldTower();
        RefreshHud();
    }

    public void SelectBombardTower()
    {
        ClearPlacedStructureSelection();
        _placementInteractionController?.SelectBombardTower();
        RefreshHud();
    }

    /// <summary>
    /// 清空当前部署选择。
    /// 这里会同时取消拖拽中的预览状态，避免界面显示和内部选择状态脱节。
    /// </summary>
    public void ClearSelection()
    {
        _placementInteractionController?.ClearSelection();
        ClearPlacedStructureSelection();
        RefreshHud();
    }

    /// <summary>
    /// 判断当前废料是否足够支付指定塔型的造价。
    /// `None` 永远视为不可购买，这样可以避免“未选中状态”误走通过分支。
    /// </summary>
    public bool CanAffordTower(TowerType towerType)
    {
        if (towerType == TowerType.None)
        {
            return false;
        }

        return _sessionState != null &&
               _sessionState.CanAfford(_placementSupportCoordinator != null ? _placementSupportCoordinator.GetTowerCost(towerType) : 0);
    }

    public bool TryUpgradeSelectedStructure()
    {
        if (_sessionState == null || _powerGridCoordinator == null || IsGameOver)
        {
            return false;
        }

        if (_selectedRelayTower != null)
        {
            if (!_powerGridCoordinator.CanUpgradeRelay(
                    _selectedRelayTower,
                    _sessionState.CurrentScrap,
                    out int upgradeCost,
                    out string invalidReason))
            {
                SetStatusMessage(invalidReason);
                RefreshHud();
                return false;
            }

            _sessionState.SetCurrentScrap(_sessionState.CurrentScrap - upgradeCost);
            _powerGridCoordinator.ApplyRelayUpgrade(_selectedRelayTower);
            SetStatusMessage(
                $"Relay #{_selectedRelayTower.RelayNumber} upgraded to LV {_selectedRelayTower.CurrentLevel}. Capacity is now {_selectedRelayTower.SupplyCapacity}.");
            ShowTransientHudNotice($"-{upgradeCost} SCRAP relay upgrade.", 2.2f, HudNoticeTone.Spending);
            InvalidatePlacementAreaOverlayCache();
            RefreshHud();
            return true;
        }

        if (_selectedDefenseTower != null)
        {
            if (!_powerGridCoordinator.CanUpgradeDefenseTower(
                    _selectedDefenseTower,
                    _sessionState.CurrentScrap,
                    out int upgradeCost,
                    out string invalidReason))
            {
                SetStatusMessage(invalidReason);
                RefreshHud();
                return false;
            }

            _sessionState.SetCurrentScrap(_sessionState.CurrentScrap - upgradeCost);
            _powerGridCoordinator.ApplyDefenseTowerUpgrade(_selectedDefenseTower);
            SetStatusMessage(
                $"{GetTowerDisplayName(_selectedDefenseTower.BuildType)} #{_selectedDefenseTower.TowerNumber} upgraded to LV {_selectedDefenseTower.CurrentLevel}. Power demand is now {_selectedDefenseTower.PowerRequired}.");
            ShowTransientHudNotice($"-{upgradeCost} SCRAP tower upgrade.", 2.2f, HudNoticeTone.Spending);
            RefreshHud();
            return true;
        }

        SetStatusMessage("Select a placed relay or defense tower first.");
        return false;
    }

    public bool TryDemolishSelectedStructure()
    {
        if (IsGameOver)
        {
            return false;
        }

        if (_selectedRelayTower != null)
        {
            RelayTower relayTower = _selectedRelayTower;
            int refundAmount = CalculateRelayDemolishRefund(relayTower);
            ClearPlacedStructureSelection();
            Destroy(relayTower.gameObject);
            InvalidatePlacementAreaOverlayCache();
            if (refundAmount > 0)
            {
                AddScrap(refundAmount);
            }

            SetStatusMessage(
                refundAmount > 0
                    ? $"Relay #{relayTower.RelayNumber} dismantled. Refunded {refundAmount} SCRAP."
                    : $"Relay #{relayTower.RelayNumber} dismantled.");
            RefreshHud();
            return true;
        }

        if (_selectedDefenseTower != null)
        {
            DefenseTower defenseTower = _selectedDefenseTower;
            int refundAmount = CalculateDefenseTowerDemolishRefund(defenseTower);
            ClearPlacedStructureSelection();
            Destroy(defenseTower.gameObject);
            InvalidatePlacementAreaOverlayCache();
            if (refundAmount > 0)
            {
                AddScrap(refundAmount);
            }

            SetStatusMessage(
                refundAmount > 0
                    ? $"{GetTowerDisplayName(defenseTower.BuildType)} #{defenseTower.TowerNumber} dismantled. Refunded {refundAmount} SCRAP."
                    : $"{GetTowerDisplayName(defenseTower.BuildType)} #{defenseTower.TowerNumber} dismantled.");
            RefreshHud();
            return true;
        }

        SetStatusMessage("Select a placed relay or defense tower first.");
        return false;
    }


    /// <summary>
    /// 统一输出放置诊断日志。
    /// 这里单独收口，是为了以后可以集中控制开关、格式和节流策略，而不是把 `Debug.Log` 散落在整条放置链路里。
    /// </summary>
    private void LogPlacementDiagnostic(string message)
    {
        if (!enablePlacementDiagnostics)
        {
            return;
        }

        Debug.Log($"[PlacementDebug] {message}", this);
    }

    /// <summary>
    /// 进入 Play 后，立刻用“起手区中心点”做一次非常轻量的放置自检。
    /// 这个方法的价值不是参与真正建塔，而是快速回答一个关键问题：
    /// 如果玩家怎么拖都放不下第一座塔，究竟是交互入口失效了，还是规则本身就把起手点判成了非法。
    /// 这里故意只测两次：
    /// 1. `Relay` 在起手区中心是否合法。
    /// 2. `Defense` 在起手区中心是否合法。
    /// 这样既能提供排查信息，又不会做整区扫描造成额外负担。
    /// </summary>
    private void RunStarterPlacementSanityCheck()
    {
        _placementSupportCoordinator?.RunStarterPlacementSanityCheck();
    }


    /// <summary>
    /// 初始化当前总控依赖的几个核心协作模块。
    /// 包括：塔静态数据目录、输入协调器、HUD 表现层、放置规则入口、放置交互、建塔执行、表现协调与场景装配器。
    /// 这样后续逻辑就能围绕这些边界清晰的对象展开，而不是继续把所有细节塞在总控里。
    /// </summary>
    private void InitializeArchitectureModules()
    {
        _towerCatalog = new TowerCatalog(
            relayDefinition: new TowerDefinition(
                towerType: TowerType.Relay,
                displayName: relayPresentation.DisplayName,
                buildCost: relayTowerCost,
                placementRadius: relayPlacementRadius,
                expansionSquareSize: relayExpansionSquareSize,
                cardRoleSummary: relayPresentation.CardRoleSummary,
                selectionHint: relayPresentation.SelectionHint,
                upgradeFocusSummary: relayPresentation.UpgradeFocusSummary,
                accentColor: relayPresentation.AccentColor,
                cardIconSprite: relayPresentation.CardIconSprite,
                cardIconTint: relayPresentation.CardIconTint,
                cardBackgroundTint: relayPresentation.CardBackgroundTint,
                cardAccentTint: relayPresentation.CardAccentTint),
            singleTargetDefinition: new TowerDefinition(
                towerType: TowerType.SingleTarget,
                displayName: singleTargetPresentation.DisplayName,
                buildCost: singleTargetTowerCost,
                placementRadius: defensePlacementRadius,
                expansionSquareSize: defenseExpansionSquareSize,
                cardRoleSummary: singleTargetPresentation.CardRoleSummary,
                selectionHint: singleTargetPresentation.SelectionHint,
                upgradeFocusSummary: singleTargetPresentation.UpgradeFocusSummary,
                accentColor: singleTargetPresentation.AccentColor,
                cardIconSprite: singleTargetPresentation.CardIconSprite,
                cardIconTint: singleTargetPresentation.CardIconTint,
                cardBackgroundTint: singleTargetPresentation.CardBackgroundTint,
                cardAccentTint: singleTargetPresentation.CardAccentTint),
            slowFieldDefinition: new TowerDefinition(
                towerType: TowerType.SlowField,
                displayName: slowFieldPresentation.DisplayName,
                buildCost: slowFieldTowerCost,
                placementRadius: defensePlacementRadius,
                expansionSquareSize: defenseExpansionSquareSize,
                cardRoleSummary: slowFieldPresentation.CardRoleSummary,
                selectionHint: slowFieldPresentation.SelectionHint,
                upgradeFocusSummary: slowFieldPresentation.UpgradeFocusSummary,
                accentColor: slowFieldPresentation.AccentColor,
                cardIconSprite: slowFieldPresentation.CardIconSprite,
                cardIconTint: slowFieldPresentation.CardIconTint,
                cardBackgroundTint: slowFieldPresentation.CardBackgroundTint,
                cardAccentTint: slowFieldPresentation.CardAccentTint),
            bombardDefinition: new TowerDefinition(
                towerType: TowerType.Bombard,
                displayName: bombardPresentation.DisplayName,
                buildCost: bombardTowerCost,
                placementRadius: defensePlacementRadius,
                expansionSquareSize: defenseExpansionSquareSize,
                cardRoleSummary: bombardPresentation.CardRoleSummary,
                selectionHint: bombardPresentation.SelectionHint,
                upgradeFocusSummary: bombardPresentation.UpgradeFocusSummary,
                accentColor: bombardPresentation.AccentColor,
                cardIconSprite: bombardPresentation.CardIconSprite,
                cardIconTint: bombardPresentation.CardIconTint,
                cardBackgroundTint: bombardPresentation.CardBackgroundTint,
                cardAccentTint: bombardPresentation.CardAccentTint));

        _placementRules = new TowerPlacementRules(
            towerType => _placementSupportCoordinator != null ? _placementSupportCoordinator.GetPlacementRadius(towerType) : 0.5f,
            towerType => _placementSupportCoordinator != null ? _placementSupportCoordinator.GetPlacementCenterOffset(towerType) : Vector2.zero,
            towerType => _placementSupportCoordinator != null ? _placementSupportCoordinator.GetExpansionSquareSize(towerType) : 4.5f);
        _placementSupportCoordinator = new TowerPlacementSupportCoordinator(
            initialPlacementSquareCenter,
            initialPlacementSquareSize,
            starterZoneMarkerFillColor,
            starterZoneMarkerEdgeColor,
            towerCatalogQuery: () => _towerCatalog,
            placementRulesQuery: () => _placementRules,
            placementVisualControllerQuery: () => _placementVisualController,
            placedTowerRootQuery: () => _placedTowerRoot != null ? _placedTowerRoot : placedTowerRootReference,
            buildZoneQuery: () => _buildZone != null ? _buildZone : buildZoneReference,
            placementGridQuery: () => _placementGrid != null ? _placementGrid : placementGridReference,
            relayTowerPrototypeQuery: () => _relayTowerPrototype,
            singleTargetTowerPrototypeQuery: () => _singleTargetTowerPrototype,
            slowFieldTowerPrototypeQuery: () => _slowFieldTowerPrototype,
            bombardTowerPrototypeQuery: () => _bombardTowerPrototype,
            powerGridCoordinatorQuery: () => _powerGridCoordinator,
            isGameOverQuery: () => IsGameOver,
            logPlacementDiagnostic: LogPlacementDiagnostic);
        _powerGridCoordinator = new TowerPowerGridCoordinator(
            mapDefinitionQuery: () => _battlefieldMapDefinition != null ? _battlefieldMapDefinition : battlefieldMapReference,
            logDiagnostic: LogPlacementDiagnostic);
        _inputCoordinator = new TowerDefenseInputCoordinator(
            isGameOverQuery: () => IsGameOver,
            trySelectPlacedStructureAtWorld: TrySelectPlacedStructureAtWorld,
            tryQuickPlacementAtCurrentMouse: () => _placementInteractionController != null &&
                                                   _inputCoordinator != null &&
                                                   _placementInteractionController.TryQuickPlacementAt(_inputCoordinator.GetMouseWorldPosition()),
            tryUpgradeSelectedStructure: TryUpgradeSelectedStructure,
            tryDemolishSelectedStructure: TryDemolishSelectedStructure,
            selectRelayTower: SelectRelayTower,
            selectSingleTargetTower: SelectDefenseTower,
            selectSlowFieldTower: SelectSlowFieldTower,
            selectBombardTower: SelectBombardTower,
            clearSelection: ClearSelection);
        _hudPresenter = new TowerDefenseHudPresenter();
        _placementInteractionController = new TowerPlacementInteractionController(
            isGameOverQuery: () => _sessionState != null && _sessionState.IsGameOver,
            currentScrapQuery: () => _sessionState != null ? _sessionState.CurrentScrap : 0,
            canAffordTower: CanAffordTower,
            getPrototype: GetPrototype,
            getTowerDisplayName: GetTowerDisplayName,
            screenToWorldPosition: screenPosition => _inputCoordinator != null
                ? SnapPlacementWorldPosition(_inputCoordinator.ScreenToWorldPosition(screenPosition))
                : Vector3.zero,
            validatePlacementPosition: ValidatePlacementPosition,
            getPlacementOverlayWorldBounds: GetPlacementOverlayWorldBounds,
            buildPlacementOverlayValidator: towerType => _placementSupportCoordinator != null
                ? _placementSupportCoordinator.BuildPlacementOverlayValidator(towerType)
                : null,
            tryPlaceTowerAt: (worldPosition, towerType) => TryPlaceTowerAt(worldPosition, towerType),
            refreshHud: RefreshHud,
            setStatusMessage: SetStatusMessage,
            logPlacementDiagnostic: LogPlacementDiagnostic);
        _placementBuildExecutor = new TowerPlacementBuildExecutor(
            isGameOverQuery: () => _sessionState != null && _sessionState.IsGameOver,
            currentScrapQuery: () => _sessionState != null ? _sessionState.CurrentScrap : 0,
            setCurrentScrap: value => _sessionState?.SetCurrentScrap(value),
            getTowerCost: GetTowerCost,
            getTowerDisplayName: GetTowerDisplayName,
            getPrototype: GetPrototype,
            getPlacedTowerRoot: () => _placedTowerRoot,
            getPlacementRadius: GetPlacementRadius,
            snapPlacementWorldPosition: SnapPlacementWorldPosition,
            validatePlacementPosition: ValidatePlacementPosition,
            registerPlacedStructure: (structureObject, towerType) => _powerGridCoordinator?.RegisterPlacedStructure(structureObject, towerType),
            invalidatePlacementAreaOverlayCache: InvalidatePlacementAreaOverlayCache,
            refreshHud: RefreshHud,
            setStatusMessage: SetStatusMessage,
            logPlacementDiagnostic: LogPlacementDiagnostic);
        _presentationCoordinator = new TowerDefensePresentationCoordinator(
            sessionStateQuery: () => _sessionState,
            interactionControllerQuery: () => _placementInteractionController,
            placedStructureHudStateQuery: BuildPlacedStructureHudState,
            powerGridHudSnapshotQuery: () => _powerGridCoordinator != null
                ? _powerGridCoordinator.GetHudSnapshot()
                : new PowerGridHudSnapshot(0, 0, 0, 0, 0, 0, 0, string.Empty),
            canAffordTower: CanAffordTower,
            tutorialTowerAvailabilityQuery: GetTutorialTowerAvailability,
            refreshStarterZoneMarker: () => _placementSupportCoordinator?.RefreshStarterZoneMarker());
        _hudPresenter.SetTheme(hudTheme.ToRuntimeTheme());
        _hudPresenter.BindDemolishSelectedStructureButton(() => TryDemolishSelectedStructure());
        _presentationCoordinator.BindPresentation(_hudPresenter, _towerCatalog);
        _sceneBootstrapper = new TowerDefenseSceneBootstrapper();
        InitializeAudioCoordinator();
    }

    /// <summary>
    /// 初始化放置可视化控制器。
    /// 这里会把颜色、排序、资源入口以及规则查询函数一次性注入，
    /// 让可视化层只专注于“怎么显示”，而不是反向知道整局状态或自己去找场景对象。
    /// </summary>
    private void InitializePlacementVisuals()
    {
        _placementVisualController?.Dispose();

        _placementVisualController = new TowerPlacementVisualController(
            placementRingSpriteReference,
            placementRingResourcePath,
            validPreviewColor,
            invalidPreviewColor,
            placementAreaOverlayPixelsPerUnit,
            placementAreaOverlayFillColor,
            placementAreaOverlayEdgeColor,
            placementAreaOverlaySortingOrder,
            starterZoneMarkerFillColor,
            starterZoneMarkerEdgeColor,
            starterZoneMarkerSortingOrder,
            GetPrototype,
            GetTowerDisplayName,
            GetPlacementRadius);

        _placementVisualController.BindPlacementPreviewRoot(_placementPreviewRoot);
        _placementInteractionController?.BindPresentation(_placementVisualController, _hudPresenter, _towerCatalog);

        if (_selectionRangeVisualizer == null)
        {
            _selectionRangeVisualizer = new StructureSelectionRangeVisualizer(GetSelectionRangeColor);
        }

        _selectionRangeVisualizer.BindRoot(_placementPreviewRoot);
    }

    private void InitializeAudioCoordinator()
    {
        if (audioProfileReference == null)
        {
            audioProfileReference = Resources.Load<TowerDefenseAudioProfileAsset>("TowerDefense/Configs/TowerDefenseAudioProfile");
        }

        EnsureAudioSource(ref _backgroundMusicSource);
        EnsureAudioSource(ref _soundEffectSource);

        _backgroundMusicSource.volume = backgroundMusicVolume;
        _soundEffectSource.volume = soundEffectVolume;

        AudioClip resolvedBackgroundMusic = audioProfileReference != null && audioProfileReference.BackgroundMusic != null
            ? audioProfileReference.BackgroundMusic
            : backgroundMusicClip;
        AudioClip resolvedPlaceStructure = audioProfileReference != null && audioProfileReference.PlaceStructureClip != null
            ? audioProfileReference.PlaceStructureClip
            : placeStructureClip;
        AudioClip resolvedSingleTarget = audioProfileReference != null && audioProfileReference.SingleTargetImpactClip != null
            ? audioProfileReference.SingleTargetImpactClip
            : singleTargetImpactClip;
        AudioClip resolvedSlowField = audioProfileReference != null && audioProfileReference.SlowFieldImpactClip != null
            ? audioProfileReference.SlowFieldImpactClip
            : slowFieldImpactClip;
        AudioClip resolvedBombard = audioProfileReference != null && audioProfileReference.BombardImpactClip != null
            ? audioProfileReference.BombardImpactClip
            : bombardImpactClip;

        _audioCoordinator = new TowerDefenseAudioCoordinator(
            _backgroundMusicSource,
            _soundEffectSource,
            resolvedBackgroundMusic,
            resolvedPlaceStructure,
            resolvedSingleTarget,
            resolvedSlowField,
            resolvedBombard);
    }

    private void EnsureAudioSource(ref AudioSource source)
    {
        if (source != null)
        {
            return;
        }

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    /// <summary>
    /// 把场景层和 Inspector 上的放置规则上下文同步给 `TowerPlacementRules`。
    ///
    /// 这一步之所以单独抽出来，是为了避免以后每次场景引用或起手区参数变化时，
    /// 又把散落的同步代码塞回 `Start / FindSceneReferences / EnsureRuntimeRoots` 这些生命周期方法里。
    /// </summary>
    private void RefreshPlacementRuleContext()
    {
        _placementSupportCoordinator?.RefreshPlacementRuleContext();
    }

    /// <summary>
    /// 刷新 HUD。
    /// 当前这一步已经继续下沉到 `_presentationCoordinator`，
    /// 所以总控这里保留的是一个稳定门面，方便其他模块仍然通过统一入口触发表现刷新。
    /// </summary>
    private void RefreshHud()
    {
        RefreshSelectedStructureRangeVisualization();
        _hudPresenter?.SetDemolishSelectedStructureButtonVisible(HasSelectedPlacedStructure());
        _presentationCoordinator?.RefreshHud();
    }

    public void NotifyStructureTopologyChanged()
    {
        if (_selectedRelayTower == null)
        {
            _selectedRelayTower = null;
        }

        if (_selectedDefenseTower == null)
        {
            _selectedDefenseTower = null;
        }

        _powerGridCoordinator?.NotifyTopologyChanged();
        RefreshHud();
    }

    private void ClearPlacedStructureSelection()
    {
        _selectedRelayTower = null;
        _selectedDefenseTower = null;
        _selectionRangeVisualizer?.Hide();
    }

    private bool HasSelectedPlacedStructure()
    {
        return _selectedRelayTower != null || _selectedDefenseTower != null;
    }

    /// <summary>
    /// Tries to select an already-placed structure under the given world position.
    ///
    /// We route this through the game coordinator instead of relying only on `OnMouseDown()`
    /// because scene UI can legitimately block some Unity mouse callbacks. The input layer now
    /// asks gameplay first: "is this click actually selecting an existing structure?" before it
    /// falls through to quick-build placement.
    /// </summary>
    private bool TrySelectPlacedStructureAtWorld(Vector3 worldPosition)
    {
        Transform placedTowerRoot = _placedTowerRoot != null ? _placedTowerRoot : placedTowerRootReference;
        if (placedTowerRoot == null)
        {
            return false;
        }

        DefenseTower bestDefenseTower = null;
        RelayTower bestRelayTower = null;
        float bestDistanceSqr = float.MaxValue;

        for (int index = 0; index < placedTowerRoot.childCount; index++)
        {
            Transform placedStructure = placedTowerRoot.GetChild(index);
            if (placedStructure == null)
            {
                continue;
            }

            DefenseTower defenseTower = placedStructure.GetComponent<DefenseTower>();
            if (defenseTower != null)
            {
                if (TryMeasureStructureSelectionHit(placedStructure, worldPosition, out float defenseDistanceSqr) &&
                    defenseDistanceSqr < bestDistanceSqr)
                {
                    bestDefenseTower = defenseTower;
                    bestRelayTower = null;
                    bestDistanceSqr = defenseDistanceSqr;
                }

                continue;
            }

            RelayTower relayTower = placedStructure.GetComponent<RelayTower>();
            if (relayTower != null &&
                TryMeasureStructureSelectionHit(placedStructure, worldPosition, out float relayDistanceSqr) &&
                relayDistanceSqr < bestDistanceSqr)
            {
                bestRelayTower = relayTower;
                bestDefenseTower = null;
                bestDistanceSqr = relayDistanceSqr;
            }
        }

        if (bestDefenseTower != null)
        {
            SelectPlacedStructure(bestDefenseTower);
            return true;
        }

        if (bestRelayTower != null)
        {
            SelectPlacedStructure(bestRelayTower);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Measures whether the player click should count as hitting an already placed structure.
    ///
    /// We intentionally avoid relying only on global 2D overlap queries here:
    /// - some placed-structure colliders are triggers
    /// - trigger query behavior can be scene / project setting dependent
    /// - scene-authored art and collider offsets are not always centered the same way
    ///
    /// Instead, selection uses a layered fallback:
    /// 1. primary collider bounds
    /// 2. primary sprite renderer bounds
    /// 3. a small radius fallback around the structure pivot
    ///
    /// This makes "click the visible tower / relay" much more forgiving and reliable in Play.
    /// </summary>
    private static bool TryMeasureStructureSelectionHit(Transform structureTransform, Vector3 worldPosition, out float distanceSqr)
    {
        distanceSqr = float.MaxValue;
        if (structureTransform == null)
        {
            return false;
        }

        Vector2 clickPoint = worldPosition;
        Vector2 pivotPoint = structureTransform.position;
        distanceSqr = (clickPoint - pivotPoint).sqrMagnitude;

        Collider2D primaryCollider = structureTransform.GetComponent<Collider2D>();
        if (primaryCollider != null)
        {
            Vector3 boundsPoint = new Vector3(worldPosition.x, worldPosition.y, primaryCollider.bounds.center.z);
            if (primaryCollider.bounds.Contains(boundsPoint))
            {
                return true;
            }

            if (primaryCollider is CircleCollider2D circleCollider)
            {
                Vector2 worldCenter = circleCollider.transform.TransformPoint(circleCollider.offset);
                float worldRadius = circleCollider.radius * Mathf.Max(
                    Mathf.Abs(circleCollider.transform.lossyScale.x),
                    Mathf.Abs(circleCollider.transform.lossyScale.y));
                distanceSqr = (clickPoint - worldCenter).sqrMagnitude;
                if (distanceSqr <= worldRadius * worldRadius)
                {
                    return true;
                }
            }
        }

        SpriteRenderer primaryRenderer = structureTransform.GetComponent<SpriteRenderer>();
        if (primaryRenderer != null)
        {
            Bounds rendererBounds = primaryRenderer.bounds;
            Vector3 boundsPoint = new Vector3(worldPosition.x, worldPosition.y, rendererBounds.center.z);
            if (rendererBounds.Contains(boundsPoint))
            {
                distanceSqr = (clickPoint - (Vector2)rendererBounds.center).sqrMagnitude;
                return true;
            }

            float rendererRadius = Mathf.Max(rendererBounds.extents.x, rendererBounds.extents.y);
            if (rendererRadius > 0.01f)
            {
                distanceSqr = (clickPoint - (Vector2)rendererBounds.center).sqrMagnitude;
                if (distanceSqr <= rendererRadius * rendererRadius)
                {
                    return true;
                }
            }
        }

        return distanceSqr <= 0.45f * 0.45f;
    }

    private PlacedStructureHudState BuildPlacedStructureHudState()
    {
        if (_selectedRelayTower != null)
        {
            string detail =
                $"POWER {_selectedRelayTower.RemainingCapacity} / {_selectedRelayTower.SupplyCapacity}";
            return new PlacedStructureHudState(true, "Relay Node", detail);
        }

        if (_selectedDefenseTower != null)
        {
            string powerState = _selectedDefenseTower.IsPowered ? "powered" : "unpowered";
            string detail =
                $"POWER {_selectedDefenseTower.PowerRequired}  |  STATE {powerState}";
            return new PlacedStructureHudState(true, GetTowerDisplayName(_selectedDefenseTower.BuildType), detail);
        }

        TowerType selectedTowerType = _placementInteractionController != null
            ? _placementInteractionController.SelectedTowerType
            : TowerType.None;
        if (selectedTowerType == TowerType.SingleTarget ||
            selectedTowerType == TowerType.SlowField ||
            selectedTowerType == TowerType.Bombard)
        {
            GameObject prototype = GetPrototype(selectedTowerType);
            DefenseTower prototypeTower = prototype != null ? prototype.GetComponent<DefenseTower>() : null;
            if (prototypeTower != null)
            {
                return new PlacedStructureHudState(true, GetTowerDisplayName(selectedTowerType), $"POWER {prototypeTower.PowerRequired}");
            }
        }

        return new PlacedStructureHudState(false, string.Empty, string.Empty);
    }

    public void SelectPlacedStructure(RelayTower relayTower)
    {
        if (relayTower == null || IsGameOver)
        {
            return;
        }

        _placementInteractionController?.CancelPlacementDrag();
        _placementInteractionController?.SetSelectionSilently(TowerType.None);
        _selectedRelayTower = relayTower;
        _selectedDefenseTower = null;
        int refundAmount = CalculateRelayDemolishRefund(relayTower);
        SetStatusMessage($"Selected relay #{relayTower.RelayNumber}. Press U to upgrade or click Delete to dismantle for {refundAmount} SCRAP.");
        RefreshHud();
    }

    public void SelectPlacedStructure(DefenseTower defenseTower)
    {
        if (defenseTower == null || IsGameOver)
        {
            return;
        }

        _placementInteractionController?.CancelPlacementDrag();
        _placementInteractionController?.SetSelectionSilently(TowerType.None);
        _selectedDefenseTower = defenseTower;
        _selectedRelayTower = null;
        int refundAmount = CalculateDefenseTowerDemolishRefund(defenseTower);
        SetStatusMessage($"Selected {GetTowerDisplayName(defenseTower.BuildType)} #{defenseTower.TowerNumber}. Press U to upgrade or click Delete to dismantle for {refundAmount} SCRAP.");
        RefreshHud();
    }

    /// <summary>
    /// 触发 Game Over。
    /// 这里保留玩法层面的结算切态：
    /// - 标记会话进入 Game Over
    /// - 强制取消当前部署交互
    /// - 暂停时间
    /// 而 HUD 广播、面板显示和血条隐藏，则继续交给 `_presentationCoordinator`。
    /// </summary>
    private void ShowGameOver()
    {
        if (_sessionState != null)
        {
            _sessionState.MarkGameOver();
        }

        _placementInteractionController?.ForceCancelPlacementDrag();
        _gameOverRestartTriggered = false;
        _gameOverRestartAllowedAtUnscaledTime = Time.unscaledTime + Mathf.Max(0f, gameOverRestartClickDelaySeconds);
        Time.timeScale = 0f;
        ShowFormalGameOverPresentation();
    }

    /// <summary>
    /// 胜利后不再立即切剧情，
    /// 而是先进入一个短暂停住的胜利界面，
    /// 等玩家点击确认后再继续切场。
    ///
    /// 这样胜利反馈就不会像以前那样一闪而过，
    /// 也更符合你现在希望的“塔防胜利 -> 胜利界面 -> 点击继续 -> 2D 剧情”的节奏。
    /// </summary>
    public bool BeginLevelClearSequence(
        bool continueCampaignAfterClear,
        string fallbackNextSceneNameAfterClear,
        float fadeOutToBlackDurationAfterClear,
        float fadeInFromBlackDurationAfterClear,
        bool startOpaqueOnContinue = false)
    {
        if (IsGameOver || _levelClearPresentationActive || _levelClearContinueTriggered)
        {
            return false;
        }

        _placementInteractionController?.ForceCancelPlacementDrag();
        _continueCampaignAfterLevelClear = continueCampaignAfterClear;
        _fallbackNextSceneNameAfterLevelClear = fallbackNextSceneNameAfterClear ?? string.Empty;
        _levelClearFadeOutDuration = Mathf.Max(0f, fadeOutToBlackDurationAfterClear);
        _levelClearFadeInDuration = Mathf.Max(0f, fadeInFromBlackDurationAfterClear);
        _levelClearStartOpaqueOnContinue = startOpaqueOnContinue;
        _levelClearPresentationActive = true;
        _levelClearContinueTriggered = false;
        _levelClearContinueAllowedAtUnscaledTime = Time.unscaledTime + Mathf.Max(0f, levelClearContinueClickDelaySeconds);

        Time.timeScale = 0f;
        ShowFormalVictoryPresentation();
        return true;
    }

    /// <summary>
    /// 失败后的补救路径非常明确：
    /// 先停住战斗并展示 Game Over，
    /// 然后在一个很短的无缩放时间窗口后，允许玩家点击任意位置重开当前关卡。
    /// 这样既能保证玩家看见失败反馈，也不会被迫回到别的入口重新找关卡。
    /// </summary>
    private void HandleGameOverRestartInput()
    {
        if (!restartCurrentSceneOnGameOverClick || !IsGameOver || _gameOverRestartTriggered)
        {
            return;
        }

        if (Time.unscaledTime < _gameOverRestartAllowedAtUnscaledTime)
        {
            return;
        }

        if (!DidPressRestartAfterGameOver())
        {
            return;
        }

        RestartCurrentScene();
    }

    private static bool DidPressRestartAfterGameOver()
    {
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        for (int index = 0; index < Input.touchCount; index++)
        {
            if (Input.GetTouch(index).phase == TouchPhase.Began)
            {
                return true;
            }
        }

        return false;
    }

    private void RestartCurrentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.name))
        {
            Debug.LogWarning("TowerDefenseGame could not restart the current scene because the active scene name was unavailable.", this);
            return;
        }

        _gameOverRestartTriggered = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
    }

    private bool HandleLevelClearContinueInput()
    {
        if (!_levelClearPresentationActive)
        {
            return false;
        }

        if (Time.unscaledTime < _levelClearContinueAllowedAtUnscaledTime)
        {
            return true;
        }

        if (!DidPressRestartAfterGameOver())
        {
            return true;
        }

        return HandleLevelClearContinueInputFromUi();
    }

    private bool HandleLevelClearContinueInputFromUi()
    {
        if (_levelClearContinueTriggered)
        {
            return true;
        }

        _levelClearContinueTriggered = true;
        Time.timeScale = 1f;

        bool shouldAdvanceCampaign = _continueCampaignAfterLevelClear && CampaignFlowController.HasActiveCampaign;
        bool transitioned = shouldAdvanceCampaign
            ? CampaignFlowController.AdvanceToNextStepOrLoadFallback(
                _fallbackNextSceneNameAfterLevelClear,
                _levelClearFadeOutDuration,
                _levelClearFadeInDuration,
                _levelClearStartOpaqueOnContinue)
            : !string.IsNullOrWhiteSpace(_fallbackNextSceneNameAfterLevelClear) &&
              CampaignFlowController.AdvanceToNextStepOrLoadFallback(
                  _fallbackNextSceneNameAfterLevelClear,
                  _levelClearFadeOutDuration,
                  _levelClearFadeInDuration,
                  _levelClearStartOpaqueOnContinue);

        if (!transitioned)
        {
            Debug.LogWarning("TowerDefenseGame 无法在胜利界面后继续切场：既没有活动战役流程，也没有有效的 fallback 场景名。", this);
            _levelClearContinueTriggered = false;
            return true;
        }

        HideForSceneTransition();
        _levelClearPresentationActive = false;
        return true;
    }

    /// <summary>
    /// 在胜利点击继续后的真正切场开始前，
    /// 先把当前塔防场景里最醒目的可见层收掉。
    ///
    /// 这里优先隐藏：
    /// - HUD 与结果面板
    /// - 路线预览 / 放置覆盖层
    /// - 选中范围线
    ///
    /// 再叠加立即黑场，
    /// 玩家看到的就会是干净纯黑，而不是塔防界面残留。
    /// </summary>
    private void HideForSceneTransition()
    {
        if (victoryResultPageViewReference != null)
        {
            victoryResultPageViewReference.Hide();
        }

        _presentationCoordinator?.HideGameplayPresentationForSceneTransition();
        _selectionRangeVisualizer?.Hide();
        _placementSupportCoordinator?.HidePlacementAreaOverlay();
    }


    /// <summary>
    /// 对总控内部与外部兼容层保留一个统一的“真正建塔”入口。
    /// 现在具体执行细节已经下沉到 `_placementBuildExecutor`，
    /// 所以这个方法更像一个稳定门面，避免别的脚本将来直接耦合到执行器实现。
    /// </summary>
    private bool TryPlaceTowerAt(Vector3 worldPosition, TowerType towerType, BuildPad ownerPad = null)
    {
        return _placementBuildExecutor != null &&
               _placementBuildExecutor.TryPlaceTowerAt(SnapPlacementWorldPosition(worldPosition), towerType, ownerPad);
    }

    /// <summary>
    /// 所有“玩家想在这里建”的世界坐标都先经过这一层。
    ///
    /// 这样拖拽、快速点击和旧 BuildPad 兼容入口都会使用同一套格子锚点，
    /// 避免出现“预览看起来在格子上，真正落塔却落在鼠标原点”的错位。
    /// </summary>
    private Vector3 SnapPlacementWorldPosition(Vector3 worldPosition)
    {
        PlacementGrid placementGrid = _placementGrid != null ? _placementGrid : placementGridReference;
        return placementGrid != null ? placementGrid.SnapWorldPosition(worldPosition) : worldPosition;
    }

    /// <summary>
    /// 这是总控侧的放置校验入口。
    /// 当前真正的规则判断已经下沉到 `TowerPlacementRules`，这里主要负责把忽略条件和输出消息统一转发进去。
    /// 下面保留的 `#if false` 旧实现只作为历史对照，不参与运行时判定。
    /// </summary>
    private bool ValidatePlacementPosition(Vector3 worldPosition, TowerType towerType, out string invalidReason)
    {
        if (_placementSupportCoordinator != null)
        {
            return _placementSupportCoordinator.ValidatePlacementPosition(worldPosition, towerType, out invalidReason);
        }

        invalidReason = "Placement support is not initialized.";
        return false;
#if false
        invalidReason = string.Empty;

        if (_buildZone == null)
        {
            invalidReason = "No BuildZone is configured in this level.";
            return false;
        }

        if (!_buildZone.ContainsPoint(worldPosition))
        {
            invalidReason = "Outside the level's buildable area.";
            return false;
        }

        if (!IsWithinPlacementNetwork(worldPosition, out invalidReason))
        {
            return false;
        }

        float placementRadius = GetPlacementRadius(towerType);
        int overlapCount = Physics2D.OverlapCircleNonAlloc(worldPosition, placementRadius, _placementValidationOverlapBuffer);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = _placementValidationOverlapBuffer[i];
            if (overlap == null)
            {
                continue;
            }

            if (_placementVisualController != null && _placementVisualController.ContainsPreviewTransform(overlap.transform))
            {
                continue;
            }

            PlacementBlocker blocker = overlap.GetComponentInParent<PlacementBlocker>();
            if (blocker != null)
            {
                invalidReason = blocker.BlockerReason;
                return false;
            }


            // 这里再补一层边界判断，只把真正挂在 `PlacedTowers` 根节点下的正式塔实例算作已建结构。
            Transform placedTowerRoot = _placedTowerRoot != null ? _placedTowerRoot : placedTowerRootReference;
            bool belongsToPlacedTower = placedTowerRoot != null && overlap.transform.IsChildOf(placedTowerRoot);
            if (belongsToPlacedTower && (overlap.GetComponentInParent<DefenseTower>() != null || overlap.GetComponentInParent<RelayTower>() != null))
            {
                invalidReason = "Too close to another structure. Move it a little.";
                return false;
            }
        }


        return true;
#endif
    }

    /// <summary>
    /// 放置规则层本身不应该知道“预览对象”是谁。
    ///
    /// 这里由总控提供一个非常窄的忽略入口：
    /// - 如果当前重叠对象属于预览塔，就忽略
    /// - 其他对象仍然全部交给规则层判断
    ///
    /// 这样既保住了解耦，也不会丢掉之前修首塔误判时建立的那层边界。
    /// </summary>
    private bool ShouldIgnorePlacementTransform(Transform candidate)
    {
        return _placementSupportCoordinator != null && _placementSupportCoordinator.ShouldIgnorePlacementTransform(candidate);
    }

    /// <summary>
    /// 读取塔型对应的占地半径。
    /// </summary>
    private float GetPlacementRadius(TowerType towerType)
    {
        return _placementSupportCoordinator != null ? _placementSupportCoordinator.GetPlacementRadius(towerType) : 0.5f;
    }

    private int CalculateRelayDemolishRefund(RelayTower relayTower)
    {
        if (relayTower == null)
        {
            return 0;
        }

        int totalInvestedCost = Mathf.Max(0, relayTowerCost) + relayTower.CalculateAccumulatedUpgradeCost();
        return Mathf.FloorToInt(totalInvestedCost * DemolishRefundRatio);
    }

    private int CalculateDefenseTowerDemolishRefund(DefenseTower defenseTower)
    {
        if (defenseTower == null)
        {
            return 0;
        }

        int totalInvestedCost = Mathf.Max(0, GetTowerCost(defenseTower.BuildType)) + defenseTower.CalculateAccumulatedUpgradeCost();
        return Mathf.FloorToInt(totalInvestedCost * DemolishRefundRatio);
    }

    /// <summary>
    /// 读取塔型对应的扩张方格边长。
    /// </summary>
    private float GetExpansionSquareSize(TowerType towerType)
    {
        return _placementSupportCoordinator != null ? _placementSupportCoordinator.GetExpansionSquareSize(towerType) : 4.5f;
    }

    /// <summary>
    /// 给 Scene Gizmo 和后续编辑器工具读取当前建筑周边禁建方形大小。
    ///
    /// 它本质上复用现有的 `ExpansionSquareSize` 配置：
    /// - 旧名字暂时保留，避免破坏现有场景序列化数据
    /// - 新语义是“已放置建筑周围生成多大的方形禁建格”
    /// </summary>
    public float GetPlacementNoBuildSquareSize(TowerType towerType)
    {
        return GetExpansionSquareSize(towerType);
    }

    /// <summary>
    /// 计算合法区域覆盖层需要扫描的世界边界。
    /// 这一步很重要，因为它决定覆盖层只扫描和当前部署网络相关的区域，
    /// 而不是每次都把整张 `BuildZone` 全量采样一遍。
    /// </summary>
    private Bounds GetPlacementOverlayWorldBounds(TowerType towerType)
    {
        return _placementSupportCoordinator != null
            ? _placementSupportCoordinator.GetPlacementOverlayWorldBounds(towerType)
            : new Bounds(Vector3.zero, Vector3.zero);
    }

    /// <summary>
    /// 预热指定塔型的合法区域覆盖层。
    /// 常见调用时机是悬停部署卡或刚切换选中塔型时，
    /// 目的是把代价提前摊掉，减少真正开始拖拽那一瞬间的卡顿感。
    /// </summary>
    public void PrewarmPlacementAreaOverlay(TowerType towerType)
    {
        _placementSupportCoordinator?.PrewarmPlacementAreaOverlay(towerType);
    }

    /// <summary>
    /// 标记合法区域覆盖层缓存失效。
    /// 当场上的塔布局变化后，旧缓存就不再可信，下一次需要重新生成。
    /// </summary>
    private void InvalidatePlacementAreaOverlayCache()
    {
        _placementSupportCoordinator?.InvalidatePlacementAreaOverlayCache();
    }

    /// <summary>
    /// 隐藏合法区域覆盖层。
    /// </summary>
    private void HidePlacementAreaOverlay()
    {
        _placementSupportCoordinator?.HidePlacementAreaOverlay();
    }

    /// <summary>
    /// 同步首塔起手区标记的显隐。
    /// 每次 HUD 刷新或放置状态变化时都会走这里，保证“首塔前显示、首塔后隐藏”的规则稳定成立。
    /// </summary>
    private void RefreshStarterZoneMarker()
    {
        _placementSupportCoordinator?.RefreshStarterZoneMarker();
    }

    /// <summary>
    /// 判断当前是否应该显示首塔起手区标记。
    /// 只有在还没放下任何塔、并且没有进入结算时，这块提示区域才应该出现。
    /// </summary>
    private bool ShouldShowStarterZoneMarker()
    {
        return _placementSupportCoordinator != null && _placementSupportCoordinator.ShouldShowStarterZoneMarker();
    }

    /// <summary>
    /// 常规 Scene 视图 Gizmo 入口。
    /// 当前主要用它在不进 Play 的情况下，把首塔起手区直接画在编辑器里。
    /// </summary>
    private void OnDrawGizmos()
    {
        if (Application.isPlaying || !ShouldShowStarterZoneMarker())
        {
            return;
        }

        _placementSupportCoordinator?.DrawStarterZoneGizmo();
    }

    /// <summary>
    /// 选中对象时也绘制起手区 Gizmo，方便调整时更容易看清边界。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying || !ShouldShowStarterZoneMarker())
        {
            return;
        }

        _placementSupportCoordinator?.DrawStarterZoneGizmo();
    }

    /// <summary>
    /// 读取塔的建造成本。
    /// 如果目录还没准备好，就返回 `0`，避免空引用把整条购买链路打断。
    /// </summary>
    private int GetTowerCost(TowerType towerType)
    {
        return _placementSupportCoordinator != null ? _placementSupportCoordinator.GetTowerCost(towerType) : 0;
    }

    /// <summary>
    /// 读取塔的显示名称。
    /// 如果目录还没准备好，就返回一个安全的占位文本。
    /// </summary>
    private string GetTowerDisplayName(TowerType towerType)
    {
        return _placementSupportCoordinator != null ? _placementSupportCoordinator.GetTowerDisplayName(towerType) : "None";
    }

    /// <summary>
    /// 根据塔型拿到对应的原型体。
    /// </summary>
    private GameObject GetPrototype(TowerType towerType)
    {
        return _placementSupportCoordinator != null ? _placementSupportCoordinator.GetPrototype(towerType) : null;
    }

    /// <summary>
    /// 把场景里的显式引用读进总控运行时字段。
    /// 现在具体装配细节已经下沉到 `_sceneBootstrapper`，
    /// 所以总控这里保留一个稳定门面，负责取回装配结果并继续把它分发给其他运行时子模块。
    /// </summary>
    private void FindSceneReferences()
    {
        if (_sceneBootstrapper == null)
        {
            return;
        }

        TowerDefenseSceneBootstrapResult bootstrapResult = _sceneBootstrapper.BootstrapScene(
            mainCameraReference,
            relayTowerPrototypeReference,
            singleTargetTowerPrototypeReference,
            slowFieldTowerPrototypeReference,
            bombardTowerPrototypeReference,
            placedTowerRootReference,
            placedTowerRootName,
            placementPreviewRootReference,
            placementPreviewRootName,
            buildZoneReference,
            buildZoneName,
            placementGridReference,
            placementGridName,
            placementGridCellSize,
            placementGridOrigin,
            snapPlacementToCellCenter,
            relayFootprintCells,
            defenseFootprintCells,
            new TowerDefenseHudSceneReferences(
                scrapTextReference,
                baseHealthTextReference,
                waveTextReference,
                selectionTextReference,
                structureStatusTextReference,
                relayTowerButtonReference,
                defenseTowerButtonReference,
                slowFieldTowerButtonReference,
                bombardTowerButtonReference,
                clearSelectionButtonReference,
                demolishSelectedStructureButtonReference,
                gameOverPanelReference,
                gameOverTitleReference,
                gameOverHintReference,
                dragPreviewPanelReference,
                dragPreviewLabelReference),
            _hudPresenter);

        _mainCamera = bootstrapResult.MainCamera;
        _relayTowerPrototype = bootstrapResult.RelayTowerPrototype;
        _singleTargetTowerPrototype = bootstrapResult.SingleTargetTowerPrototype;
        _slowFieldTowerPrototype = bootstrapResult.SlowFieldTowerPrototype;
        _bombardTowerPrototype = bootstrapResult.BombardTowerPrototype;
        _buildZone = bootstrapResult.BuildZone;
        _placementGrid = bootstrapResult.PlacementGrid;
        _placedTowerRoot = bootstrapResult.PlacedTowerRoot;
        _placementPreviewRoot = bootstrapResult.PlacementPreviewRoot;
        _battlefieldMapDefinition = battlefieldMapReference != null ? battlefieldMapReference : FindFirstObjectByType<BattlefieldMapDefinition>();
        _hudPresenter.BindDemolishSelectedStructureButton(() => TryDemolishSelectedStructure());

        mainCameraReference = _mainCamera;
        buildZoneReference = _buildZone;
        placementGridReference = _placementGrid;
        singleTargetTowerPrototypeReference = _singleTargetTowerPrototype;
        slowFieldTowerPrototypeReference = _slowFieldTowerPrototype;
        bombardTowerPrototypeReference = _bombardTowerPrototype;
        placedTowerRootReference = _placedTowerRoot;
        placementPreviewRootReference = _placementPreviewRoot;
        battlefieldMapReference = _battlefieldMapDefinition;
        _inputCoordinator?.BindMainCamera(_mainCamera);
        _powerGridCoordinator?.BindPlacedTowerRoot(_placedTowerRoot);

        if (_mainCamera == null)
        {
            Debug.LogWarning("TowerDefenseGame is missing Main Camera reference. Camera.main fallback also failed.");
        }

        if (_relayTowerPrototype == null || _singleTargetTowerPrototype == null || _slowFieldTowerPrototype == null || _bombardTowerPrototype == null)
        {
            Debug.LogWarning("TowerDefenseGame is missing one or more tower prototype references. Check the scene wiring.");
        }
    }

    private void EnsureVictoryResultPageView(VictoryResultPageView.ResultPageTone tone)
    {
        // Destroy any existing page view with the wrong prefab origin.
        if (victoryResultPageViewReference != null)
        {
            Destroy(victoryResultPageViewReference.gameObject);
            victoryResultPageViewReference = null;
        }

        string resourcePath = tone == VictoryResultPageView.ResultPageTone.Failure
            ? FailureResultPagePrefabResourcePath
            : VictoryResultPagePrefabResourcePath;

        GameObject prefabRoot = Resources.Load<GameObject>(resourcePath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"TowerDefenseGame could not load result page prefab from Resources path '{resourcePath}'.", this);
            return;
        }

        GameObject instantiatedRoot = Instantiate(prefabRoot);
        instantiatedRoot.name = prefabRoot.name;
        instantiatedRoot.transform.localScale = Vector3.one;

        VictoryResultPageView instantiatedView = instantiatedRoot.GetComponent<VictoryResultPageView>();
        if (instantiatedView == null)
        {
            instantiatedView = instantiatedRoot.AddComponent<VictoryResultPageView>();
        }

        instantiatedView.Hide();
        victoryResultPageViewReference = instantiatedView;
    }

    private void ShowFormalVictoryPresentation()
    {
        EnsureVictoryResultPageView(VictoryResultPageView.ResultPageTone.Victory);
        if (victoryResultPageViewReference != null)
        {
            victoryResultPageViewReference.BindContinueAction(ContinueAfterVictoryPresentation);
            VictoryResultPageContent content = BuildVictoryResultPageContent();
            victoryResultPageViewReference.Show(content);
            _presentationCoordinator?.ShowVictorySummaryOnly();
            return;
        }

        _presentationCoordinator?.ShowVictory();
    }

    private void ShowFormalGameOverPresentation()
    {
        EnsureVictoryResultPageView(VictoryResultPageView.ResultPageTone.Failure);
        if (victoryResultPageViewReference != null)
        {
            victoryResultPageViewReference.BindContinueAction(RetryAfterFailurePresentation);
            VictoryResultPageContent content = BuildFailureResultPageContent();
            victoryResultPageViewReference.Show(content);
            _presentationCoordinator?.ShowGameOverSummaryOnly();
            return;
        }

        _presentationCoordinator?.ShowGameOver();
    }

    private VictoryResultPageContent BuildVictoryResultPageContent()
    {
        int currentBaseHealth = _sessionState != null ? _sessionState.CurrentBaseHealth : 0;
        int startingBase = Mathf.Max(1, startingBaseHealth);
        int integrityPercent = Mathf.RoundToInt((currentBaseHealth / (float)startingBase) * 100f);
        int currentScrap = _sessionState != null ? _sessionState.CurrentScrap : 0;
        int currentWave = _sessionState != null ? _sessionState.CurrentWave : 0;
        int totalWaves = _sessionState != null ? _sessionState.TotalWaves : 0;
        string commanderLine = integrityPercent >= 80
            ? "做得不错，这一波我们守住了。"
            : "守得很好，虽然代价不小，但防线仍在我们手里。";

        return new VictoryResultPageContent(
            tone: VictoryResultPageView.ResultPageTone.Victory,
            signalTitle: "指挥链路接通",
            signalStatus: "战区信号稳定",
            signalChannel: "HOLO-LINK / FRONT 02",
            title: "防线稳定",
            subtitle: "战术全息简报已生成",
            reportHeader: "战区汇报",
            integrityRow: $"基地完整度：{Mathf.Clamp(integrityPercent, 0, 999)}%",
            scrapRow: $"当前废料：{currentScrap}",
            eventRow: $"关键记录：第 {Mathf.Max(1, currentWave)} 波已完成，当前进度 {currentWave}/{Mathf.Max(1, totalWaves)}。",
            footerHint: "等待接收后续指令",
            commanderName: "指挥官",
            commanderCodename: "前线总控 / C-07",
            dialogueText: commanderLine + "\n前线已经稳定，准备接收下一阶段指令。",
            continueButtonText: "继续汇报",
            continueHintText: "同步完成后可继续推进剧情");
    }

    private VictoryResultPageContent BuildFailureResultPageContent()
    {
        int currentBaseHealth = _sessionState != null ? _sessionState.CurrentBaseHealth : 0;
        int startingBase = Mathf.Max(1, startingBaseHealth);
        int integrityPercent = Mathf.RoundToInt((currentBaseHealth / (float)startingBase) * 100f);
        int currentScrap = _sessionState != null ? _sessionState.CurrentScrap : 0;
        int currentWave = _sessionState != null ? _sessionState.CurrentWave : 0;
        int totalWaves = _sessionState != null ? _sessionState.TotalWaves : 0;
        string commanderLine = integrityPercent <= 0
            ? "核心防区已经失守，我们丢掉了整条前线。"
            : "敌军已经撕开防线，当前部署没能把战区稳住。";

        return new VictoryResultPageContent(
            tone: VictoryResultPageView.ResultPageTone.Failure,
            signalTitle: "指挥链路震荡",
            signalStatus: "战区失稳 / 核心告警",
            signalChannel: "DISTRESS // CORE BREACH",
            title: "防线崩溃",
            subtitle: "失利全息警报已锁定",
            reportHeader: "战损断面",
            integrityRow: $"基地完整度：{Mathf.Clamp(integrityPercent, 0, 999)}%",
            scrapRow: $"残余废料：{currentScrap}",
            eventRow: $"战损焦点：第 {Mathf.Max(1, currentWave)} 波撕开防线，核心节点失守，当前进度 {currentWave}/{Mathf.Max(1, totalWaves)}。",
            footerHint: "立即重组部署序列",
            commanderName: "指挥官",
            commanderCodename: "前线总控 / C-07",
            dialogueText: commanderLine + "\n放弃旧部署思路，立刻重算塔位、供电和火力覆盖，马上回到战区。",
            continueButtonText: "紧急重部署",
            continueHintText: "点击按钮或任意位置，立即回到当前关卡");
    }

    /// <summary>
    /// 让规则层和可视化层继续拿到当前真正可用的运行时根节点。
    /// 由于根节点的确保存在已经由 `_sceneBootstrapper` 处理，
    /// 这里主要负责把结果同步给其他子模块，而不是继续自己创建对象。
    /// </summary>
    private void EnsureRuntimeRoots()
    {
        placedTowerRootReference = _placedTowerRoot;
        placementPreviewRootReference = _placementPreviewRoot;
        RefreshPlacementRuleContext();
        _placementVisualController?.BindPlacementPreviewRoot(_placementPreviewRoot);
        _selectionRangeVisualizer?.BindRoot(_placementPreviewRoot);
    }

    /// <summary>
    /// 同步“当前已选中建筑”的范围显示。
    ///
    /// 这里故意把刷新入口挂在 `RefreshHud()` 之前，
    /// 因为选中建筑后的可视化和状态栏本质上是同一批状态变化：
    /// - HUD 要更新标题 / 详情
    /// - 世界里的范围辅助线也要跟着切换
    ///
    /// 这样外部只需要继续走现有的 `RefreshHud()` 门面，
    /// 不需要再记住额外调用一套“刷新选中范围”的分支。
    /// </summary>
    private void RefreshSelectedStructureRangeVisualization()
    {
        if (_selectionRangeVisualizer == null)
        {
            return;
        }

        if (_selectedRelayTower != null)
        {
            _selectionRangeVisualizer.ShowRelayCoverage(_selectedRelayTower);
            return;
        }

        if (_selectedDefenseTower != null)
        {
            _selectionRangeVisualizer.ShowDefenseRange(_selectedDefenseTower);
            return;
        }

        _selectionRangeVisualizer.Hide();
    }

    /// <summary>
    /// 统一解析“当前这类建筑的范围线颜色”。
    ///
    /// 这里优先复用塔目录里已经存在的语义强调色，
    /// 保持：
    /// - 继电器仍然偏橙
    /// - 单体塔偏蓝
    /// - 减速塔偏青绿
    /// - 轰炸塔偏橙红
    ///
    /// 这样玩家在看 UI 卡片颜色和世界范围线时，会自然把两层认知连起来。
    /// </summary>
    private Color GetSelectionRangeColor(TowerType towerType)
    {
        if (_towerCatalog != null && _towerCatalog.TryGetDefinition(towerType, out TowerDefinition definition) && definition != null)
        {
            return definition.AccentColor;
        }

        switch (towerType)
        {
            case TowerType.Relay:
                return relayPresentation.AccentColor;
            case TowerType.SlowField:
                return slowFieldPresentation.AccentColor;
            case TowerType.Bombard:
                return bombardPresentation.AccentColor;
            case TowerType.SingleTarget:
            default:
                return singleTargetPresentation.AccentColor;
        }
    }
}
