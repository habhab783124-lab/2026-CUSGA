using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// `TowerDefenseHudSceneReferences` 把当前关卡里与 HUD 装配有关的显式引用收成一组。
///
/// 之所以单独做这个小结构，而不是继续给方法传十几个分散参数，
/// 是因为“场景装配”本来就应该是一块边界清楚的职责：
/// 它关注的是“这一组场景对象怎样接线”，而不是某个单独文本框或按钮。
/// </summary>
public readonly struct TowerDefenseHudSceneReferences
{
    public TowerDefenseHudSceneReferences(
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
        ScrapText = scrapText;
        BaseHealthText = baseHealthText;
        WaveText = waveText;
        SelectionText = selectionText;
        OperationText = operationText;
        LiveStatusText = liveStatusText;
        PowerGridText = powerGridText;
        LatestEventText = latestEventText;
        RecentLogText = recentLogText;
        RelayTowerButton = relayTowerButton;
        DefenseTowerButton = defenseTowerButton;
        SlowFieldTowerButton = slowFieldTowerButton;
        BombardTowerButton = bombardTowerButton;
        ClearSelectionButton = clearSelectionButton;
        GameOverPanel = gameOverPanel;
        GameOverTitle = gameOverTitle;
        GameOverHint = gameOverHint;
        DragPreviewPanel = dragPreviewPanel;
        DragPreviewLabel = dragPreviewLabel;
    }

    public TMP_Text ScrapText { get; } // 中文：废料文本
    public TMP_Text BaseHealthText { get; } // 中文：基础生命文本
    public TMP_Text WaveText { get; } // 中文：波次文本
    public TMP_Text SelectionText { get; } // 中文：Selection文本
    public TMP_Text OperationText { get; } // 中文：操作文本
    public TMP_Text LiveStatusText { get; } // 中文：实时状态文本
    public TMP_Text PowerGridText { get; } // 中文：供电电网文本
    public TMP_Text LatestEventText { get; } // 中文：最新事件文本
    public TMP_Text RecentLogText { get; } // 中文：近期日志文本
    public Button RelayTowerButton { get; } // 中文：继电器塔按钮
    public Button DefenseTowerButton { get; } // 中文：防御塔按钮
    public Button SlowFieldTowerButton { get; } // 中文：减速区域塔按钮
    public Button BombardTowerButton { get; } // 中文：炸弹塔按钮
    public Button ClearSelectionButton { get; } // 中文：清除Selection按钮
    public GameObject GameOverPanel { get; } // 中文：游戏结束面板
    public TMP_Text GameOverTitle { get; } // 中文：游戏结束标题
    public TMP_Text GameOverHint { get; } // 中文：游戏结束提示
    public GameObject DragPreviewPanel { get; } // 中文：拖拽预览面板
    public TMP_Text DragPreviewLabel { get; } // 中文：拖拽预览标签
}

/// <summary>
/// `TowerDefenseSceneBootstrapResult` 表示当前关卡启动装配后真正可用的运行时引用集合。
///
/// 总控只需要消费这份结果，
/// 不需要再亲自知道 BuildZone 是不是临时创建出来的、运行时根节点是不是兜底补的。
/// </summary>
public readonly struct TowerDefenseSceneBootstrapResult
{
    public TowerDefenseSceneBootstrapResult(
        Camera mainCamera,
        GameObject relayTowerPrototype,
        GameObject singleTargetTowerPrototype,
        GameObject slowFieldTowerPrototype,
        GameObject bombardTowerPrototype,
        BuildZone buildZone,
        Transform placedTowerRoot,
        Transform placementPreviewRoot)
    {
        MainCamera = mainCamera;
        RelayTowerPrototype = relayTowerPrototype;
        SingleTargetTowerPrototype = singleTargetTowerPrototype;
        SlowFieldTowerPrototype = slowFieldTowerPrototype;
        BombardTowerPrototype = bombardTowerPrototype;
        BuildZone = buildZone;
        PlacedTowerRoot = placedTowerRoot;
        PlacementPreviewRoot = placementPreviewRoot;
    }

    public Camera MainCamera { get; } // 中文：主相机
    public GameObject RelayTowerPrototype { get; } // 中文：继电器塔原型
    public GameObject SingleTargetTowerPrototype { get; } // 中文：单体目标塔原型
    public GameObject SlowFieldTowerPrototype { get; } // 中文：减速区域塔原型
    public GameObject BombardTowerPrototype { get; } // 中文：炸弹塔原型
    public BuildZone BuildZone { get; } // 中文：建造区域
    public Transform PlacedTowerRoot { get; } // 中文：已放置塔根节点
    public Transform PlacementPreviewRoot { get; } // 中文：放置预览根节点
}

