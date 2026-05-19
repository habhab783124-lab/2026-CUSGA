using System;
using UnityEngine;

/// <summary>
/// `TowerPlacementVisualController` 负责“自由放置”这一整条链路里的**纯视觉反馈**。
///
/// 这里专门强调“纯视觉”，是为了给后续维护者一个很清晰的边界：
/// 1. 它可以决定预览塔长什么样、放在哪里、什么时候显示或隐藏。
/// 2. 它可以决定精确合法区覆盖层和首塔起手区标记什么时候刷新。
/// 3. 它**不能**改变放置规则本身，也不直接做 BuildZone / Blocker / 塔间距判定。
///
/// 这样拆出来以后，`TowerDefenseGame` 就只负责：
/// - 什么时候开始拖拽
/// - 当前坐标是否合法
/// - 什么时候真正落塔
///
/// 而所有“给玩家看见的反馈”都集中在这里，职责边界会更清楚。
/// </summary>
public sealed class TowerPlacementVisualController : IDisposable
{
    private readonly Sprite _placementRingSprite;
    private readonly string _placementRingResourcePath;
    private readonly Color _validPreviewColor;
    private readonly Color _invalidPreviewColor;
    private readonly Func<TowerType, GameObject> _getPrototype;
    private readonly Func<TowerType, string> _getTowerDisplayName;
    private readonly Func<TowerType, float> _getPlacementRadius;
    private readonly PlacementAreaOverlayRenderer _placementAreaOverlayRenderer;
    private readonly StarterZoneMarkerRenderer _starterZoneMarkerRenderer;

    private Transform _placementPreviewRoot;

    private GameObject _placementPreviewInstance;
    private TowerType _placementPreviewTowerType = TowerType.None;
    private SpriteRenderer _placementPreviewSpriteRenderer;
    private SpriteRenderer _placementPreviewRingRenderer;

    private int _placementAreaOverlayRevision;
    private int _placementAreaOverlayPreparedRevision = -1;
    private TowerType _placementAreaOverlayPreparedTowerType = TowerType.None;

    /// <summary>
    /// 构造时只注入“长期稳定的配置”和“总控提供的查询入口”。
    ///
    /// 这样有两个好处：
    /// 1. 可视化控制器不需要偷偷回头去查场景对象。
    /// 2. 它仍然能复用总控里已经存在的塔目录、原型和规则参数。
    /// </summary>
    public TowerPlacementVisualController(
        Sprite placementRingSprite,
        string placementRingResourcePath,
        Color validPreviewColor,
        Color invalidPreviewColor,
        float placementAreaOverlayPixelsPerUnit,
        Color placementAreaOverlayFillColor,
        Color placementAreaOverlayEdgeColor,
        int placementAreaOverlaySortingOrder,
        Color starterZoneMarkerFillColor,
        Color starterZoneMarkerEdgeColor,
        int starterZoneMarkerSortingOrder,
        Func<TowerType, GameObject> getPrototype,
        Func<TowerType, string> getTowerDisplayName,
        Func<TowerType, float> getPlacementRadius)
    {
        _placementRingSprite = placementRingSprite;
        _placementRingResourcePath = placementRingResourcePath;
        _validPreviewColor = validPreviewColor;
        _invalidPreviewColor = invalidPreviewColor;
        _getPrototype = getPrototype;
        _getTowerDisplayName = getTowerDisplayName;
        _getPlacementRadius = getPlacementRadius;

        _placementAreaOverlayRenderer = new PlacementAreaOverlayRenderer(
            placementAreaOverlayPixelsPerUnit,
            placementAreaOverlayFillColor,
            placementAreaOverlayEdgeColor,
            placementAreaOverlaySortingOrder);

        _starterZoneMarkerRenderer = new StarterZoneMarkerRenderer(
            starterZoneMarkerFillColor,
            starterZoneMarkerEdgeColor,
            starterZoneMarkerSortingOrder);
    }

    /// <summary>
    /// 放置反馈的所有运行时对象都挂到同一个根节点下。
    ///
    /// 这样一来：
    /// - Scene 层级更好读
    /// - 后续如果要统一改 Sorting / 可见性，也只需要管这一棵树
    /// </summary>
    public void BindPlacementPreviewRoot(Transform placementPreviewRoot)
    {
        _placementPreviewRoot = placementPreviewRoot;
    }

