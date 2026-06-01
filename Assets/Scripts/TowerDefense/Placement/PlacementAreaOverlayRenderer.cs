using System;
using System.Collections.Generic;
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
    private const string ContourRootName = "SmoothPlacementContours";

    /// <summary>
    /// 覆盖层现在承担“平滑显示”的职责，采样密度需要明显高于最终落塔格子。
    /// 真正落塔判定仍由规则层负责，这里只把可视边缘画得更接近地图几何。
    /// </summary>
    private const float OverlayResolutionScale = 2.4f;

    /// <summary>
    /// 后期部署网络变大后，覆盖层重建的主要风险不再是“边界够不够顺滑”，
    /// 而是“像素总数会不会指数级把主线程压卡”。
    ///
    /// 所以这里给整张覆盖层一个总像素预算上限。
    /// 当扫描范围继续变大时，我们优先主动降低分辨率，
    /// 保证拖拽起手仍然顺，而不是死守同样密度导致后期每次放塔都轻微顿一下。
    /// </summary>
    private const int MaxOverlayPixelCount = 262144;

    /// <summary>
    /// 只在“边界像素”上做轻量细采样。
    ///
    /// 这样既能让边界更顺滑，也不会像整图多重采样那样把性能打爆。
    /// </summary>
    private const int EdgeSupersampleGridSize = 3;
    private const int MaxContourRendererCount = 384;
    private const float ContourWidthScale = 1.15f;

    private readonly float _pixelsPerUnit;
    private readonly Color _fillColor;
    private readonly Color _edgeColor;
    private readonly int _sortingOrder;
    private readonly List<ContourSegment> _contourSegments = new List<ContourSegment>(512);
    private readonly List<LineRenderer> _contourRenderers = new List<LineRenderer>(32);

    private GameObject _overlayObject;
    private Transform _contourRoot;
    private SpriteRenderer _spriteRenderer;
    private Texture2D _overlayTexture;
    private Sprite _overlaySprite;
    private bool[] _legalMaskBuffer;
    private Color[] _pixelBuffer;
    private int _bufferWidth;
    private int _bufferHeight;
    private static Material s_sharedContourMaterial;

    private readonly struct ContourSegment
    {
        public ContourSegment(Vector2Int start, Vector2Int end)
        {
            Start = start;
            End = end;
        }

        public Vector2Int Start { get; }
        public Vector2Int End { get; }
    }

    private readonly struct ContourEdgeKey : IEquatable<ContourEdgeKey>
    {
        private readonly int _startX;
        private readonly int _startY;
        private readonly int _endX;
        private readonly int _endY;

        public ContourEdgeKey(Vector2Int a, Vector2Int b)
        {
            bool keepOrder = a.x < b.x || (a.x == b.x && a.y <= b.y);
            Vector2Int start = keepOrder ? a : b;
            Vector2Int end = keepOrder ? b : a;

            _startX = start.x;
            _startY = start.y;
            _endX = end.x;
            _endY = end.y;
        }

        public Vector2Int Start => new Vector2Int(_startX, _startY);
        public Vector2Int End => new Vector2Int(_endX, _endY);

        public bool Equals(ContourEdgeKey other)
        {
            return _startX == other._startX
                && _startY == other._startY
                && _endX == other._endX
                && _endY == other._endY;
        }

        public override bool Equals(object obj)
        {
            return obj is ContourEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + _startX;
                hash = (hash * 31) + _startY;
                hash = (hash * 31) + _endX;
                hash = (hash * 31) + _endY;
                return hash;
            }
        }
    }

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

        EnsureContourRoot();
    }

    private void EnsureContourRoot()
    {
        if (_overlayObject == null)
        {
            _contourRoot = null;
            return;
        }

        if (_contourRoot != null && _contourRoot.parent == _overlayObject.transform)
        {
            return;
        }

        Transform existingRoot = _overlayObject.transform.Find(ContourRootName);
        if (existingRoot == null)
        {
            GameObject contourObject = new GameObject(ContourRootName);
            existingRoot = contourObject.transform;
            existingRoot.SetParent(_overlayObject.transform, false);
        }

        existingRoot.localPosition = Vector3.zero;
        existingRoot.localRotation = Quaternion.identity;
        existingRoot.localScale = Vector3.one;
        _contourRoot = existingRoot;
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

        EnsureTextureResources(width, height, worldBounds.size.x);
        EnsureWorkingBuffers(width, height);

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

        RebuildSmoothContours(worldBounds, width, height, pixelWidth, pixelHeight);

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
            ? Mathf.Lerp(0.04f, 0.16f, 1f - coverage)
            : 0f;

        Color baseColor = Color.Lerp(_fillColor, _edgeColor, boundaryBlend);
        float softenedAlpha = baseColor.a * Mathf.SmoothStep(0f, 1f, coverage);
        baseColor.a = softenedAlpha;
        return baseColor;
    }

    /// <summary>
    /// 从同一份合法区采样结果里提取平滑轮廓。
    ///
    /// 填充贴图继续负责告诉玩家“哪里大体可放”，
    /// Marching Squares 轮廓则负责把边界从像素格子读感里解放出来。
    /// </summary>
    private void RebuildSmoothContours(Bounds worldBounds, int width, int height, float pixelWidth, float pixelHeight)
    {
        BuildMarchingSquaresSegments(width, height);

        List<List<Vector3>> contours = BuildContourPolylines(worldBounds, pixelWidth, pixelHeight);
        contours.Sort((left, right) => right.Count.CompareTo(left.Count));

        ApplyContourRenderers(contours, Mathf.Min(pixelWidth, pixelHeight));
    }

    private void BuildMarchingSquaresSegments(int width, int height)
    {
        _contourSegments.Clear();

        int paddedCellWidth = width + 1;
        int paddedCellHeight = height + 1;
        for (int y = 0; y < paddedCellHeight; y++)
        {
            for (int x = 0; x < paddedCellWidth; x++)
            {
                bool bottomLeft = GetPaddedMaskValue(x, y, width, height);
                bool bottomRight = GetPaddedMaskValue(x + 1, y, width, height);
                bool topRight = GetPaddedMaskValue(x + 1, y + 1, width, height);
                bool topLeft = GetPaddedMaskValue(x, y + 1, width, height);

                int configuration = 0;
                if (bottomLeft)
                {
                    configuration |= 1;
                }

                if (bottomRight)
                {
                    configuration |= 2;
                }

                if (topRight)
                {
                    configuration |= 4;
                }

                if (topLeft)
                {
                    configuration |= 8;
                }

                AddMarchingSquareSegments(configuration, x, y);
            }
        }
    }

    private bool GetPaddedMaskValue(int x, int y, int width, int height)
    {
        int maskX = x - 1;
        int maskY = y - 1;
        if (maskX < 0 || maskX >= width || maskY < 0 || maskY >= height)
        {
            return false;
        }

        return _legalMaskBuffer[(maskY * width) + maskX];
    }

    private void AddMarchingSquareSegments(int configuration, int x, int y)
    {
        if (configuration == 0 || configuration == 15)
        {
            return;
        }

        Vector2Int bottom = new Vector2Int((x * 2) + 1, y * 2);
        Vector2Int right = new Vector2Int((x + 1) * 2, (y * 2) + 1);
        Vector2Int top = new Vector2Int((x * 2) + 1, (y + 1) * 2);
        Vector2Int left = new Vector2Int(x * 2, (y * 2) + 1);

        switch (configuration)
        {
            case 1:
            case 14:
                AddContourSegment(left, bottom);
                break;
            case 2:
            case 13:
                AddContourSegment(bottom, right);
                break;
            case 3:
            case 12:
                AddContourSegment(left, right);
                break;
            case 4:
            case 11:
                AddContourSegment(right, top);
                break;
            case 5:
                AddContourSegment(left, bottom);
                AddContourSegment(right, top);
                break;
            case 6:
            case 9:
                AddContourSegment(bottom, top);
                break;
            case 7:
            case 8:
                AddContourSegment(left, top);
                break;
            case 10:
                AddContourSegment(bottom, right);
                AddContourSegment(top, left);
                break;
        }
    }

    private void AddContourSegment(Vector2Int start, Vector2Int end)
    {
        if (start == end)
        {
            return;
        }

        _contourSegments.Add(new ContourSegment(start, end));
    }

    private List<List<Vector3>> BuildContourPolylines(Bounds worldBounds, float pixelWidth, float pixelHeight)
    {
        Dictionary<Vector2Int, List<Vector2Int>> adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
        HashSet<ContourEdgeKey> unusedEdges = new HashSet<ContourEdgeKey>();

        for (int i = 0; i < _contourSegments.Count; i++)
        {
            ContourSegment segment = _contourSegments[i];
            ContourEdgeKey edgeKey = new ContourEdgeKey(segment.Start, segment.End);
            if (!unusedEdges.Add(edgeKey))
            {
                continue;
            }

            AddAdjacency(adjacency, segment.Start, segment.End);
            AddAdjacency(adjacency, segment.End, segment.Start);
        }

        List<List<Vector3>> contours = new List<List<Vector3>>();
        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> entry in adjacency)
        {
            if (entry.Value.Count != 1)
            {
                continue;
            }

            while (TryGetUnusedNeighbour(entry.Key, adjacency, unusedEdges, out Vector2Int next))
            {
                AddTracedContour(contours, TraceContour(entry.Key, next, adjacency, unusedEdges, worldBounds, pixelWidth, pixelHeight));
            }
        }

        while (unusedEdges.Count > 0)
        {
            ContourEdgeKey firstUnusedEdge = default;
            foreach (ContourEdgeKey edge in unusedEdges)
            {
                firstUnusedEdge = edge;
                break;
            }

            AddTracedContour(contours, TraceContour(firstUnusedEdge.Start, firstUnusedEdge.End, adjacency, unusedEdges, worldBounds, pixelWidth, pixelHeight));
        }

        return contours;
    }

    private static void AddAdjacency(Dictionary<Vector2Int, List<Vector2Int>> adjacency, Vector2Int point, Vector2Int neighbour)
    {
        if (!adjacency.TryGetValue(point, out List<Vector2Int> neighbours))
        {
            neighbours = new List<Vector2Int>(2);
            adjacency.Add(point, neighbours);
        }

        neighbours.Add(neighbour);
    }

    private static bool TryGetUnusedNeighbour(
        Vector2Int point,
        Dictionary<Vector2Int, List<Vector2Int>> adjacency,
        HashSet<ContourEdgeKey> unusedEdges,
        out Vector2Int next)
    {
        next = default;
        if (!adjacency.TryGetValue(point, out List<Vector2Int> neighbours))
        {
            return false;
        }

        for (int i = 0; i < neighbours.Count; i++)
        {
            Vector2Int candidate = neighbours[i];
            if (unusedEdges.Contains(new ContourEdgeKey(point, candidate)))
            {
                next = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetNextContourPoint(
        Vector2Int current,
        Vector2Int previous,
        Dictionary<Vector2Int, List<Vector2Int>> adjacency,
        HashSet<ContourEdgeKey> unusedEdges,
        out Vector2Int next)
    {
        next = default;
        if (!adjacency.TryGetValue(current, out List<Vector2Int> neighbours))
        {
            return false;
        }

        for (int i = 0; i < neighbours.Count; i++)
        {
            Vector2Int candidate = neighbours[i];
            if (candidate == previous)
            {
                continue;
            }

            if (unusedEdges.Contains(new ContourEdgeKey(current, candidate)))
            {
                next = candidate;
                return true;
            }
        }

        return false;
    }

    private List<Vector3> TraceContour(
        Vector2Int start,
        Vector2Int firstNext,
        Dictionary<Vector2Int, List<Vector2Int>> adjacency,
        HashSet<ContourEdgeKey> unusedEdges,
        Bounds worldBounds,
        float pixelWidth,
        float pixelHeight)
    {
        List<Vector3> points = new List<Vector3>();
        points.Add(ConvertContourKeyToLocalPoint(start, worldBounds, pixelWidth, pixelHeight));

        Vector2Int previous = start;
        Vector2Int current = firstNext;
        int guard = _contourSegments.Count + 1;

        while (guard > 0)
        {
            guard--;

            ContourEdgeKey edge = new ContourEdgeKey(previous, current);
            if (!unusedEdges.Remove(edge))
            {
                break;
            }

            points.Add(ConvertContourKeyToLocalPoint(current, worldBounds, pixelWidth, pixelHeight));
            if (current == start)
            {
                break;
            }

            if (!TryGetNextContourPoint(current, previous, adjacency, unusedEdges, out Vector2Int next))
            {
                break;
            }

            previous = current;
            current = next;
        }

        return points;
    }

    private void AddTracedContour(List<List<Vector3>> contours, List<Vector3> contour)
    {
        if (contour == null || contour.Count < 2)
        {
            return;
        }

        contours.Add(contour);
    }

    private static Vector3 ConvertContourKeyToLocalPoint(Vector2Int contourKey, Bounds worldBounds, float pixelWidth, float pixelHeight)
    {
        float gridX = contourKey.x * 0.5f;
        float gridY = contourKey.y * 0.5f;
        float worldX = worldBounds.min.x + ((gridX - 0.5f) * pixelWidth);
        float worldY = worldBounds.min.y + ((gridY - 0.5f) * pixelHeight);

        worldX = Mathf.Clamp(worldX, worldBounds.min.x, worldBounds.max.x);
        worldY = Mathf.Clamp(worldY, worldBounds.min.y, worldBounds.max.y);

        return new Vector3(
            worldX - worldBounds.center.x,
            worldY - worldBounds.center.y,
            0f);
    }

    private void ApplyContourRenderers(List<List<Vector3>> contours, float pixelSize)
    {
        EnsureContourRoot();

        int contourCount = _contourRoot != null
            ? Mathf.Min(contours.Count, MaxContourRendererCount)
            : 0;
        float contourWidth = Mathf.Clamp(pixelSize * ContourWidthScale, 0.025f, 0.16f);

        for (int i = 0; i < contourCount; i++)
        {
            List<Vector3> contour = contours[i];
            LineRenderer lineRenderer = EnsureContourRenderer(i, contourWidth);
            lineRenderer.positionCount = contour.Count;
            for (int pointIndex = 0; pointIndex < contour.Count; pointIndex++)
            {
                lineRenderer.SetPosition(pointIndex, contour[pointIndex]);
            }
        }

        for (int i = contourCount; i < _contourRenderers.Count; i++)
        {
            if (_contourRenderers[i] != null)
            {
                _contourRenderers[i].enabled = false;
                _contourRenderers[i].positionCount = 0;
            }
        }
    }

    private LineRenderer EnsureContourRenderer(int index, float contourWidth)
    {
        EnsureContourRoot();

        while (_contourRenderers.Count <= index)
        {
            string childName = $"Contour_{_contourRenderers.Count:000}";
            GameObject contourObject = new GameObject(childName);
            contourObject.transform.SetParent(_contourRoot, false);
            contourObject.transform.localPosition = Vector3.zero;
            contourObject.transform.localRotation = Quaternion.identity;
            contourObject.transform.localScale = Vector3.one;

            LineRenderer createdRenderer = contourObject.AddComponent<LineRenderer>();
            _contourRenderers.Add(createdRenderer);
        }

        LineRenderer lineRenderer = _contourRenderers[index];
        lineRenderer.sharedMaterial = GetSharedContourMaterial();
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCornerVertices = 6;
        lineRenderer.numCapVertices = 6;
        lineRenderer.sortingOrder = _sortingOrder + 1;
        lineRenderer.widthMultiplier = contourWidth;
        lineRenderer.startWidth = contourWidth;
        lineRenderer.endWidth = contourWidth;
        lineRenderer.startColor = _edgeColor;
        lineRenderer.endColor = _edgeColor;
        lineRenderer.loop = false;
        lineRenderer.useWorldSpace = false;
        lineRenderer.enabled = true;
        return lineRenderer;
    }

    private static Material GetSharedContourMaterial()
    {
        if (s_sharedContourMaterial != null)
        {
            return s_sharedContourMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            return null;
        }

        s_sharedContourMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return s_sharedContourMaterial;
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
