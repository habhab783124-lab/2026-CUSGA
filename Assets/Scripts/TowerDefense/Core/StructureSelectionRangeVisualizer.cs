using UnityEngine;

/// <summary>
/// `StructureSelectionRangeVisualizer` 只负责一件事：
/// 当玩家点击场上已经放下的继电器或战斗塔时，
/// 在世界里把“这座建筑真正影响到的范围”画出来。
///
/// 这里刻意把它做成一个很薄的运行时辅助类，而不是塞回 `TowerDefenseGame`：
/// - 总控负责“现在选中了谁”
/// - 这个类负责“如果已经选中了，就该画出什么范围”
///
/// 这样职责边界更清楚，后续如果你想改颜色、线宽、排序层级，
/// 也不需要再回总控脚本里翻整段选中逻辑。
/// </summary>
public sealed class StructureSelectionRangeVisualizer
{
    /// <summary>
    /// 圆形攻击范围的分段数。
    ///
    /// 这里不需要无限逼真；
    /// 重点是让玩家能稳定读出“这座塔大概打到哪里”。
    /// 48 段在当前 2D 原型里已经足够顺滑，同时开销也很轻。
    /// </summary>
    private const int CircleSegmentCount = 48;

    /// <summary>
    /// 范围线整体排序层级。
    ///
    /// 这里刻意比放置覆盖层、起手区标记和预览环更高一点，
    /// 避免玩家已经点中了建筑，但范围线又被别的教学层遮住，看起来像“没生效”。
    /// </summary>
    private const int RangeSortingOrder = 400;

    /// <summary>
    /// 线宽保持轻一点，避免喧宾夺主。
    /// 这层的目标是“辅助读数”，不是新的主视觉特效。
    /// </summary>
    private const float RangeLineWidth = 0.08f;

    private readonly System.Func<TowerType, Color> _resolveRangeColor;
    private readonly Vector3[] _circlePoints = new Vector3[CircleSegmentCount];

    private Transform _visualRoot;
    private LineRenderer _rangeLineRenderer;

    public StructureSelectionRangeVisualizer(System.Func<TowerType, Color> resolveRangeColor)
    {
        _resolveRangeColor = resolveRangeColor;
    }

    /// <summary>
    /// 绑定当前关卡里用于承载运行时视觉辅助的根节点。
    ///
    /// 这里优先复用现有 `PlacementPreviewRoot`，
    /// 因为它本来就承担“世界空间教学 / 提示型视觉”的职责，
    /// 比另外再造一棵孤立层级更符合当前项目结构。
    /// </summary>
    public void BindRoot(Transform visualRoot)
    {
        _visualRoot = visualRoot;
        if (_rangeLineRenderer != null && _rangeLineRenderer.transform.parent != _visualRoot)
        {
            _rangeLineRenderer.transform.SetParent(_visualRoot, false);
        }
    }

    /// <summary>
    /// 显示继电器的供电范围。
    ///
    /// 注意这里故意画的是“方形供电覆盖区”，
    /// 而不是圆形近似，
    /// 因为当前继电器真实规则就是轴对齐方形，
    /// 可视化必须跟实际规则保持一致。
    /// </summary>
    public void ShowRelayCoverage(RelayTower relayTower)
    {
        if (relayTower == null || _visualRoot == null)
        {
            Hide();
            return;
        }

        EnsureLineRenderer(TowerType.Relay);

        float halfExtent = relayTower.SupplyRange;
        Vector3 center = relayTower.transform.position;
        Vector3[] squarePoints =
        {
            center + new Vector3(-halfExtent, -halfExtent, 0f),
            center + new Vector3(-halfExtent, halfExtent, 0f),
            center + new Vector3(halfExtent, halfExtent, 0f),
            center + new Vector3(halfExtent, -halfExtent, 0f)
        };

        BattlefieldReadabilityVisualUtility.SetPolyline(
            _rangeLineRenderer,
            squarePoints,
            loop: true,
            width: RangeLineWidth,
            color: ResolveRangeColor(TowerType.Relay));
        _rangeLineRenderer.enabled = true;
    }

    /// <summary>
    /// 显示战斗塔的攻击范围。
    ///
    /// 当前三种塔都共享“圆形攻击范围”这个读法，
    /// 所以这里统一按半径画圆，
    /// 由外部把“到底是哪种塔、该用什么颜色”告诉我们即可。
    /// </summary>
    public void ShowDefenseRange(DefenseTower defenseTower)
    {
        if (defenseTower == null || _visualRoot == null)
        {
            Hide();
            return;
        }

        EnsureLineRenderer(defenseTower.BuildType);

        float range = Mathf.Max(0.1f, defenseTower.AttackRange);
        Vector3 center = defenseTower.transform.position;
        for (int index = 0; index < CircleSegmentCount; index++)
        {
            float angle01 = index / (float)CircleSegmentCount;
            float radians = angle01 * Mathf.PI * 2f;
            _circlePoints[index] = center + new Vector3(Mathf.Cos(radians) * range, Mathf.Sin(radians) * range, 0f);
        }

        BattlefieldReadabilityVisualUtility.SetPolyline(
            _rangeLineRenderer,
            _circlePoints,
            loop: true,
            width: RangeLineWidth,
            color: ResolveRangeColor(defenseTower.BuildType));
        _rangeLineRenderer.enabled = true;
    }

    /// <summary>
    /// 当前没有已放置建筑被选中时，范围线应该完全隐藏。
    ///
    /// 这里不销毁对象，只关掉渲染器，
    /// 这样在玩家连续点击不同建筑时，不会反复 new / destroy 线框对象。
    /// </summary>
    public void Hide()
    {
        if (_rangeLineRenderer != null)
        {
            _rangeLineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 场景销毁或总控释放时，把这层运行时线框一起清掉。
    /// </summary>
    public void Dispose()
    {
        if (_rangeLineRenderer != null)
        {
            Object.Destroy(_rangeLineRenderer.gameObject);
            _rangeLineRenderer = null;
        }
    }

    private void EnsureLineRenderer(TowerType towerType)
    {
        if (_visualRoot == null)
        {
            return;
        }

        if (_rangeLineRenderer == null)
        {
            _rangeLineRenderer = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
                _visualRoot,
                childName: "SelectedStructureRange",
                sortingOrder: RangeSortingOrder,
                width: RangeLineWidth,
                color: ResolveRangeColor(towerType),
                loop: true,
                useWorldSpace: true);
        }
        else
        {
            Color color = ResolveRangeColor(towerType);
            _rangeLineRenderer.startColor = color;
            _rangeLineRenderer.endColor = color;
            _rangeLineRenderer.widthMultiplier = RangeLineWidth;
            _rangeLineRenderer.startWidth = RangeLineWidth;
            _rangeLineRenderer.endWidth = RangeLineWidth;
            _rangeLineRenderer.sortingOrder = RangeSortingOrder;
        }
    }

    private Color ResolveRangeColor(TowerType towerType)
    {
        Color baseColor = _resolveRangeColor != null ? _resolveRangeColor(towerType) : Color.white;
        baseColor.a = 0.92f;
        return baseColor;
    }
}
