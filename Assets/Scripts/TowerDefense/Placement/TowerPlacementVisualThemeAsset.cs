using UnityEngine;

/// <summary>
/// `TowerPlacementVisualThemeAsset` 把放置可视化链里的主题参数抽成共享资产。
///
/// 这部分虽然大多是运行时反馈，
/// 但它们本质上仍然属于“作者希望统一调整的一套视觉规则”，例如：
/// - 预览合法 / 非法颜色
/// - 覆盖层描边和填充色
/// - 首塔起手区标记颜色
/// - 放置圆环 Sprite
///
/// 抽成资产后，多关卡可以更稳定地共用同一套放置反馈风格。
/// </summary>
[CreateAssetMenu(
    fileName = "TowerPlacementVisualTheme",
    menuName = "Tower Defense/Placement/Placement Visual Theme")]
public sealed class TowerPlacementVisualThemeAsset : ScriptableObject
{
    [Header("预览")]
    [SerializeField, InspectorName("合法预览颜色")] private Color validPreviewColor = new Color(0.26f, 0.95f, 0.78f, 0.72f); // 中文：有效预览颜色
    [SerializeField, InspectorName("非法预览颜色")] private Color invalidPreviewColor = new Color(1f, 0.32f, 0.38f, 0.72f); // 中文：无效预览颜色
    [SerializeField, InspectorName("放置圆环精灵")] private Sprite placementRingSprite; // 中文：放置圆环精灵

    [Header("覆盖层")]
    [SerializeField, InspectorName("覆盖层像素密度")] private float placementAreaOverlayPixelsPerUnit = 20f; // 中文：放置Area覆盖层PixelsPerUnit
    [SerializeField, InspectorName("覆盖层填充色")] private Color placementAreaOverlayFillColor = new Color(0.18f, 0.82f, 0.86f, 0.16f); // 中文：放置Area覆盖层Fill颜色
    [SerializeField, InspectorName("覆盖层描边色")] private Color placementAreaOverlayEdgeColor = new Color(0.72f, 1f, 0.97f, 0.52f); // 中文：放置Area覆盖层Edge颜色
    [SerializeField, InspectorName("覆盖层排序值")] private int placementAreaOverlaySortingOrder = 12; // 中文：放置Area覆盖层Sorting顺序

    [Header("首塔起始区标记")]
    [SerializeField, InspectorName("起始区填充色")] private Color starterZoneMarkerFillColor = new Color(0.22f, 0.82f, 0.88f, 0.22f); // 中文：起始区域标记Fill颜色
    [SerializeField, InspectorName("起始区描边色")] private Color starterZoneMarkerEdgeColor = new Color(0.9f, 1f, 0.98f, 1f); // 中文：起始区域标记Edge颜色
    [SerializeField, InspectorName("起始区排序值")] private int starterZoneMarkerSortingOrder = 10; // 中文：起始区域标记Sorting顺序

    public Color ValidPreviewColor => validPreviewColor; // 中文：有效预览颜色
    public Color InvalidPreviewColor => invalidPreviewColor; // 中文：无效预览颜色
    public Sprite PlacementRingSprite => placementRingSprite; // 中文：放置圆环精灵
    public float PlacementAreaOverlayPixelsPerUnit => placementAreaOverlayPixelsPerUnit; // 中文：放置Area覆盖层PixelsPerUnit
    public Color PlacementAreaOverlayFillColor => placementAreaOverlayFillColor; // 中文：放置Area覆盖层Fill颜色
    public Color PlacementAreaOverlayEdgeColor => placementAreaOverlayEdgeColor; // 中文：放置Area覆盖层Edge颜色
    public int PlacementAreaOverlaySortingOrder => placementAreaOverlaySortingOrder; // 中文：放置Area覆盖层Sorting顺序
    public Color StarterZoneMarkerFillColor => starterZoneMarkerFillColor; // 中文：起始区域标记Fill颜色
    public Color StarterZoneMarkerEdgeColor => starterZoneMarkerEdgeColor; // 中文：起始区域标记Edge颜色
    public int StarterZoneMarkerSortingOrder => starterZoneMarkerSortingOrder; // 中文：起始区域标记Sorting顺序
}
