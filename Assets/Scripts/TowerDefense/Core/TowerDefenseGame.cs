using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// `TowerType` 描述当前原型里玩家可部署的建筑类型。
/// `None` 表示未选择，`Relay` 表示发电机，`Defense` 表示防御塔。
/// 这个枚举会贯穿部署卡、拖拽预览、放置校验、扣费和真正建塔等整条链路。
/// </summary>
public enum TowerType
{
    [InspectorName("未选择")]
    None,
    [InspectorName("继电器")]
    Relay,
    [InspectorName("单体塔")]
    SingleTarget,
    [InspectorName("减速塔")]
    SlowField,
    [InspectorName("炸弹塔")]
    Bombard
}

/// <summary>
/// `TowerDefenseGame` 是当前塔防原型的整局总协调器。
/// 它负责把“运行状态、放置规则、放置交互、建塔执行、HUD 表现”这些子模块装配成一条完整主链。
/// 需要注意的是：资源/基地/波次状态、放置交互和真正建塔执行都已经继续下沉到独立组件里，
/// 所以这个类越来越像一个编排层，而不是继续把所有细节都塞进一个上帝脚本。
/// </summary>
public class TowerDefenseGame : MonoBehaviour
{
#if UNITY_EDITOR
    private const string DefaultTowerPresentationCatalogAssetPath = "Assets/Resources/TowerDefense/Configs/TowerPresentationCatalog.asset"; // 中文：默认塔展示目录资产路径
    private const string DefaultHudThemeAssetPath = "Assets/Resources/TowerDefense/Configs/TowerDefenseHudTheme.asset"; // 中文：默认HUD主题资产路径
    private const string DefaultHudCopyAssetPath = "Assets/Resources/TowerDefense/Configs/TowerDefenseHudCopy.asset"; // 中文：默认HUD文案资产路径
    private const string DefaultPlacementVisualThemeAssetPath = "Assets/Resources/TowerDefense/Configs/TowerPlacementVisualTheme.asset"; // 中文：默认放置视觉主题资产路径
#endif

    /// <summary>
    /// `TowerPresentationAuthoring` 把“某种塔在 UI 和文案层该怎样被表现”收口成一组 Inspector 配置。
    ///
    /// 这样做以后，商店卡、HUD 操作区和后续更多界面都可以从同一份配置读样式，
    /// 而不是继续把名字、摘要、强调色和图标散落在不同脚本里。
    /// </summary>
    [Serializable]
    private sealed class TowerPresentationAuthoring
    {
        [InspectorName("显示名称")]
        public string displayName = "建筑"; // 中文：显示名称
        [InspectorName("卡片职责摘要")]
        public string cardRoleSummary = "职责摘要"; // 中文：卡片RoleSummary
        [InspectorName("选中提示")]
        public string selectionHint = "选择提示。"; // 中文：selection提示
        [InspectorName("升级方向摘要")]
        public string upgradeFocusSummary = "升级方向摘要。"; // 中文：升级FocusSummary
        [InspectorName("强调色")]
        public Color accentColor = Color.white; // 中文：accent颜色
        [InspectorName("卡片图标")]
        public Sprite cardIconSprite = null; // 中文：卡片图标精灵
        [InspectorName("图标着色")]
        public Color cardIconTint = Color.white; // 中文：卡片图标Tint
        [InspectorName("卡片底色")]
        public Color cardBackgroundTint = new Color(0.08f, 0.11f, 0.16f, 0.96f); // 中文：卡片背景Tint
        [InspectorName("卡片强调色")]
        public Color cardAccentTint = Color.white; // 中文：卡片AccentTint

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "建筑" : displayName; // 中文：显示名称
        public string CardRoleSummary => string.IsNullOrWhiteSpace(cardRoleSummary) ? DisplayName : cardRoleSummary; // 中文：卡片RoleSummary
        public string SelectionHint => string.IsNullOrWhiteSpace(selectionHint) ? CardRoleSummary : selectionHint; // 中文：Selection提示
        public string UpgradeFocusSummary => string.IsNullOrWhiteSpace(upgradeFocusSummary) ? "升级会强化这座建筑。" : upgradeFocusSummary; // 中文：升级FocusSummary
        public Color AccentColor => accentColor; // 中文：Accent颜色
        public Sprite CardIconSprite => cardIconSprite; // 中文：卡片图标精灵
        public Color CardIconTint => cardIconTint; // 中文：卡片图标Tint
        public Color CardBackgroundTint => cardBackgroundTint; // 中文：卡片背景Tint
        public Color CardAccentTint => cardAccentTint; // 中文：卡片AccentTint
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
        [SerializeField, InspectorName("指标标题颜色")] private Color metricLabelColor = new Color(0.56f, 0.66f, 0.75f, 1f); // 中文：指标标签颜色
        [SerializeField, InspectorName("废料数值颜色")] private Color scrapValueColor = new Color(1f, 0.71f, 0.4f, 1f); // 中文：废料Value颜色
        [SerializeField, InspectorName("基地数值颜色")] private Color baseValueColor = new Color(0.45f, 0.91f, 1f, 1f); // 中文：基础Value颜色
        [SerializeField, InspectorName("波次数值颜色")] private Color waveValueColor = new Color(1f, 0.85f, 0.47f, 1f); // 中文：波次Value颜色
        [SerializeField, InspectorName("卡片文本颜色")] private Color cardTextColor = new Color(0.96f, 0.98f, 1f, 1f); // 中文：卡片文本颜色
        [SerializeField, InspectorName("次级信息颜色")] private Color secondaryInfoColor = new Color(0.54f, 0.65f, 0.75f, 1f); // 中文：副Info颜色
        [SerializeField, InspectorName("状态文本颜色")] private Color statusTextColor = new Color(0.84f, 0.9f, 0.94f, 1f); // 中文：状态文本颜色
        [SerializeField, InspectorName("中性提示颜色")] private Color neutralNoticeColor = new Color(0.81f, 0.88f, 0.92f, 1f); // 中文：中性提示颜色
        [SerializeField, InspectorName("正向提示颜色")] private Color positiveNoticeColor = new Color(0.49f, 0.95f, 0.69f, 1f); // 中文：正向提示颜色
        [SerializeField, InspectorName("消耗提示颜色")] private Color spendingNoticeColor = new Color(1f, 0.85f, 0.47f, 1f); // 中文：消耗提示颜色
        [SerializeField, InspectorName("警告提示颜色")] private Color warningNoticeColor = new Color(1f, 0.72f, 0.44f, 1f); // 中文：警告提示颜色
        [SerializeField, InspectorName("危险提示颜色")] private Color dangerNoticeColor = new Color(1f, 0.55f, 0.5f, 1f); // 中文：危险提示颜色
        [SerializeField, InspectorName("拖拽信息颜色")] private Color dragPreviewInfoColor = new Color(0.53f, 0.65f, 0.74f, 1f); // 中文：拖拽预览Info颜色
        [SerializeField, InspectorName("拖拽合法颜色")] private Color dragPreviewValidColor = new Color(0.47f, 0.95f, 0.85f, 1f); // 中文：拖拽预览有效颜色
        [SerializeField, InspectorName("拖拽非法颜色")] private Color dragPreviewInvalidColor = new Color(1f, 0.45f, 0.51f, 1f); // 中文：拖拽预览无效颜色
        [SerializeField, InspectorName("卡片标签边距")] private Vector4 cardLabelMargin = new Vector4(108f, 18f, 24f, 18f); // 中文：卡片标签Margin
        [SerializeField, InspectorName("卡片字距")] private float cardLabelCharacterSpacing = 1.2f; // 中文：卡片标签Character间距
        [SerializeField, InspectorName("卡片行距")] private float cardLabelLineSpacing = -10f; // 中文：卡片标签线间距
        [SerializeField, InspectorName("拖拽面板偏移")] private Vector2 dragPreviewPanelOffset = new Vector2(142f, -92f); // 中文：拖拽预览面板偏移

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
    public static TowerDefenseGame Instance { get; private set; } // 中文：实例

