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
    private readonly Func<TowerType, float> _getPlacementRadius; // 中文：获取放置半径
    private readonly Func<TowerType, float> _getExpansionSquareSize; // 中文：获取Expansion方格大小
    private readonly Collider2D[] _placementValidationOverlapBuffer = new Collider2D[64]; // 中文：放置ValidationOverlapBuffer

    private BuildZone _buildZone; // 中文：建造区域
    private Transform _placedTowerRoot; // 中文：已放置塔根节点
    private Vector2 _starterZoneCenter; // 中文：起始区域中心
    private float _starterZoneSize; // 中文：起始区域大小

    public TowerPlacementRules(
        Func<TowerType, float> getPlacementRadius,
        Func<TowerType, float> getExpansionSquareSize)
    {
        _getPlacementRadius = getPlacementRadius;
        _getExpansionSquareSize = getExpansionSquareSize;
    }

    /// <summary>
    /// 放置规则层只接受显式注入的场景依赖。
    ///
    /// 这里不做任何场景查找，就是为了避免规则组件再次滑回到“名字查找 + 隐式依赖”的旧路。
    /// </summary>
    public void BindSceneReferences(BuildZone buildZone, Transform placedTowerRoot)
    {
        _buildZone = buildZone;
        _placedTowerRoot = placedTowerRoot;
    }

    /// <summary>
    /// 起手区规则本来就在总控 Inspector 上配置；
    /// 这里把它同步给规则组件，保证“起手判定”和“可视化边界计算”继续共用同一组参数。
    /// </summary>
    public void ConfigureStarterZone(Vector2 starterZoneCenter, float starterZoneSize)
    {
        _starterZoneCenter = starterZoneCenter;
        _starterZoneSize = starterZoneSize;
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
            invalidReason = "当前关卡没有配置 BuildZone。";
            return false;
        }

        if (!_buildZone.ContainsPoint(worldPosition))
        {
            invalidReason = "超出当前关卡的可建造区域。";
            return false;
        }

        float placementRadius = _getPlacementRadius != null ? _getPlacementRadius(towerType) : 0f;
        int overlapCount = Physics2D.OverlapCircleNonAlloc(worldPosition, placementRadius, _placementValidationOverlapBuffer);
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
                invalidReason = "离其他建筑太近了，请稍微挪开一点。";
                return false;
            }
        }

        return true;
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

        Bounds buildBounds = _buildZone.WorldBounds;

        if (_placedTowerRoot == null || _placedTowerRoot.childCount == 0)
        {
            Bounds initialBounds = CreateSquareBounds(_starterZoneCenter, _starterZoneSize);
            return IntersectBounds(buildBounds, initialBounds);
        }

        if (!TryBuildPlacementNetworkBounds(out Bounds networkBounds))
        {
            return buildBounds;
        }

        return IntersectBounds(buildBounds, networkBounds);
    }

    /// <summary>
    /// 供起手区标记和 Scene Gizmos 共用。
    /// </summary>
    public Bounds GetStarterZoneBounds()
    {
        return CreateSquareBounds(_starterZoneCenter, _starterZoneSize);
    }

    /// <summary>
    /// 当前起手区标记是否应该显示。
    /// </summary>
    public bool ShouldShowStarterZoneMarker()
    {
        return _placedTowerRoot == null || _placedTowerRoot.childCount == 0;
    }

    /// <summary>
    /// 部署网络规则：
    /// - 场上没塔时，只能放在起手区
    /// - 场上已有塔时，只能放在所有已建塔扩张方格的并集里
    /// </summary>
    private bool IsWithinPlacementNetwork(Vector3 worldPosition, out string invalidReason)
    {
        invalidReason = string.Empty;

        if (_placedTowerRoot == null || _placedTowerRoot.childCount == 0)
        {
            if (IsInsideSquare(worldPosition, _starterZoneCenter, _starterZoneSize))
            {
                return true;
            }

            invalidReason = "你的第一座建筑必须放在起始区内。";
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

            if (IsInsideSquare(worldPosition, placedTower.position, _getExpansionSquareSize(placedTowerType)))
            {
                return true;
            }
        }

        invalidReason = "请放在当前部署网络范围内。";
        return false;
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
