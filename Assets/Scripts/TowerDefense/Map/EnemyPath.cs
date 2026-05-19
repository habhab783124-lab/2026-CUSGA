using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `EnemyPath` 用来保存敌人需要经过的一整条路线。
///
/// 这里采用的是一种非常适合原型和关卡编辑的做法：
/// - 场景里放一个父对象 `EnemyPath`
/// - 在它下面摆若干子节点作为路径点
/// - 敌人按这些子节点的顺序依次前进
///
/// 这种设计把“路径数据”放在场景层面管理，而不是硬编码在脚本里。
/// 好处很明显：
/// - 调整路线时不需要改代码
/// - 关卡设计可以直接在 Scene 里拖点位
/// - 敌人逻辑与地图路线配置天然解耦
///
/// 这一轮又额外给它补了一层“可读性表现”职责：
/// - 在 Scene / Game 里补一条更容易读的路线描边
/// - 沿路径补方向箭头
/// - 在转弯处补战斗热点提示
///
/// 这样玩家和关卡作者都更容易一眼看懂：
/// “敌人从哪里来、往哪里去、哪里最可能形成交火”。
/// </summary>
[ExecuteAlways]
public class EnemyPath : MonoBehaviour
{
    private const string ReadabilityRootName = "__PathReadability";
    private const string WaypointRootName = "Waypoints";

    [Header("Waypoint Authoring")]
    [SerializeField] private Transform waypointRootReference;
    [SerializeField] private bool autoFindWaypointRoot = true;

    [Header("Readability Overlay")]
    [SerializeField] private bool showReadabilityOverlay = true;
    [SerializeField] private bool autoCreateReadabilityRoot = true;
    [SerializeField] private Transform readabilityRootReference;
    [SerializeField] private Material readabilityMaterialOverride;
    [SerializeField] private Color routeLineColor = new Color(1f, 0.62f, 0.26f, 0.92f);
    [SerializeField] private Color routeArrowColor = new Color(1f, 0.93f, 0.72f, 0.96f);
    [SerializeField] private Color hotspotColor = new Color(1f, 0.52f, 0.26f, 0.92f);
    [SerializeField] private float routeLineWidth = 0.22f;
    [SerializeField] private float arrowSpacing = 1.8f;
    [SerializeField] private float arrowSize = 0.38f;
    [SerializeField] private float hotspotRadius = 0.42f;
    [SerializeField] private float turnHotspotThreshold = 20f;
    [SerializeField] private int readabilitySortingOrder = 2;

    /// <summary>
    /// 运行时是否允许显示路线辅助层。
    /// 在编辑器模式下我们仍然默认显示，方便继续调路径；
    /// 在 Play 模式下则由 `WaveSpawner` 控制显隐。
    /// </summary>
    private bool _runtimeReadabilityVisible = true;

    /// <summary>
    /// 当前路径上缓存的所有路径点。
    ///
    /// 它们来自该对象的直接子节点，顺序与 Hierarchy 中的排列顺序一致。
    /// 敌人会按照这个列表依次移动。
    /// </summary>
    private readonly List<Transform> _waypoints = new List<Transform>();

    /// <summary>
    /// 路线的本地坐标缓存。
    ///
    /// 这份缓存主要给“路径可读性表现”使用。
    /// 因为表现层根节点也会挂在 `EnemyPath` 下面，
    /// 所以本地坐标能直接拿来做折线、箭头和热点的局部摆放。
    /// </summary>
    private readonly List<Vector3> _localRoutePoints = new List<Vector3>();

    private int _lastReadabilityHash;

    /// <summary>
    /// 当前路径点数量。
    /// </summary>
    public int WaypointCount => _waypoints.Count;
    public Transform WaypointRoot => waypointRootReference;

