using System;
using UnityEngine;

/// <summary>
/// `PlacementAreaOverlayRenderer` 负责把“当前塔型的精确合法区”画成一张世界空间覆盖层。
///
/// 这里的核心约定是：
/// - 外部传入一个 `validator`
/// - 它只负责采样、生成纹理、渲染结果
/// - 它不直接知道 BuildZone、Blocker、首塔规则等玩法细节
///
/// 这样可视化和规则就保持了“同源、但不混职”的关系：
/// 规则还是总控说了算，Renderer 只是把结果画出来。
/// </summary>
public sealed class PlacementAreaOverlayRenderer : IDisposable
{
    /// <summary>
    /// 略微把采样密度提一点，让边界不会显得太粗糙。
    /// 这里保留的是已经调过性能和视觉平衡后的参数。
    /// </summary>
    private const float OverlayResolutionScale = 1.1f;

    /// <summary>
    /// 后期部署网络变大后，覆盖层重建的主要风险不再是“边界够不够顺滑”，
    /// 而是“像素总数会不会指数级把主线程压卡”。
    ///
    /// 所以这里给整张覆盖层一个总像素预算上限。
    /// 当扫描范围继续变大时，我们优先主动降低分辨率，
    /// 保证拖拽起手仍然顺，而不是死守同样密度导致后期每次放塔都轻微顿一下。
    /// </summary>
    private const int MaxOverlayPixelCount = 64000;

    /// <summary>
    /// 只在“边界像素”上做轻量细采样。
    ///
    /// 这样既能让边界更顺滑，也不会像整图多重采样那样把性能打爆。
    /// </summary>
    private const int EdgeSupersampleGridSize = 2;

    private readonly float _pixelsPerUnit;
    private readonly Color _fillColor;
    private readonly Color _edgeColor;
    private readonly int _sortingOrder;

    private GameObject _overlayObject;
    private SpriteRenderer _spriteRenderer;
    private Texture2D _overlayTexture;
    private Sprite _overlaySprite;
    private bool[] _legalMaskBuffer;
    private Color[] _pixelBuffer;
    private int _bufferWidth;
    private int _bufferHeight;

    public PlacementAreaOverlayRenderer(float pixelsPerUnit, Color fillColor, Color edgeColor, int sortingOrder)
    {
        _pixelsPerUnit = Mathf.Max(4f, pixelsPerUnit);
        _fillColor = fillColor;
        _edgeColor = edgeColor;
        _sortingOrder = sortingOrder;
    }

    /// <summary>
    /// 重新生成整张覆盖层。
    ///
    /// 这通常发生在：
    /// - 拖拽刚开始但缓存不可复用
    /// - 部署网络发生变化，旧缓存失效
    /// </summary>
    public void Show(Transform parent, Bounds worldBounds, Func<Vector3, bool> validator)
    {
        if (parent == null || validator == null || worldBounds.size.x <= Mathf.Epsilon || worldBounds.size.y <= Mathf.Epsilon)
        {
            Hide();
            return;
        }

        EnsureOverlayObject(parent);
        RebuildOverlayTexture(worldBounds, validator);

        if (_overlayObject != null)
        {
            ApplyOverlayTransform(worldBounds);
            _overlayObject.SetActive(true);
        }
    }

    /// <summary>
    /// 如果缓存纹理还有效，就只改位置和显示状态，不再重建纹理。
    /// </summary>
    public bool ShowPrepared(Transform parent, Bounds worldBounds)
    {
        if (_overlayObject == null || _spriteRenderer == null || _spriteRenderer.sprite == null)
        {
            return false;
        }

        if (parent == null || worldBounds.size.x <= Mathf.Epsilon || worldBounds.size.y <= Mathf.Epsilon)
        {
            Hide();
            return false;
        }

        EnsureOverlayObject(parent);
        ApplyOverlayTransform(worldBounds);
        _overlayObject.SetActive(true);
        return true;
    }

