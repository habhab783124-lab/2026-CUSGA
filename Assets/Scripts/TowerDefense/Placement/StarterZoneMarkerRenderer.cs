using System;
using UnityEngine;

/// <summary>
/// `StarterZoneMarkerRenderer` 只负责把“首塔起手区”画成一个世界空间方形标记。
///
/// 它不关心为什么要显示，也不关心什么时候隐藏。
/// 外部只需要告诉它：
/// - 挂到哪个根节点下面
/// - 当前世界空间边界是多少
///
/// 这样它就能稳定地做一件事：把规则层给出的首塔区域画出来。
/// </summary>
public sealed class StarterZoneMarkerRenderer : IDisposable
{
    private const int MarkerTextureSize = 64; // 中文：标记Texture大小
    private const int MarkerBorderThickness = 3; // 中文：标记BorderThickness

    private readonly Color _fillColor; // 中文：fill颜色
    private readonly Color _edgeColor; // 中文：edge颜色
    private readonly int _sortingOrder; // 中文：sorting顺序

    private GameObject _markerObject; // 中文：标记Object
    private SpriteRenderer _spriteRenderer; // 中文：精灵Renderer
    private Texture2D _markerTexture; // 中文：标记Texture
    private Sprite _markerSprite; // 中文：标记精灵

    public StarterZoneMarkerRenderer(Color fillColor, Color edgeColor, int sortingOrder)
    {
        _fillColor = fillColor;
        _edgeColor = edgeColor;
        _sortingOrder = sortingOrder;
    }

    /// <summary>
    /// 显示起手区标记。
    ///
    /// 如果外部给的边界无效，就自动退回隐藏状态，避免在世界原点画出脏标记。
    /// </summary>
    public void Show(Transform parent, Bounds worldBounds)
    {
        if (parent == null || worldBounds.size.x <= Mathf.Epsilon || worldBounds.size.y <= Mathf.Epsilon)
        {
            Hide();
            return;
        }

        EnsureMarkerObject(parent);
        EnsureMarkerTexture();
        ApplyMarkerTransform(worldBounds);

        _spriteRenderer.sprite = _markerSprite;
        _markerObject.SetActive(true);
    }

    public void Hide()
    {
        if (_markerObject != null)
        {
            _markerObject.SetActive(false);
        }
    }

    public void Dispose()
    {
        DestroyMarkerResources();

        if (_markerObject != null)
        {
            UnityEngine.Object.Destroy(_markerObject);
            _markerObject = null;
            _spriteRenderer = null;
        }
    }

    private void EnsureMarkerObject(Transform parent)
    {
        if (_markerObject == null)
        {
            _markerObject = new GameObject("StarterPlacementMarker");
            _markerObject.transform.SetParent(parent, false);

            _spriteRenderer = _markerObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sortingOrder = _sortingOrder;
            _spriteRenderer.color = Color.white;
        }
        else if (_markerObject.transform.parent != parent)
        {
            _markerObject.transform.SetParent(parent, false);
        }
    }

    /// <summary>
    /// 这里直接在运行时生成一张极小的程序纹理。
    ///
    /// 这么做的好处是：
    /// - 不额外依赖美术资源
    /// - 起手区大小可以直接按规则缩放
    /// - 边框和填充颜色完全由代码配置控制
    /// </summary>
    private void EnsureMarkerTexture()
    {
        if (_markerTexture != null && _markerSprite != null)
        {
            return;
        }

        DestroyMarkerResources();

        _markerTexture = new Texture2D(MarkerTextureSize, MarkerTextureSize, TextureFormat.RGBA32, false);
        _markerTexture.wrapMode = TextureWrapMode.Clamp;
        _markerTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[MarkerTextureSize * MarkerTextureSize];
        for (int y = 0; y < MarkerTextureSize; y++)
        {
            for (int x = 0; x < MarkerTextureSize; x++)
            {
                bool isBorder = x < MarkerBorderThickness
                    || x >= MarkerTextureSize - MarkerBorderThickness
                    || y < MarkerBorderThickness
                    || y >= MarkerTextureSize - MarkerBorderThickness;

                pixels[(y * MarkerTextureSize) + x] = isBorder ? _edgeColor : _fillColor;
            }
        }

        _markerTexture.SetPixels(pixels);
        _markerTexture.Apply(false, false);

        _markerSprite = Sprite.Create(
            _markerTexture,
            new Rect(0f, 0f, MarkerTextureSize, MarkerTextureSize),
            new Vector2(0.5f, 0.5f),
            MarkerTextureSize,
            0,
            SpriteMeshType.FullRect);
    }

    private void ApplyMarkerTransform(Bounds worldBounds)
    {
        if (_markerObject == null)
        {
            return;
        }

        _markerObject.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, 0f);
        _markerObject.transform.localScale = new Vector3(worldBounds.size.x, worldBounds.size.y, 1f);
    }

    private void DestroyMarkerResources()
    {
        if (_markerSprite != null)
        {
            UnityEngine.Object.Destroy(_markerSprite);
            _markerSprite = null;
        }

        if (_markerTexture != null)
        {
            UnityEngine.Object.Destroy(_markerTexture);
            _markerTexture = null;
        }
    }
}