    /// <summary>
    /// 在运行时初始化路径点缓存和路径表现。
    /// </summary>
    private void Awake()
    {
        if (Application.isPlaying)
        {
            _runtimeReadabilityVisible = false;
        }

        CacheWaypoints();

        if (ShouldSkipAuthoringReadabilityRefresh())
        {
            _lastReadabilityHash = 0;
            return;
        }

        RefreshReadabilityVisuals(force: true);
    }

    /// <summary>
    /// `OnEnable()` 也主动补一次可读性表现。
    ///
    /// 这样无论是场景首次打开、脚本重编译，还是对象重新激活，
    /// 路径表现都能尽快回到稳定状态。
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            _runtimeReadabilityVisible = false;
        }

        CacheWaypoints();

        if (ShouldSkipAuthoringReadabilityRefresh())
        {
            _lastReadabilityHash = 0;
            return;
        }

        RefreshReadabilityVisuals(force: true);
    }

    /// <summary>
    /// 当 Inspector 中的值或子物体结构变化时，在编辑器中重新整理路径点缓存。
    ///
    /// 这样你在场景里拖动、增删或重排路径点后，
    /// 路径数据和可读性表现都能尽量保持与当前层级结构一致。
    /// </summary>
    private void OnValidate()
    {
        if (waypointRootReference == null && autoFindWaypointRoot)
        {
            Transform existingWaypointRoot = transform.Find(WaypointRootName);
            if (existingWaypointRoot != null)
            {
                waypointRootReference = existingWaypointRoot;
            }
        }

        if (readabilityRootReference == null)
        {
            Transform existingRoot = transform.Find(ReadabilityRootName);
            if (existingRoot != null)
            {
                readabilityRootReference = existingRoot;
            }
        }

        CacheWaypoints();

        if (ShouldSkipAuthoringReadabilityRefresh())
        {
            _lastReadabilityHash = 0;
            return;
        }

        RefreshReadabilityVisuals(force: true);
    }

    /// <summary>
    /// 获取敌人的出生位置。
    ///
    /// 默认取第一个路径点的位置作为出生点；
    /// 如果当前没有任何路径点，则退回到 `EnemyPath` 对象自身的位置。
    /// 这种回退策略能在配置不完整时提供一个可预期结果。
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        if (_waypoints.Count == 0)
        {
            return transform.position;
        }

        return _waypoints[0].position;
    }

    /// <summary>
    /// 获取指定索引路径点的世界坐标。
    /// </summary>
    public Vector3 GetWaypointPosition(int index)
    {
        return _waypoints[index].position;
    }

    /// <summary>
    /// 读取某一段路径的朝向。
    ///
    /// 这个接口专门服务于“关卡可读性表现”：
    /// 出怪口图标、方向箭头和后续更多路线提示层，
    /// 都需要知道敌人接下来要往哪边走。
    /// </summary>
    public bool TryGetSegmentDirection(int segmentIndex, out Vector3 direction)
    {
        direction = Vector3.right;

        if (segmentIndex < 0 || segmentIndex >= _waypoints.Count - 1)
        {
            return false;
        }

        Vector3 delta = _waypoints[segmentIndex + 1].position - _waypoints[segmentIndex].position;
        if (delta.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        direction = delta.normalized;
        return true;
    }

    /// <summary>
    /// 读取整条路径一开始的移动朝向。
    /// </summary>
    public bool TryGetInitialDirection(out Vector3 direction)
    {
        return TryGetSegmentDirection(0, out direction);
    }

    /// <summary>
    /// 重新扫描当前对象的子节点，并用它们刷新路径点列表。
    ///
    /// 这里仍然保持“直接子节点即路径点”的简单规则，
    /// 只是额外显式跳过自动生成的可读性根节点，
    /// 避免辅助表现反过来污染真正的敌人路线。
    /// </summary>
    private void CacheWaypoints()
    {
        _waypoints.Clear();
        _localRoutePoints.Clear();

        Transform waypointContainer = waypointRootReference;
        if (waypointContainer != null)
        {
            foreach (Transform child in waypointContainer)
            {
                if (child == null)
                {
                    continue;
                }

                _waypoints.Add(child);
                _localRoutePoints.Add(transform.InverseTransformPoint(child.position));
            }

            return;
        }

        foreach (Transform child in transform)
        {
            if (child == null || child.name == ReadabilityRootName || child.name == WaypointRootName)
            {
                continue;
            }

            _waypoints.Add(child);
            _localRoutePoints.Add(child.localPosition);
        }
    }

    /// <summary>
    /// 在 Scene 视图中绘制路线辅助图形。
    ///
    /// 这里会同时做两件事：
    /// - 保留最基础的 Gizmo 球和连线，方便快速检查路径顺序
    /// - 顺带刷新程序化路线表现，让你在拖点位时，方向箭头和热点也一起跟着更新
    /// </summary>
    private void OnDrawGizmos()
    {
        CacheWaypoints();

        if (ShouldSkipAuthoringReadabilityRefresh())
        {
            return;
        }

        RefreshReadabilityVisuals(force: false);

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 1f);

        for (int i = 0; i < _waypoints.Count; i++)
        {
            Transform waypoint = _waypoints[i];
            if (waypoint == null)
            {
                continue;
            }

            Gizmos.DrawSphere(waypoint.position, 0.12f);

            if (i < _waypoints.Count - 1 && _waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoint.position, _waypoints[i + 1].position);
            }
        }
    }

    /// <summary>
    /// 只在必要时刷新路径可读性表现。
    ///
    /// 这里做成“按 hash 判断”的原因很实际：
    /// - 关卡作者拖路径点时，表现要及时跟上
    /// - 但又不希望每次 Scene 重绘都无脑重建整套路由子节点
    /// </summary>
    private void RefreshReadabilityVisuals(bool force)
    {
        if (ShouldSkipAuthoringReadabilityRefresh())
        {
            _lastReadabilityHash = 0;
            return;
        }

        if (!ShouldShowReadabilityOverlay())
        {
            Transform readabilityRoot = ResolveReadabilityRoot(allowCreate: false);
            if (readabilityRoot != null && readabilityRoot.gameObject.activeSelf)
            {
                readabilityRoot.gameObject.SetActive(false);
            }

            _lastReadabilityHash = 0;
            return;
        }

        int readabilityHash = ComputeReadabilityHash();
        if (!force && readabilityHash == _lastReadabilityHash)
        {
            return;
        }

        _lastReadabilityHash = readabilityHash;
        RebuildReadabilityVisuals();
    }

    /// <summary>
    /// 运行时由外部统一控制路径辅助层是否可见。
    /// 这正是波次系统用来实现“出怪前 2 秒显示路线、出怪后隐藏路线”的入口。
    /// </summary>
    public void SetRuntimeReadabilityVisible(bool visible)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_runtimeReadabilityVisible == visible)
        {
            return;
        }

        _runtimeReadabilityVisible = visible;
        RefreshReadabilityVisuals(force: true);
    }

    public void EditorRefreshAuthoringState()
    {
        if (ShouldSkipAuthoringReadabilityRefresh())
        {
            CacheWaypoints();
            _lastReadabilityHash = 0;
            return;
        }

        CacheWaypoints();
        RefreshReadabilityVisuals(force: true);
    }

    private bool ShouldShowReadabilityOverlay()
    {
        if (!showReadabilityOverlay)
        {
            return false;
        }

        if (!Application.isPlaying)
        {
            return true;
        }

        return _runtimeReadabilityVisible;
    }

    /// <summary>
    /// Returns true while an editor tool is intentionally restructuring the scene in bulk.
    ///
    /// This keeps `OnValidate` helper regeneration from fighting the authoring tool mid-edit.
    /// The tool will refresh visuals explicitly once the structural work is complete.
    /// </summary>
    private static bool ShouldSkipAuthoringReadabilityRefresh()
    {
        return !Application.isPlaying && BattlefieldAuthoringGuard.IsReadabilityRefreshSuppressed;
    }

    /// <summary>
    /// 真正生成或更新路径表现。
    ///
    /// 这一层会给路径补三类可读性信息：
    /// - 主路线描边
    /// - 前进方向箭头
    /// - 转弯热点提示
    ///
    /// 当前这些都用程序化线框生成，是为了保证：
    /// - 没有正式美术时也能先读清楚关卡
    /// - 后续替换正式资源时，不需要把玩法脚本一起改掉
    /// </summary>
    private void RebuildReadabilityVisuals()
    {
        Transform readabilityRoot = ResolveReadabilityRoot(allowCreate: autoCreateReadabilityRoot);
        if (readabilityRoot == null)
        {
            return;
        }

        readabilityRoot.gameObject.SetActive(true);

        LineRenderer routeLine = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            readabilityRoot,
            "RouteLine",
            readabilitySortingOrder,
            routeLineWidth,
            routeLineColor,
            loop: false,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetPolyline(routeLine, _localRoutePoints, false, routeLineWidth, routeLineColor);

        Transform arrowsRoot = BattlefieldReadabilityVisualUtility.EnsureChild(readabilityRoot, "DirectionArrows");
        Transform hotspotsRoot = BattlefieldReadabilityVisualUtility.EnsureChild(readabilityRoot, "TurnHotspots");

        int arrowIndex = 0;
        for (int segmentIndex = 0; segmentIndex < _localRoutePoints.Count - 1; segmentIndex++)
        {
            Vector3 localStart = _localRoutePoints[segmentIndex];
            Vector3 localEnd = _localRoutePoints[segmentIndex + 1];
            Vector3 localDelta = localEnd - localStart;
            float segmentLength = localDelta.magnitude;
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            Vector3 direction = localDelta / segmentLength;
            int arrowsOnSegment = Mathf.Max(1, Mathf.FloorToInt(segmentLength / Mathf.Max(0.35f, arrowSpacing)));

            for (int arrowOnSegmentIndex = 0; arrowOnSegmentIndex < arrowsOnSegment; arrowOnSegmentIndex++)
            {
                float t = (arrowOnSegmentIndex + 1f) / (arrowsOnSegment + 1f);
                Vector3 localPosition = Vector3.Lerp(localStart, localEnd, t);

                Transform arrow = BattlefieldReadabilityVisualUtility.EnsureChild(arrowsRoot, $"Arrow_{arrowIndex:00}");
                arrow.gameObject.SetActive(true);
                arrow.localPosition = localPosition;
                arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                arrow.localScale = Vector3.one;

                LineRenderer arrowRenderer = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
                    arrow,
                    "ArrowShape",
                    readabilitySortingOrder + 1,
                    routeLineWidth * 0.72f,
                    routeArrowColor,
                    loop: false,
                    sharedMaterialOverride: readabilityMaterialOverride);
                BattlefieldReadabilityVisualUtility.SetArrow(
                    arrowRenderer,
                    arrowSize,
                    routeLineWidth * 0.72f,
                    routeArrowColor);

                arrowIndex++;
            }
        }

        BattlefieldReadabilityVisualUtility.SetChildrenActiveFromIndex(arrowsRoot, arrowIndex, false);

        int hotspotIndex = 0;
        for (int waypointIndex = 1; waypointIndex < _localRoutePoints.Count - 1; waypointIndex++)
        {
            Vector3 previousDirection = (_localRoutePoints[waypointIndex] - _localRoutePoints[waypointIndex - 1]).normalized;
            Vector3 nextDirection = (_localRoutePoints[waypointIndex + 1] - _localRoutePoints[waypointIndex]).normalized;
            float turnAngle = Vector3.Angle(previousDirection, nextDirection);
            if (turnAngle < turnHotspotThreshold)
            {
                continue;
            }

            Transform hotspot = BattlefieldReadabilityVisualUtility.EnsureChild(hotspotsRoot, $"Hotspot_{hotspotIndex:00}");
            hotspot.gameObject.SetActive(true);
            hotspot.localPosition = _localRoutePoints[waypointIndex];
            hotspot.localRotation = Quaternion.identity;
            hotspot.localScale = Vector3.one;

            LineRenderer hotspotRing = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
                hotspot,
                "HotspotRing",
                readabilitySortingOrder + 1,
                routeLineWidth * 0.6f,
                hotspotColor,
                loop: true,
                sharedMaterialOverride: readabilityMaterialOverride);
            BattlefieldReadabilityVisualUtility.SetCircle(
                hotspotRing,
                hotspotRadius,
                22,
                routeLineWidth * 0.6f,
                hotspotColor);

            LineRenderer hotspotDiamond = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
                hotspot,
                "HotspotDiamond",
                readabilitySortingOrder + 2,
                routeLineWidth * 0.45f,
                routeArrowColor,
                loop: true,
                sharedMaterialOverride: readabilityMaterialOverride);
            BattlefieldReadabilityVisualUtility.SetDiamond(
                hotspotDiamond,
                hotspotRadius * 0.62f,
                routeLineWidth * 0.45f,
                routeArrowColor);

            hotspotIndex++;
        }

        BattlefieldReadabilityVisualUtility.SetChildrenActiveFromIndex(hotspotsRoot, hotspotIndex, false);
    }

    /// <summary>
    /// 把当前路径表现的关键输入压成一个 hash。
    /// </summary>
    private int ComputeReadabilityHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + showReadabilityOverlay.GetHashCode();
            hash = hash * 31 + autoCreateReadabilityRoot.GetHashCode();
            hash = hash * 31 + routeLineColor.GetHashCode();
            hash = hash * 31 + routeArrowColor.GetHashCode();
            hash = hash * 31 + hotspotColor.GetHashCode();
            hash = hash * 31 + routeLineWidth.GetHashCode();
            hash = hash * 31 + arrowSpacing.GetHashCode();
            hash = hash * 31 + arrowSize.GetHashCode();
            hash = hash * 31 + hotspotRadius.GetHashCode();
            hash = hash * 31 + turnHotspotThreshold.GetHashCode();
            hash = hash * 31 + readabilitySortingOrder;
            hash = hash * 31 + (readabilityRootReference != null ? readabilityRootReference.GetInstanceID() : 0);
            hash = hash * 31 + (readabilityMaterialOverride != null ? readabilityMaterialOverride.GetInstanceID() : 0);

            for (int i = 0; i < _localRoutePoints.Count; i++)
            {
                hash = hash * 31 + _localRoutePoints[i].GetHashCode();
            }

            return hash;
        }
    }

    /// <summary>
    /// 统一解析路径可读性根节点。
    ///
    /// 现在这层支持两种工作流：
    /// - 用户自己在 Scene 里显式指定 `readabilityRootReference`
    /// - 没指定时，脚本按约定名称自动创建一个占位根节点
    ///
    /// 这样就比原来更作者友好：
    /// 你既可以继续吃默认自动化，也可以自己接管这一层层级。
    /// </summary>
    private Transform ResolveReadabilityRoot(bool allowCreate)
    {
        if (readabilityRootReference != null)
        {
            return readabilityRootReference;
        }

        Transform existingRoot = transform.Find(ReadabilityRootName);
        if (existingRoot != null)
        {
            readabilityRootReference = existingRoot;
            return existingRoot;
        }

        if (!allowCreate)
        {
            return null;
        }

        readabilityRootReference = BattlefieldReadabilityVisualUtility.EnsureChild(transform, ReadabilityRootName);
        return readabilityRootReference;
    }
}

