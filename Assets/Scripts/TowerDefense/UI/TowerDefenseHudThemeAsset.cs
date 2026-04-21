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
    [Header("Metric Cards")]
    [SerializeField] private Color metricLabelColor = new Color(0.56f, 0.66f, 0.75f, 1f);
    [SerializeField] private Color scrapValueColor = new Color(1f, 0.71f, 0.4f, 1f);
    [SerializeField] private Color baseValueColor = new Color(0.45f, 0.91f, 1f, 1f);
    [SerializeField] private Color waveValueColor = new Color(1f, 0.85f, 0.47f, 1f);

    [Header("Text")]
    [SerializeField] private Color cardTextColor = new Color(0.96f, 0.98f, 1f, 1f);
    [SerializeField] private Color secondaryInfoColor = new Color(0.54f, 0.65f, 0.75f, 1f);
    [SerializeField] private Color statusTextColor = new Color(0.84f, 0.9f, 0.94f, 1f);

    [Header("Notices")]
    [SerializeField] private Color neutralNoticeColor = new Color(0.81f, 0.88f, 0.92f, 1f);
    [SerializeField] private Color positiveNoticeColor = new Color(0.49f, 0.95f, 0.69f, 1f);
    [SerializeField] private Color spendingNoticeColor = new Color(1f, 0.85f, 0.47f, 1f);
    [SerializeField] private Color warningNoticeColor = new Color(1f, 0.72f, 0.44f, 1f);
    [SerializeField] private Color dangerNoticeColor = new Color(1f, 0.55f, 0.5f, 1f);

    [Header("Drag Preview")]
    [SerializeField] private Color dragPreviewInfoColor = new Color(0.53f, 0.65f, 0.74f, 1f);
    [SerializeField] private Color dragPreviewValidColor = new Color(0.47f, 0.95f, 0.85f, 1f);
    [SerializeField] private Color dragPreviewInvalidColor = new Color(1f, 0.45f, 0.51f, 1f);
    [SerializeField] private Vector4 cardLabelMargin = new Vector4(108f, 18f, 24f, 18f);
    [SerializeField] private float cardLabelCharacterSpacing = 1.2f;
    [SerializeField] private float cardLabelLineSpacing = -10f;
    [SerializeField] private Vector2 dragPreviewPanelOffset = new Vector2(142f, -92f);

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
