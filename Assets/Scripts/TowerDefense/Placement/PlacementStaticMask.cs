using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `PlacementStaticMaskBakeData` 是第二阶段加入的“可序列化静态遮罩数据”。
///
/// 第一阶段已经证明：把 `BuildZone + PlacementBlocker` 栅格化后，
/// 运行时放置判定与覆盖层采样都能明显减少重复查询。
/// 第二阶段要解决的问题，则是把这份栅格化结果提前到编辑器里：
/// - 作者继续画同一套 `BuildZone / PlacementBlocker`
/// - 但进入 Play 时优先读取已经 Bake 好的数据
/// - 只有缺失时，才退回第一阶段的运行时现算
///
/// 这里把数据做成普通可序列化类，而不是强行再起一套外部资产，
/// 是为了继续贴合当前项目“Scene-first”的关卡工作流：
/// - 关卡作者仍然主要改 Scene
/// - `PlacementGrid` 继续作为这张地图的格子语义锚点
/// - Bake 结果直接跟着场景走，避免额外管理一堆配套资源
/// </summary>
[Serializable]
public sealed class PlacementStaticMaskBakeData
{
    [SerializeField] private bool hasData;
    [SerializeField] private float cellSize = 0.25f;
    [SerializeField] private Vector2 origin;
    [SerializeField] private int width = 1;
    [SerializeField] private int height = 1;
    [SerializeField] private Vector2 worldMin;
    [SerializeField] private Vector2 worldSize = Vector2.one;
    [SerializeField] private byte[] packedCellKinds = Array.Empty<byte>();
    [SerializeField] private string sourceSummary = string.Empty;
    [SerializeField] private string bakedAtUtc = string.Empty;

    public bool HasData =>
        hasData
        && cellSize > Mathf.Epsilon
        && width > 0
        && height > 0
        && packedCellKinds != null
        && packedCellKinds.Length == width * height;

    public string SourceSummary => sourceSummary ?? string.Empty;
    public string BakedAtUtc => bakedAtUtc ?? string.Empty;

    internal float CellSize => cellSize;
    internal Vector2 Origin => origin;
    internal int Width => width;
    internal int Height => height;
    internal byte[] PackedCellKinds => packedCellKinds ?? Array.Empty<byte>();

