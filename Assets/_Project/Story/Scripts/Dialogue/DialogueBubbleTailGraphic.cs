using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural triangle tail for a dialogue bubble (no sprite required).
/// Attach to a RectTransform; it renders a downward-pointing isosceles triangle.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class DialogueBubbleTailGraphic : MaskableGraphic
{
    [SerializeField] [Range(0.05f, 1f)]
    private float topWidth01 = 0.8f;

    /// <summary>0..1 fraction of rect width used by the flat top edge.</summary>
    public float TopWidth01
    {
        get => topWidth01;
        set
        {
            topWidth01 = Mathf.Clamp01(value);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        // GetPixelAdjustedRect helps avoid half-pixel seams in UGUI.
        Rect r = GetPixelAdjustedRect();
        float w = r.width;
        float h = r.height;
        if (w <= 0.01f || h <= 0.01f)
        {
            return;
        }

        // IMPORTANT: Rect is expressed in local space and is affected by pivot.
        // Always use xMin/xMax/yMin/yMax rather than assuming (0..w, 0..h).
        float topW = Mathf.Clamp01(topWidth01) * w;
        float cx = r.center.x;
        float x0 = cx - topW * 0.5f;
        float x1 = cx + topW * 0.5f;

        Vector2 v0 = new Vector2(x0, r.yMax); // top-left
        Vector2 v1 = new Vector2(x1, r.yMax); // top-right
        Vector2 v2 = new Vector2(cx, r.yMin); // bottom tip

        var col32 = (Color32)color;

        int i0 = AddVert(vh, v0, col32);
        int i1 = AddVert(vh, v1, col32);
        int i2 = AddVert(vh, v2, col32);
        vh.AddTriangle(i0, i1, i2);
    }

    private static int AddVert(VertexHelper vh, Vector2 pos, Color32 col)
    {
        var v = UIVertex.simpleVert;
        v.color = col;
        v.position = pos;
        v.uv0 = Vector2.zero;
        vh.AddVert(v);
        return vh.currentVertCount - 1;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        topWidth01 = Mathf.Clamp01(topWidth01);
        SetVerticesDirty();
    }
#endif
}