/// <summary>
/// `BattlefieldReadabilityVisualUtility` 是这一轮“关卡可读性表现”使用的轻量工具箱。
///
/// 它专门解决一个很现实的问题：
/// - 路径、出怪口、防御点都想在 Scene / Game 里补一些占位表现
/// - 这些表现又不应该强耦合某张正式美术贴图
///
/// 所以这里统一提供一组“程序化线框图形”工具：
/// - 圆环
/// - 折线
/// - 菱形
/// - 箭头
/// - 角框
///
/// 这样后续哪怕用户把当前占位视觉全部替换掉，
/// 玩法层和场景骨架也不会被拖着一起改。
/// </summary>
public static class BattlefieldReadabilityVisualUtility
{
    private static Material s_sharedLineMaterial;

    /// <summary>
    /// 确保某个父节点下存在一个指定名字的子节点。
    /// </summary>
    public static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        child = childObject.transform;
        child.SetParent(parent, false);
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    /// <summary>
    /// 确保某个子节点带有可用的 `LineRenderer`。
    /// 这里统一把材质、排序和基础参数写齐，避免各处重复样板代码。
    /// </summary>
    public static LineRenderer EnsureLineRenderer(
        Transform parent,
        string childName,
        int sortingOrder,
        float width,
        Color color,
        bool loop,
        bool useWorldSpace = false,
        Material sharedMaterialOverride = null)
    {
        Transform child = EnsureChild(parent, childName);
        LineRenderer lineRenderer = child.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = child.gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.sharedMaterial = sharedMaterialOverride != null ? sharedMaterialOverride : GetSharedLineMaterial();
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCornerVertices = 6;
        lineRenderer.numCapVertices = 6;
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.widthMultiplier = width;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.loop = loop;
        lineRenderer.useWorldSpace = useWorldSpace;
        lineRenderer.enabled = true;
        return lineRenderer;
    }