    /// <summary>
    /// 外部只要知道“规则变了”，就调用这个方法失效缓存即可。
    ///
    /// 当前最典型的触发点就是：
    /// - 放下一座新塔
    /// - 导致部署网络边界扩张
    /// - 所以旧的精确合法区覆盖层就不再可信
    /// </summary>
    public void InvalidatePlacementAreaOverlayCache()
    {
        _placementAreaOverlayRevision++;
        _placementAreaOverlayPreparedRevision = -1;
        _placementAreaOverlayPreparedTowerType = TowerType.None;
    }

    /// <summary>
    /// 在玩家真正拖拽之前，提前把“精确合法区覆盖层”热好。
    ///
    /// 这样常见路径“鼠标先悬停卡片，再按下开始拖”时，
    /// 起手帧就不会为了整张覆盖层临时重算而卡一下。
    /// </summary>
    public void PrewarmPlacementAreaOverlay(TowerType towerType, bool isGameOver, Bounds overlayBounds, Func<Vector3, bool> validator)
    {
        if (isGameOver || towerType == TowerType.None || validator == null)
        {
            return;
        }

        if (_placementPreviewRoot == null || overlayBounds.size.x <= Mathf.Epsilon || overlayBounds.size.y <= Mathf.Epsilon)
        {
            return;
        }

        if (IsPlacementAreaOverlayPreparedFor(towerType))
        {
            return;
        }

        _placementAreaOverlayRenderer.Show(_placementPreviewRoot, overlayBounds, validator);
        _placementAreaOverlayRenderer.Hide();

        _placementAreaOverlayPreparedRevision = _placementAreaOverlayRevision;
        _placementAreaOverlayPreparedTowerType = towerType;
    }

    /// <summary>
    /// 提前把某个塔型的预览对象准备好，并立刻隐藏起来。
    ///
    /// 这样真正开始拖拽时，大多数情况下就不需要第一次再 `Instantiate`，
    /// 只需要把已经准备好的预览塔挪到鼠标下方重新激活即可。
    ///
    /// 这里故意把预热位置放到远离战场的地方，再立即隐藏，
    /// 是为了保证：
    /// - 不会在地图中央闪一下
    /// - 也不会误参与任何可见反馈
    /// </summary>
    public void PrewarmPlacementPreviewInstance(TowerType towerType)
    {
        if (towerType == TowerType.None || _placementPreviewRoot == null || _getPrototype == null || _getPrototype(towerType) == null)
        {
            return;
        }

        EnsurePlacementPreviewInstance(towerType, new Vector3(10000f, 10000f, 0f));
        DeactivatePlacementPreview();
    }

    /// <summary>
    /// 拖拽刚开始时优先显示预热结果。
    ///
    /// 如果当前没有可复用缓存，就退回实时刷新，保证正确性优先。
    /// </summary>
    public void ShowPreparedPlacementAreaOverlay(TowerType towerType, Bounds overlayBounds, Func<Vector3, bool> validator)
    {
        if (_placementPreviewRoot == null || overlayBounds.size.x <= Mathf.Epsilon || overlayBounds.size.y <= Mathf.Epsilon)
        {
            return;
        }

        if (IsPlacementAreaOverlayPreparedFor(towerType))
        {
            if (_placementAreaOverlayRenderer.ShowPrepared(_placementPreviewRoot, overlayBounds))
            {
                return;
            }
        }

        RefreshPlacementAreaOverlay(towerType, true, overlayBounds, validator);
        _placementAreaOverlayPreparedRevision = _placementAreaOverlayRevision;
        _placementAreaOverlayPreparedTowerType = towerType;
    }

    /// <summary>
    /// 这是拖拽过程中的主刷新入口。
    ///
    /// 注意这里依然只接收外部提供的 `validator`，不直接知道游戏规则细节。
    /// 这样即便以后放置规则继续演化，这个类也仍然只是“把给定规则画出来”。
    /// </summary>
    public void RefreshPlacementAreaOverlay(TowerType towerType, bool isPlacementDragActive, Bounds overlayBounds, Func<Vector3, bool> validator)
    {
        if (!isPlacementDragActive || towerType == TowerType.None || validator == null)
        {
            HidePlacementAreaOverlay();
            return;
        }

        if (_placementPreviewRoot == null || overlayBounds.size.x <= Mathf.Epsilon || overlayBounds.size.y <= Mathf.Epsilon)
        {
            HidePlacementAreaOverlay();
            return;
        }

        _placementAreaOverlayRenderer.Show(_placementPreviewRoot, overlayBounds, validator);
    }