    [Header("核心规则")]
    [FormerlySerializedAs("startingEnergy")]
    [SerializeField, InspectorName("初始废料")] private int startingScrap = 80; // 中文：starting废料
    [SerializeField, InspectorName("初始基地生命")] private int startingBaseHealth = 10; // 中文：starting基础生命
    [SerializeField, InspectorName("继电器造价")] private int relayTowerCost = 0; // 中文：继电器塔费用
    [SerializeField, InspectorName("单体塔造价")] private int singleTargetTowerCost = 38; // 中文：单体目标塔费用
    [SerializeField, InspectorName("减速塔造价")] private int slowFieldTowerCost = 50; // 中文：减速区域塔费用
    [SerializeField, InspectorName("炸弹塔造价")] private int bombardTowerCost = 62; // 中文：炸弹塔费用

    [Header("放置规则")]
    [SerializeField, InspectorName("继电器放置半径")] private float relayPlacementRadius = 0.52f; // 中文：继电器放置半径
    [SerializeField, InspectorName("战斗塔放置半径")] private float defensePlacementRadius = 0.58f; // 中文：防御放置半径

    [Header("放置扩张")]
    [SerializeField, InspectorName("继电器扩张方格边长")] private float relayExpansionSquareSize = 4.5f; // 中文：继电器Expansion方格大小
    [SerializeField, InspectorName("战斗塔扩张方格边长")] private float defenseExpansionSquareSize = 4.5f; // 中文：防御Expansion方格大小
    [SerializeField, InspectorName("首塔起始区中心")] private Vector2 initialPlacementSquareCenter = new Vector2(-6.5f, -2.25f); // 中文：initial放置方格中心
    [SerializeField, InspectorName("首塔起始区边长")] private float initialPlacementSquareSize = 3f; // 中文：initial放置方格大小

    [Header("放置预览")]
    [SerializeField, InspectorName("合法预览颜色")] private Color validPreviewColor = new Color(0.26f, 0.95f, 0.78f, 0.72f); // 中文：有效预览颜色
    [SerializeField, InspectorName("非法预览颜色")] private Color invalidPreviewColor = new Color(1f, 0.32f, 0.38f, 0.72f); // 中文：无效预览颜色
    [SerializeField, InspectorName("放置圆环精灵")] private Sprite placementRingSpriteReference; // 中文：放置圆环精灵引用
    [SerializeField, InspectorName("放置圆环资源路径")] private string placementRingResourcePath = "UI/placement-ring"; // 中文：放置圆环Resource路径

    [Header("放置覆盖层")]
    [SerializeField, InspectorName("覆盖层像素密度")] private float placementAreaOverlayPixelsPerUnit = 20f; // 中文：放置Area覆盖层PixelsPerUnit
    [SerializeField, InspectorName("覆盖层填充色")] private Color placementAreaOverlayFillColor = new Color(0.18f, 0.82f, 0.86f, 0.16f); // 中文：放置Area覆盖层Fill颜色
    [SerializeField, InspectorName("覆盖层描边色")] private Color placementAreaOverlayEdgeColor = new Color(0.72f, 1f, 0.97f, 0.52f); // 中文：放置Area覆盖层Edge颜色
    [SerializeField, InspectorName("覆盖层排序值")] private int placementAreaOverlaySortingOrder = 12; // 中文：放置Area覆盖层Sorting顺序

