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
    [Header("Metric Labels")]
    [SerializeField] private string scrapMetricLabel = "SCRAP STOCK";
    [SerializeField] private string baseMetricLabel = "BASE CORE";
    [SerializeField] private string waveMetricLabel = "WAVE CLOCK";

    [Header("Operation Labels")]
    [SerializeField] private string deployTraceTitle = "DEPLOY TRACE";
    [SerializeField] private string tacticalReadyTitle = "TACTICAL READY";
    [SerializeField] private string structureLinkTitle = "STRUCTURE LINK";
    [SerializeField] private string operationLinkTitle = "OPERATION LINK";
    [SerializeField] [TextArea(2, 4)] private string idleOperationSummary = "Click or drag a tower card to project legal sectors";
    [SerializeField] private string idleOperationHotkeys = "1 Relay / 2 Single / 3 Slow / 4 Bomb / Esc Cancel";

    [Header("Drag Preview")]
    [SerializeField] private string dragGridLabel = "GRID";
    [SerializeField] private string dragLegalHint = "Cyan sectors show exact legal drop zones";
    [SerializeField] private string dragValidStateLabel = "DROP POINT CONFIRMED";
    [SerializeField] private string dragSelectedLegalHint = "Cyan sectors = exact legal zone";

    [Header("Event Sections")]
    [SerializeField] private string liveStatusTitle = "LIVE STATUS";
    [SerializeField] private string powerGridTitle = "POWER GRID";
    [SerializeField] private string latestEventTitle = "LATEST EVENT";
    [SerializeField] private string recentLogTitle = "RECENT LOG";

    [Header("Power Grid Copy")]
    [SerializeField] private string relayCountLabel = "Relays";
    [SerializeField] private string onlineTowerCountLabel = "Towers";
    [SerializeField] private string onlineTowerSuffix = "online";
    [SerializeField] private string loadLabel = "Load";

    [Header("Selection Copy")]
    [SerializeField] private string freeDeployLine = "FREE deploy. Scrap remains unchanged.";
    [SerializeField] private string scrapLeftSuffix = "SCRAP left after deploy.";
    [SerializeField] private string needMoreScrapPrefix = "Need";
    [SerializeField] private string needMoreScrapSuffix = "more SCRAP to deploy.";

    [Header("Buttons")]
    [SerializeField] private string cancelDeployPrimary = "CANCEL DEPLOY";
    [SerializeField] private string cancelDeploySecondary = "Esc / RMB";

    public string ScrapMetricLabel => scrapMetricLabel;
    public string BaseMetricLabel => baseMetricLabel;
    public string WaveMetricLabel => waveMetricLabel;
    public string DeployTraceTitle => deployTraceTitle;
    public string TacticalReadyTitle => tacticalReadyTitle;
    public string StructureLinkTitle => structureLinkTitle;
    public string OperationLinkTitle => operationLinkTitle;
    public string IdleOperationSummary => idleOperationSummary;
    public string IdleOperationHotkeys => idleOperationHotkeys;
    public string DragGridLabel => dragGridLabel;
    public string DragLegalHint => dragLegalHint;
    public string DragValidStateLabel => dragValidStateLabel;
    public string DragSelectedLegalHint => dragSelectedLegalHint;
    public string LiveStatusTitle => liveStatusTitle;
    public string PowerGridTitle => powerGridTitle;
    public string LatestEventTitle => latestEventTitle;
    public string RecentLogTitle => recentLogTitle;
    public string RelayCountLabel => relayCountLabel;
    public string OnlineTowerCountLabel => onlineTowerCountLabel;
    public string OnlineTowerSuffix => onlineTowerSuffix;
    public string LoadLabel => loadLabel;
    public string FreeDeployLine => freeDeployLine;
    public string ScrapLeftSuffix => scrapLeftSuffix;
    public string NeedMoreScrapPrefix => needMoreScrapPrefix;
    public string NeedMoreScrapSuffix => needMoreScrapSuffix;
    public string CancelDeployPrimary => cancelDeployPrimary;
    public string CancelDeploySecondary => cancelDeploySecondary;
}
