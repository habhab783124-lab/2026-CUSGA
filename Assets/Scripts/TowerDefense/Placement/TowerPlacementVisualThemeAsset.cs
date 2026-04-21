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
    [Header("Preview")]
    [SerializeField] private Color validPreviewColor = new Color(0.26f, 0.95f, 0.78f, 0.72f);
    [SerializeField] private Color invalidPreviewColor = new Color(1f, 0.32f, 0.38f, 0.72f);
    [SerializeField] private Sprite placementRingSprite;

    [Header("Overlay")]
    [SerializeField] private float placementAreaOverlayPixelsPerUnit = 20f;
    [SerializeField] private Color placementAreaOverlayFillColor = new Color(0.18f, 0.82f, 0.86f, 0.16f);
    [SerializeField] private Color placementAreaOverlayEdgeColor = new Color(0.72f, 1f, 0.97f, 0.52f);
    [SerializeField] private int placementAreaOverlaySortingOrder = 12;

    [Header("Starter Zone Marker")]
    [SerializeField] private Color starterZoneMarkerFillColor = new Color(0.22f, 0.82f, 0.88f, 0.22f);
    [SerializeField] private Color starterZoneMarkerEdgeColor = new Color(0.9f, 1f, 0.98f, 1f);
    [SerializeField] private int starterZoneMarkerSortingOrder = 10;

    public Color ValidPreviewColor => validPreviewColor;
    public Color InvalidPreviewColor => invalidPreviewColor;
    public Sprite PlacementRingSprite => placementRingSprite;
    public float PlacementAreaOverlayPixelsPerUnit => placementAreaOverlayPixelsPerUnit;
    public Color PlacementAreaOverlayFillColor => placementAreaOverlayFillColor;
    public Color PlacementAreaOverlayEdgeColor => placementAreaOverlayEdgeColor;
    public int PlacementAreaOverlaySortingOrder => placementAreaOverlaySortingOrder;
    public Color StarterZoneMarkerFillColor => starterZoneMarkerFillColor;
    public Color StarterZoneMarkerEdgeColor => starterZoneMarkerEdgeColor;
    public int StarterZoneMarkerSortingOrder => starterZoneMarkerSortingOrder;
}
