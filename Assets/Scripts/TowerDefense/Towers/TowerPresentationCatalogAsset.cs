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
        [SerializeField] private TowerType towerType = TowerType.None;
        [SerializeField] private string displayName = "Tower";
        [SerializeField] private string cardRoleSummary = "Role Summary";
        [SerializeField] private string selectionHint = "Selection hint.";
        [SerializeField] private string upgradeFocusSummary = "Upgrade summary.";
        [SerializeField] private Color accentColor = Color.white;
        [SerializeField] private Sprite cardIconSprite;
        [SerializeField] private Color cardIconTint = Color.white;
        [SerializeField] private Color cardBackgroundTint = new Color(0.08f, 0.11f, 0.16f, 0.96f);
        [SerializeField] private Color cardAccentTint = Color.white;

        public TowerType TowerType => towerType;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Tower" : displayName;
        public string CardRoleSummary => string.IsNullOrWhiteSpace(cardRoleSummary) ? DisplayName : cardRoleSummary;
        public string SelectionHint => string.IsNullOrWhiteSpace(selectionHint) ? CardRoleSummary : selectionHint;
        public string UpgradeFocusSummary => string.IsNullOrWhiteSpace(upgradeFocusSummary) ? "Upgrade improves this structure." : upgradeFocusSummary;
        public Color AccentColor => accentColor;
        public Sprite CardIconSprite => cardIconSprite;
        public Color CardIconTint => cardIconTint;
        public Color CardBackgroundTint => cardBackgroundTint;
        public Color CardAccentTint => cardAccentTint;
    }

    [SerializeField] private TowerPresentationEntry[] entries = Array.Empty<TowerPresentationEntry>();

    public TowerPresentationEntry[] Entries => entries ?? Array.Empty<TowerPresentationEntry>();

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