    /// <summary>
    /// 把一条折线写进 `LineRenderer`。
    /// </summary>
    public static void SetPolyline(LineRenderer lineRenderer, IList<Vector3> points, bool loop, float width, Color color)
    {
        if (lineRenderer == null)
        {
            return;
        }

        int pointCount = points != null ? points.Count : 0;
        lineRenderer.loop = loop;
        lineRenderer.widthMultiplier = width;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            lineRenderer.SetPosition(i, points[i]);
        }
    }

    /// <summary>
    /// 把一个圆环写进 `LineRenderer`。
    /// </summary>
    public static void SetCircle(LineRenderer lineRenderer, float radius, int segments, float width, Color color)
    {
        int safeSegments = Mathf.Max(8, segments);
        List<Vector3> points = new List<Vector3>(safeSegments);
        for (int i = 0; i < safeSegments; i++)
        {
            float t = i / (float)safeSegments;
            float angle = t * Mathf.PI * 2f;
            points.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        SetPolyline(lineRenderer, points, true, width, color);
    }

    /// <summary>
    /// 写一个菱形框，适合用在热点或目标提示上。
    /// </summary>
    public static void SetDiamond(LineRenderer lineRenderer, float radius, float width, Color color)
    {
        List<Vector3> points = new List<Vector3>(4)
        {
            new Vector3(0f, radius, 0f),
            new Vector3(radius, 0f, 0f),
            new Vector3(0f, -radius, 0f),
            new Vector3(-radius, 0f, 0f)
        };

        SetPolyline(lineRenderer, points, true, width, color);
    }

    /// <summary>
    /// 写一个朝向本地 +X 的箭头轮廓。
    /// 外部只需要旋转子节点，就能把同一模板复用到任意方向。
    /// </summary>
    public static void SetArrow(LineRenderer lineRenderer, float size, float width, Color color)
    {
        float bodyLength = size * 1.1f;
        float wingOffset = size * 0.55f;
        List<Vector3> points = new List<Vector3>(5)
        {
            new Vector3(-bodyLength * 0.55f, wingOffset, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(-bodyLength * 0.55f, -wingOffset, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(bodyLength * 0.65f, 0f, 0f)
        };

        SetPolyline(lineRenderer, points, false, width, color);
    }

    /// <summary>
    /// 写一个方括号式目标框。
    /// 它比完整方框更轻，不会把目标点整个盖住。
    /// </summary>
    public static void SetCornerFrame(LineRenderer lineRenderer, float halfSize, float cornerLength, float width, Color color)
    {
        float c = Mathf.Clamp(cornerLength, 0.05f, halfSize);
        List<Vector3> points = new List<Vector3>(15)
        {
            new Vector3(-halfSize, halfSize - c, 0f),
            new Vector3(-halfSize, halfSize, 0f),
            new Vector3(-halfSize + c, halfSize, 0f),

            new Vector3(halfSize - c, halfSize, 0f),
            new Vector3(halfSize, halfSize, 0f),
            new Vector3(halfSize, halfSize - c, 0f),

            new Vector3(halfSize, -halfSize + c, 0f),
            new Vector3(halfSize, -halfSize, 0f),
            new Vector3(halfSize - c, -halfSize, 0f),

            new Vector3(-halfSize + c, -halfSize, 0f),
            new Vector3(-halfSize, -halfSize, 0f),
            new Vector3(-halfSize, -halfSize + c, 0f),

            new Vector3(-halfSize, halfSize - c, 0f),
            new Vector3(-halfSize, halfSize, 0f),
            new Vector3(-halfSize + c, halfSize, 0f)
        };

        SetPolyline(lineRenderer, points, false, width, color);
    }

    /// <summary>
    /// 把某个容器下超出的子节点隐藏掉。
    /// 这样路径箭头数量减少后，旧节点不会残留在场景里误导人。
    /// </summary>
    public static void SetChildrenActiveFromIndex(Transform parent, int startIndex, bool active)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = startIndex; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.gameObject.activeSelf != active)
            {
                child.gameObject.SetActive(active);
            }
        }
    }

    private static Material GetSharedLineMaterial()
    {
        if (s_sharedLineMaterial != null)
        {
            return s_sharedLineMaterial;
        }

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
        {
            spriteShader = Shader.Find("Hidden/Internal-Colored");
        }

        s_sharedLineMaterial = new Material(spriteShader)
        {
            name = "BattlefieldReadabilityLineMaterial"
        };
        return s_sharedLineMaterial;
    }
}