    public void Hide()
    {
        if (_overlayObject != null)
        {
            _overlayObject.SetActive(false);
        }
    }

    public void Dispose()
    {
        DestroyTextureResources();

        if (_overlayObject != null)
        {
            UnityEngine.Object.Destroy(_overlayObject);
            _overlayObject = null;
            _spriteRenderer = null;
        }
    }

    private void EnsureOverlayObject(Transform parent)
    {
        if (_overlayObject == null)
        {
            _overlayObject = new GameObject("PlacementAreaOverlay");
            _overlayObject.transform.SetParent(parent, false);

            _spriteRenderer = _overlayObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sortingOrder = _sortingOrder;
            _spriteRenderer.color = Color.white;
        }
        else if (_overlayObject.transform.parent != parent)
        {
            _overlayObject.transform.SetParent(parent, false);
        }
    }

    private void ApplyOverlayTransform(Bounds worldBounds)
    {
        if (_overlayObject == null)
        {
            return;
        }

        _overlayObject.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, 0f);
        _overlayObject.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 这里是覆盖层真正的热区。
    ///
    /// 当前策略是：
    /// 1. 先用单次采样把整张合法掩码算出来
    /// 2. 再只对边界像素做细采样
    /// 3. 最终按覆盖率和边界状态生成更平滑的颜色
    ///
    /// 这样能兼顾“边界不粗糙”和“拖拽时不卡爆”。
    /// </summary>
    private void RebuildOverlayTexture(Bounds worldBounds, Func<Vector3, bool> validator)
    {
        float effectivePixelsPerUnit = _pixelsPerUnit * OverlayResolutionScale;
        int width = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.x * effectivePixelsPerUnit));
        int height = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.y * effectivePixelsPerUnit));

        CapOverlayResolution(ref width, ref height);

        EnsureWorkingBuffers(width, height);
        EnsureTextureResources(width, height, worldBounds.size.x);

        float pixelWidth = worldBounds.size.x / width;
        float pixelHeight = worldBounds.size.y / height;
        float minX = worldBounds.min.x;
        float minY = worldBounds.min.y;

        for (int y = 0; y < height; y++)
        {
            float sampleY = minY + ((y + 0.5f) * pixelHeight);
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                float sampleX = minX + ((x + 0.5f) * pixelWidth);
                _legalMaskBuffer[rowOffset + x] = validator(new Vector3(sampleX, sampleY, 0f));
            }
        }

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowOffset + x;
                bool isLegal = _legalMaskBuffer[index];
                if (!isLegal)
                {
                    _pixelBuffer[index] = Color.clear;
                    continue;
                }

                bool isBoundaryPixel = HasIllegalNeighbour(_legalMaskBuffer, width, height, x, y);
                float coverage = isBoundaryPixel
                    ? SamplePixelCoverage(worldBounds, width, height, x, y, validator)
                    : 1f;

                _pixelBuffer[index] = BuildPixelColor(isBoundaryPixel, coverage);
            }
        }

        _overlayTexture.SetPixels(_pixelBuffer);
        _overlayTexture.Apply(false, false);
        _spriteRenderer.sprite = _overlaySprite;
    }

    /// <summary>
    /// 当部署网络越铺越大时，覆盖层像素总数也会随面积快速增长。
    ///
    /// 这里不去改玩法规则，只在可视化层主动做“按面积降采样”：
    /// - 小范围仍然保持当前细腻度
    /// - 大范围则按比例整体缩小宽高
    ///
    /// 这样可以把卡顿控制在更平缓的范围里，而不是到后几座塔时突然明显抖一下。
    /// </summary>
    private static void CapOverlayResolution(ref int width, ref int height)
    {
        long totalPixelCount = (long)width * height;
        if (totalPixelCount <= MaxOverlayPixelCount)
        {
            return;
        }

        float scale = Mathf.Sqrt(MaxOverlayPixelCount / (float)totalPixelCount);
        width = Mathf.Max(1, Mathf.FloorToInt(width * scale));
        height = Mathf.Max(1, Mathf.FloorToInt(height * scale));
    }

    private void EnsureWorkingBuffers(int width, int height)
    {
        int totalPixelCount = width * height;
        if (_legalMaskBuffer == null || _legalMaskBuffer.Length != totalPixelCount)
        {
            _legalMaskBuffer = new bool[totalPixelCount];
        }

        if (_pixelBuffer == null || _pixelBuffer.Length != totalPixelCount)
        {
            _pixelBuffer = new Color[totalPixelCount];
        }

        _bufferWidth = width;
        _bufferHeight = height;
    }

    /// <summary>
    /// 同尺寸时直接复用 `Texture2D` 和 `Sprite`，避免反复 new / destroy 造成延迟 GC 卡顿。
    /// </summary>
    private void EnsureTextureResources(int width, int height, float worldWidth)
    {
        if (_overlayTexture != null && _overlaySprite != null && _overlayTexture.width == width && _overlayTexture.height == height)
        {
            return;
        }

        DestroyTextureResources();

        _overlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        _overlayTexture.wrapMode = TextureWrapMode.Clamp;
        _overlayTexture.filterMode = FilterMode.Bilinear;

        float pixelsPerUnitForSprite = worldWidth > Mathf.Epsilon ? width / worldWidth : width;
        _overlaySprite = Sprite.Create(
            _overlayTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnitForSprite,
            0,
            SpriteMeshType.FullRect);
    }

    private static float SamplePixelCoverage(Bounds worldBounds, int width, int height, int pixelX, int pixelY, Func<Vector3, bool> validator)
    {
        int sampleGridSize = EdgeSupersampleGridSize;
        int totalSamples = sampleGridSize * sampleGridSize;
        int validSamples = 0;

        float pixelWidth = worldBounds.size.x / width;
        float pixelHeight = worldBounds.size.y / height;
        float minX = worldBounds.min.x + (pixelX * pixelWidth);
        float minY = worldBounds.min.y + (pixelY * pixelHeight);
        float sampleStepX = pixelWidth / sampleGridSize;
        float sampleStepY = pixelHeight / sampleGridSize;

        for (int sampleYIndex = 0; sampleYIndex < sampleGridSize; sampleYIndex++)
        {
            for (int sampleXIndex = 0; sampleXIndex < sampleGridSize; sampleXIndex++)
            {
                float sampleX = minX + ((sampleXIndex + 0.5f) * sampleStepX);
                float sampleY = minY + ((sampleYIndex + 0.5f) * sampleStepY);
                if (validator(new Vector3(sampleX, sampleY, 0f)))
                {
                    validSamples++;
                }
            }
        }

        return totalSamples > 0 ? (float)validSamples / totalSamples : 0f;
    }

    private Color BuildPixelColor(bool isBoundaryPixel, float coverage)
    {
        float boundaryBlend = isBoundaryPixel
            ? Mathf.Lerp(0.25f, 0.65f, 1f - coverage)
            : 0f;

        Color baseColor = Color.Lerp(_fillColor, _edgeColor, boundaryBlend);
        float softenedAlpha = baseColor.a * Mathf.SmoothStep(0f, 1f, coverage);
        baseColor.a = softenedAlpha;
        return baseColor;
    }

    private static bool HasIllegalNeighbour(bool[] legalMask, int width, int height, int x, int y)
    {
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                int neighbourX = x + offsetX;
                int neighbourY = y + offsetY;
                if (neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height)
                {
                    return true;
                }

                if (!legalMask[(neighbourY * width) + neighbourX])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void DestroyTextureResources()
    {
        if (_overlaySprite != null)
        {
            UnityEngine.Object.Destroy(_overlaySprite);
            _overlaySprite = null;
        }

        if (_overlayTexture != null)
        {
            UnityEngine.Object.Destroy(_overlayTexture);
            _overlayTexture = null;
        }

        _legalMaskBuffer = null;
        _pixelBuffer = null;
        _bufferWidth = 0;
        _bufferHeight = 0;
    }
}
