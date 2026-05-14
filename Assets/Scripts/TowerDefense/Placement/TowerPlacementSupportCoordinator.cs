using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `TowerPlacementSupportCoordinator` 负责把“放置链里剩下的那些支持型能力”收成一层。
///
/// 这里刻意不去抢 `TowerPlacementRules / TowerPlacementVisualController / TowerPlacementInteractionController`
/// 的职责，而是只负责连接它们之间缺少的那部分胶水：
/// - 放置规则入口转发
/// - 塔静态定义查询
/// - 合法区覆盖层的预热 / 失效 / 隐藏
/// - 首塔起手区运行时标记与编辑器 Gizmo
/// - 起手区轻量自检
///
/// 这样总控最后留下来的更多就是：
/// - Unity 生命周期入口
/// - 模块装配
/// - 对外兼容门面
/// 而不会继续夹着一大团“既不是规则本身，也不是表现本身”的支持逻辑。
/// </summary>
public sealed class TowerPlacementSupportCoordinator
{
    private readonly struct PlacedStructureFootprint
    {
        public PlacedStructureFootprint(Vector2 position, float radius)
        {
            Position = position;
            Radius = radius;
        }

        public Vector2 Position { get; }
        public float Radius { get; }
    }

    public delegate bool PlacementValidator(Vector3 worldPosition, TowerType towerType, out string invalidReason);

    private readonly Vector2 _initialPlacementSquareCenter;
    private readonly float _initialPlacementSquareSize;
    private readonly Color _starterZoneMarkerFillColor;
    private readonly Color _starterZoneMarkerEdgeColor;
    private readonly Func<TowerCatalog> _towerCatalogQuery;
    private readonly Func<TowerPlacementRules> _placementRulesQuery;
    private readonly Func<TowerPlacementVisualController> _placementVisualControllerQuery;
    private readonly Func<Transform> _placedTowerRootQuery;
    private readonly Func<BuildZone> _buildZoneQuery;
    private readonly Func<GameObject> _relayTowerPrototypeQuery;
    private readonly Func<GameObject> _singleTargetTowerPrototypeQuery;
    private readonly Func<GameObject> _slowFieldTowerPrototypeQuery;
    private readonly Func<GameObject> _bombardTowerPrototypeQuery;
    private readonly Func<TowerPowerGridCoordinator> _powerGridCoordinatorQuery;
    private readonly Func<bool> _isGameOverQuery;
    private readonly Action<string> _logPlacementDiagnostic;

    public TowerPlacementSupportCoordinator(
        Vector2 initialPlacementSquareCenter,
        float initialPlacementSquareSize,
        Color starterZoneMarkerFillColor,
        Color starterZoneMarkerEdgeColor,
        Func<TowerCatalog> towerCatalogQuery,
        Func<TowerPlacementRules> placementRulesQuery,
        Func<TowerPlacementVisualController> placementVisualControllerQuery,
        Func<Transform> placedTowerRootQuery,
        Func<BuildZone> buildZoneQuery,
        Func<GameObject> relayTowerPrototypeQuery,
        Func<GameObject> singleTargetTowerPrototypeQuery,
        Func<GameObject> slowFieldTowerPrototypeQuery,
        Func<GameObject> bombardTowerPrototypeQuery,
        Func<TowerPowerGridCoordinator> powerGridCoordinatorQuery,
        Func<bool> isGameOverQuery,
        Action<string> logPlacementDiagnostic)
    {
        _initialPlacementSquareCenter = initialPlacementSquareCenter;
        _initialPlacementSquareSize = initialPlacementSquareSize;
        _starterZoneMarkerFillColor = starterZoneMarkerFillColor;
        _starterZoneMarkerEdgeColor = starterZoneMarkerEdgeColor;
        _towerCatalogQuery = towerCatalogQuery;
        _placementRulesQuery = placementRulesQuery;
        _placementVisualControllerQuery = placementVisualControllerQuery;
        _placedTowerRootQuery = placedTowerRootQuery;
        _buildZoneQuery = buildZoneQuery;
        _relayTowerPrototypeQuery = relayTowerPrototypeQuery;
        _singleTargetTowerPrototypeQuery = singleTargetTowerPrototypeQuery;
        _slowFieldTowerPrototypeQuery = slowFieldTowerPrototypeQuery;
        _bombardTowerPrototypeQuery = bombardTowerPrototypeQuery;
        _powerGridCoordinatorQuery = powerGridCoordinatorQuery;
        _isGameOverQuery = isGameOverQuery;
        _logPlacementDiagnostic = logPlacementDiagnostic;
    }

