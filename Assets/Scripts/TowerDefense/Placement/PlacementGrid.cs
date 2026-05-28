using System;
using UnityEngine;

/// <summary>
/// `PlacementGrid` 是塔防自由放置系统里的“格子坐标尺”。
///
/// 它不直接决定哪里能建，而是统一回答三个基础问题：
/// 1. 世界坐标属于哪一个格子。
/// 2. 某个建筑中心应该吸附到哪个格子中心。
/// 3. 建筑占地与周边禁建范围应该覆盖哪些格子。
///
/// 这样做的好处是：后续如果你想把地图格子改成更粗或更细，
/// 只需要调这个组件的 `Cell Size` 和 `Origin`，不用去散改放置规则代码。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlacementGrid : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float cellSize = 0.5f;
    [SerializeField] private Vector2 origin;
    [SerializeField] private bool snapPlacementToCellCenter = true;

    [Header("Default Footprints")]
    [SerializeField] private Vector2Int relayFootprintCells = new Vector2Int(2, 2);
    [SerializeField] private Vector2Int defenseFootprintCells = new Vector2Int(2, 2);

    [Header("Static Mask Bake")]
    [SerializeField] private PlacementStaticMaskBakeData staticMaskBakeData = new PlacementStaticMaskBakeData();

    [Header("Gizmo")]
    [SerializeField] private bool drawGridWhenSelected = true;
    [SerializeField] private Vector2 gizmoSize = new Vector2(18f, 10f);
    [SerializeField] private Color gridLineColor = new Color(0.45f, 0.95f, 1f, 0.18f);
    [SerializeField] private Color originColor = new Color(1f, 0.85f, 0.28f, 0.75f);
    [SerializeField] private Color footprintGizmoColor = new Color(0.45f, 1f, 0.72f, 0.9f);
    [SerializeField] private Color noBuildGizmoColor = new Color(1f, 0.38f, 0.28f, 0.9f);

    public float CellSize => Mathf.Max(0.05f, cellSize);
    public Vector2 Origin => origin;
    public bool SnapPlacementToCellCenter => snapPlacementToCellCenter;
    public bool HasBakedPlacementStaticMask => staticMaskBakeData != null && staticMaskBakeData.HasData;

    /// <summary>
    /// 仅供运行时兜底网格使用的初始化入口。
    ///
    /// 如果场景里已经作者化放好了 `PlacementGrid`，启动装配不会调用这个方法；
    /// 那时以 Scene 里的组件参数为准，避免运行时覆盖你在 Scene 视图里调好的表现。
    /// </summary>
    public void ApplyRuntimeFallbackSettings(
        float fallbackCellSize,
        Vector2 fallbackOrigin,
        bool fallbackSnapPlacementToCellCenter,
        Vector2Int fallbackRelayFootprintCells,
        Vector2Int fallbackDefenseFootprintCells)
    {
        cellSize = Mathf.Max(0.05f, fallbackCellSize);
        origin = fallbackOrigin;
        snapPlacementToCellCenter = fallbackSnapPlacementToCellCenter;
        relayFootprintCells = ClampCellSize(fallbackRelayFootprintCells);
        defenseFootprintCells = ClampCellSize(fallbackDefenseFootprintCells);
        OnValidate();
    }

    private void OnValidate()
    {
        cellSize = Mathf.Max(0.05f, cellSize);
        relayFootprintCells = ClampCellSize(relayFootprintCells);
        defenseFootprintCells = ClampCellSize(defenseFootprintCells);
        gizmoSize.x = Mathf.Max(CellSize, gizmoSize.x);
        gizmoSize.y = Mathf.Max(CellSize, gizmoSize.y);

        if (staticMaskBakeData == null)
        {
            staticMaskBakeData = new PlacementStaticMaskBakeData();
        }
    }

    /// <summary>
    /// 供运行时静态遮罩构建入口查询：
    /// 当前场景里是否已经保存了可直接加载的 Bake 结果。
    ///
    /// 这里刻意把“能不能读 Bake”挂在 `PlacementGrid` 上，
    /// 是因为这份数据本质上描述的就是“这张地图这套格子语义下的静态可建区”。
    /// </summary>
    public bool TryCreatePlacementStaticMaskFromBake(Action<string> logDiagnostic, out PlacementStaticMask mask)
    {
        if (staticMaskBakeData == null || !staticMaskBakeData.HasData)
        {
            mask = null;
            return false;
        }

        mask = PlacementStaticMask.CreateFromBakeData(staticMaskBakeData, logDiagnostic);
        return mask != null;
    }

    /// <summary>
    /// 编辑器工具把新 Bake 出来的数据写回到场景时，会走这个入口。
    ///
    /// 返回值代表“序列化内容是否真的变化了”，
    /// 这样工具层才能决定是否需要把场景标脏并保存。
    /// </summary>
    public bool ApplyPlacementStaticMaskBakeData(PlacementStaticMaskBakeData bakeData)
    {
        if (staticMaskBakeData == null)
        {
            staticMaskBakeData = new PlacementStaticMaskBakeData();
        }

        return staticMaskBakeData.OverwriteFrom(bakeData);
    }

    /// <summary>
    /// 清除当前场景里已经保存的静态遮罩 Bake 数据。
    /// 这个入口目前主要给编辑器工具和排查流程使用。
    /// </summary>
    public bool ClearPlacementStaticMaskBakeData()
    {
        if (staticMaskBakeData == null)
        {
            staticMaskBakeData = new PlacementStaticMaskBakeData();
            return false;
        }

        return staticMaskBakeData.Clear();
    }

    /// <summary>
    /// 给作者工具和日志输出一个简短摘要，
    /// 让你不用展开大数组也能知道当前场景里到底有没有 Bake、Bake 的尺度是什么。
    /// </summary>
    public string GetPlacementStaticMaskBakeSummary()
    {
        return staticMaskBakeData != null
            ? staticMaskBakeData.BuildSummary()
            : "未初始化静态遮罩 Bake 容器。";
    }

    /// <summary>
    /// 把鼠标世界坐标吸附到格子中心。
    /// 第一版先使用格子中心作为建筑锚点，这最接近《魔兽争霸 3》那种“格子落点”的手感。
    /// </summary>
    public Vector3 SnapWorldPosition(Vector3 worldPosition)
    {
        if (!snapPlacementToCellCenter)
        {
            return worldPosition;
        }

        Vector2Int cell = WorldToCell(worldPosition);
        Vector2 center = CellToWorldCenter(cell);
        return new Vector3(center.x, center.y, worldPosition.z);
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector2 local = (Vector2)worldPosition - origin;
        return new Vector2Int(
            Mathf.FloorToInt(local.x / CellSize),
            Mathf.FloorToInt(local.y / CellSize));
    }

    public Vector2 CellToWorldCenter(Vector2Int cell)
    {
        return origin + new Vector2((cell.x + 0.5f) * CellSize, (cell.y + 0.5f) * CellSize);
    }

    public Vector2Int GetFootprintSize(TowerType towerType)
    {
        return TowerTypeUtility.IsRelay(towerType)
            ? ClampCellSize(relayFootprintCells)
            : ClampCellSize(defenseFootprintCells);
    }

    /// <summary>
    /// 计算建筑本体占用格。
    /// 偶数尺寸时会天然围绕鼠标所在格子偏向右上半格，这是方格系统里最稳定、最容易调试的锚点规则。
    /// </summary>
    public BoundsInt GetFootprintCells(Vector3 worldPosition, TowerType towerType)
    {
        return CreateCenteredCellBounds(WorldToCell(SnapWorldPosition(worldPosition)), GetFootprintSize(towerType));
    }

    /// <summary>
    /// 计算“建筑占地 + 周围禁建边距”的完整正方形/矩形格子范围。
    /// 这个范围用于阻挡后续建筑，不会改变建筑自身真实位置。
    /// </summary>
    public BoundsInt GetNoBuildCells(Vector3 worldPosition, TowerType towerType, float noBuildSquareSize)
    {
        BoundsInt footprint = GetFootprintCells(worldPosition, towerType);
        BoundsInt square = GetSquareCells(worldPosition, Mathf.Max(noBuildSquareSize, CellSize));
        return Encapsulate(footprint, square);
    }

    public BoundsInt GetNoBuildCells(Vector3 worldPosition, TowerType towerType)
    {
        Vector2Int footprintSize = GetFootprintSize(towerType);
        float fallbackSquareSize = Mathf.Max(footprintSize.x, footprintSize.y) * CellSize;
        return GetNoBuildCells(worldPosition, towerType, fallbackSquareSize);
    }

    public Bounds GetWorldBounds(BoundsInt cellBounds)
    {
        Vector2 min = origin + new Vector2(cellBounds.xMin * CellSize, cellBounds.yMin * CellSize);
        Vector2 max = origin + new Vector2(cellBounds.xMax * CellSize, cellBounds.yMax * CellSize);
        Bounds bounds = new Bounds();
        bounds.SetMinMax(
            new Vector3(min.x, min.y, 0f),
            new Vector3(max.x, max.y, 0f));
        return bounds;
    }

    public static BoundsInt Expand(BoundsInt source, int padding)
    {
        padding = Mathf.Max(0, padding);
        return new BoundsInt(
            source.xMin - padding,
            source.yMin - padding,
            0,
            source.size.x + (padding * 2),
            source.size.y + (padding * 2),
            1);
    }

    public static bool Overlaps(BoundsInt a, BoundsInt b)
    {
        return a.xMin < b.xMax
            && a.xMax > b.xMin
            && a.yMin < b.yMax
            && a.yMax > b.yMin;
    }

    private BoundsInt GetSquareCells(Vector3 worldPosition, float squareSize)
    {
        Vector3 snappedWorldPosition = SnapWorldPosition(worldPosition);
        float halfSize = Mathf.Max(CellSize, squareSize) * 0.5f;
        Vector2 min = (Vector2)snappedWorldPosition - new Vector2(halfSize, halfSize);
        Vector2 max = (Vector2)snappedWorldPosition + new Vector2(halfSize, halfSize);

        int minX = Mathf.FloorToInt((min.x - origin.x) / CellSize);
        int minY = Mathf.FloorToInt((min.y - origin.y) / CellSize);
        int maxX = Mathf.CeilToInt((max.x - origin.x) / CellSize);
        int maxY = Mathf.CeilToInt((max.y - origin.y) / CellSize);

        return new BoundsInt(minX, minY, 0, Mathf.Max(1, maxX - minX), Mathf.Max(1, maxY - minY), 1);
    }

    private static BoundsInt Encapsulate(BoundsInt a, BoundsInt b)
    {
        int minX = Mathf.Min(a.xMin, b.xMin);
        int minY = Mathf.Min(a.yMin, b.yMin);
        int maxX = Mathf.Max(a.xMax, b.xMax);
        int maxY = Mathf.Max(a.yMax, b.yMax);
        return new BoundsInt(minX, minY, 0, Mathf.Max(1, maxX - minX), Mathf.Max(1, maxY - minY), 1);
    }

    private static BoundsInt CreateCenteredCellBounds(Vector2Int centerCell, Vector2Int size)
    {
        size = ClampCellSize(size);
        int minX = centerCell.x - (size.x / 2);
        int minY = centerCell.y - (size.y / 2);
        return new BoundsInt(minX, minY, 0, size.x, size.y, 1);
    }

    private static Vector2Int ClampCellSize(Vector2Int value)
    {
        return new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGridWhenSelected)
        {
            return;
        }

        DrawGridGizmo();
    }

    /// <summary>
    /// 供塔和继电器在 Scene 视图里绘制自己的格子占地。
    /// 这里把绘制逻辑放回网格组件，是为了保证“显示出来的格子”和“规则使用的格子”一定同源。
    /// </summary>
    public void DrawStructureGizmo(Vector3 worldPosition, TowerType towerType)
    {
        Vector2Int footprintSize = GetFootprintSize(towerType);
        float fallbackSquareSize = Mathf.Max(footprintSize.x, footprintSize.y) * CellSize;
        DrawStructureGizmo(worldPosition, towerType, fallbackSquareSize);
    }

    public void DrawStructureGizmo(Vector3 worldPosition, TowerType towerType, float noBuildSquareSize)
    {
        Bounds footprintWorldBounds = GetWorldBounds(GetFootprintCells(worldPosition, towerType));
        Bounds noBuildWorldBounds = GetWorldBounds(GetNoBuildCells(worldPosition, towerType, noBuildSquareSize));

        Gizmos.color = noBuildGizmoColor;
        Gizmos.DrawWireCube(noBuildWorldBounds.center, noBuildWorldBounds.size);

        Gizmos.color = footprintGizmoColor;
        Gizmos.DrawWireCube(footprintWorldBounds.center, footprintWorldBounds.size);
    }

    private void DrawGridGizmo()
    {
        float size = CellSize;
        Vector2 half = gizmoSize * 0.5f;
        float minX = origin.x - half.x;
        float maxX = origin.x + half.x;
        float minY = origin.y - half.y;
        float maxY = origin.y + half.y;

        int startX = Mathf.FloorToInt((minX - origin.x) / size);
        int endX = Mathf.CeilToInt((maxX - origin.x) / size);
        int startY = Mathf.FloorToInt((minY - origin.y) / size);
        int endY = Mathf.CeilToInt((maxY - origin.y) / size);

        Gizmos.color = gridLineColor;
        for (int x = startX; x <= endX; x++)
        {
            float worldX = origin.x + (x * size);
            Gizmos.DrawLine(new Vector3(worldX, minY, 0f), new Vector3(worldX, maxY, 0f));
        }

        for (int y = startY; y <= endY; y++)
        {
            float worldY = origin.y + (y * size);
            Gizmos.DrawLine(new Vector3(minX, worldY, 0f), new Vector3(maxX, worldY, 0f));
        }

        Gizmos.color = originColor;
        Gizmos.DrawWireCube(origin, Vector3.one * (size * 0.35f));
    }
}
