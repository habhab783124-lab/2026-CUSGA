using System;
using UnityEngine;

/// <summary>
/// `EnemyArchetypeId` 描述当前塔防主玩法里可被波次系统刷出的敌人种类。
///
/// 这里先按当前文档确认过的种类收口：
/// - 基础杂兵
/// - 快速突破单位
/// - 护盾支援单位
/// - 修理支援单位
/// - 重甲机械单位
/// - 隐身单位
/// - 死亡分裂单位
/// - 由分裂产生的小型单位
///
/// 以后如果继续加敌人，不需要再把判断逻辑散在 `WaveSpawner` 和 `Enemy` 里，
/// 只需要在目录资产里补一份定义，再让波次引用这个类型即可。
/// </summary>
public enum EnemyArchetypeId
{
    None,
    Scavenger,
    Wolf,
    BannerScavenger,
    Mechanic,
    HeavyArmoredMachine,
    StealthStalker,
    Abomination,
    SmallScavenger
}

/// <summary>
/// `EnemyArmorTier` 是敌人护甲强度的轻量抽象。
///
/// 这里不做很重的伤害公式系统，
/// 只先把“无甲 / 轻甲 / 重甲”做成一个明确语义层，
/// 方便不同怪物在运行时走不同的减伤逻辑。
/// </summary>
public enum EnemyArmorTier
{
    None,
    Light,
    Heavy
}

/// <summary>
/// `EnemyCatalogAsset` 把敌人静态定义从 `Enemy` 和 `WaveSpawner` 里抽成共享资产。
///
/// 这份资产主要服务两件事：
/// 1. 波次系统按敌人类型刷怪时，能统一查到这类敌人的基础属性和特殊机制。
/// 2. 后续你或别人继续扩展怪物时，不需要再去翻很多脚本找散落常量。
/// </summary>
[CreateAssetMenu(
    fileName = "EnemyCatalog",
    menuName = "Tower Defense/Enemies/Enemy Catalog")]
public sealed class EnemyCatalogAsset : ScriptableObject
{
    [Serializable]
    public sealed class EnemyArchetypeDefinition
    {
        [Header("Identity")]
        [SerializeField] private EnemyArchetypeId archetypeId = EnemyArchetypeId.Scavenger;
        [SerializeField] private string displayName = "Enemy";

        [Header("Core Stats")]
        [SerializeField] [Min(1)] private int maxHealth = 3;
        [SerializeField] [Min(0.05f)] private float moveSpeed = 1.8f;
        [SerializeField] [Min(0)] private int scrapReward = 0;
        [SerializeField] [Min(1)] private int baseDamageToBase = 1;

        [Header("Armor")]
        [SerializeField] private EnemyArmorTier armorTier = EnemyArmorTier.None;
        [SerializeField] [Range(0.05f, 1f)] private float nonPiercingDamageMultiplier = 1f;

        [Header("Flags")]
        [SerializeField] private bool ignoresSlowEffects;
        [SerializeField] private bool canBeRepairedByMechanic;

        [Header("Visuals")]
        [SerializeField] private GameObject runtimePrefab;
        [SerializeField] private Sprite bodySpriteOverride;
        [SerializeField] private Color bodyColor = new Color(0.9f, 0.25f, 0.25f, 1f);
        [SerializeField] [Min(0.2f)] private float bodyScaleMultiplier = 1f;

        [Header("Shield Aura")]
        [SerializeField] [Min(0)] private int shieldAmount;
        [SerializeField] [Min(0.1f)] private float shieldAuraRadius = 1.8f;
        [SerializeField] [Min(0.1f)] private float shieldRefreshInterval = 0.45f;

        [Header("Repair Support")]
        [SerializeField] [Min(0)] private int repairAmount;
        [SerializeField] [Min(0.1f)] private float repairRadius = 2.1f;
        [SerializeField] [Min(0.1f)] private float repairCooldown = 2.5f;

        [Header("Stealth")]
        [SerializeField] private bool entersStealthAfterFirstDirectHit;
        [SerializeField] [Min(0.1f)] private float stealthDuration = 1.8f;
        [SerializeField] [Min(0.1f)] private float signalRevealDuration = 1.2f;
        [SerializeField] [Range(0.05f, 1f)] private float hiddenAlpha = 0.22f;

        [Header("Split On Death")]
        [SerializeField] private EnemyArchetypeId splitChildType = EnemyArchetypeId.None;
        [SerializeField] [Min(0)] private int splitChildCount;
        [SerializeField] [Min(0f)] private float splitSpawnRadius = 0.4f;

        public EnemyArchetypeId ArchetypeId => archetypeId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? archetypeId.ToString() : displayName;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        public float MoveSpeed => Mathf.Max(0.05f, moveSpeed);
        public int ScrapReward => Mathf.Max(0, scrapReward);
        public int BaseDamageToBase => Mathf.Max(1, baseDamageToBase);
        public EnemyArmorTier ArmorTier => armorTier;
        public float NonPiercingDamageMultiplier => Mathf.Clamp(nonPiercingDamageMultiplier, 0.05f, 1f);
        public bool IgnoresSlowEffects => ignoresSlowEffects;
        public bool CanBeRepairedByMechanic => canBeRepairedByMechanic;
        public GameObject RuntimePrefab => runtimePrefab;
        public Sprite BodySpriteOverride => bodySpriteOverride;
        public Color BodyColor => bodyColor;
        public float BodyScaleMultiplier => Mathf.Max(0.2f, bodyScaleMultiplier);
        public int ShieldAmount => Mathf.Max(0, shieldAmount);
        public float ShieldAuraRadius => Mathf.Max(0.1f, shieldAuraRadius);
        public float ShieldRefreshInterval => Mathf.Max(0.1f, shieldRefreshInterval);
        public int RepairAmount => Mathf.Max(0, repairAmount);
        public float RepairRadius => Mathf.Max(0.1f, repairRadius);
        public float RepairCooldown => Mathf.Max(0.1f, repairCooldown);
        public bool EntersStealthAfterFirstDirectHit => entersStealthAfterFirstDirectHit;
        public float StealthDuration => Mathf.Max(0.1f, stealthDuration);
        public float SignalRevealDuration => Mathf.Max(0.1f, signalRevealDuration);
        public float HiddenAlpha => Mathf.Clamp(hiddenAlpha, 0.05f, 1f);
        public EnemyArchetypeId SplitChildType => splitChildType;
        public int SplitChildCount => Mathf.Max(0, splitChildCount);
        public float SplitSpawnRadius => Mathf.Max(0f, splitSpawnRadius);
    }

    [SerializeField] private EnemyArchetypeDefinition[] definitions = Array.Empty<EnemyArchetypeDefinition>();

    public EnemyArchetypeDefinition[] Definitions => definitions ?? Array.Empty<EnemyArchetypeDefinition>();

    public bool TryGetDefinition(EnemyArchetypeId archetypeId, out EnemyArchetypeDefinition definition)
    {
        if (definitions != null)
        {
            for (int index = 0; index < definitions.Length; index++)
            {
                EnemyArchetypeDefinition candidate = definitions[index];
                if (candidate != null && candidate.ArchetypeId == archetypeId)
                {
                    definition = candidate;
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    public EnemyArchetypeDefinition GetDefinition(EnemyArchetypeId archetypeId)
    {
        TryGetDefinition(archetypeId, out EnemyArchetypeDefinition definition);
        return definition;
    }
}