    internal Bounds WorldBounds
    {
        get
        {
            Bounds bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(worldMin.x, worldMin.y, 0f),
                new Vector3(worldMin.x + worldSize.x, worldMin.y + worldSize.y, 0f));
            return bounds;
        }
    }

    /// <summary>
    /// 把新 Bake 出来的数据覆盖到当前容器里。
    ///
    /// 返回值用来告诉调用者“这次写入是否真的改变了序列化内容”，
    /// 这样编辑器工具就能避免每次都把场景标脏。
    /// </summary>
    public bool OverwriteFrom(PlacementStaticMaskBakeData source)
    {
        if (source == null)
        {
            return Clear();
        }

        if (Matches(source))
        {
            return false;
        }

        hasData = source.hasData;
        cellSize = source.cellSize;
        origin = source.origin;
        width = source.width;
        height = source.height;
        worldMin = source.worldMin;
        worldSize = source.worldSize;
        sourceSummary = source.sourceSummary ?? string.Empty;
        bakedAtUtc = source.bakedAtUtc ?? string.Empty;
        packedCellKinds = source.packedCellKinds != null
            ? (byte[])source.packedCellKinds.Clone()
            : Array.Empty<byte>();
        return true;
    }

    /// <summary>
    /// 清空现有 Bake 数据。
    /// </summary>
    public bool Clear()
    {
        if (!HasData
            && string.IsNullOrEmpty(sourceSummary)
            && string.IsNullOrEmpty(bakedAtUtc)
            && (packedCellKinds == null || packedCellKinds.Length == 0))
        {
            return false;
        }

        hasData = false;
        cellSize = 0.25f;
        origin = Vector2.zero;
        width = 1;
        height = 1;
        worldMin = Vector2.zero;
        worldSize = Vector2.one;
        sourceSummary = string.Empty;
        bakedAtUtc = string.Empty;
        packedCellKinds = Array.Empty<byte>();
        return true;
    }

    public string BuildSummary()
    {
        if (!HasData)
        {
            return "未写入静态遮罩 Bake 数据。";
        }

        string source = string.IsNullOrWhiteSpace(sourceSummary) ? "未知来源" : sourceSummary;
        string bakedTime = string.IsNullOrWhiteSpace(bakedAtUtc) ? "未记录时间" : bakedAtUtc;
        return $"Raster={width}x{height} Cell={cellSize:0.###} Source={source} BakedAt={bakedTime}";
    }

    internal void Write(
        float bakedCellSize,
        Vector2 bakedOrigin,
        int bakedWidth,
        int bakedHeight,
        Bounds bakedWorldBounds,
        byte[] bakedPackedCellKinds,
        string bakedSourceSummary)
    {
        hasData = bakedPackedCellKinds != null && bakedPackedCellKinds.Length == bakedWidth * bakedHeight;
        cellSize = bakedCellSize;
        origin = bakedOrigin;
        width = Mathf.Max(1, bakedWidth);
        height = Mathf.Max(1, bakedHeight);
        worldMin = new Vector2(bakedWorldBounds.min.x, bakedWorldBounds.min.y);
        worldSize = new Vector2(bakedWorldBounds.size.x, bakedWorldBounds.size.y);
        packedCellKinds = bakedPackedCellKinds != null ? (byte[])bakedPackedCellKinds.Clone() : Array.Empty<byte>();
        sourceSummary = bakedSourceSummary ?? string.Empty;
        bakedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }

    private bool Matches(PlacementStaticMaskBakeData other)
    {
        if (other == null)
        {
            return !HasData
                && string.IsNullOrEmpty(sourceSummary)
                && string.IsNullOrEmpty(bakedAtUtc)
                && (packedCellKinds == null || packedCellKinds.Length == 0);
        }

        if (hasData != other.hasData
            || !Mathf.Approximately(cellSize, other.cellSize)
            || origin != other.origin
            || width != other.width
            || height != other.height
            || worldMin != other.worldMin
            || worldSize != other.worldSize
            || !string.Equals(sourceSummary, other.sourceSummary, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] selfCells = packedCellKinds ?? Array.Empty<byte>();
        byte[] otherCells = other.packedCellKinds ?? Array.Empty<byte>();
        if (selfCells.Length != otherCells.Length)
        {
            return false;
        }

        for (int index = 0; index < selfCells.Length; index++)
        {
            if (selfCells[index] != otherCells[index])
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// `PlacementStaticMask` 是放置系统里的静态地图缓存。
///
/// 它的职责始终只有一件事：
/// 把“地面是否可建 / 是否是静态禁建区”这类场景语义，
/// 转换成能被运行时快速查询的细网格。
///
/// 与第一阶段相比，这里现在支持两种来源：
/// 1. 优先读取编辑器里已经 Bake 好的数据
/// 2. 缺失时退回第一阶段的运行时现算
///
/// 这样做的好处是：
/// - 不破坏当前作者化流程
/// - 又能把第一次进关时的静态遮罩构建成本继续压下去
/// </summary>
public sealed class PlacementStaticMask
{
    /// <summary>
    /// 平滑覆盖层阶段把静态遮罩推进到“比建筑吸附网格更细两档”的分辨率。
    ///
    /// 这份数据仍然是运行时放置判定和覆盖层显示的共同来源；
    /// 提升 Bake 精度后，Marching Squares 轮廓就能沿着更细的缓存边界走，
    /// 而不需要重新引入逐像素物理查询。
    /// </summary>
    private const float RasterResolutionScale = 4f;
    private const float MinimumRasterCellSize = 0.04f;

    private enum RasterCellKind : byte
    {
        Buildable = 0,
        OutsideBuildZone = 1,
        PlacementBlocker = 2
    }

    private const int OverlapBufferCapacity = 16;

    private readonly float _cellSize;
    private readonly Vector2 _origin;
    private readonly int _width;
    private readonly int _height;
    private readonly Bounds _worldBounds;
    private readonly RasterCellKind[] _cellKinds;
    private readonly int[] _blockedPrefixSums;

    private PlacementStaticMask(
        float cellSize,
        Vector2 origin,
        int width,
        int height,
        Bounds worldBounds,
        RasterCellKind[] cellKinds,
        int[] blockedPrefixSums)
    {
        _cellSize = cellSize;
        _origin = origin;
        _width = width;
        _height = height;
        _worldBounds = worldBounds;
        _cellKinds = cellKinds;
        _blockedPrefixSums = blockedPrefixSums;
    }

    public float CellSize => _cellSize;
    public Bounds WorldBounds => _worldBounds;

    /// <summary>
    /// 构造运行时静态遮罩。
    ///
    /// 第二阶段开始，这里会先尝试从 `PlacementGrid` 上读取编辑器 Bake 结果；
    /// 只有缺失时，才退回到第一阶段那套“进入 Play 后现算一遍”的路径。
    /// </summary>
    public static PlacementStaticMask Build(
        BuildZone buildZone,
        PlacementGrid placementGrid,
        Action<string> logDiagnostic = null)
    {
        if (placementGrid != null
            && placementGrid.TryCreatePlacementStaticMaskFromBake(logDiagnostic, out PlacementStaticMask bakedMask))
        {
            logDiagnostic?.Invoke($"PlacementStaticMask loaded scene bake: {placementGrid.GetPlacementStaticMaskBakeSummary()}");
            return bakedMask;
        }

        PlacementStaticMaskBakeData runtimeBakeData = BuildBakeData(buildZone, placementGrid, logDiagnostic);
        PlacementStaticMask fallbackMask = CreateFromBakeData(runtimeBakeData, logDiagnostic);
        if (fallbackMask != null)
        {
            logDiagnostic?.Invoke("PlacementStaticMask fallback: no baked scene data was available, rebuilt from scene geometry at runtime.");
        }

        return fallbackMask;
    }

    /// <summary>
    /// 供编辑器工具调用的 Bake 入口。
    ///
    /// 它会沿用运行时完全相同的栅格化语义，
    /// 只是把结果保存在可序列化数据里，而不是直接做成运行时实例。
    /// </summary>
    public static PlacementStaticMaskBakeData BuildBakeData(
        BuildZone buildZone,
        PlacementGrid placementGrid,
        Action<string> logDiagnostic = null)
    {
        if (buildZone == null || placementGrid == null)
        {
            return null;
        }

        Bounds buildableBounds = buildZone.WorldBounds;
        if (buildableBounds.size.x <= Mathf.Epsilon || buildableBounds.size.y <= Mathf.Epsilon)
        {
            logDiagnostic?.Invoke("PlacementStaticMask bake skipped: BuildZone bounds were empty.");
            return null;
        }

        float rasterCellSize = Mathf.Max(MinimumRasterCellSize, placementGrid.CellSize / RasterResolutionScale);
        Vector2 gridOrigin = placementGrid.Origin;

        int minRasterX = Mathf.FloorToInt((buildableBounds.min.x - gridOrigin.x) / rasterCellSize);
        int minRasterY = Mathf.FloorToInt((buildableBounds.min.y - gridOrigin.y) / rasterCellSize);
        Vector2 rasterOrigin = gridOrigin + new Vector2(minRasterX * rasterCellSize, minRasterY * rasterCellSize);

        int width = Mathf.Max(1, Mathf.CeilToInt((buildableBounds.max.x - rasterOrigin.x) / rasterCellSize));
        int height = Mathf.Max(1, Mathf.CeilToInt((buildableBounds.max.y - rasterOrigin.y) / rasterCellSize));

        RasterCellKind[] cellKinds = new RasterCellKind[width * height];
        HashSet<Collider2D> blockerSet = BuildBlockerHashSet(
            out int placementBlockerCount,
            out int pathSurfaceCount);
        Collider2D[] overlapBuffer = new Collider2D[OverlapBufferCapacity];
        Vector2 cellSize = new Vector2(rasterCellSize * 0.95f, rasterCellSize * 0.95f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                Vector2 cellCenter = rasterOrigin + new Vector2((x + 0.5f) * rasterCellSize, (y + 0.5f) * rasterCellSize);

                if (!buildZone.ContainsPoint(cellCenter))
                {
                    cellKinds[index] = RasterCellKind.OutsideBuildZone;
                    continue;
                }

                // 用 Physics2D.OverlapBoxNonAlloc 做整格区域碰撞检测，
                // 彻底杜绝薄形路径 Collider 穿过栅格但不到中心点的问题。
                // 此处仅编辑器 Bake 时执行，不影响运行时性能。
                int hitCount = Physics2D.OverlapBoxNonAlloc(cellCenter, cellSize, 0f, overlapBuffer);
                bool blocked = false;
                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D hit = overlapBuffer[i];
                    if (hit != null && blockerSet.Contains(hit))
                    {
                        blocked = true;
                        break;
                    }
                }

                cellKinds[index] = blocked
                    ? RasterCellKind.PlacementBlocker
                    : RasterCellKind.Buildable;
            }
        }

        Bounds worldBounds = new Bounds();
        worldBounds.SetMinMax(
            new Vector3(rasterOrigin.x, rasterOrigin.y, 0f),
            new Vector3(rasterOrigin.x + (width * rasterCellSize), rasterOrigin.y + (height * rasterCellSize), 0f));

        PlacementStaticMaskBakeData bakeData = new PlacementStaticMaskBakeData();
        bakeData.Write(
            rasterCellSize,
            rasterOrigin,
            width,
            height,
            worldBounds,
            PackCellKinds(cellKinds),
            BuildBakeSourceSummary(buildZone, placementBlockerCount, pathSurfaceCount));

        logDiagnostic?.Invoke(
            $"PlacementStaticMask baked: rasterCell={rasterCellSize:0.###} scale={RasterResolutionScale:0.#}x size={width}x{height} world={worldBounds.size.x:0.##}x{worldBounds.size.y:0.##}");

        return bakeData;
    }

    /// <summary>
    /// 这是覆盖层采样最需要的查询：
    /// “这个矩形区域里是否完全没有静态阻挡？”
    ///
    /// 为了让覆盖层在大面积采样时保持便宜，
    /// 这里通过前缀和把查询压成近似 `O(1)`。
    /// </summary>
    public bool IsWorldRectFullyBuildable(Bounds worldBounds)
    {
        if (!TryResolveRasterRect(worldBounds, out int minX, out int minY, out int maxXExclusive, out int maxYExclusive, out bool touchesOutside))
        {
            return false;
        }

        if (touchesOutside)
        {
            return false;
        }

        return GetBlockedCellCount(minX, minY, maxXExclusive, maxYExclusive) == 0;
    }

    /// <summary>
    /// 规则层需要的不只是“能不能放”，还需要一个可以回退到具体提示语的阻挡采样点。
    ///
    /// 这里先快速查前缀和确认“矩形里有没有阻挡”，
    /// 只有真的有时，才在那个小矩形里找第一个阻挡格。
    /// 这样仍然比逐格做物理查询便宜很多。
    /// </summary>
    public bool TryGetFirstBlockingSample(
        Bounds worldBounds,
        out Vector3 blockingSample,
        out PlacementStaticMaskBlockReason blockingReason)
    {
        blockingSample = worldBounds.center;
        blockingReason = PlacementStaticMaskBlockReason.OutsideBuildZone;

        if (!TryResolveRasterRect(worldBounds, out int minX, out int minY, out int maxXExclusive, out int maxYExclusive, out bool touchesOutside))
        {
            return true;
        }

        if (touchesOutside)
        {
            blockingSample = worldBounds.center;
            blockingReason = PlacementStaticMaskBlockReason.OutsideBuildZone;
            return true;
        }

        if (GetBlockedCellCount(minX, minY, maxXExclusive, maxYExclusive) <= 0)
        {
            return false;
        }

        for (int y = minY; y < maxYExclusive; y++)
        {
            for (int x = minX; x < maxXExclusive; x++)
            {
                RasterCellKind cellKind = _cellKinds[(y * _width) + x];
                if (cellKind == RasterCellKind.Buildable)
                {
                    continue;
                }

                blockingSample = new Vector3(
                    _origin.x + ((x + 0.5f) * _cellSize),
                    _origin.y + ((y + 0.5f) * _cellSize),
                    0f);
                blockingReason = cellKind == RasterCellKind.PlacementBlocker
                    ? PlacementStaticMaskBlockReason.PlacementBlocker
                    : PlacementStaticMaskBlockReason.OutsideBuildZone;
                return true;
            }
        }

        return false;
    }

    internal static PlacementStaticMask CreateFromBakeData(
        PlacementStaticMaskBakeData bakeData,
        Action<string> logDiagnostic = null)
    {
        if (bakeData == null || !bakeData.HasData)
        {
            return null;
        }

        if (!BakeDataIncludesPathSurfaceBlockers(bakeData))
        {
            logDiagnostic?.Invoke("PlacementStaticMask ignored stale bake data: path surface blockers were not included. Please rebake this scene.");
            return null;
        }

        if (!TryUnpackCellKinds(bakeData.PackedCellKinds, bakeData.Width, bakeData.Height, out RasterCellKind[] unpackedKinds))
        {
            logDiagnostic?.Invoke("PlacementStaticMask failed to load bake data: packed raster cell data was invalid.");
            return null;
        }

        int[] blockedPrefixSums = BuildBlockedPrefixSums(unpackedKinds, bakeData.Width, bakeData.Height);
        return new PlacementStaticMask(
            bakeData.CellSize,
            bakeData.Origin,
            bakeData.Width,
            bakeData.Height,
            bakeData.WorldBounds,
            unpackedKinds,
            blockedPrefixSums);
    }

    private bool TryResolveRasterRect(
        Bounds worldBounds,
        out int minX,
        out int minY,
        out int maxXExclusive,
        out int maxYExclusive,
        out bool touchesOutside)
    {
        minX = Mathf.FloorToInt((worldBounds.min.x - _origin.x) / _cellSize);
        minY = Mathf.FloorToInt((worldBounds.min.y - _origin.y) / _cellSize);
        maxXExclusive = Mathf.CeilToInt((worldBounds.max.x - _origin.x) / _cellSize);
        maxYExclusive = Mathf.CeilToInt((worldBounds.max.y - _origin.y) / _cellSize);

        touchesOutside = minX < 0 || minY < 0 || maxXExclusive > _width || maxYExclusive > _height;

        if (maxXExclusive <= 0 || maxYExclusive <= 0 || minX >= _width || minY >= _height)
        {
            return false;
        }

        minX = Mathf.Clamp(minX, 0, _width);
        minY = Mathf.Clamp(minY, 0, _height);
        maxXExclusive = Mathf.Clamp(maxXExclusive, 0, _width);
        maxYExclusive = Mathf.Clamp(maxYExclusive, 0, _height);

        return maxXExclusive > minX && maxYExclusive > minY;
    }

    private int GetBlockedCellCount(int minX, int minY, int maxXExclusive, int maxYExclusive)
    {
        int stride = _width + 1;
        int topLeft = (maxYExclusive * stride) + maxXExclusive;
        int topRight = (minY * stride) + maxXExclusive;
        int bottomLeft = (maxYExclusive * stride) + minX;
        int bottomRight = (minY * stride) + minX;

        return _blockedPrefixSums[topLeft]
            - _blockedPrefixSums[topRight]
            - _blockedPrefixSums[bottomLeft]
            + _blockedPrefixSums[bottomRight];
    }

    private static int[] BuildBlockedPrefixSums(RasterCellKind[] cellKinds, int width, int height)
    {
        int[] prefix = new int[(width + 1) * (height + 1)];
        int stride = width + 1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int currentIndex = ((y + 1) * stride) + (x + 1);
                int blockedValue = cellKinds[(y * width) + x] == RasterCellKind.Buildable ? 0 : 1;

                prefix[currentIndex] =
                    blockedValue
                    + prefix[currentIndex - 1]
                    + prefix[currentIndex - stride]
                    - prefix[currentIndex - stride - 1];
            }
        }

        return prefix;
    }

    /// <summary>
    /// 收集场景中所有禁建区 Collider，返回 HashSet 供 Editor Bake 时
    /// 配合 Physics2D.OverlapBoxNonAlloc 做 O(1) 查表判定。
    /// </summary>
    private static HashSet<Collider2D> BuildBlockerHashSet(
        out int placementBlockerCount,
        out int pathSurfaceCount)
    {
        HashSet<Collider2D> set = new HashSet<Collider2D>();
        placementBlockerCount = 0;
        pathSurfaceCount = 0;

        PlacementBlocker[] blockers = UnityEngine.Object.FindObjectsByType<PlacementBlocker>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int index = 0; index < blockers.Length; index++)
        {
            PlacementBlocker blocker = blockers[index];
            if (blocker == null || !blocker.isActiveAndEnabled)
            {
                continue;
            }

            Collider2D blockerCollider = blocker.GetComponent<Collider2D>();
            if (blockerCollider == null || !blockerCollider.enabled)
            {
                continue;
            }

            if (set.Add(blockerCollider))
            {
                placementBlockerCount++;
            }
        }

        Collider2D[] sceneColliders = UnityEngine.Object.FindObjectsByType<Collider2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int index = 0; index < sceneColliders.Length; index++)
        {
            Collider2D collider = sceneColliders[index];
            if (!IsPathSurfaceCollider(collider))
            {
                continue;
            }

            if (set.Add(collider))
            {
                pathSurfaceCount++;
            }
        }

        return set;
    }

    private static bool IsPathSurfaceCollider(Collider2D collider)
    {
        return collider != null
            && collider.enabled
            && collider.gameObject != null
            && collider.gameObject.activeInHierarchy
            && collider.gameObject.name.StartsWith("PathSegment_", StringComparison.Ordinal);
    }

    /// <summary>
    /// O(1) 查询世界坐标是否落在可建造栅格内。
    ///
    /// 覆盖层采样时每次拖拽都要调用数千次，必须是极轻量的数组查表。
    /// 边界外的点也统一返回 false，避免额外判断分支。
    /// </summary>
    public bool IsWorldPointBuildable(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - _origin.x) / _cellSize);
        int y = Mathf.FloorToInt((worldPosition.y - _origin.y) / _cellSize);
        if (x < 0 || x >= _width || y < 0 || y >= _height)
        {
            return false;
        }

        return _cellKinds[(y * _width) + x] == RasterCellKind.Buildable;
    }

    private static byte[] PackCellKinds(RasterCellKind[] cellKinds)
    {
        if (cellKinds == null || cellKinds.Length == 0)
        {
            return Array.Empty<byte>();
        }

        byte[] packed = new byte[cellKinds.Length];
        for (int index = 0; index < cellKinds.Length; index++)
        {
            packed[index] = (byte)cellKinds[index];
        }

        return packed;
    }

    private static bool TryUnpackCellKinds(byte[] packedCellKinds, int width, int height, out RasterCellKind[] cellKinds)
    {
        int expectedLength = Mathf.Max(1, width) * Mathf.Max(1, height);
        if (packedCellKinds == null || packedCellKinds.Length != expectedLength)
        {
            cellKinds = null;
            return false;
        }

        cellKinds = new RasterCellKind[expectedLength];
        for (int index = 0; index < packedCellKinds.Length; index++)
        {
            byte packedValue = packedCellKinds[index];
            if (packedValue > (byte)RasterCellKind.PlacementBlocker)
            {
                cellKinds = null;
                return false;
            }

            cellKinds[index] = (RasterCellKind)packedValue;
        }

        return true;
    }

    private static bool BakeDataIncludesPathSurfaceBlockers(PlacementStaticMaskBakeData bakeData)
    {
        return bakeData != null
            && bakeData.SourceSummary.IndexOf("PathSurfaces=", StringComparison.Ordinal) >= 0;
    }

    private static string BuildBakeSourceSummary(BuildZone buildZone, int placementBlockerCount, int pathSurfaceCount)
    {
        string sceneName = buildZone != null ? buildZone.gameObject.scene.name : "UnknownScene";
        string zoneSummary = buildZone != null ? buildZone.BuildAuthoringSummary() : "NoBuildZone";
        return $"Scene={sceneName} | {zoneSummary} | Blockers={Mathf.Max(0, placementBlockerCount)} | PathSurfaces={Mathf.Max(0, pathSurfaceCount)} | RasterScale={RasterResolutionScale:0.#}x";
    }
}

public enum PlacementStaticMaskBlockReason
{
    OutsideBuildZone = 0,
    PlacementBlocker = 1
}