    /// <summary>
    /// 起手区标记只服务于“首塔还没放下”阶段。
    ///
    /// 一旦规则层告诉我们已经不该显示了，就立即收掉。
    /// </summary>
    public void RefreshStarterZoneMarker(bool shouldShow, Bounds starterBounds)
    {
        if (!shouldShow || _placementPreviewRoot == null)
        {
            _starterZoneMarkerRenderer.Hide();
            return;
        }

        _starterZoneMarkerRenderer.Show(_placementPreviewRoot, starterBounds);
    }

    /// <summary>
    /// 确保当前塔型的预览对象存在。
    ///
    /// 这里做了三层处理：
    /// 1. 同塔型复用已有预览对象，避免反复 Instantiate。
    /// 2. 切换塔型时才真正释放旧对象。
    /// 3. 预览对象上的战斗脚本和碰撞体全部关掉，保证它永远只承担视觉职责。
    /// </summary>
    public void EnsurePlacementPreviewInstance(TowerType towerType, Vector3 initialWorldPosition)
    {
        if (_placementPreviewInstance != null && _placementPreviewTowerType == towerType)
        {
            // 同塔型复用时，也先把位置对到这次拖拽真正的起始鼠标世界坐标，
            // 再重新激活对象。
            //
            // 这样可以避免“上一次停在 A 点的隐藏预览塔，
            // 这一次先在旧位置或世界原点闪一帧，再跳到鼠标下面”的视觉抖动。
            _placementPreviewInstance.transform.position = initialWorldPosition;
            _placementPreviewInstance.SetActive(true);
            return;
        }

        ReleasePlacementPreviewInstance();

        if (_placementPreviewRoot == null || _getPrototype == null || _getTowerDisplayName == null || _getPlacementRadius == null)
        {
            return;
        }

        GameObject prototype = _getPrototype(towerType);
        if (prototype == null)
        {
            return;
        }

        // 这里不再把预览塔先生成在世界原点，再等下一步 Update 去挪。
        //
        // 原来的顺序会导致一个典型闪帧：
        // 1. 预览塔先在 (0,0,0) 被实例化并显示
        // 2. 紧接着下一次 `UpdatePlacementDrag()` 才把它搬到鼠标位置
        // 于是玩家第一次拖卡时，会看到地图中央短暂闪过一次预览塔
        //
        // 现在直接用拖拽起始点作为实例化位置，让它“第一次出现就出现在正确位置”。
        _placementPreviewInstance = UnityEngine.Object.Instantiate(prototype, initialWorldPosition, Quaternion.identity, _placementPreviewRoot);
        _placementPreviewTowerType = towerType;
        _placementPreviewInstance.name = $"{_getTowerDisplayName(towerType)}_Preview";
        _placementPreviewInstance.SetActive(true);

        DisablePreviewRuntimeBehaviour();
        CachePreviewRenderers();
        CreatePlacementRing(towerType);
    }

    /// <summary>
    /// 拖拽取消时我们只隐藏预览对象，不立即销毁。
    ///
    /// 这样连续多次拖同一种塔时，不会因为频繁创建 / 销毁而额外制造 GC 抖动。
    /// </summary>
    public void DeactivatePlacementPreview()
    {
        if (_placementPreviewInstance != null)
        {
            _placementPreviewInstance.SetActive(false);
        }
    }

    /// <summary>
    /// 预览对象位置由总控在拖拽时持续喂进来。
    /// </summary>
    public void SetPreviewPosition(Vector3 previewWorldPosition)
    {
        if (_placementPreviewInstance != null)
        {
            _placementPreviewInstance.transform.position = previewWorldPosition;
        }
    }

    /// <summary>
    /// 放置规则层偶尔需要跳过“预览塔自己”的碰撞。
    ///
    /// 这个查询入口专门暴露给总控做校验过滤，避免它再次直接持有预览对象引用。
    /// </summary>
    public bool ContainsPreviewTransform(Transform candidate)
    {
        return _placementPreviewInstance != null
            && candidate != null
            && candidate.IsChildOf(_placementPreviewInstance.transform);
    }

    /// <summary>
    /// 合法 / 非法颜色反馈只在这里收口。
    ///
    /// 这样以后如果想统一换一套拖拽配色，不用再回 `TowerDefenseGame` 里找散落逻辑。
    /// </summary>
    public void UpdatePlacementPreviewVisual(bool isValid)
    {
        if (_placementPreviewSpriteRenderer == null)
        {
            return;
        }

        Color previewColor = isValid ? _validPreviewColor : _invalidPreviewColor;
        _placementPreviewSpriteRenderer.color = previewColor;

        if (_placementPreviewRingRenderer != null)
        {
            Color ringColor = previewColor;
            ringColor.a = isValid ? 0.9f : 0.82f;
            _placementPreviewRingRenderer.color = ringColor;
        }
    }

