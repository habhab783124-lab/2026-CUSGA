using System;
using UnityEngine;

/// <summary>
/// `TowerPlacementRules` 负责“这个位置到底能不能放塔”的规则判断。
///
/// 这一轮把它从 `TowerDefenseGame` 里拆出来，主要是为了解决两个长期问题：
/// 1. 总控同时管输入、资源、HUD、拖拽、放置判定，职责太密。
/// 2. 放置规则后续还会继续长，例如卖塔、升级、特殊塔型铺路能力等，如果继续堆在总控里，
///    维护成本会越来越高。
///
/// 这里特意把边界写清楚：
/// - 它负责判断“能不能放”
/// - 它负责计算部署网络相关边界
/// - 它不负责实例化塔
/// - 它不负责更新 HUD
/// - 它不负责拖拽视觉表现
///
/// 这样后面继续拆时，`TowerDefenseGame` 就能更像一个“协调器”，
/// 而不是继续往上长成更大的上帝脚本。
/// </summary>
public sealed class TowerPlacementRules
{
    private readonly Func<TowerType, float> _getPlacementRadius;
    private readonly Func<TowerType, Vector2> _getPlacementCenterOffset;
    private readonly Func<TowerType, float> _getExpansionSquareSize;
    private readonly Collider2D[] _placementValidationOverlapBuffer = new Collider2D[64];

    private BuildZone _buildZone;
    private PlacementGrid _placementGrid;
    private PlacementStaticMask _staticPlacementMask;
    private Transform _placedTowerRoot;

    public TowerPlacementRules(
        Func<TowerType, float> getPlacementRadius,
        Func<TowerType, Vector2> getPlacementCenterOffset,
        Func<TowerType, float> getExpansionSquareSize)
    {
        _getPlacementRadius = getPlacementRadius;
        _getPlacementCenterOffset = getPlacementCenterOffset;
        _getExpansionSquareSize = getExpansionSquareSize;
    }

    /// <summary>
    /// 放置规则层只接受显式注入的场景依赖。
    ///
    /// 这里不做任何场景查找，就是为了避免规则组件再次滑回到“名字查找 + 隐式依赖”的旧路。
    /// </summary>
    public void BindSceneReferences(BuildZone buildZone, PlacementGrid placementGrid, PlacementStaticMask staticPlacementMask, Transform placedTowerRoot)
    {
        _buildZone = buildZone;
        _placementGrid = placementGrid;
        _staticPlacementMask = staticPlacementMask;
        _placedTowerRoot = placedTowerRoot;
    }

    /// <summary>
    /// 起手区规则本来就在总控 Inspector 上配置；
    /// 这里把它同步给规则组件，保证“起手判定”和“可视化边界计算”继续共用同一组参数。
    /// </summary>
    public void ConfigureStarterZone(Vector2 starterZoneCenter, float starterZoneSize)
    {
        // 起手区规则已经废弃。
        // 这里保留空实现，只是为了兼容旧的调用点和旧场景序列化数据，
        // 避免在这轮规则切换时引入额外的装配层回归。
    }

