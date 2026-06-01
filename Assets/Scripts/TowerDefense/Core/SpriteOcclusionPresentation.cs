using UnityEngine;

/// <summary>
/// `SpriteOcclusionPresentation` 是当前塔防俯视/斜视角下的轻量遮挡桥接器。
///
/// 它只处理两件事：
/// 1. 根据“脚底点”的世界 Y 值动态计算排序，使单位能形成前后关系。
/// 2. 对高物体（例如塔）把单张 sprite 拆成上下两层，模拟“怪物走到塔后方时只被上半部分挡住”的视觉效果。
///
/// 这里刻意把它做成独立的 `ExecuteAlways` 表现脚本，而不是继续塞进战斗逻辑里，
/// 目的是让后续维护者很容易看清边界：
/// - `Enemy / DefenseTower / RelayTower` 继续关心玩法
/// - 这份脚本只关心排序和遮挡表现
///
/// 同时因为它会在编辑态运行，所以：
/// - Scene 视图中也能直接看到接近 Play 的遮挡结果
/// - 更符合本项目“Scene 作者化结果应成为视觉权威来源”的长期规则
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class SpriteOcclusionPresentation : MonoBehaviour
{
    private const string BaseLayerObjectName = "OcclusionBaseLayer";
    private const string TopLayerObjectName = "OcclusionTopLayer";

    [Header("Source")]
    [SerializeField] private SpriteRenderer sourceRendererReference;

    [Header("Sorting")]
    [SerializeField] private int sortingBaseOrder = 300;
    [SerializeField] private int sortingOrdersPerUnit = 20;
    [SerializeField] private int topLayerSortingOffset = 24;
    [SerializeField] private int healthBarSortingOffset = 40;

    [Header("Layer Split")]
    [SerializeField] private bool splitIntoBaseAndTop;
    [Range(0.15f, 0.85f)]
    [SerializeField] private float topLayerStartNormalized = 0.38f;

    [Header("Auxiliary Renderers")]
    [SerializeField] private bool autoSortHealthBarRenderers;

    private Transform _baseLayerTransform;
    private Transform _topLayerTransform;
    private SpriteRenderer _baseLayerRenderer;
    private SpriteRenderer _topLayerRenderer;
    private SpriteRenderer _healthBarBackgroundRenderer;
    private SpriteRenderer _healthBarFillRenderer;

    private Sprite _cachedSourceSprite;
    private Sprite _cachedBaseLayerSprite;
    private Sprite _cachedTopLayerSprite;
    private float _cachedSplitNormalized = -1f;

    private void OnEnable()
    {
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        RefreshPresentation();
    }

    private void OnValidate()
    {
        if (sourceRendererReference == null)
        {
            sourceRendererReference = GetComponent<SpriteRenderer>();
        }

        topLayerStartNormalized = Mathf.Clamp(topLayerStartNormalized, 0.15f, 0.85f);
        sortingOrdersPerUnit = Mathf.Max(1, sortingOrdersPerUnit);
        healthBarSortingOffset = Mathf.Max(1, healthBarSortingOffset);
        RefreshPresentation();
    }

    private void OnDestroy()
    {
        DestroyGeneratedLayerSprites();
    }

    /// <summary>
    /// 统一刷新当前表现层状态。
    ///
    /// 这一步每次只做很小的同步：
    /// - 排序变化时更新排序
    /// - 源 sprite 或颜色变化时同步子层
    ///
    /// 这样既能在编辑器里实时响应作者修改，
    /// 也不会把场景标脏得过于频繁。
    /// </summary>
    private void RefreshPresentation()
    {
        SpriteRenderer sourceRenderer = ResolveSourceRenderer();
        if (sourceRenderer == null)
        {
            return;
        }

        int baseSortingOrder = CalculateBaseSortingOrder(sourceRenderer);
        if (splitIntoBaseAndTop)
        {
            EnsureLayerRenderers();
            RefreshLayerSprites(sourceRenderer.sprite);
            SyncLayerRendererVisual(sourceRenderer, _baseLayerRenderer, _cachedBaseLayerSprite);
            SyncLayerRendererVisual(sourceRenderer, _topLayerRenderer, _cachedTopLayerSprite);

            sourceRenderer.enabled = false;
            sourceRenderer.sortingOrder = baseSortingOrder + topLayerSortingOffset;

            if (_baseLayerRenderer != null)
            {
                SetSortingIfDifferent(_baseLayerRenderer, baseSortingOrder);
                _baseLayerRenderer.enabled = true;
            }

            if (_topLayerRenderer != null)
            {
                SetSortingIfDifferent(_topLayerRenderer, baseSortingOrder + topLayerSortingOffset);
                _topLayerRenderer.enabled = true;
            }
        }
        else
        {
            sourceRenderer.enabled = true;
            SetSortingIfDifferent(sourceRenderer, baseSortingOrder);
            DisableGeneratedLayerRenderers();
        }

        RefreshHealthBarSorting(baseSortingOrder);
    }

    private SpriteRenderer ResolveSourceRenderer()
    {
        if (sourceRendererReference == null)
        {
            sourceRendererReference = GetComponent<SpriteRenderer>();
        }

        return sourceRendererReference;
    }

    /// <summary>
    /// 当前遮挡排序默认优先使用碰撞体底边作为“脚底点”。
    ///
    /// 这样做比直接看 transform.position 更接近玩家视觉认知：
    /// 玩家在意的不是 pivot 在哪，而是谁的“脚底 / 底盘”更靠下。
    ///
    /// 如果对象没有碰撞体，再退回到可见 sprite 的底边。
    /// </summary>
    private int CalculateBaseSortingOrder(SpriteRenderer sourceRenderer)
    {
        float pivotWorldY = transform.position.y;

        Collider2D primaryCollider = GetComponent<Collider2D>();
        if (primaryCollider != null)
        {
            pivotWorldY = primaryCollider.bounds.min.y;
        }
        else if (sourceRenderer != null)
        {
            pivotWorldY = sourceRenderer.bounds.min.y;
        }

        return sortingBaseOrder - Mathf.RoundToInt(pivotWorldY * sortingOrdersPerUnit);
    }

    private void EnsureLayerRenderers()
    {
        _baseLayerTransform = EnsureChildTransform(_baseLayerTransform, BaseLayerObjectName);
        _topLayerTransform = EnsureChildTransform(_topLayerTransform, TopLayerObjectName);

        _baseLayerRenderer = EnsureChildRenderer(_baseLayerTransform, ref _baseLayerRenderer);
        _topLayerRenderer = EnsureChildRenderer(_topLayerTransform, ref _topLayerRenderer);
    }

    private Transform EnsureChildTransform(Transform existingTransform, string childName)
    {
        if (existingTransform != null)
        {
            return existingTransform;
        }

        Transform existing = transform.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject childObject = new GameObject(childName);
        Transform childTransform = childObject.transform;
        childTransform.SetParent(transform, false);
        childTransform.localPosition = Vector3.zero;
        childTransform.localRotation = Quaternion.identity;
        childTransform.localScale = Vector3.one;
        return childTransform;
    }

    private static SpriteRenderer EnsureChildRenderer(Transform layerTransform, ref SpriteRenderer cachedRenderer)
    {
        if (cachedRenderer != null)
        {
            return cachedRenderer;
        }

        cachedRenderer = layerTransform != null ? layerTransform.GetComponent<SpriteRenderer>() : null;
        if (cachedRenderer == null && layerTransform != null)
        {
            cachedRenderer = layerTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        return cachedRenderer;
    }

    /// <summary>
    /// 把一张完整 sprite 拆成“下半层 + 上半层”两张运行时 sprite。
    ///
    /// 这里选择按像素 rect 水平切一刀，而不是上更重的遮罩方案，
    /// 原因是当前项目更需要：
    /// - 轻量
    /// - 可维护
    /// - 容易继续在 Scene / prefab 上调整
    ///
    /// 这套方案虽然不如 shader 遮挡那么花哨，
    /// 但对于当前塔防视角已经足够模拟“怪物被高塔上半部分挡住”的关系。
    /// </summary>
    private void RefreshLayerSprites(Sprite sourceSprite)
    {
        if (sourceSprite == null)
        {
            DestroyGeneratedLayerSprites();
            return;
        }

        if (_cachedSourceSprite == sourceSprite &&
            _cachedBaseLayerSprite != null &&
            _cachedTopLayerSprite != null &&
            Mathf.Approximately(_cachedSplitNormalized, topLayerStartNormalized))
        {
            return;
        }

        DestroyGeneratedLayerSprites();

        Rect sourceRect = sourceSprite.rect;
        float splitPixels = Mathf.Clamp(sourceRect.height * topLayerStartNormalized, 1f, sourceRect.height - 1f);
        float pivotXNormalized = sourceRect.width > 0f ? sourceSprite.pivot.x / sourceRect.width : 0.5f;

        Rect baseRect = new Rect(sourceRect.x, sourceRect.y, sourceRect.width, splitPixels);
        Rect topRect = new Rect(sourceRect.x, sourceRect.y + splitPixels, sourceRect.width, sourceRect.height - splitPixels);

        _cachedBaseLayerSprite = Sprite.Create(
            sourceSprite.texture,
            baseRect,
            new Vector2(pivotXNormalized, 0f),
            sourceSprite.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        _cachedTopLayerSprite = Sprite.Create(
            sourceSprite.texture,
            topRect,
            new Vector2(pivotXNormalized, 0f),
            sourceSprite.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        _cachedBaseLayerSprite.name = $"{sourceSprite.name}_OcclusionBase";
        _cachedTopLayerSprite.name = $"{sourceSprite.name}_OcclusionTop";
        _cachedSourceSprite = sourceSprite;
        _cachedSplitNormalized = topLayerStartNormalized;

        float bottomWorldOffset = -sourceSprite.pivot.y / sourceSprite.pixelsPerUnit;
        float splitWorldOffset = splitPixels / sourceSprite.pixelsPerUnit;

        if (_baseLayerTransform != null)
        {
            _baseLayerTransform.localPosition = new Vector3(0f, bottomWorldOffset, 0f);
        }

        if (_topLayerTransform != null)
        {
            _topLayerTransform.localPosition = new Vector3(0f, bottomWorldOffset + splitWorldOffset, 0f);
        }
    }

    private static void SyncLayerRendererVisual(SpriteRenderer sourceRenderer, SpriteRenderer targetRenderer, Sprite targetSprite)
    {
        if (sourceRenderer == null || targetRenderer == null)
        {
            return;
        }

        if (targetRenderer.sprite != targetSprite)
        {
            targetRenderer.sprite = targetSprite;
        }

        targetRenderer.color = sourceRenderer.color;
        targetRenderer.flipX = sourceRenderer.flipX;
        targetRenderer.flipY = sourceRenderer.flipY;
        targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        targetRenderer.maskInteraction = sourceRenderer.maskInteraction;
        targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        targetRenderer.sortingLayerName = sourceRenderer.sortingLayerName;
        targetRenderer.drawMode = SpriteDrawMode.Simple;
        targetRenderer.enabled = true;
    }

    private void DisableGeneratedLayerRenderers()
    {
        if (_baseLayerRenderer != null)
        {
            _baseLayerRenderer.enabled = false;
        }

        if (_topLayerRenderer != null)
        {
            _topLayerRenderer.enabled = false;
        }
    }

    private void RefreshHealthBarSorting(int baseSortingOrder)
    {
        if (!autoSortHealthBarRenderers)
        {
            return;
        }

        if (_healthBarBackgroundRenderer == null)
        {
            Transform backgroundTransform = transform.Find("HealthBarRoot/HealthBarBackground");
            _healthBarBackgroundRenderer = backgroundTransform != null ? backgroundTransform.GetComponent<SpriteRenderer>() : null;
        }

        if (_healthBarFillRenderer == null)
        {
            Transform fillTransform = transform.Find("HealthBarRoot/HealthBarFill");
            _healthBarFillRenderer = fillTransform != null ? fillTransform.GetComponent<SpriteRenderer>() : null;
        }

        if (_healthBarBackgroundRenderer != null)
        {
            SetSortingIfDifferent(_healthBarBackgroundRenderer, baseSortingOrder + healthBarSortingOffset);
        }

        if (_healthBarFillRenderer != null)
        {
            SetSortingIfDifferent(_healthBarFillRenderer, baseSortingOrder + healthBarSortingOffset + 1);
        }
    }

    private static void SetSortingIfDifferent(SpriteRenderer renderer, int sortingOrder)
    {
        if (renderer == null || renderer.sortingOrder == sortingOrder)
        {
            return;
        }

        renderer.sortingOrder = sortingOrder;
    }

    private void DestroyGeneratedLayerSprites()
    {
        if (_cachedBaseLayerSprite != null)
        {
            DestroyImmediateSafely(_cachedBaseLayerSprite);
            _cachedBaseLayerSprite = null;
        }

        if (_cachedTopLayerSprite != null)
        {
            DestroyImmediateSafely(_cachedTopLayerSprite);
            _cachedTopLayerSprite = null;
        }

        _cachedSourceSprite = null;
        _cachedSplitNormalized = -1f;
    }

    private static void DestroyImmediateSafely(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
