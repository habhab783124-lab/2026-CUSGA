using UnityEngine;

/// <summary>
/// `TowerDefenseHudCopyAsset` 把 HUD 里剩余的静态文案从 Presenter 代码里抽成共享资产。
///
/// 这样后续如果你想继续调整：
/// - 顶部指标标题
/// - 右侧操作区分区标题
/// - 拖拽提示里的固定说明
/// - 取消部署按钮文案
///
/// 就不需要再去改 `TowerDefenseHudPresenter.cs`。
/// </summary>
[CreateAssetMenu(
    fileName = "TowerDefenseHudCopy",
    menuName = "Tower Defense/UI/HUD Copy")]
public sealed class TowerDefenseHudCopyAsset : ScriptableObject
{
    [Header("指标标题")]
    [SerializeField, InspectorName("废料标题")] private string scrapMetricLabel = "废料储备"; // 中文：废料指标标签
    [SerializeField, InspectorName("基地标题")] private string baseMetricLabel = "基地核心"; // 中文：基础指标标签
    [SerializeField, InspectorName("波次标题")] private string waveMetricLabel = "波次进度"; // 中文：波次指标标签

    [Header("操作区标题")]
    [SerializeField, InspectorName("部署追踪标题")] private string deployTraceTitle = "部署追踪"; // 中文：部署轨迹标题
    [SerializeField, InspectorName("战术就绪标题")] private string tacticalReadyTitle = "战术待命"; // 中文：tacticalReady标题
    [SerializeField, InspectorName("建筑链接标题")] private string structureLinkTitle = "建筑链接"; // 中文：structureLink标题
    [SerializeField, InspectorName("操作链接标题")] private string operationLinkTitle = "战局记录"; // 中文：操作Link标题
    [SerializeField, TextArea(2, 4), InspectorName("空闲操作摘要")] private string idleOperationSummary = "点击或拖拽塔卡，预览当前合法建造区域。"; // 中文：idle操作Summary
    [SerializeField, InspectorName("空闲操作热键")] private string idleOperationHotkeys = "1 继电器 / 2 单体塔 / 3 减速塔 / 4 炸弹塔 / Esc 取消"; // 中文：idle操作Hotkeys

    [Header("拖拽预览")]
    [SerializeField, InspectorName("网格标签")] private string dragGridLabel = "网络"; // 中文：拖拽电网标签
    [SerializeField, InspectorName("合法提示")] private string dragLegalHint = "青色区域代表当前准确的合法落点"; // 中文：拖拽合法提示
    [SerializeField, InspectorName("合法状态标签")] private string dragValidStateLabel = "落点确认"; // 中文：拖拽有效状态标签
    [SerializeField, InspectorName("选中合法提示")] private string dragSelectedLegalHint = "青色区域 = 当前合法范围"; // 中文：拖拽选中合法提示

    [Header("事件分区")]
    [SerializeField, InspectorName("实时状态标题")] private string liveStatusTitle = "实时状态"; // 中文：实时状态标题
    [SerializeField, InspectorName("供电网络标题")] private string powerGridTitle = "供电网络"; // 中文：供电电网标题
    [SerializeField, InspectorName("最新事件标题")] private string latestEventTitle = "最新事件"; // 中文：最新事件标题
    [SerializeField, InspectorName("近期日志标题")] private string recentLogTitle = "近期记录"; // 中文：近期日志标题

    [Header("供电文案")]
    [SerializeField, InspectorName("继电器计数标签")] private string relayCountLabel = "继电器"; // 中文：继电器数量标签
    [SerializeField, InspectorName("塔计数标签")] private string onlineTowerCountLabel = "战斗塔"; // 中文：在线塔数量标签
    [SerializeField, InspectorName("在线后缀")] private string onlineTowerSuffix = "在线"; // 中文：在线塔Suffix
    [SerializeField, InspectorName("负载标签")] private string loadLabel = "负载"; // 中文：加载标签

    [Header("选中文案")]
    [SerializeField, InspectorName("免费部署提示")] private string freeDeployLine = "免费部署，不消耗废料。"; // 中文：免费部署线
    [SerializeField, InspectorName("部署后剩余后缀")] private string scrapLeftSuffix = "部署后剩余废料。"; // 中文：废料剩余Suffix
    [SerializeField, InspectorName("废料不足前缀")] private string needMoreScrapPrefix = "还需要"; // 中文：需要More废料Prefix
    [SerializeField, InspectorName("废料不足后缀")] private string needMoreScrapSuffix = "点废料才能部署。"; // 中文：需要More废料Suffix

    [Header("按钮")]
    [SerializeField, InspectorName("取消部署主标题")] private string cancelDeployPrimary = "取消部署"; // 中文：取消部署主
    [SerializeField, InspectorName("取消部署副标题")] private string cancelDeploySecondary = "Esc / 右键"; // 中文：取消部署副

    public string ScrapMetricLabel => scrapMetricLabel; // 中文：废料指标标签
    public string BaseMetricLabel => baseMetricLabel; // 中文：基础指标标签
    public string WaveMetricLabel => waveMetricLabel; // 中文：波次指标标签
    public string DeployTraceTitle => deployTraceTitle; // 中文：部署轨迹标题
    public string TacticalReadyTitle => tacticalReadyTitle; // 中文：TacticalReady标题
    public string StructureLinkTitle => structureLinkTitle; // 中文：StructureLink标题
    public string OperationLinkTitle => operationLinkTitle; // 中文：操作Link标题
    public string IdleOperationSummary => idleOperationSummary; // 中文：Idle操作Summary
    public string IdleOperationHotkeys => idleOperationHotkeys; // 中文：Idle操作Hotkeys
    public string DragGridLabel => dragGridLabel; // 中文：拖拽电网标签
    public string DragLegalHint => dragLegalHint; // 中文：拖拽合法提示
    public string DragValidStateLabel => dragValidStateLabel; // 中文：拖拽有效状态标签
    public string DragSelectedLegalHint => dragSelectedLegalHint; // 中文：拖拽选中合法提示
    public string LiveStatusTitle => liveStatusTitle; // 中文：实时状态标题
    public string PowerGridTitle => powerGridTitle; // 中文：供电电网标题
    public string LatestEventTitle => latestEventTitle; // 中文：最新事件标题
    public string RecentLogTitle => recentLogTitle; // 中文：近期日志标题
    public string RelayCountLabel => relayCountLabel; // 中文：继电器数量标签
    public string OnlineTowerCountLabel => onlineTowerCountLabel; // 中文：在线塔数量标签
    public string OnlineTowerSuffix => onlineTowerSuffix; // 中文：在线塔Suffix
    public string LoadLabel => loadLabel; // 中文：加载标签
    public string FreeDeployLine => freeDeployLine; // 中文：免费部署线
    public string ScrapLeftSuffix => scrapLeftSuffix; // 中文：废料剩余Suffix
    public string NeedMoreScrapPrefix => needMoreScrapPrefix; // 中文：需要More废料Prefix
    public string NeedMoreScrapSuffix => needMoreScrapSuffix; // 中文：需要More废料Suffix
    public string CancelDeployPrimary => cancelDeployPrimary; // 中文：取消部署主
    public string CancelDeploySecondary => cancelDeploySecondary; // 中文：取消部署副
}