    [Header("首塔起始区标记")]
    [SerializeField, InspectorName("起始区填充色")] private Color starterZoneMarkerFillColor = new Color(0.22f, 0.82f, 0.88f, 0.22f); // 中文：起始区域标记Fill颜色
    [SerializeField, InspectorName("起始区描边色")] private Color starterZoneMarkerEdgeColor = new Color(0.9f, 1f, 0.98f, 1f); // 中文：起始区域标记Edge颜色
    [SerializeField, InspectorName("起始区排序值")] private int starterZoneMarkerSortingOrder = 10; // 中文：起始区域标记Sorting顺序

    [Header("共享表现资产")]
    [Tooltip("推荐使用的共用塔展示配置资产。多个关卡可以共用这一份，而不是在每个场景里重复维护塔卡文案和配色。")]
    [SerializeField, InspectorName("塔展示目录资产")] private TowerPresentationCatalogAsset towerPresentationCatalogAsset; // 中文：塔展示目录资产
    [Tooltip("推荐使用的共用 HUD 主题资产。多个关卡如果想保持统一 HUD 风格，应优先共用这一份资产。")]
    [SerializeField, InspectorName("HUD 主题资产")] private TowerDefenseHudThemeAsset hudThemeAsset; // 中文：HUD主题资产
    [Tooltip("推荐使用的共用 HUD 文案资产。操作区标题、拖拽提示固定文案等应优先统一收在这里。")]
    [SerializeField, InspectorName("HUD 文案资产")] private TowerDefenseHudCopyAsset hudCopyAsset; // 中文：HUD文案资产
    [Tooltip("推荐使用的共用放置可视化主题资产。预览颜色、覆盖层和首塔标记风格应优先统一维护在这里。")]
    [SerializeField, InspectorName("放置可视化主题资产")] private TowerPlacementVisualThemeAsset placementVisualThemeAsset; // 中文：放置视觉主题资产

    [Header("塔展示回退配置")]
    [SerializeField, InspectorName("继电器展示配置")] private TowerPresentationAuthoring relayPresentation = new TowerPresentationAuthoring // 中文：继电器展示
    {
        displayName = "继电器",
        cardRoleSummary = "供电节点 / 网络扩张",
        selectionHint = "先铺设继电器，再把战斗塔接入供电范围。",
        upgradeFocusSummary = "升级会提升供电容量，但不会扩大覆盖范围。",
        accentColor = new Color(1f, 0.55f, 0.22f, 1f),
        cardIconTint = new Color(1f, 0.66f, 0.3f, 1f),
        cardBackgroundTint = new Color(0.14f, 0.1f, 0.08f, 0.96f),
        cardAccentTint = new Color(1f, 0.55f, 0.22f, 1f)
    };
    [SerializeField, InspectorName("单体塔展示配置")] private TowerPresentationAuthoring singleTargetPresentation = new TowerPresentationAuthoring // 中文：单体目标展示
    {
        displayName = "单体塔",
        cardRoleSummary = "点杀 / 前线",
        selectionHint = "稳定的单体直伤，适合补掉关键目标。",
        upgradeFocusSummary = "升级会强化射速、射程和单体输出。",
        accentColor = new Color(0.28f, 0.78f, 1f, 1f),
        cardIconTint = new Color(0.55f, 0.88f, 1f, 1f),
        cardBackgroundTint = new Color(0.07f, 0.11f, 0.16f, 0.96f),
        cardAccentTint = new Color(0.28f, 0.78f, 1f, 1f)
    };
    [SerializeField, InspectorName("减速塔展示配置")] private TowerPresentationAuthoring slowFieldPresentation = new TowerPresentationAuthoring // 中文：减速区域展示
    {
        displayName = "减速塔",
        cardRoleSummary = "范围控制 / 减速",
        selectionHint = "减速范围内所有敌人，适合控线。",
        upgradeFocusSummary = "升级会强化减速、延长控制时间并扩大压制力。",
        accentColor = new Color(0.36f, 0.95f, 0.84f, 1f),
        cardIconTint = new Color(0.66f, 1f, 0.91f, 1f),
        cardBackgroundTint = new Color(0.07f, 0.14f, 0.14f, 0.96f),
        cardAccentTint = new Color(0.36f, 0.95f, 0.84f, 1f)
    };
    [SerializeField, InspectorName("炸弹塔展示配置")] private TowerPresentationAuthoring bombardPresentation = new TowerPresentationAuthoring // 中文：炸弹展示
    {
        displayName = "炸弹塔",
        cardRoleSummary = "爆发溅射 / 延迟",
        selectionHint = "延迟爆炸，适合打击密集敌群。",
        upgradeFocusSummary = "升级会扩大爆炸范围、缩短飞行时间并提高爆发伤害。",
        accentColor = new Color(1f, 0.62f, 0.26f, 1f),
        cardIconTint = new Color(1f, 0.78f, 0.46f, 1f),
        cardBackgroundTint = new Color(0.16f, 0.1f, 0.08f, 0.96f),
        cardAccentTint = new Color(1f, 0.62f, 0.26f, 1f)
    };

    [Header("HUD 主题回退配置")]
    [SerializeField, InspectorName("HUD 主题配置")] private HudThemeAuthoring hudTheme = new HudThemeAuthoring(); // 中文：HUD主题

    [Header("场景引用（推荐）")]