    /// <summary>
    /// 这是当前自由放置判定的总入口。
    ///
    /// 规则顺序继续保持和重构前一致：
    /// 1. 必须在 BuildZone 内
    /// 2. 必须在当前允许的部署网络内
    /// 3. 不能碰到 PlacementBlocker
    /// 4. 不能和已建塔过近
    ///
    /// 外部可以传入一个 `shouldIgnoreTransform`，用于忽略预览塔这类“只显示、不参与规则”的对象。
    /// 这也是修复首塔误判和拖拽误挡问题后，当前比较稳的一种边界注入方式。
    /// </summary>
    public bool ValidatePlacementPosition(
        Vector3 worldPosition,
        TowerType towerType,
        Func<Transform, bool> shouldIgnoreTransform,
        out string invalidReason)
    {
        invalidReason = string.Empty;

        if (_buildZone == null)
        {
            invalidReason = "No BuildZone is configured in this level.";
            return false;
        }

        if (_placementGrid != null && _placementGrid.SnapPlacementToCellCenter)
        {
            worldPosition = _placementGrid.SnapWorldPosition(worldPosition);
        }

        if (_placementGrid != null)
        {
            return ValidateGridPlacementPosition(worldPosition, towerType, shouldIgnoreTransform, out invalidReason);
        }

        if (!_buildZone.ContainsPoint(worldPosition))
        {
            invalidReason = "Outside the level's buildable area.";
            return false;
        }

        float placementRadius = _getPlacementRadius != null ? _getPlacementRadius(towerType) : 0f;
        Vector2 centerOffset = _getPlacementCenterOffset != null ? _getPlacementCenterOffset(towerType) : Vector2.zero;
        Vector2 overlapCenter = (Vector2)worldPosition + centerOffset;
        int overlapCount = Physics2D.OverlapCircleNonAlloc(overlapCenter, placementRadius, _placementValidationOverlapBuffer);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = _placementValidationOverlapBuffer[i];
            if (overlap == null)
            {
                continue;
            }

            if (shouldIgnoreTransform != null && shouldIgnoreTransform(overlap.transform))
            {
                continue;
            }

            PlacementBlocker blocker = overlap.GetComponentInParent<PlacementBlocker>();
            if (blocker != null)
            {
                invalidReason = blocker.BlockerReason;
                return false;
            }

            // 只把真正挂在 PlacedTowers 根节点下的正式落塔实例当成“已有结构”。
            // 这能避免场景原型、预览塔或教学演示对象误伤首塔判定。
            bool belongsToPlacedTower = _placedTowerRoot != null && overlap.transform.IsChildOf(_placedTowerRoot);
            if (belongsToPlacedTower
                && (overlap.GetComponentInParent<DefenseTower>() != null || overlap.GetComponentInParent<RelayTower>() != null))
            {
                invalidReason = "Too close to another structure. Move it a little.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 新版格子放置判定入口。
    ///
    /// 规则参考《魔兽争霸 3》这类 RTS 建筑放置方式：
    /// - 建筑不再只看一个点或一个圆形碰撞体
    /// - 建筑会占用一组小格子
    /// - 建筑周围还会生成一圈可配置的正方形禁建格
    /// - 新建筑的占用格不能碰到已有建筑的占用格或禁建格
    ///
    /// 这套规则仍然复用当前场景里的 BuildZone / PlacementBlocker，
    /// 因为它们已经是项目里“哪里是空地、哪里是路径/障碍”的作者化来源。
    /// </summary>
    private bool ValidateGridPlacementPosition(
        Vector3 worldPosition,
        TowerType towerType,
        Func<Transform, bool> shouldIgnoreTransform,
        out string invalidReason)
    {
        invalidReason = string.Empty;

        BoundsInt footprintCells = _placementGrid.GetFootprintCells(worldPosition, towerType);
        if (!AreFootprintCellsBuildable(footprintCells, out invalidReason))
        {
            return false;
        }

        // Use the candidate's full no-build cells (footprint + expansion square) for
        // the overlap check so the dragged square visual matches the actual validation.
        BoundsInt candidateNoBuildCells = _placementGrid.GetNoBuildCells(
            worldPosition, towerType, ResolveNoBuildSquareSize(towerType));
        if (OverlapsExistingStructureGrid(candidateNoBuildCells, shouldIgnoreTransform, out invalidReason))
        {
            return false;
        }

        return true;
    }

    private bool AreFootprintCellsBuildable(BoundsInt footprintCells, out string invalidReason)
    {
        invalidReason = string.Empty;

        if (_staticPlacementMask != null)
        {
            Bounds footprintWorldBounds = _placementGrid.GetWorldBounds(footprintCells);
            if (_staticPlacementMask.TryGetFirstBlockingSample(
                    footprintWorldBounds,
                    out Vector3 blockingSample,
                    out PlacementStaticMaskBlockReason blockingReason))
            {
                if (blockingReason == PlacementStaticMaskBlockReason.PlacementBlocker &&
                    TryGetBlockerAtPoint(blockingSample, out PlacementBlocker blocker))
                {
                    invalidReason = blocker != null ? blocker.BlockerReason : "This grid cell is blocked.";
                }
                else
                {
                    invalidReason = "Building footprint must stay inside buildable ground.";
                }

                return false;
            }

            return true;
        }

        for (int x = footprintCells.xMin; x < footprintCells.xMax; x++)
        {
            for (int y = footprintCells.yMin; y < footprintCells.yMax; y++)
            {
                Vector2 cellCenter = _placementGrid.CellToWorldCenter(new Vector2Int(x, y));
                Vector3 samplePosition = new Vector3(cellCenter.x, cellCenter.y, 0f);

                if (!_buildZone.ContainsPoint(samplePosition))
                {
                    invalidReason = "Building footprint must stay inside buildable ground.";
                    return false;
                }

                if (TryGetBlockerAtPoint(samplePosition, out PlacementBlocker blocker))
                {
                    invalidReason = blocker != null ? blocker.BlockerReason : "This grid cell is blocked.";
                    return false;
                }
            }
        }

        return true;
    }

    private bool OverlapsExistingStructureGrid(
        BoundsInt candidateFootprintCells,
        Func<Transform, bool> shouldIgnoreTransform,
        out string invalidReason)
    {
        invalidReason = string.Empty;

        if (_placedTowerRoot == null)
        {
            return false;
        }

        for (int i = 0; i < _placedTowerRoot.childCount; i++)
        {
            Transform placedTower = _placedTowerRoot.GetChild(i);
            if (placedTower == null || !placedTower.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (shouldIgnoreTransform != null && shouldIgnoreTransform(placedTower))
            {
                continue;
            }

            if (!TryGetPlacedTowerType(placedTower, out TowerType placedTowerType))
            {
                continue;
            }

            BoundsInt placedNoBuildCells = _placementGrid.GetNoBuildCells(
                placedTower.position,
                placedTowerType,
                ResolveNoBuildSquareSize(placedTowerType));
            if (PlacementGrid.Overlaps(candidateFootprintCells, placedNoBuildCells))
            {
                invalidReason = "Too close to another structure. Move it outside the blocked grid.";
                return true;
            }
        }

        return false;
    }

    private float ResolveNoBuildSquareSize(TowerType towerType)
    {
        float configuredSize = _getExpansionSquareSize != null ? _getExpansionSquareSize(towerType) : 0f;
        return Mathf.Max(_placementGrid != null ? _placementGrid.CellSize : 0.05f, configuredSize);
    }

    private bool TryGetBlockerAtPoint(Vector3 worldPosition, out PlacementBlocker blocker)
    {
        int overlapCount = Physics2D.OverlapPointNonAlloc(worldPosition, _placementValidationOverlapBuffer);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = _placementValidationOverlapBuffer[i];
            if (overlap == null)
            {
                continue;
            }

            blocker = overlap.GetComponentInParent<PlacementBlocker>();
            if (blocker != null)
            {
                return true;
            }
        }

        blocker = null;
        return false;
    }

    /// <summary>
    /// 覆盖层只需要知道“应该扫描哪一块世界空间区域”，不需要扫描整张 BuildZone。
    ///
    /// 所以这里继续沿用重构前的优化策略：
    /// - 首塔阶段只扫描起手区和 BuildZone 的交集
    /// - 有塔后只扫描部署网络包围范围和 BuildZone 的交集
    /// </summary>
    public Bounds GetPlacementOverlayWorldBounds(TowerType towerType)
    {
        if (_buildZone == null)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        return _buildZone.WorldBounds;
    }

    /// <summary>
    /// 供起手区标记和 Scene Gizmos 共用。
    /// </summary>
    public Bounds GetStarterZoneBounds()
    {
        return new Bounds(Vector3.zero, Vector3.zero);
    }

    /// <summary>
    /// 当前起手区标记是否应该显示。
    /// </summary>
    public bool ShouldShowStarterZoneMarker()
    {
        return false;
    }

    /// <summary>
    /// 部署网络规则：
    /// - 场上没塔时，只能放在起手区
    /// - 场上已有塔时，只能放在所有已建塔扩张方格的并集里
    /// </summary>
    private bool IsWithinPlacementNetwork(Vector3 worldPosition, out string invalidReason)
    {
        invalidReason = string.Empty;
        return true;
    }

    private bool TryBuildPlacementNetworkBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasAnyBounds = false;

        if (_placedTowerRoot == null)
        {
            return false;
        }

        for (int i = 0; i < _placedTowerRoot.childCount; i++)
        {
            Transform placedTower = _placedTowerRoot.GetChild(i);
            if (placedTower == null || !placedTower.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!TryGetPlacedTowerType(placedTower, out TowerType placedTowerType))
            {
                continue;
            }

            Bounds squareBounds = CreateSquareBounds(placedTower.position, _getExpansionSquareSize(placedTowerType));
            if (!hasAnyBounds)
            {
                bounds = squareBounds;
                hasAnyBounds = true;
            }
            else
            {
                bounds.Encapsulate(squareBounds.min);
                bounds.Encapsulate(squareBounds.max);
            }
        }

        return hasAnyBounds;
    }

    /// <summary>
    /// 这里仍然根据正式已建塔实例上挂的战斗脚本来回推塔型。
    /// 这和“按对象名查找”不同，因为它依赖的是组件事实，而不是名字约定。
    /// </summary>
    private static bool TryGetPlacedTowerType(Transform placedTower, out TowerType towerType)
    {
        towerType = TowerType.None;
        if (placedTower == null)
        {
            return false;
        }

        if (placedTower.GetComponent<RelayTower>() != null)
        {
            towerType = TowerType.Relay;
            return true;
        }

        DefenseTower defenseTower = placedTower.GetComponent<DefenseTower>();
        if (defenseTower != null)
        {
            towerType = defenseTower.BuildType;
            return true;
        }

        return false;
    }

    private static bool IsInsideSquare(Vector3 worldPosition, Vector2 squareCenter, float squareSize)
    {
        float halfSize = squareSize * 0.5f;
        return Mathf.Abs(worldPosition.x - squareCenter.x) <= halfSize
            && Mathf.Abs(worldPosition.y - squareCenter.y) <= halfSize;
    }

    public static Bounds CreateSquareBounds(Vector2 center, float size)
    {
        return new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(size, size, 0f));
    }

    public static Bounds IntersectBounds(Bounds a, Bounds b)
    {
        Vector3 min = Vector3.Max(a.min, b.min);
        Vector3 max = Vector3.Min(a.max, b.max);
        if (min.x > max.x || min.y > max.y)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds intersection = new Bounds();
        intersection.SetMinMax(min, max);
        return intersection;
    }
}
