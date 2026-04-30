using System;
using UnityEngine;

/// <summary>
/// `TowerPresentationCatalogAsset` 把塔的“展示配置”从场景总控里抽成可复用资产。
///
/// 这里刻意只放展示层稳定信息，例如：
/// - 展示名
/// - 卡片摘要
/// - 操作提示
/// - 强调色
/// - 图标与卡片配色
///
/// 而建造成本、放置半径、扩张边长这些仍然保留在玩法场景里，
/// 因为它们更像“每张地图的当前平衡参数”。
/// 这样拆开后：
/// - 一套塔展示风格可以被多个关卡场景共用
/// - 单关经济或放置参数仍然能独立调
/// </summary>
[CreateAssetMenu(
    fileName = "TowerPresentationCatalog",
    menuName = "Tower Defense/Presentation/Tower Presentation Catalog")]
public sealed class TowerPresentationCatalogAsset : ScriptableObject
{
    [Serializable]
    public sealed class TowerPresentationEntry
    {
        [SerializeField] private TowerType towerType = TowerType.None; // 中文：塔类型
        [SerializeField] private string displayName = "建筑"; // 中文：显示名称
        [SerializeField] private string cardRoleSummary = "职责摘要"; // 中文：卡片RoleSummary
        [SerializeField] private string selectionHint = "选择提示。"; // 中文：selection提示
        [SerializeField] private string upgradeFocusSummary = "升级方向摘要。"; // 中文：升级FocusSummary
        [SerializeField] private Color accentColor = Color.white; // 中文：accent颜色
        [SerializeField] private Sprite cardIconSprite; // 中文：卡片图标精灵
        [SerializeField] private Color cardIconTint = Color.white; // 中文：卡片图标Tint
        [SerializeField] private Color cardBackgroundTint = new Color(0.08f, 0.11f, 0.16f, 0.96f); // 中文：卡片背景Tint
        [SerializeField] private Color cardAccentTint = Color.white; // 中文：卡片AccentTint

        public TowerType TowerType => towerType; // 中文：塔类型
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "建筑" : displayName; // 中文：显示名称
        public string CardRoleSummary => string.IsNullOrWhiteSpace(cardRoleSummary) ? DisplayName : cardRoleSummary; // 中文：卡片RoleSummary
        public string SelectionHint => string.IsNullOrWhiteSpace(selectionHint) ? CardRoleSummary : selectionHint; // 中文：Selection提示
        public string UpgradeFocusSummary => string.IsNullOrWhiteSpace(upgradeFocusSummary) ? "升级会强化这座建筑。" : upgradeFocusSummary; // 中文：升级FocusSummary
        public Color AccentColor => accentColor; // 中文：Accent颜色
        public Sprite CardIconSprite => cardIconSprite; // 中文：卡片图标精灵
        public Color CardIconTint => cardIconTint; // 中文：卡片图标Tint
        public Color CardBackgroundTint => cardBackgroundTint; // 中文：卡片背景Tint
        public Color CardAccentTint => cardAccentTint; // 中文：卡片AccentTint
    }

    [SerializeField] private TowerPresentationEntry[] entries = Array.Empty<TowerPresentationEntry>(); // 中文：条目列表

    public TowerPresentationEntry[] Entries => entries ?? Array.Empty<TowerPresentationEntry>(); // 中文：条目列表

    public bool TryGetEntry(TowerType towerType, out TowerPresentationEntry entry)
    {
        if (entries != null)
        {
            for (int index = 0; index < entries.Length; index++)
            {
                TowerPresentationEntry candidate = entries[index];
                if (candidate != null && candidate.TowerType == towerType)
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = null;
        return false;
    }
}