    /// <summary>
    /// 这一组是玩法主链路优先使用的显式场景引用。
    /// 包括主相机、塔原型、运行时根节点和 `BuildZone`。如果这些引用已经在 Inspector 里配好，
    /// 运行时就不应该再依赖对象名查找；名字字段只保留给过渡期兜底或运行时容器命名。
    /// </summary>
    [SerializeField, InspectorName("主相机")] private Camera mainCameraReference; // 中文：主相机引用
    [SerializeField, InspectorName("继电器原型 Prefab")] private GameObject relayTowerPrototypeReference; // 中文：继电器塔原型引用
    [FormerlySerializedAs("defenseTowerPrototypeReference")]
    [SerializeField, InspectorName("单体塔原型 Prefab")] private GameObject singleTargetTowerPrototypeReference; // 中文：单体目标塔原型引用
    [SerializeField, InspectorName("减速塔原型 Prefab")] private GameObject slowFieldTowerPrototypeReference; // 中文：减速区域塔原型引用
    [SerializeField, InspectorName("炸弹塔原型 Prefab")] private GameObject bombardTowerPrototypeReference; // 中文：炸弹塔原型引用
    [SerializeField, InspectorName("已放置塔根节点")] private Transform placedTowerRootReference; // 中文：已放置塔根节点引用
    [SerializeField, InspectorName("放置预览根节点")] private Transform placementPreviewRootReference; // 中文：放置预览根节点引用
    [SerializeField, InspectorName("建造区")] private BuildZone buildZoneReference; // 中文：建造区域引用
    [SerializeField, InspectorName("战场地图定义")] private BattlefieldMapDefinition battlefieldMapReference; // 中文：战场地图引用

    [Header("HUD 引用（推荐）")]

    /// <summary>
    /// 这一组是玩法 HUD 的显式场景引用。
    /// 当前策略是优先直接拖 Inspector，引导项目逐步摆脱按名字查找 UI 对象的旧做法。
    /// </summary>
    [FormerlySerializedAs("energyTextReference")]
    [SerializeField, InspectorName("废料文本")] private TMP_Text scrapTextReference; // 中文：废料文本引用
    [SerializeField, InspectorName("基地生命文本")] private TMP_Text baseHealthTextReference; // 中文：基础生命文本引用
    [SerializeField, InspectorName("波次文本")] private TMP_Text waveTextReference; // 中文：波次文本引用
    [SerializeField, InspectorName("选中文本")] private TMP_Text selectionTextReference; // 中文：selection文本引用
    [SerializeField, InspectorName("操作文本")] private TMP_Text operationTextReference; // 中文：操作文本引用
    [SerializeField, InspectorName("实时状态文本")] private TMP_Text liveStatusTextReference; // 中文：实时状态文本引用
    [SerializeField, InspectorName("供电文本")] private TMP_Text powerGridTextReference; // 中文：供电电网文本引用
    [SerializeField, InspectorName("最新事件文本")] private TMP_Text latestEventTextReference; // 中文：最新事件文本引用
    [SerializeField, InspectorName("近期日志文本")] private TMP_Text recentLogTextReference; // 中文：近期日志文本引用

    [SerializeField, InspectorName("继电器按钮")] private Button relayTowerButtonReference; // 中文：继电器塔按钮引用
    [SerializeField, InspectorName("单体塔按钮")] private Button defenseTowerButtonReference; // 中文：防御塔按钮引用
    [SerializeField, InspectorName("减速塔按钮")] private Button slowFieldTowerButtonReference; // 中文：减速区域塔按钮引用
    [SerializeField, InspectorName("炸弹塔按钮")] private Button bombardTowerButtonReference; // 中文：炸弹塔按钮引用
    [SerializeField, InspectorName("清除选择按钮")] private Button clearSelectionButtonReference; // 中文：清除Selection按钮引用
    [SerializeField, InspectorName("结算面板")] private GameObject gameOverPanelReference; // 中文：游戏结束面板引用
    [SerializeField, InspectorName("结算标题文本")] private TMP_Text gameOverTitleReference; // 中文：游戏结束标题引用
    [SerializeField, InspectorName("结算提示文本")] private TMP_Text gameOverHintReference; // 中文：游戏结束提示引用
    [SerializeField, InspectorName("拖拽预览面板")] private GameObject dragPreviewPanelReference; // 中文：拖拽预览面板引用
    [SerializeField, InspectorName("拖拽预览文本")] private TMP_Text dragPreviewLabelReference; // 中文：拖拽预览标签引用

    /// <summary>
    /// `_sessionState` 负责保存这一局的资源、基地、波次和结算状态。
    /// 它是当前总控最核心的一份“局内运行状态源”。
    /// </summary>
    private TowerDefenseSessionState _sessionState; // 中文：会话状态

    private GameObject _relayTowerPrototype; // 中文：继电器塔原型
    private GameObject _singleTargetTowerPrototype; // 中文：单体目标塔原型
    private GameObject _slowFieldTowerPrototype; // 中文：减速区域塔原型
    private GameObject _bombardTowerPrototype; // 中文：炸弹塔原型
    private Camera _mainCamera; // 中文：主相机
    private BuildZone _buildZone; // 中文：建造区域
    private BattlefieldMapDefinition _battlefieldMapDefinition; // 中文：战场地图定义
    private Transform _placedTowerRoot; // 中文：已放置塔根节点
    private Transform _placementPreviewRoot; // 中文：放置预览根节点
    private TowerPlacementRules _placementRules; // 中文：放置Rules
    private TowerPowerGridCoordinator _powerGridCoordinator; // 中文：供电电网协调器

    /// <summary>
    /// `_placementVisualController` 负责放置阶段的可视化反馈。
    /// 它会统一管理预览塔、合法区域覆盖层和首塔起手区标记，让 `TowerDefenseGame` 只保留调度职责。
    /// </summary>
    private TowerPlacementVisualController _placementVisualController; // 中文：放置视觉控制器

    /// <summary>
    /// `_placementInteractionController` 负责“玩家怎样进入放置流程、怎样更新流程、怎样结束流程”。
    /// 这一轮把交互状态从总控里迁出去后，
    /// `TowerDefenseGame` 更明确地退回到“整局编排 + 真正建塔 + HUD 刷新入口”的职责边界。
    /// </summary>
    private TowerPlacementInteractionController _placementInteractionController; // 中文：放置交互控制器