    /// <summary>
    /// 把当前场景里的 BuildZone 与运行时根节点同步给规则层。
    /// 同时把起手区配置也一起灌进去，保证规则层永远看到的是最新上下文。
    /// </summary>
    public void RefreshPlacementRuleContext()
    {
        TowerPlacementRules placementRules = _placementRulesQuery != null ? _placementRulesQuery() : null;
        if (placementRules == null)
        {
            return;
        }

        placementRules.BindSceneReferences(_buildZoneQuery != null ? _buildZoneQuery() : null, _placedTowerRootQuery != null ? _placedTowerRootQuery() : null);
    }

    /// <summary>
    /// 总控侧的放置校验入口。
    /// 当前真正的规则判断已经在 `TowerPlacementRules` 里，
    /// 这里主要负责统一转发，并在需要时忽略预览对象本身。
    /// </summary>
    public bool ValidatePlacementPosition(Vector3 worldPosition, TowerType towerType, out string invalidReason)
    {
        TowerPowerGridCoordinator powerGridCoordinator = _powerGridCoordinatorQuery != null ? _powerGridCoordinatorQuery() : null;
        if (TowerTypeUtility.IsRelay(towerType) && powerGridCoordinator != null && !powerGridCoordinator.CanPlaceRelay(out invalidReason))
        {
            return false;
        }

        if (TowerTypeUtility.IsCombatTower(towerType))
        {
            if (powerGridCoordinator == null)
            {
                invalidReason = "Power grid is not initialized.";
                return false;
            }

            if (!powerGridCoordinator.IsWithinAnyRelayCoverage(worldPosition))
            {
                invalidReason = "Defense towers must be placed inside relay coverage.";
                return false;
            }
        }

        TowerPlacementRules placementRules = _placementRulesQuery != null ? _placementRulesQuery() : null;
        if (placementRules == null)
        {
            invalidReason = "Placement rules are not initialized.";
            return false;
        }

        return placementRules.ValidatePlacementPosition(worldPosition, towerType, ShouldIgnorePlacementTransform, out invalidReason);
    }

    /// <summary>
    /// 放置规则层本身不应该知道“预览对象”是谁。
    /// 这里由支持层统一提供一个非常窄的忽略入口，避免规则层反向耦合到可视化实现。
    /// </summary>
    public bool ShouldIgnorePlacementTransform(Transform candidate)
    {
        TowerPlacementVisualController placementVisualController = _placementVisualControllerQuery != null ? _placementVisualControllerQuery() : null;
        return placementVisualController != null && placementVisualController.ContainsPreviewTransform(candidate);
    }

    /// <summary>
    /// 读取塔型对应的占地半径。
    /// 这类查询本质上属于塔静态定义，不应该继续散落在总控里。
    /// </summary>
    public float GetPlacementRadius(TowerType towerType)
    {
        TowerDefinition definition = _towerCatalogQuery != null ? _towerCatalogQuery()?.GetDefinition(towerType) : null;
        return definition != null ? definition.PlacementRadius : 0.5f;
    }

    /// <summary>
    /// 读取塔型对应的扩张方格边长。
    /// </summary>
    public float GetExpansionSquareSize(TowerType towerType)
    {
        TowerDefinition definition = _towerCatalogQuery != null ? _towerCatalogQuery()?.GetDefinition(towerType) : null;
        return definition != null ? definition.ExpansionSquareSize : 4.5f;
    }

    /// <summary>
    /// 读取塔型对应的建造成本。
    /// </summary>
    public int GetTowerCost(TowerType towerType)
    {
        TowerDefinition definition = _towerCatalogQuery != null ? _towerCatalogQuery()?.GetDefinition(towerType) : null;
        return definition != null ? definition.BuildCost : 0;
    }

    /// <summary>
    /// 读取塔型对应的显示名称。
    /// </summary>
    public string GetTowerDisplayName(TowerType towerType)
    {
        TowerDefinition definition = _towerCatalogQuery != null ? _towerCatalogQuery()?.GetDefinition(towerType) : null;
        return definition != null ? definition.DisplayName : "None";
    }