/// <summary>
/// `TowerDefenseSceneBootstrapper` 负责把当前玩法场景所需的关键对象装配成可运行状态。
///
/// 这一层专门收口三类事情：
/// 1. 显式场景引用如何绑定到 HUD Presenter。
/// 2. 缺失关键引用时如何明确报错。
/// 3. 把场景里已经显式配置好的对象收口成稳定结果。
///
/// 这样做以后，`TowerDefenseGame` 不需要再自己持有整段“开局装配流水线”代码，
/// 它只需要拿到一份已经解析好的结果，再把结果交给规则层、可视化层和别的子模块。
///
/// 需要特别强调的一点是：
/// 这一版已经不再负责“偷偷创建运行时兜底节点”。
/// 如果 `BuildZone`、运行时根节点或主相机没接好，就应该直接报错提醒作者补场景，
/// 而不是继续把问题藏到运行时临时对象里。
/// </summary>
public sealed class TowerDefenseSceneBootstrapper
{
    private const string PlacementPreviewRootName = "PlacementPreviewRoot"; // 中文：放置预览根节点名称

    /// <summary>
    /// 执行当前关卡启动所需的场景装配，并返回装配后的可用引用集合。
    /// </summary>
    public TowerDefenseSceneBootstrapResult BootstrapScene(
        Camera mainCameraReference,
        GameObject relayTowerPrototypeReference,
        GameObject singleTargetTowerPrototypeReference,
        GameObject slowFieldTowerPrototypeReference,
        GameObject bombardTowerPrototypeReference,
        Transform placedTowerRootReference,
        Transform placementPreviewRootReference,
        BuildZone buildZoneReference,
        TowerDefenseHudSceneReferences hudSceneReferences,
        TowerDefenseHudPresenter hudPresenter)
    {
        hudPresenter?.BindSceneReferences(
            scrapText: hudSceneReferences.ScrapText,
            baseHealthText: hudSceneReferences.BaseHealthText,
            waveText: hudSceneReferences.WaveText,
            selectionText: hudSceneReferences.SelectionText,
            operationText: hudSceneReferences.OperationText,
            liveStatusText: hudSceneReferences.LiveStatusText,
            powerGridText: hudSceneReferences.PowerGridText,
            latestEventText: hudSceneReferences.LatestEventText,
            recentLogText: hudSceneReferences.RecentLogText,
            relayTowerButton: hudSceneReferences.RelayTowerButton,
            defenseTowerButton: hudSceneReferences.DefenseTowerButton,
            slowFieldTowerButton: hudSceneReferences.SlowFieldTowerButton,
            bombardTowerButton: hudSceneReferences.BombardTowerButton,
            clearSelectionButton: hudSceneReferences.ClearSelectionButton,
            gameOverPanel: hudSceneReferences.GameOverPanel,
            gameOverTitle: hudSceneReferences.GameOverTitle,
            gameOverHint: hudSceneReferences.GameOverHint,
            dragPreviewPanel: hudSceneReferences.DragPreviewPanel,
            dragPreviewLabel: hudSceneReferences.DragPreviewLabel);
        hudPresenter?.FindSceneReferences();

        Camera resolvedMainCamera = mainCameraReference;
        BuildZone resolvedBuildZone = buildZoneReference;
        Transform resolvedPlacedTowerRoot = placedTowerRootReference;
        Transform resolvedPlacementPreviewRoot = placementPreviewRootReference;

        if (resolvedPlacementPreviewRoot == null)
        {
            resolvedPlacementPreviewRoot = ResolvePlacementPreviewRootFallback(resolvedPlacedTowerRoot);
        }

        LogIfMissing(resolvedMainCamera, "Main Camera");
        LogIfMissing(resolvedBuildZone, "BuildZone");
        LogIfMissing(resolvedPlacedTowerRoot, "PlacedTowers Root");
        LogIfMissing(resolvedPlacementPreviewRoot, "PlacementPreviewRoot");

        return new TowerDefenseSceneBootstrapResult(
            mainCamera: resolvedMainCamera,
            relayTowerPrototype: relayTowerPrototypeReference,
            singleTargetTowerPrototype: singleTargetTowerPrototypeReference,
            slowFieldTowerPrototype: slowFieldTowerPrototypeReference,
            bombardTowerPrototype: bombardTowerPrototypeReference,
            buildZone: resolvedBuildZone,
            placedTowerRoot: resolvedPlacedTowerRoot,
            placementPreviewRoot: resolvedPlacementPreviewRoot);
    }

    private static void LogIfMissing(Object reference, string expectedName)
    {
        if (reference != null)
        {
            return;
        }

        Debug.LogError($"TowerDefenseSceneBootstrapper 缺少关键场景引用：{expectedName}。请在场景 Inspector 中显式补齐。");
    }

    private static Transform ResolvePlacementPreviewRootFallback(Transform placedTowerRoot)
    {
        Transform siblingRoot = null;
        if (placedTowerRoot != null && placedTowerRoot.parent != null)
        {
            siblingRoot = placedTowerRoot.parent.Find(PlacementPreviewRootName);
            if (siblingRoot != null)
            {
                return siblingRoot;
            }
        }

        GameObject runtimeRoot = new GameObject(PlacementPreviewRootName);
        if (placedTowerRoot != null && placedTowerRoot.parent != null)
        {
            runtimeRoot.transform.SetParent(placedTowerRoot.parent, false);
        }

        return runtimeRoot.transform;
    }
}