    /// <summary>
    /// `_placementBuildExecutor` 负责真正建塔这一段执行链。
    /// 也就是：最终校验、实例化塔、兼容旧 BuildPad、补碰撞体、扣费和放置成功后的收尾刷新。
    /// 这样总控就不用再同时承担“整局状态管理”和“建塔流水线细节”两种职责。
    /// </summary>
    private TowerPlacementBuildExecutor _placementBuildExecutor; // 中文：放置建造执行器

    /// <summary>
    /// `_presentationCoordinator` 负责 HUD 广播与结算表现收尾。
    /// 它把 HUD 快照组装、状态消息转发、Game Over 面板显示和敌人血条隐藏这些表现层协调逻辑
    /// 从总控中继续收口出去。
    /// </summary>
    private TowerDefensePresentationCoordinator _presentationCoordinator; // 中文：展示协调器

    /// <summary>
    /// `_sceneBootstrapper` 负责把当前关卡里的显式引用、运行时根节点和兜底对象装配成可用状态。
    /// 这样总控就不必继续内联整段“场景怎么接线、根节点怎么补、BuildZone 怎么兜底”的启动代码。
    /// </summary>
    private TowerDefenseSceneBootstrapper _sceneBootstrapper; // 中文：场景引导器

    /// <summary>
    /// `_inputCoordinator` 负责输入轮询、快速点击放置、屏幕坐标换算和 UI 阻挡判断。
    /// 这样总控就不再自己持有这一组输入工具层细节。
    /// </summary>
    private TowerDefenseInputCoordinator _inputCoordinator; // 中文：输入协调器

    /// <summary>
    /// `_placementSupportCoordinator` 负责放置链里剩下的支持型能力，
    /// 例如：起手区标记、合法区预热、塔静态定义查询、规则桥接与起手区自检。
    /// 这是让总控在最后一轮尽量收敛成“装配层”的关键一步。
    /// </summary>
    private TowerPlacementSupportCoordinator _placementSupportCoordinator; // 中文：放置支持协调器
    private RelayTower _selectedRelayTower; // 中文：选中继电器塔
    private DefenseTower _selectedDefenseTower; // 中文：选中防御塔

    /// <summary>
    /// `_towerCatalog` 提供塔的静态定义，例如显示名、造价、占地半径和扩张方格边长。
    /// 总控通过它读配置，而不是把这些常量散落在很多 `switch` 里。
    /// </summary>
    private TowerCatalog _towerCatalog; // 中文：塔目录

    /// <summary>
    /// `_hudPresenter` 是 HUD 表现层适配器。
    /// 它只负责把当前状态刷到界面上，并同步拖拽提示、按钮可用性与结算面板。
    /// 这样做的目的，是把“状态计算”和“界面呈现”分开，减少总控脚本继续膨胀。
    /// </summary>
    private TowerDefenseHudPresenter _hudPresenter; // 中文：HUDPresenter

    /// <summary>
    /// 对外暴露只读的结算状态，方便 HUD、敌人和其他运行时对象判断当前是否已经 Game Over。
    /// </summary>
    public bool IsGameOver => _sessionState != null && _sessionState.IsGameOver; // 中文：是否游戏结束

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

        EnsureSharedPresentationAssetsAssigned();
        _sessionState = new TowerDefenseSessionState(startingScrap, startingBaseHealth);
        InitializeArchitectureModules();
    }

    /// <summary>
    /// 释放放置可视化控制器，并在对象销毁时安全清理单例引用。
    /// 这样可以避免场景重载或脚本重编译后残留旧实例状态。
    /// </summary>
    private void OnDestroy()
    {
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
        _placementSupportCoordinator?.RunStarterPlacementSanityCheck();
        _powerGridCoordinator?.RecalculatePowerDistribution();
    }

    private void OnValidate()
    {
        EnsureSharedPresentationAssetsAssigned();
    }

