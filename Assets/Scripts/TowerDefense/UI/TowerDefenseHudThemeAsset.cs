using UnityEngine;

/// <summary>
/// `TowerDefenseHudThemeAsset` 把 HUD 主题从 `TowerDefenseGame` 场景脚本里抽成共享资产。
///
/// 这样以后如果你想统一多关卡的 HUD 配色和拖拽提示风格，
/// 直接改这一份资产就可以，不需要再到每个场景里重复调同一组颜色。
/// </summary>
[CreateAssetMenu(
    fileName = "TowerDefenseHudTheme",
    menuName = "Tower Defense/UI/HUD Theme")]
public sealed class TowerDefenseHudThemeAsset : ScriptableObject
{
    [Header("指标卡")]
    [SerializeField, InspectorName("指标标题颜色")] private Color metricLabelColor = new Color(0.56f, 0.66f, 0.75f, 1f); // 中文：指标标签颜色
    [SerializeField, InspectorName("废料数值颜色")] private Color scrapValueColor = new Color(1f, 0.71f, 0.4f, 1f); // 中文：废料Value颜色
    [SerializeField, InspectorName("基地数值颜色")] private Color baseValueColor = new Color(0.45f, 0.91f, 1f, 1f); // 中文：基础Value颜色
    [SerializeField, InspectorName("波次数值颜色")] private Color waveValueColor = new Color(1f, 0.85f, 0.47f, 1f); // 中文：波次Value颜色

    [Header("文本")]
    [SerializeField, InspectorName("卡片文本颜色")] private Color cardTextColor = new Color(0.96f, 0.98f, 1f, 1f); // 中文：卡片文本颜色
    [SerializeField, InspectorName("次级信息颜色")] private Color secondaryInfoColor = new Color(0.54f, 0.65f, 0.75f, 1f); // 中文：副Info颜色
    [SerializeField, InspectorName("状态文本颜色")] private Color statusTextColor = new Color(0.84f, 0.9f, 0.94f, 1f); // 中文：状态文本颜色

    [Header("提示")]
    [SerializeField, InspectorName("中性提示颜色")] private Color neutralNoticeColor = new Color(0.81f, 0.88f, 0.92f, 1f); // 中文：中性提示颜色
    [SerializeField, InspectorName("正向提示颜色")] private Color positiveNoticeColor = new Color(0.49f, 0.95f, 0.69f, 1f); // 中文：正向提示颜色
    [SerializeField, InspectorName("消耗提示颜色")] private Color spendingNoticeColor = new Color(1f, 0.85f, 0.47f, 1f); // 中文：消耗提示颜色
    [SerializeField, InspectorName("警告提示颜色")] private Color warningNoticeColor = new Color(1f, 0.72f, 0.44f, 1f); // 中文：警告提示颜色
    [SerializeField, InspectorName("危险提示颜色")] private Color dangerNoticeColor = new Color(1f, 0.55f, 0.5f, 1f); // 中文：危险提示颜色

    [Header("拖拽预览")]
    [SerializeField, InspectorName("拖拽信息颜色")] private Color dragPreviewInfoColor = new Color(0.53f, 0.65f, 0.74f, 1f); // 中文：拖拽预览Info颜色
    [SerializeField, InspectorName("拖拽合法颜色")] private Color dragPreviewValidColor = new Color(0.47f, 0.95f, 0.85f, 1f); // 中文：拖拽预览有效颜色
    [SerializeField, InspectorName("拖拽非法颜色")] private Color dragPreviewInvalidColor = new Color(1f, 0.45f, 0.51f, 1f); // 中文：拖拽预览无效颜色
    [SerializeField, InspectorName("卡片标签边距")] private Vector4 cardLabelMargin = new Vector4(108f, 18f, 24f, 18f); // 中文：卡片标签Margin
    [SerializeField, InspectorName("卡片字距")] private float cardLabelCharacterSpacing = 1.2f; // 中文：卡片标签Character间距
    [SerializeField, InspectorName("卡片行距")] private float cardLabelLineSpacing = -10f; // 中文：卡片标签线间距
    [SerializeField, InspectorName("拖拽面板偏移")] private Vector2 dragPreviewPanelOffset = new Vector2(142f, -92f); // 中文：拖拽预览面板偏移

    public TowerDefenseHudTheme ToRuntimeTheme()
    {
        return new TowerDefenseHudTheme(
            metricLabelColor,
            scrapValueColor,
            baseValueColor,
            waveValueColor,
            cardTextColor,
            secondaryInfoColor,
            statusTextColor,
            neutralNoticeColor,
            positiveNoticeColor,
            spendingNoticeColor,
            warningNoticeColor,
            dangerNoticeColor,
            dragPreviewInfoColor,
            dragPreviewValidColor,
            dragPreviewInvalidColor,
            cardLabelMargin,
            cardLabelCharacterSpacing,
            cardLabelLineSpacing,
            dragPreviewPanelOffset);
    }
}
