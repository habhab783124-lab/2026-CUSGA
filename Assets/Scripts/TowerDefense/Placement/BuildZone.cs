using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BuildZone 用来定义“这张地图允许玩家放塔的大区域”。
///
/// 在旧版本里，我们是通过若干固定 BuildPad 来限制建造位置；
/// 现在改成自由放置后，系统需要一个新的“第一层过滤器”：
/// 先判断玩家拖拽落点是否位于关卡允许建造的大范围内，
/// 再继续判断它有没有压到敌人路径、其他塔或特殊阻挡区。
///
/// 你可以把 BuildZone 理解成一张“建造许可证边界框”：
/// - 在框里：有资格继续做更细的合法性校验
/// - 在框外：直接判定为不可建造
///
/// 这种做法的好处是职责非常清楚：
/// 1. BuildZone 只负责回答“这个点有没有落在允许建造的大区域里”。
/// 2. PlacementBlocker 负责回答“这个点是不是压到了禁建区域”。
/// 3. TowerDefenseGame 负责把这些规则组合起来，形成最终可部署判断。
///
/// 相比把所有坐标范围硬编码进总控脚本，单独放一个可视化对象更利于教学和迭代：
/// - 调整范围时不必改逻辑代码
/// - Scene 里更容易理解“为什么这里能建、那里不能建”
/// - 后续如果关卡越来越多，这种做法也更容易复用
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BuildZone : MonoBehaviour
{
    private const string DefaultZoneShapeRootName = "ZoneShapes";

    [Header("Shape Authoring")]
    [SerializeField] private Transform zoneShapeRootReference;
    [SerializeField] private Collider2D[] zoneShapeColliders = new Collider2D[0];
    [SerializeField] private bool autoCollectZoneShapes = true;
    [SerializeField] private bool includeInactiveShapes = true;

    [Header("Gizmo")]

    /// <summary>
    /// 在 Scene 视图里绘制建造区轮廓时使用的颜色。
    ///
    /// 这纯粹是编辑期辅助信息，
    /// 目的是让你在调关卡时更容易看清当前允许建造的边界。
    /// </summary>
    [SerializeField] private Color gizmoColor = new Color(0.25f, 0.85f, 0.95f, 0.9f);

    /// <summary>
    /// 对建造区碰撞盒的缓存引用。
    ///
    /// 由于 BoxCollider2D 就是这张“许可区域”的真实数据来源，
    /// 所以脚本会把它缓存下来，避免反复 GetComponent。
    /// </summary>
    private BoxCollider2D _boxCollider;

    /// <summary>
    /// 当前建造区在世界空间中的包围盒。
    ///
    /// 对外暴露这个只读属性，
    /// 主要是为了以后如果你想做调试显示或编辑器工具时能方便取到范围数据。
    /// </summary>
    public Bounds WorldBounds
    {
        get
        {
            CacheReference();

            List<Collider2D> validShapes = new List<Collider2D>();
            if (CollectValidZoneShapes(validShapes) > 0)
            {
                Bounds combinedBounds = validShapes[0].bounds;
                for (int index = 1; index < validShapes.Count; index++)
                {
                    combinedBounds.Encapsulate(validShapes[index].bounds.min);
                    combinedBounds.Encapsulate(validShapes[index].bounds.max);
                }

                return combinedBounds;
            }

            return _boxCollider != null ? _boxCollider.bounds : new Bounds(transform.position, Vector3.zero);
        }
    }
    public Transform ZoneShapeRoot => zoneShapeRootReference;
    public int ZoneShapeCount => CollectValidZoneShapes(null);

    /// <summary>
    /// 在运行时初始化引用，并确保碰撞盒以 Trigger 模式工作。
    ///
    /// 这里使用 Trigger 是因为我们只想拿它做“区域判定”，
    /// 而不是让它真实参与物理碰撞和阻挡。
    /// </summary>
    private void Awake()
    {
        CacheReference();
        TryAssignZoneShapeRoot();
        if (autoCollectZoneShapes)
        {
            CollectZoneShapeColliders();
        }

        EnsureTriggerMode();
    }

    /// <summary>
    /// 当 Inspector 参数变化时，在编辑器里同步修正组件状态。
    ///
    /// 这样可以避免你不小心把 BoxCollider2D 设成非 Trigger，
    /// 然后在 Play 模式里才发现建造判定和预期不一致。
    /// </summary>
    private void OnValidate()
    {
        CacheReference();
        TryAssignZoneShapeRoot();
        if (autoCollectZoneShapes)
        {
            CollectZoneShapeColliders();
        }

        EnsureTriggerMode();
    }

    /// <summary>
    /// 判断某个世界坐标点是否位于允许建造的大区域内。
    ///
    /// 这里直接调用 BoxCollider2D 的 OverlapPoint，
    /// 因为它已经很好地封装了“点是否落在当前碰撞盒范围里”的逻辑。
    /// </summary>
    public bool ContainsPoint(Vector3 worldPosition)
    {
        CacheReference();

        if (CollectValidZoneShapes(null) > 0)
        {
            for (int index = 0; index < zoneShapeColliders.Length; index++)
            {
                Collider2D shapeCollider = zoneShapeColliders[index];
                if (shapeCollider != null && shapeCollider.OverlapPoint(worldPosition))
                {
                    return true;
                }
            }

            return false;
        }

        return _boxCollider != null && _boxCollider.OverlapPoint(worldPosition);
    }

    public string BuildAuthoringSummary()
    {
        CacheReference();
        string rootName = zoneShapeRootReference != null ? zoneShapeRootReference.name : "(BuildZone Root)";
        string colliderName = _boxCollider != null ? _boxCollider.GetType().Name : "未设置";
        return $"ZoneShapeRoot={rootName} | ShapeColliders={ZoneShapeCount} | FallbackCollider={colliderName}";
    }

    public bool CollectZoneShapeColliders()
    {
        List<Collider2D> collectedColliders = new List<Collider2D>();
        Transform collectionRoot = zoneShapeRootReference != null ? zoneShapeRootReference : transform;
        CollectCollidersInHierarchyOrder(collectionRoot, includeInactiveShapes, collectedColliders);

        if (collectionRoot == transform && _boxCollider != null)
        {
            collectedColliders.RemoveAll(candidate => candidate == _boxCollider);
        }

        return AssignColliderArray(collectedColliders);
    }

    /// <summary>
    /// 缓存 BoxCollider2D 引用。
    ///
    /// 这里采用“如果还没缓存过，再去取一次”的写法，
    /// 兼顾了稳健性和运行效率。
    /// </summary>
    private void CacheReference()
    {
        if (_boxCollider == null)
        {
            _boxCollider = GetComponent<BoxCollider2D>();
        }
    }

    /// <summary>
    /// 强制把建造区碰撞盒设为 Trigger。
    ///
    /// 这是一个很重要的小细节：
    /// BuildZone 应该是一个“逻辑区域”，而不是“物理墙体”。
    /// 如果让它参与真实碰撞，后面很容易引入不必要的副作用。
    /// </summary>
    private void EnsureTriggerMode()
    {
        if (_boxCollider != null)
        {
            _boxCollider.isTrigger = true;
        }

        for (int index = 0; index < zoneShapeColliders.Length; index++)
        {
            if (zoneShapeColliders[index] != null)
            {
                zoneShapeColliders[index].isTrigger = true;
            }
        }
    }

    private void TryAssignZoneShapeRoot()
    {
        if (zoneShapeRootReference != null)
        {
            return;
        }

        Transform existingRoot = transform.Find(DefaultZoneShapeRootName);
        if (existingRoot != null)
        {
            zoneShapeRootReference = existingRoot;
        }
    }

    private int CollectValidZoneShapes(List<Collider2D> output)
    {
        int count = 0;
        for (int index = 0; index < zoneShapeColliders.Length; index++)
        {
            Collider2D shapeCollider = zoneShapeColliders[index];
            if (shapeCollider == null)
            {
                continue;
            }

            count++;
            output?.Add(shapeCollider);
        }

        return count;
    }

    private bool AssignColliderArray(List<Collider2D> collectedColliders)
    {
        bool changed = zoneShapeColliders.Length != collectedColliders.Count;
        if (!changed)
        {
            for (int index = 0; index < zoneShapeColliders.Length; index++)
            {
                if (zoneShapeColliders[index] != collectedColliders[index])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return false;
        }

        zoneShapeColliders = collectedColliders.ToArray();
        return true;
    }

    private static void CollectCollidersInHierarchyOrder(Transform root, bool includeInactive, List<Collider2D> output)
    {
        output.Clear();
        if (root == null)
        {
            return;
        }

        TraverseColliderHierarchy(root, includeInactive, output);
    }

    private static void TraverseColliderHierarchy(Transform current, bool includeInactive, List<Collider2D> output)
    {
        if (current == null)
        {
            return;
        }

        if (!includeInactive && !current.gameObject.activeInHierarchy)
        {
            return;
        }

        Collider2D collider = current.GetComponent<Collider2D>();
        if (collider != null)
        {
            output.Add(collider);
        }

        for (int childIndex = 0; childIndex < current.childCount; childIndex++)
        {
            TraverseColliderHierarchy(current.GetChild(childIndex), includeInactive, output);
        }
    }

    /// <summary>
    /// 在 Scene 视图里绘制建造区线框，帮助你调试关卡边界。
    ///
    /// 对自由放置系统来说，
    /// Gizmo 能显著降低“明明不能放但我不知道为什么”的编辑期困惑。
    /// </summary>
    private void OnDrawGizmos()
    {
        CacheReference();
        Gizmos.color = gizmoColor;

        if (CollectValidZoneShapes(null) > 0)
        {
            for (int index = 0; index < zoneShapeColliders.Length; index++)
            {
                DrawColliderGizmo(zoneShapeColliders[index]);
            }

            return;
        }

        if (_boxCollider == null)
        {
            return;
        }

        DrawColliderGizmo(_boxCollider);
    }

    private void DrawColliderGizmo(Collider2D collider)
    {
        if (collider == null)
        {
            return;
        }

        switch (collider)
        {
            case BoxCollider2D boxCollider:
                DrawBoxColliderGizmo(boxCollider);
                return;

            case CircleCollider2D circleCollider:
                DrawCircleColliderGizmo(circleCollider);
                return;

            case PolygonCollider2D polygonCollider:
                DrawPolygonColliderGizmo(polygonCollider);
                return;

            case CapsuleCollider2D capsuleCollider:
                DrawCapsuleColliderGizmo(capsuleCollider);
                return;
        }

        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }

    private void DrawBoxColliderGizmo(BoxCollider2D collider)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = collider.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(collider.offset, collider.size);
        Gizmos.matrix = previousMatrix;
    }

    private void DrawCircleColliderGizmo(CircleCollider2D collider)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = collider.transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(collider.offset, collider.radius);
        Gizmos.matrix = previousMatrix;
    }

    private void DrawCapsuleColliderGizmo(CapsuleCollider2D collider)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = collider.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(collider.offset, collider.size);
        Gizmos.matrix = previousMatrix;
    }

    private void DrawPolygonColliderGizmo(PolygonCollider2D collider)
    {
        for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex++)
        {
            Vector2[] path = collider.GetPath(pathIndex);
            if (path == null || path.Length < 2)
            {
                continue;
            }

            for (int pointIndex = 0; pointIndex < path.Length; pointIndex++)
            {
                Vector3 start = collider.transform.TransformPoint(path[pointIndex]);
                Vector3 end = collider.transform.TransformPoint(path[(pointIndex + 1) % path.Length]);
                Gizmos.DrawLine(start, end);
            }
        }
    }
}