    /// <summary>
    /// `Update()` 现在只负责驱动输入协调器。
    /// 这样总控不再自己轮询热键、快速点击放置和 UI 阻挡判断，
    /// 而是把这些输入层细节统一交给 `_inputCoordinator`。
    /// </summary>
    private void Update()
    {
        _inputCoordinator?.Tick();
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

        ShowTransientHudNotice($"+{amount} 废料回收。", tone: HudNoticeTone.Positive);
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
        SetStatusMessage($"有敌人突破了防线，基地损失 {actualDamage} 点生命。");
        ShowTransientHudNotice($"基地完整度 -{actualDamage}", duration: 3f, tone: HudNoticeTone.Danger);

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

    public void ShowTransientHudNotice(string message, float duration = 2.5f, HudNoticeTone tone = HudNoticeTone.Auto)
    {
        _presentationCoordinator?.ShowTransientHudNotice(message, duration, tone);
    }

    /// <summary>
    /// 选中发电机，供按钮事件或快捷键直接调用。
    /// </summary>
    public void SelectRelayTower()
    {
        ClearPlacedStructureSelection();
        _placementInteractionController?.SelectRelayTower();
    }

    /// <summary>
    /// 选中防御塔，供按钮事件或快捷键直接调用。
    /// </summary>
    public void SelectDefenseTower()
    {
        ClearPlacedStructureSelection();
        _placementInteractionController?.SelectSingleTargetTower();
    }

    public void SelectSlowFieldTower()
    {
        ClearPlacedStructureSelection();
        _placementInteractionController?.SelectSlowFieldTower();
    }

    public void SelectBombardTower()
    {
        ClearPlacedStructureSelection();
        _placementInteractionController?.SelectBombardTower();
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
                $"继电器 #{_selectedRelayTower.RelayNumber} 已升级到 LV {_selectedRelayTower.CurrentLevel}。当前容量 {_selectedRelayTower.SupplyCapacity}。");
            ShowTransientHudNotice($"-{upgradeCost} 废料用于继电器升级。", 2.2f, HudNoticeTone.Spending);
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
                $"{GetTowerDisplayName(_selectedDefenseTower.BuildType)} #{_selectedDefenseTower.TowerNumber} 已升级到 LV {_selectedDefenseTower.CurrentLevel}。当前耗电 {_selectedDefenseTower.PowerRequired}。");
            ShowTransientHudNotice($"-{upgradeCost} 废料用于塔升级。", 2.2f, HudNoticeTone.Spending);
            RefreshHud();
            return true;
        }

        SetStatusMessage("请先选中一个已放置的继电器或战斗塔。");
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
            ClearPlacedStructureSelection();
            Destroy(relayTower.gameObject);
            InvalidatePlacementAreaOverlayCache();
            SetStatusMessage($"继电器 #{relayTower.RelayNumber} 已拆除。");
            RefreshHud();
            return true;
        }

        if (_selectedDefenseTower != null)
        {
            DefenseTower defenseTower = _selectedDefenseTower;
            ClearPlacedStructureSelection();
            Destroy(defenseTower.gameObject);
            InvalidatePlacementAreaOverlayCache();
            SetStatusMessage($"{GetTowerDisplayName(defenseTower.BuildType)} #{defenseTower.TowerNumber} 已拆除。");
            RefreshHud();
            return true;
        }

        SetStatusMessage("请先选中一个已放置的继电器或战斗塔。");
        return false;
    }


    /// <summary>
    /// 统一输出放置诊断日志。
    /// 这里单独收口，是为了以后可以集中控制开关、格式和节流策略，而不是把 `Debug.Log` 散落在整条放置链路里。
    /// </summary>
    private void LogPlacementDiagnostic(string message)
    {
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
        TowerPresentationCatalogAsset resolvedPresentationCatalogAsset = ResolveTowerPresentationCatalogAsset();
        TowerDefenseHudThemeAsset resolvedHudThemeAsset = ResolveHudThemeAsset();
        TowerDefenseHudCopyAsset resolvedHudCopyAsset = ResolveHudCopyAsset();

        _towerCatalog = new TowerCatalog(
            relayDefinition: BuildTowerDefinition(TowerType.Relay, relayTowerCost, relayPlacementRadius, relayExpansionSquareSize, relayPresentation, resolvedPresentationCatalogAsset),
            singleTargetDefinition: BuildTowerDefinition(TowerType.SingleTarget, singleTargetTowerCost, defensePlacementRadius, defenseExpansionSquareSize, singleTargetPresentation, resolvedPresentationCatalogAsset),
            slowFieldDefinition: BuildTowerDefinition(TowerType.SlowField, slowFieldTowerCost, defensePlacementRadius, defenseExpansionSquareSize, slowFieldPresentation, resolvedPresentationCatalogAsset),
            bombardDefinition: BuildTowerDefinition(TowerType.Bombard, bombardTowerCost, defensePlacementRadius, defenseExpansionSquareSize, bombardPresentation, resolvedPresentationCatalogAsset));

        _placementRules = new TowerPlacementRules(
            towerType => _placementSupportCoordinator != null ? _placementSupportCoordinator.GetPlacementRadius(towerType) : 0.5f,
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
                ? _inputCoordinator.ScreenToWorldPosition(screenPosition)
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
            refreshStarterZoneMarker: () => _placementSupportCoordinator?.RefreshStarterZoneMarker());
        _hudPresenter.SetTheme(resolvedHudThemeAsset != null ? resolvedHudThemeAsset.ToRuntimeTheme() : hudTheme.ToRuntimeTheme());
        _hudPresenter.SetCopy(resolvedHudCopyAsset);
        _presentationCoordinator.BindPresentation(_hudPresenter, _towerCatalog);
        _sceneBootstrapper = new TowerDefenseSceneBootstrapper();
    }

    private TowerDefinition BuildTowerDefinition(
        TowerType towerType,
        int buildCost,
        float placementRadius,
        float expansionSquareSize,
        TowerPresentationAuthoring fallbackPresentation,
        TowerPresentationCatalogAsset resolvedCatalogAsset)
    {
        if (resolvedCatalogAsset != null &&
            resolvedCatalogAsset.TryGetEntry(towerType, out TowerPresentationCatalogAsset.TowerPresentationEntry entry) &&
            entry != null)
        {
            return new TowerDefinition(
                towerType,
                entry.DisplayName,
                buildCost,
                placementRadius,
                expansionSquareSize,
                entry.CardRoleSummary,
                entry.SelectionHint,
                entry.UpgradeFocusSummary,
                entry.AccentColor,
                entry.CardIconSprite,
                entry.CardIconTint,
                entry.CardBackgroundTint,
                entry.CardAccentTint);
        }

        return new TowerDefinition(
            towerType,
            fallbackPresentation.DisplayName,
            buildCost,
            placementRadius,
            expansionSquareSize,
            fallbackPresentation.CardRoleSummary,
            fallbackPresentation.SelectionHint,
            fallbackPresentation.UpgradeFocusSummary,
            fallbackPresentation.AccentColor,
            fallbackPresentation.CardIconSprite,
            fallbackPresentation.CardIconTint,
            fallbackPresentation.CardBackgroundTint,
            fallbackPresentation.CardAccentTint);
    }

    private void EnsureSharedPresentationAssetsAssigned()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (towerPresentationCatalogAsset == null)
            {
                towerPresentationCatalogAsset = AssetDatabase.LoadAssetAtPath<TowerPresentationCatalogAsset>(DefaultTowerPresentationCatalogAssetPath);
            }

            if (hudThemeAsset == null)
            {
                hudThemeAsset = AssetDatabase.LoadAssetAtPath<TowerDefenseHudThemeAsset>(DefaultHudThemeAssetPath);
            }

            if (hudCopyAsset == null)
            {
                hudCopyAsset = AssetDatabase.LoadAssetAtPath<TowerDefenseHudCopyAsset>(DefaultHudCopyAssetPath);
            }

            if (placementVisualThemeAsset == null)
            {
                placementVisualThemeAsset = AssetDatabase.LoadAssetAtPath<TowerPlacementVisualThemeAsset>(DefaultPlacementVisualThemeAssetPath);
            }
        }
