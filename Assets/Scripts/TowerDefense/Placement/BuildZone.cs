using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `BuildZone` 定义“这张地图原则上允许玩家建造的大区域”。
///
/// 早期版本把它做成了一个单独的 `BoxCollider2D`，
/// 这对规则矩形地图很够用，但对后续不规则空地并不友好。
///
/// 当前版本把作者工作流升级成两层：
/// 1. 如果你什么都不额外配置，就继续兼容“当前对象自己挂一个 Collider2D”的简单模式。
/// 2. 如果你想做不规则地形，就在 `ZoneShapes` 根下面摆多个 `Collider2D`，
///    系统会把这些碰撞体的并集当成真正的可建造区域。
///
/// 这样后续做复杂关卡时，优先是继续摆 Scene 里的形状，
/// 而不是回到代码里重写放置逻辑。
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildZone : MonoBehaviour
{
    private const string DefaultZoneShapeRootName = "ZoneShapes"; // 中文：默认区域形状根节点名称

    [Header("形状作者设置")]
    [Tooltip("可选的建造区形状根节点。指定后，系统会优先从它下面收集多个 Collider2D 作为真实建造区形状。")]
    [SerializeField, InspectorName("形状根节点")] private Transform zoneShapeRootReference; // 中文：区域形状根节点引用
    [Tooltip("当前 BuildZone 真正参与判定的形状碰撞体列表。通常通过场景自动收集维护，不建议手工长期维护。")]
    [SerializeField, InspectorName("形状碰撞体列表")] private Collider2D[] zoneShapeColliders = new Collider2D[0]; // 中文：区域形状碰撞体列表
    [Tooltip("开启后，编辑器中会自动从 ZoneShapes 根节点或当前对象层级收集形状碰撞体。")]
    [SerializeField, InspectorName("自动收集形状")] private bool autoCollectZoneShapes = true; // 中文：自动收集区域形状列表
    [Tooltip("自动收集建造区形状时，是否包含未激活对象。")]
    [SerializeField, InspectorName("包含未激活对象")] private bool includeInactiveShapes = true; // 中文：包含未激活形状列表

    [Header("Gizmo")]
    [Tooltip("Scene 视图中显示建造区轮廓时使用的颜色。")]
    [SerializeField, InspectorName("轮廓颜色")] private Color gizmoColor = new Color(0.25f, 0.85f, 0.95f, 0.9f); // 中文：Gizmo颜色

    /// <summary>
    /// 根对象上直接挂着的默认碰撞体。
    /// 当还没有显式收集到不规则形状时，它就是兼容旧工作流的回退区域。
    /// </summary>
    private Collider2D _fallbackCollider; // 中文：fallback碰撞体

    public Bounds WorldBounds => BuildWorldBounds(); // 中文：世界Bounds
    public Transform ZoneShapeRoot => zoneShapeRootReference; // 中文：区域形状根节点
    public int ZoneShapeCount => CollectValidZoneShapes(null); // 中文：区域形状数量

    private void Awake()
    {
        CacheReferences();
        TryAssignZoneShapeRoot();
        if (autoCollectZoneShapes)
        {
            CollectZoneShapeColliders();
        }

        EnsureTriggerMode();
    }

    private void OnValidate()
    {
        CacheReferences();
        TryAssignZoneShapeRoot();
        if (autoCollectZoneShapes)
        {
            CollectZoneShapeColliders();
        }

        EnsureTriggerMode();
    }

    /// <summary>
    /// 判断某个世界坐标点是否落在当前建造区内。
    /// 如果已经显式收集了多个形状碰撞体，就按那些形状的并集判断；
    /// 否则回退到根对象上的默认 Collider2D。
    /// </summary>
    public bool ContainsPoint(Vector3 worldPosition)
    {
        List<Collider2D> validShapeColliders = new List<Collider2D>();
        if (CollectValidZoneShapes(validShapeColliders) > 0)
        {
            for (int index = 0; index < validShapeColliders.Count; index++)
            {
                Collider2D shapeCollider = validShapeColliders[index];
                if (shapeCollider != null && shapeCollider.OverlapPoint(worldPosition))
                {
                    return true;
                }
            }

            return false;
        }

        return _fallbackCollider != null && _fallbackCollider.OverlapPoint(worldPosition);
    }

    /// <summary>
    /// 显式从场景层级收集建造区形状碰撞体。
    /// 这是当前“不规则建造区”作者工作流最关键的一步。
    /// </summary>
    public bool CollectZoneShapeColliders()
    {
        List<Collider2D> collectedColliders = new List<Collider2D>();
        Transform collectionRoot = zoneShapeRootReference != null ? zoneShapeRootReference : transform;
        CollectCollidersInHierarchyOrder(collectionRoot, includeInactiveShapes, collectedColliders);

        if (collectionRoot == transform && _fallbackCollider != null)
        {
            collectedColliders.RemoveAll(candidate => candidate == _fallbackCollider);
        }

        return AssignColliderArray(collectedColliders);
    }

    /// <summary>
    /// 给 Inspector 和作者工具看的摘要。
    /// </summary>
    public string BuildAuthoringSummary()
    {
        string rootName = zoneShapeRootReference != null ? zoneShapeRootReference.name : "(BuildZone Root)";
        string fallbackName = _fallbackCollider != null ? _fallbackCollider.GetType().Name : "未设置";
        return $"ZoneShapeRoot={rootName} | ShapeColliders={ZoneShapeCount} | FallbackCollider={fallbackName}";
    }

    [ContextMenu("Collect Zone Shape Colliders")]
    private void ContextCollectZoneShapeColliders()
    {
        CollectZoneShapeColliders();
    }

    private void CacheReferences()
    {
        if (_fallbackCollider == null)
        {
            _fallbackCollider = GetComponent<Collider2D>();
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

    private void EnsureTriggerMode()
    {
        if (_fallbackCollider != null)
        {
            _fallbackCollider.isTrigger = true;
        }

        for (int index = 0; index < zoneShapeColliders.Length; index++)
        {
            if (zoneShapeColliders[index] != null)
            {
                zoneShapeColliders[index].isTrigger = true;
            }
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

    private Bounds BuildWorldBounds()
    {
        List<Collider2D> validShapeColliders = new List<Collider2D>();
        if (CollectValidZoneShapes(validShapeColliders) > 0)
        {
            Bounds bounds = validShapeColliders[0].bounds;
            for (int index = 1; index < validShapeColliders.Count; index++)
            {
                bounds.Encapsulate(validShapeColliders[index].bounds.min);
                bounds.Encapsulate(validShapeColliders[index].bounds.max);
            }

            return bounds;
        }

        return _fallbackCollider != null
            ? _fallbackCollider.bounds
            : new Bounds(transform.position, Vector3.zero);
    }

    private void OnDrawGizmos()
    {
        CacheReferences();

        List<Collider2D> validShapeColliders = new List<Collider2D>();
        if (CollectValidZoneShapes(validShapeColliders) > 0)
        {
            Gizmos.color = gizmoColor;
            for (int index = 0; index < validShapeColliders.Count; index++)
            {
                DrawColliderGizmo(validShapeColliders[index]);
            }

            return;
        }

        if (_fallbackCollider == null)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        DrawColliderGizmo(_fallbackCollider);
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

    private static void DrawColliderGizmo(Collider2D collider)
    {
        if (collider == null)
        {
            return;
        }

        if (collider is BoxCollider2D boxCollider)
        {
            DrawBoxColliderGizmo(boxCollider);
            return;
        }

        if (collider is CircleCollider2D circleCollider)
        {
            DrawCircleColliderGizmo(circleCollider);
            return;
        }

        if (collider is PolygonCollider2D polygonCollider)
        {
            DrawPolygonColliderGizmo(polygonCollider);
            return;
        }

        if (collider is CompositeCollider2D compositeCollider)
        {
            DrawCompositeColliderGizmo(compositeCollider);
            return;
        }

        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }

    private static void DrawBoxColliderGizmo(BoxCollider2D collider)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = collider.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(collider.offset, collider.size);
        Gizmos.matrix = previousMatrix;
    }

    private static void DrawCircleColliderGizmo(CircleCollider2D collider)
    {
        Vector3 worldCenter = collider.transform.TransformPoint(collider.offset);
        Vector3 lossyScale = collider.transform.lossyScale;
        float radius = collider.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
        Gizmos.DrawWireSphere(worldCenter, radius);
    }

    private static void DrawPolygonColliderGizmo(PolygonCollider2D collider)
    {
        for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex++)
        {
            Vector2[] pathPoints = collider.GetPath(pathIndex);
            for (int pointIndex = 0; pointIndex < pathPoints.Length; pointIndex++)
            {
                Vector3 start = collider.transform.TransformPoint(pathPoints[pointIndex]);
                Vector3 end = collider.transform.TransformPoint(pathPoints[(pointIndex + 1) % pathPoints.Length]);
                Gizmos.DrawLine(start, end);
            }
        }
    }

    private static void DrawCompositeColliderGizmo(CompositeCollider2D collider)
    {
        for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex++)
        {
            int pointCount = collider.GetPathPointCount(pathIndex);
            Vector2[] pathPoints = new Vector2[pointCount];
            collider.GetPath(pathIndex, pathPoints);

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector3 start = collider.transform.TransformPoint(pathPoints[pointIndex]);
                Vector3 end = collider.transform.TransformPoint(pathPoints[(pointIndex + 1) % pointCount]);
                Gizmos.DrawLine(start, end);
            }
        }
    }
}
