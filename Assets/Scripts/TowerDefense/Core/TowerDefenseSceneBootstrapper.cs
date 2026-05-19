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
        TMP_Text structureStatusText,
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
        StructureStatusText = structureStatusText;
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

    public TMP_Text ScrapText { get; }
    public TMP_Text BaseHealthText { get; }
    public TMP_Text WaveText { get; }
    public TMP_Text SelectionText { get; }
    public TMP_Text StructureStatusText { get; }
    public Button RelayTowerButton { get; }
    public Button DefenseTowerButton { get; }
    public Button SlowFieldTowerButton { get; }
    public Button BombardTowerButton { get; }
    public Button ClearSelectionButton { get; }
    public GameObject GameOverPanel { get; }
    public TMP_Text GameOverTitle { get; }
    public TMP_Text GameOverHint { get; }
    public GameObject DragPreviewPanel { get; }
    public TMP_Text DragPreviewLabel { get; }
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

    public Camera MainCamera { get; }
    public GameObject RelayTowerPrototype { get; }
    public GameObject SingleTargetTowerPrototype { get; }
    public GameObject SlowFieldTowerPrototype { get; }
    public GameObject BombardTowerPrototype { get; }
    public BuildZone BuildZone { get; }
    public Transform PlacedTowerRoot { get; }
    public Transform PlacementPreviewRoot { get; }
}

/// <summary>
/// `TowerDefenseSceneBootstrapper` 负责把当前玩法场景所需的关键对象装配成可运行状态。
///
/// 这一层专门收口三类事情：
/// 1. 显式场景引用如何绑定到 HUD Presenter。
/// 2. BuildZone 缺失时如何创建运行时兜底对象。
/// 3. 运行时根节点缺失时如何补出稳定挂点。
///
/// 这样做以后，`TowerDefenseGame` 不需要再自己持有整段“开局装配流水线”代码，
/// 它只需要拿到一份已经解析好的结果，再把结果交给规则层、可视化层和别的子模块。
/// </summary>
public sealed class TowerDefenseSceneBootstrapper
{
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
        string placedTowerRootName,
        Transform placementPreviewRootReference,
        string placementPreviewRootName,
        BuildZone buildZoneReference,
        string buildZoneName,
        TowerDefenseHudSceneReferences hudSceneReferences,
        TowerDefenseHudPresenter hudPresenter)
    {
        hudPresenter?.BindSceneReferences(
            scrapText: hudSceneReferences.ScrapText,
            baseHealthText: hudSceneReferences.BaseHealthText,
            waveText: hudSceneReferences.WaveText,
            selectionText: hudSceneReferences.SelectionText,
            structureStatusText: hudSceneReferences.StructureStatusText,
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

        Camera resolvedMainCamera = mainCameraReference != null ? mainCameraReference : Camera.main;
        BuildZone resolvedBuildZone = EnsureBuildZoneExists(buildZoneReference, buildZoneName);
        Transform resolvedPlacedTowerRoot = EnsureRuntimeRoot(placedTowerRootReference, placedTowerRootName);
        Transform resolvedPlacementPreviewRoot = EnsureRuntimeRoot(placementPreviewRootReference, placementPreviewRootName);

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

    /// <summary>
    /// 确保某个运行时根节点一定存在。
    /// 如果场景里已经显式拖好了引用，就直接复用；否则按约定名称新建一个父节点。
    /// </summary>
    private static Transform EnsureRuntimeRoot(Transform existingReference, string objectName)
    {
        if (existingReference != null)
        {
            return existingReference;
        }

        GameObject runtimeRoot = new GameObject(objectName);
        return runtimeRoot.transform;
    }

    /// <summary>
    /// 确保当前关卡至少存在一个可用的 BuildZone。
    /// 如果场景作者忘了在 Inspector 里拖引用，这里会创建一个运行时兜底对象并给出明确告警。
    /// </summary>
    private static BuildZone EnsureBuildZoneExists(BuildZone buildZoneReference, string buildZoneName)
    {
        if (buildZoneReference != null)
        {
            return buildZoneReference;
        }

        Debug.LogWarning("TowerDefenseGame is missing BuildZone reference. Creating a temporary runtime BuildZone fallback.");

        GameObject buildZoneObject = new GameObject(buildZoneName);
        buildZoneObject.transform.position = new Vector3(0f, 0.25f, 0f);

        BoxCollider2D boxCollider = buildZoneObject.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector2(18f, 10.5f);

        return buildZoneObject.AddComponent<BuildZone>();
    }
}