    /// <summary>
    /// 统一隐藏覆盖层，避免外部直接碰内部 renderer。
    /// </summary>
    public void HidePlacementAreaOverlay()
    {
        _placementAreaOverlayRenderer.Hide();
    }

    /// <summary>
    /// 总控销毁时，这里负责把所有运行时视觉对象一起收掉。
    /// </summary>
    public void Dispose()
    {
        ReleasePlacementPreviewInstance();
        _placementAreaOverlayRenderer.Dispose();
        _starterZoneMarkerRenderer.Dispose();
    }

    private bool IsPlacementAreaOverlayPreparedFor(TowerType towerType)
    {
        return _placementAreaOverlayPreparedRevision == _placementAreaOverlayRevision
            && _placementAreaOverlayPreparedTowerType == towerType;
    }

    /// <summary>
    /// 预览对象必须彻底失去“真正参与玩法”的能力。
    ///
    /// 如果这里忘记关掉某个脚本或碰撞体，就会出现：
    /// - 预览塔误伤敌人
    /// - 预览塔挡住正式落塔
    /// - 首塔阶段被预览对象自己误判成“离别的结构太近”
    /// 这类非常隐蔽的回归。
    /// </summary>
    private void DisablePreviewRuntimeBehaviour()
    {
        DefenseTower defenseTower = _placementPreviewInstance.GetComponent<DefenseTower>();
        if (defenseTower != null)
        {
            defenseTower.enabled = false;
        }

        RelayTower relayTower = _placementPreviewInstance.GetComponent<RelayTower>();
        if (relayTower != null)
        {
            relayTower.enabled = false;
        }

        Collider2D[] previewColliders = _placementPreviewInstance.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < previewColliders.Length; i++)
        {
            previewColliders[i].enabled = false;
        }
    }

    private void CachePreviewRenderers()
    {
        _placementPreviewSpriteRenderer = _placementPreviewInstance.GetComponent<SpriteRenderer>();
        if (_placementPreviewSpriteRenderer != null)
        {
            _placementPreviewSpriteRenderer.sortingOrder = 15;
        }

        _placementPreviewRingRenderer = null;
    }

    /// <summary>
    /// 地面圆环是纯教学性视觉反馈。
    ///
    /// 玩家看到它之后，能更直觉地理解“这座塔落地时大概会占多大范围”。
    /// </summary>
    private void CreatePlacementRing(TowerType towerType)
    {
        Sprite ringSprite = _placementRingSprite;
        if (ringSprite == null && !string.IsNullOrWhiteSpace(_placementRingResourcePath))
        {
            ringSprite = Resources.Load<Sprite>(_placementRingResourcePath);
        }

        if (ringSprite == null)
        {
            return;
        }

        GameObject placementRing = new GameObject("PlacementRing");
        placementRing.transform.SetParent(_placementPreviewInstance.transform, false);
        placementRing.transform.localPosition = ResolvePlacementRingLocalOffset(towerType);
        placementRing.transform.localScale = Vector3.one * (_getPlacementRadius(towerType) * 2f);

        _placementPreviewRingRenderer = placementRing.AddComponent<SpriteRenderer>();
        _placementPreviewRingRenderer.sprite = ringSprite;
        _placementPreviewRingRenderer.sortingOrder = 14;
    }

    private Vector3 ResolvePlacementRingLocalOffset(TowerType towerType)
    {
        if (_getPrototype == null)
        {
            return Vector3.zero;
        }

        GameObject prototype = _getPrototype(towerType);
        if (prototype == null)
        {
            return Vector3.zero;
        }

        CircleCollider2D collider = prototype.GetComponent<CircleCollider2D>();
        if (collider == null)
        {
            return Vector3.zero;
        }

        Vector3 scale = prototype.transform.lossyScale;
        return new Vector3(
            collider.offset.x * scale.x,
            collider.offset.y * scale.y,
            0f);
    }

    /// <summary>
    /// 只有切换塔型或整体销毁时，才真的释放预览对象。
    /// </summary>
    public void ReleasePlacementPreviewInstance()
    {
        if (_placementPreviewInstance != null)
        {
            UnityEngine.Object.Destroy(_placementPreviewInstance);
        }

        _placementPreviewInstance = null;
        _placementPreviewTowerType = TowerType.None;
        _placementPreviewSpriteRenderer = null;
        _placementPreviewRingRenderer = null;
    }
}