    /// <summary>
    /// 根据塔型拿到当前关卡装配好的原型体。
    /// </summary>
    public GameObject GetPrototype(TowerType towerType)
    {
        switch (towerType)
        {
            case TowerType.Relay:
                return _relayTowerPrototypeQuery != null ? _relayTowerPrototypeQuery() : null;
            case TowerType.SingleTarget:
                return _singleTargetTowerPrototypeQuery != null ? _singleTargetTowerPrototypeQuery() : null;
            case TowerType.SlowField:
                return _slowFieldTowerPrototypeQuery != null ? _slowFieldTowerPrototypeQuery() : null;
            case TowerType.Bombard:
                return _bombardTowerPrototypeQuery != null ? _bombardTowerPrototypeQuery() : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// 计算合法区域覆盖层需要扫描的世界边界。
    /// 如果规则层还没就绪，就给一个安全的空边界，避免上层空引用。
    /// </summary>
    public Bounds GetPlacementOverlayWorldBounds(TowerType towerType)
    {
        BuildZone buildZone = _buildZoneQuery != null ? _buildZoneQuery() : null;
        if (buildZone == null)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        if (TowerTypeUtility.IsRelay(towerType))
        {
            return buildZone.WorldBounds;
        }

        TowerPowerGridCoordinator powerGridCoordinator = _powerGridCoordinatorQuery != null ? _powerGridCoordinatorQuery() : null;
        if (powerGridCoordinator == null)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds relayCoverageBounds = powerGridCoordinator.GetRelayCoverageBounds();
        if (relayCoverageBounds.size == Vector3.zero)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        return TowerPlacementRules.IntersectBounds(buildZone.WorldBounds, relayCoverageBounds);
    }

    /// <summary>
    /// 预热指定塔型的合法区域覆盖层。
    /// 常见调用时机是悬停部署卡或刚切换选中塔型时，目的是减少真正开始拖拽时的卡顿感。
    /// </summary>
    public void PrewarmPlacementAreaOverlay(TowerType towerType)
    {
        TowerPlacementVisualController placementVisualController = _placementVisualControllerQuery != null ? _placementVisualControllerQuery() : null;
        if (placementVisualController == null)
        {
            return;
        }

        placementVisualController.PrewarmPlacementAreaOverlay(
            towerType,
            _isGameOverQuery != null && _isGameOverQuery(),
            GetPlacementOverlayWorldBounds(towerType),
            BuildPlacementOverlayValidator(towerType));
    }

    /// <summary>
    /// 标记合法区域覆盖层缓存失效。
    /// 当场上的塔布局变化后，旧缓存就不再可信，下一次需要重新生成。
    /// </summary>
    public void InvalidatePlacementAreaOverlayCache()
    {
        TowerPlacementVisualController placementVisualController = _placementVisualControllerQuery != null ? _placementVisualControllerQuery() : null;
        placementVisualController?.InvalidatePlacementAreaOverlayCache();
    }

    /// <summary>
    /// 隐藏合法区域覆盖层。
    /// </summary>
    public void HidePlacementAreaOverlay()
    {
        TowerPlacementVisualController placementVisualController = _placementVisualControllerQuery != null ? _placementVisualControllerQuery() : null;
        placementVisualController?.HidePlacementAreaOverlay();
    }

    /// <summary>
    /// 同步首塔起手区标记的显隐。
    /// 每次 HUD 刷新或放置状态变化时都可以通过这里统一刷新。
    /// </summary>
    public void RefreshStarterZoneMarker()
    {
        TowerPlacementVisualController placementVisualController = _placementVisualControllerQuery != null ? _placementVisualControllerQuery() : null;
        if (placementVisualController == null)
        {
            return;
        }

        // 最新规则已经取消了“首塔起手区”概念。
        // 因此这里统一强制隐藏这类历史标记，避免作者和玩家继续被旧视觉提示误导。
        placementVisualController.RefreshStarterZoneMarker(false, new Bounds(Vector3.zero, Vector3.zero));
    }

    /// <summary>
    /// 判断当前是否应该显示首塔起手区标记。
    /// 只有在还没放下任何塔、并且没有进入结算时，这块提示区域才应该出现。
    /// </summary>
    public bool ShouldShowStarterZoneMarker()
    {
        return false;
    }

    /// <summary>
    /// 读取当前起手区边界。
    /// 如果规则层已经存在，就优先使用规则层边界，保证可视化和规则判定始终一致。
    /// </summary>
    public Bounds GetStarterZoneBounds()
    {
        return new Bounds(Vector3.zero, Vector3.zero);
    }

    /// <summary>
    /// 在 Scene 视图里画出首塔起手区的方形 Gizmo。
    /// 这一层只负责画，不负责决定要不要画。
    /// </summary>
    public void DrawStarterZoneGizmo()
    {
        // 历史起手区 Gizmo 已经废弃，不再绘制。
    }

    /// <summary>
    /// 进入 Play 后，立刻用“起手区中心点”做一次非常轻量的放置自检。
    /// 它不参与真正建塔，只负责把最关键的排查信息尽快打到日志里。
    /// </summary>
    public void RunStarterPlacementSanityCheck()
    {
        // 历史的“起手区自检”已失去意义。
        // 新规则下，初始继电器允许在任意可放置区域落点，
        // 真正的最小可玩性检查统一交给编辑器侧的运行态烟测工具处理。
    }

    /// <summary>
    /// 为合法区覆盖层构造一份更轻量的几何判定器。
    ///
    /// 这里的目标不是替代真正放置校验，
    /// 而是避免覆盖层在整张区域采样时，对每个像素都去跑一次完整物理查询。
    ///
    /// 最终落塔时仍然会走正式的 `ValidatePlacementPosition()`，
    /// 所以这一层只需要做到：
    /// - 与当前规则尽量一致
    /// - 但在大范围采样时足够轻
    /// </summary>
    public Func<Vector3, bool> BuildPlacementOverlayValidator(TowerType towerType)
    {
        BuildZone buildZone = _buildZoneQuery != null ? _buildZoneQuery() : null;
        Bounds buildBounds = buildZone != null ? buildZone.WorldBounds : new Bounds(Vector3.zero, Vector3.zero);

        List<Bounds> blockerBounds = CollectPlacementBlockerBounds();
        List<PlacedStructureFootprint> placedStructureFootprints = CollectPlacedStructureFootprints();
        List<RelayTower> relays = CollectPlacedRelays();
        float placementRadius = GetPlacementRadius(towerType);

        return worldPosition =>
        {
            if (!IsInsideBounds2D(buildBounds, worldPosition))
            {
                return false;
            }

            if (TowerTypeUtility.IsCombatTower(towerType) && !IsInsideAnyRelayCoverageSquare(worldPosition, relays))
            {
                return false;
            }

            for (int blockerIndex = 0; blockerIndex < blockerBounds.Count; blockerIndex++)
            {
                if (IsInsideBounds2D(blockerBounds[blockerIndex], worldPosition))
                {
                    return false;
                }
            }

            float requiredSeparation = placementRadius;
            for (int structureIndex = 0; structureIndex < placedStructureFootprints.Count; structureIndex++)
            {
                PlacedStructureFootprint footprint = placedStructureFootprints[structureIndex];
                float combinedRadius = requiredSeparation + footprint.Radius;
                if (((Vector2)worldPosition - footprint.Position).sqrMagnitude <= combinedRadius * combinedRadius)
                {
                    return false;
                }
            }

            return true;
        };
    }

    private List<Bounds> CollectPlacementBlockerBounds()
    {
        List<Bounds> bounds = new List<Bounds>(16);
        PlacementBlocker[] blockers = UnityEngine.Object.FindObjectsByType<PlacementBlocker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < blockers.Length; i++)
        {
            PlacementBlocker blocker = blockers[i];
            if (blocker == null || !blocker.isActiveAndEnabled)
            {
                continue;
            }

            Collider2D blockerCollider = blocker.GetComponent<Collider2D>();
            if (blockerCollider == null || !blockerCollider.enabled)
            {
                continue;
            }

            bounds.Add(blockerCollider.bounds);
        }

        return bounds;
    }

    private List<PlacedStructureFootprint> CollectPlacedStructureFootprints()
    {
        List<PlacedStructureFootprint> footprints = new List<PlacedStructureFootprint>(16);
        Transform placedTowerRoot = _placedTowerRootQuery != null ? _placedTowerRootQuery() : null;
        if (placedTowerRoot == null)
        {
            return footprints;
        }

        for (int childIndex = 0; childIndex < placedTowerRoot.childCount; childIndex++)
        {
            Transform child = placedTowerRoot.GetChild(childIndex);
            if (child == null || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            TowerType towerType = TowerType.None;
            if (child.GetComponent<RelayTower>() != null)
            {
                towerType = TowerType.Relay;
            }
            else
            {
                DefenseTower defenseTower = child.GetComponent<DefenseTower>();
                if (defenseTower != null)
                {
                    towerType = defenseTower.BuildType;
                }
            }

            if (towerType == TowerType.None)
            {
                continue;
            }

            footprints.Add(new PlacedStructureFootprint(child.position, GetPlacementRadius(towerType)));
        }

        return footprints;
    }

    private List<RelayTower> CollectPlacedRelays()
    {
        List<RelayTower> relays = new List<RelayTower>(8);
        Transform placedTowerRoot = _placedTowerRootQuery != null ? _placedTowerRootQuery() : null;
        if (placedTowerRoot == null)
        {
            return relays;
        }

        for (int childIndex = 0; childIndex < placedTowerRoot.childCount; childIndex++)
        {
            Transform child = placedTowerRoot.GetChild(childIndex);
            if (child == null || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            RelayTower relayTower = child.GetComponent<RelayTower>();
            if (relayTower != null)
            {
                relays.Add(relayTower);
            }
        }

        return relays;
    }

    private static bool IsInsideBounds2D(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x
            && point.x <= bounds.max.x
            && point.y >= bounds.min.y
            && point.y <= bounds.max.y;
    }

    private static bool IsInsideAnyRelayCoverageSquare(Vector3 worldPosition, List<RelayTower> relays)
    {
        for (int i = 0; i < relays.Count; i++)
        {
            RelayTower relayTower = relays[i];
            if (relayTower != null && relayTower.ContainsPoint(worldPosition))
            {
                return true;
            }
        }

        return false;
    }
}