#endif
    }

    private TowerPresentationCatalogAsset ResolveTowerPresentationCatalogAsset()
    {
        EnsureSharedPresentationAssetsAssigned();
        return towerPresentationCatalogAsset;
    }

    private TowerDefenseHudThemeAsset ResolveHudThemeAsset()
    {
        EnsureSharedPresentationAssetsAssigned();
        return hudThemeAsset;
    }

    private TowerDefenseHudCopyAsset ResolveHudCopyAsset()
    {
        EnsureSharedPresentationAssetsAssigned();
        return hudCopyAsset;
    }

    private TowerPlacementVisualThemeAsset ResolvePlacementVisualThemeAsset()
    {
        EnsureSharedPresentationAssetsAssigned();
        return placementVisualThemeAsset;
    }

    /// <summary>
    /// 初始化放置可视化控制器。
    /// 这里会把颜色、排序、资源入口以及规则查询函数一次性注入，
    /// 让可视化层只专注于“怎么显示”，而不是反向知道整局状态或自己去找场景对象。
    /// </summary>
    private void InitializePlacementVisuals()
    {
        _placementVisualController?.Dispose();

        TowerPlacementVisualThemeAsset placementVisualTheme = ResolvePlacementVisualThemeAsset();
        Sprite resolvedPlacementRingSprite = placementVisualTheme != null ? placementVisualTheme.PlacementRingSprite : placementRingSpriteReference;
        Color resolvedValidPreviewColor = placementVisualTheme != null ? placementVisualTheme.ValidPreviewColor : validPreviewColor;
        Color resolvedInvalidPreviewColor = placementVisualTheme != null ? placementVisualTheme.InvalidPreviewColor : invalidPreviewColor;
        float resolvedOverlayPixelsPerUnit = placementVisualTheme != null ? placementVisualTheme.PlacementAreaOverlayPixelsPerUnit : placementAreaOverlayPixelsPerUnit;
        Color resolvedOverlayFillColor = placementVisualTheme != null ? placementVisualTheme.PlacementAreaOverlayFillColor : placementAreaOverlayFillColor;
        Color resolvedOverlayEdgeColor = placementVisualTheme != null ? placementVisualTheme.PlacementAreaOverlayEdgeColor : placementAreaOverlayEdgeColor;
        int resolvedOverlaySortingOrder = placementVisualTheme != null ? placementVisualTheme.PlacementAreaOverlaySortingOrder : placementAreaOverlaySortingOrder;
        Color resolvedStarterMarkerFillColor = placementVisualTheme != null ? placementVisualTheme.StarterZoneMarkerFillColor : starterZoneMarkerFillColor;
        Color resolvedStarterMarkerEdgeColor = placementVisualTheme != null ? placementVisualTheme.StarterZoneMarkerEdgeColor : starterZoneMarkerEdgeColor;
        int resolvedStarterMarkerSortingOrder = placementVisualTheme != null ? placementVisualTheme.StarterZoneMarkerSortingOrder : starterZoneMarkerSortingOrder;

        _placementVisualController = new TowerPlacementVisualController(
            resolvedPlacementRingSprite,
            placementRingResourcePath,
            resolvedValidPreviewColor,
            resolvedInvalidPreviewColor,
            resolvedOverlayPixelsPerUnit,
            resolvedOverlayFillColor,
            resolvedOverlayEdgeColor,
            resolvedOverlaySortingOrder,
            resolvedStarterMarkerFillColor,
            resolvedStarterMarkerEdgeColor,
            resolvedStarterMarkerSortingOrder,
            GetPrototype,
            GetTowerDisplayName,
            GetPlacementRadius);

        _placementVisualController.BindPlacementPreviewRoot(_placementPreviewRoot);
        _placementInteractionController?.BindPresentation(_placementVisualController, _hudPresenter, _towerCatalog);
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
    }

    private PlacedStructureHudState BuildPlacedStructureHudState()
    {
        if (_selectedRelayTower != null)
        {
            int upgradeCost = 0;
            string invalidReason = string.Empty;
            bool canUpgrade = _powerGridCoordinator != null &&
                              _sessionState != null &&
                              _powerGridCoordinator.CanUpgradeRelay(_selectedRelayTower, _sessionState.CurrentScrap, out upgradeCost, out invalidReason);
            string detail = $"继电器 #{_selectedRelayTower.RelayNumber} / LV {_selectedRelayTower.CurrentLevel} / 负载 {_selectedRelayTower.CurrentAssignedLoad}/{_selectedRelayTower.SupplyCapacity}";
            detail += $"\n范围 {_selectedRelayTower.SupplyRange:0.0} / 升级后容量 {_selectedRelayTower.PreviewUpgradedSupplyCapacity()}";
            detail += canUpgrade
                ? $"\n升级后剩余：{_sessionState.CurrentScrap - upgradeCost} 废料。"
                  + $"\nU 升级（{upgradeCost} 废料） / Delete 拆除"
                : $"\n{invalidReason}";
            return new PlacedStructureHudState(true, "继电器节点", detail);
        }

        if (_selectedDefenseTower != null)
        {
            int upgradeCost = 0;
            string invalidReason = string.Empty;
            string powerState = _selectedDefenseTower.IsPowered
                ? $"在线 / 继电器 #{(_selectedDefenseTower.AssignedRelay != null ? _selectedDefenseTower.AssignedRelay.RelayNumber : 0)}"
                : _selectedDefenseTower.PowerStatusMessage;
            bool canUpgrade = _powerGridCoordinator != null &&
                              _sessionState != null &&
                              _powerGridCoordinator.CanUpgradeDefenseTower(_selectedDefenseTower, _sessionState.CurrentScrap, out upgradeCost, out invalidReason);
            string detail = $"塔 #{_selectedDefenseTower.TowerNumber} / LV {_selectedDefenseTower.CurrentLevel} / {powerState}";
            detail += $"\n{_selectedDefenseTower.BuildCurrentCombatSummary()}";
            detail += $"\n{_selectedDefenseTower.BuildUpgradePreviewSummary()}";
            detail += canUpgrade
                ? $"\n升级后剩余：{_sessionState.CurrentScrap - upgradeCost} 废料。"
                  + $"\nU 升级（{upgradeCost} 废料） / Delete 拆除"
                : $"\n{invalidReason}";
            return new PlacedStructureHudState(true, GetTowerDisplayName(_selectedDefenseTower.BuildType), detail);
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
        SetStatusMessage($"已选中继电器 #{relayTower.RelayNumber}。按 U 升级，按 Delete 拆除。");
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
        SetStatusMessage($"已选中 {GetTowerDisplayName(defenseTower.BuildType)} #{defenseTower.TowerNumber}。按 U 升级，按 Delete 拆除。");
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
        Time.timeScale = 0f;
        _presentationCoordinator?.ShowGameOver();
    }


    /// <summary>
    /// 对总控内部与外部兼容层保留一个统一的“真正建塔”入口。
    /// 现在具体执行细节已经下沉到 `_placementBuildExecutor`，
    /// 所以这个方法更像一个稳定门面，避免别的脚本将来直接耦合到执行器实现。
    /// </summary>
    private bool TryPlaceTowerAt(Vector3 worldPosition, TowerType towerType, BuildPad ownerPad = null)
    {
        return _placementBuildExecutor != null &&
               _placementBuildExecutor.TryPlaceTowerAt(worldPosition, towerType, ownerPad);
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

            invalidReason = "放置支持系统尚未初始化。";
        return false;
#if false
        invalidReason = string.Empty;

        if (_buildZone == null)
        {
            invalidReason = "当前关卡没有配置 BuildZone。";
            return false;
        }

        if (!_buildZone.ContainsPoint(worldPosition))
        {
            invalidReason = "超出当前关卡的可建造区域。";
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
                invalidReason = "离其他建筑太近了，请稍微挪开一点。";
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

    /// <summary>
    /// 读取塔型对应的扩张方格边长。
    /// </summary>
    private float GetExpansionSquareSize(TowerType towerType)
    {
        return _placementSupportCoordinator != null ? _placementSupportCoordinator.GetExpansionSquareSize(towerType) : 4.5f;
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
            placementPreviewRootReference,
            buildZoneReference,
            new TowerDefenseHudSceneReferences(
                scrapTextReference,
                baseHealthTextReference,
                waveTextReference,
                selectionTextReference,
                operationTextReference,
                liveStatusTextReference,
                powerGridTextReference,
                latestEventTextReference,
                recentLogTextReference,
                relayTowerButtonReference,
                defenseTowerButtonReference,
                slowFieldTowerButtonReference,
                bombardTowerButtonReference,
                clearSelectionButtonReference,
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
        _placedTowerRoot = bootstrapResult.PlacedTowerRoot;
        _placementPreviewRoot = bootstrapResult.PlacementPreviewRoot;
        _battlefieldMapDefinition = battlefieldMapReference != null
            ? battlefieldMapReference
            : ResolveBattlefieldMapFallback();

        mainCameraReference = _mainCamera;
        buildZoneReference = _buildZone;
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
            Debug.LogError("TowerDefenseGame 缺少 Main Camera 显式引用。当前玩法场景不再依赖 Camera.main 兜底。", this);
        }

        if (_relayTowerPrototype == null || _singleTargetTowerPrototype == null || _slowFieldTowerPrototype == null || _bombardTowerPrototype == null)
        {
            Debug.LogError("TowerDefenseGame 缺少一个或多个塔 Prefab 显式引用。请检查场景 Inspector 接线。", this);
        }

        if (_buildZone == null || _placedTowerRoot == null || _placementPreviewRoot == null || _battlefieldMapDefinition == null)
        {
            Debug.LogError("TowerDefenseGame 缺少 BuildZone / Runtime Root / BattlefieldMapDefinition 等关键场景引用。当前版本不再自动创建这些兜底对象。", this);
        }
    }

    private BattlefieldMapDefinition ResolveBattlefieldMapFallback()
    {
        BattlefieldMapDefinition[] discoveredMaps = FindObjectsByType<BattlefieldMapDefinition>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (discoveredMaps == null || discoveredMaps.Length == 0)
        {
            return null;
        }

        return discoveredMaps[0];
    }

    /// <summary>
    /// 让规则层和可视化层继续拿到当前场景里已经显式配置好的运行时根节点。
    /// 这一版不再在这里补创建缺失节点；
    /// 如果引用为空，应由场景接线和验证工具去修正，而不是继续在运行时兜底。
    /// </summary>
    private void EnsureRuntimeRoots()
    {
        placedTowerRootReference = _placedTowerRoot;
        placementPreviewRootReference = _placementPreviewRoot;
        RefreshPlacementRuleContext();
        _placementVisualController?.BindPlacementPreviewRoot(_placementPreviewRoot);
    }
}