/// <summary>
/// Centralizes temporary editor-only suppression switches used during large scene authoring passes.
///
/// Why this helper lives beside the map authoring scripts:
/// 1. the route blueprint applier and future topology tools modify many scene objects at once,
/// 2. path / gate / defense-point components all rebuild readability visuals during validation,
/// 3. those automatic rebuilds are useful in normal editing, but disruptive during bulk edits.
///
/// The workflow becomes:
/// - enter one suppression scope,
/// - restructure the scene,
/// - leave the scope,
/// - refresh authoring visuals explicitly once.
///
/// This keeps the behavior deterministic without introducing any runtime dependency on editor code.
/// </summary>
public static class BattlefieldAuthoringGuard
{
    private static int s_readabilitySuppressionDepth;

    /// <summary>
    /// True while an editor tool is intentionally performing a large structural edit.
    /// </summary>
    public static bool IsReadabilityRefreshSuppressed => s_readabilitySuppressionDepth > 0;

    /// <summary>
    /// Begins one nested suppression scope.
    /// Depth counting matters because several tools may compose other helpers internally.
    /// </summary>
    public static System.IDisposable BeginReadabilitySuppressionScope()
    {
        s_readabilitySuppressionDepth++;
        return new ReadabilitySuppressionScope();
    }

    private readonly struct ReadabilitySuppressionScope : System.IDisposable
    {
        public void Dispose()
        {
            if (s_readabilitySuppressionDepth <= 0)
            {
                s_readabilitySuppressionDepth = 0;
                return;
            }

            s_readabilitySuppressionDepth--;
        }
    }
}
