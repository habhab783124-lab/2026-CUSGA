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
    [InspectorName("未指定")]
    None,
    [InspectorName("拾荒者")]
    Scavenger,
    [InspectorName("狼")]
    Wolf,
    [InspectorName("旗帜拾荒者")]
    BannerScavenger,
    [InspectorName("机械师")]
    Mechanic,
    [InspectorName("重甲机械兵")]
    HeavyArmoredMachine,
    [InspectorName("隐身追猎者")]
    StealthStalker,
    [InspectorName("憎恶")]
    Abomination,
    [InspectorName("小拾荒者")]
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
    [InspectorName("无甲")]
    None,
    [InspectorName("轻甲")]
    Light,
    [InspectorName("重甲")]
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
        [Header("基础信息")]
        [SerializeField, InspectorName("敌人类型")] private EnemyArchetypeId archetypeId = EnemyArchetypeId.Scavenger; // 中文：原型类别标识
        [SerializeField, InspectorName("显示名称")] private string displayName = "Enemy"; // 中文：显示名称

        [Header("核心属性")]
        [SerializeField, Min(1), InspectorName("生命值")] private int maxHealth = 3; // 中文：最大生命
        [SerializeField, Min(0.05f), InspectorName("移动速度")] private float moveSpeed = 1.8f; // 中文：move速度
        [SerializeField, Min(0), InspectorName("死亡废料奖励")] private int scrapReward = 0; // 中文：废料Reward
        [SerializeField, Min(1), InspectorName("到点伤害")] private int baseDamageToBase = 1; // 中文：基础伤害到基础

        [Header("护甲")]
        [SerializeField, InspectorName("护甲等级")] private EnemyArmorTier armorTier = EnemyArmorTier.None; // 中文：armorTier
        [SerializeField, Range(0.05f, 1f), InspectorName("非穿甲伤害倍率")] private float nonPiercingDamageMultiplier = 1f; // 中文：nonPiercing伤害倍率

        [Header("特性开关")]
        [SerializeField, InspectorName("免疫减速")] private bool ignoresSlowEffects; // 中文：ignores减速Effects
        [SerializeField, InspectorName("可被机械师修理")] private bool canBeRepairedByMechanic; // 中文：能否BeRepairedBy机制

        [Header("外观")]
        [SerializeField, InspectorName("运行时 Prefab")] private GameObject runtimePrefab; // 中文：运行时预制体
        [SerializeField, InspectorName("主体精灵覆盖")] private Sprite bodySpriteOverride; // 中文：主体精灵覆盖
        [SerializeField, InspectorName("主体颜色")] private Color bodyColor = new Color(0.9f, 0.25f, 0.25f, 1f); // 中文：主体颜色
        [SerializeField, Min(0.2f), InspectorName("主体缩放倍率")] private float bodyScaleMultiplier = 1f; // 中文：主体缩放倍率

        [Header("护盾光环")]
        [SerializeField, Min(0), InspectorName("护盾值")] private int shieldAmount; // 中文：护盾数量
        [SerializeField, Min(0.1f), InspectorName("光环半径")] private float shieldAuraRadius = 1.8f; // 中文：护盾光环半径
        [SerializeField, Min(0.1f), InspectorName("刷新间隔")] private float shieldRefreshInterval = 0.45f; // 中文：护盾刷新间隔

        [Header("修理支援")]
        [SerializeField, Min(0), InspectorName("修理量")] private int repairAmount; // 中文：修理数量
        [SerializeField, Min(0.1f), InspectorName("修理半径")] private float repairRadius = 2.1f; // 中文：修理半径
        [SerializeField, Min(0.1f), InspectorName("修理冷却")] private float repairCooldown = 2.5f; // 中文：修理冷却

        [Header("隐身")]
        [SerializeField, InspectorName("首次直击后进入隐身")] private bool entersStealthAfterFirstDirectHit; // 中文：enters隐身AfterFirstDirectHit
        [SerializeField, Min(0.1f), InspectorName("隐身持续时间")] private float stealthDuration = 1.8f; // 中文：隐身持续时间
        [SerializeField, Min(0.1f), InspectorName("显形持续时间")] private float signalRevealDuration = 1.2f; // 中文：signalReveal持续时间
        [SerializeField, Range(0.05f, 1f), InspectorName("隐身透明度")] private float hiddenAlpha = 0.22f; // 中文：hidden透明度

        [Header("死亡分裂")]
        [SerializeField, InspectorName("子怪类型")] private EnemyArchetypeId splitChildType = EnemyArchetypeId.None; // 中文：分裂Child类型
        [SerializeField, Min(0), InspectorName("子怪数量")] private int splitChildCount; // 中文：分裂Child数量
        [SerializeField, Min(0f), InspectorName("子怪生成半径")] private float splitSpawnRadius = 0.4f; // 中文：分裂出怪半径

        public EnemyArchetypeId ArchetypeId => archetypeId; // 中文：原型类别标识
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? archetypeId.ToString() : displayName; // 中文：显示名称
        public int MaxHealth => Mathf.Max(1, maxHealth); // 中文：最大生命
        public float MoveSpeed => Mathf.Max(0.05f, moveSpeed); // 中文：Move速度
        public int ScrapReward => Mathf.Max(0, scrapReward); // 中文：废料Reward
        public int BaseDamageToBase => Mathf.Max(1, baseDamageToBase); // 中文：基础伤害到基础
        public EnemyArmorTier ArmorTier => armorTier; // 中文：ArmorTier
        public float NonPiercingDamageMultiplier => Mathf.Clamp(nonPiercingDamageMultiplier, 0.05f, 1f); // 中文：NonPiercing伤害倍率
        public bool IgnoresSlowEffects => ignoresSlowEffects; // 中文：Ignores减速Effects
        public bool CanBeRepairedByMechanic => canBeRepairedByMechanic; // 中文：能否BeRepairedBy机制
        public GameObject RuntimePrefab => runtimePrefab; // 中文：运行时预制体
        public Sprite BodySpriteOverride => bodySpriteOverride; // 中文：主体精灵覆盖
        public Color BodyColor => bodyColor; // 中文：主体颜色
        public float BodyScaleMultiplier => Mathf.Max(0.2f, bodyScaleMultiplier); // 中文：主体缩放倍率
        public int ShieldAmount => Mathf.Max(0, shieldAmount); // 中文：护盾数量
        public float ShieldAuraRadius => Mathf.Max(0.1f, shieldAuraRadius); // 中文：护盾光环半径
        public float ShieldRefreshInterval => Mathf.Max(0.1f, shieldRefreshInterval); // 中文：护盾刷新间隔
        public int RepairAmount => Mathf.Max(0, repairAmount); // 中文：修理数量
        public float RepairRadius => Mathf.Max(0.1f, repairRadius); // 中文：修理半径
        public float RepairCooldown => Mathf.Max(0.1f, repairCooldown); // 中文：修理冷却
        public bool EntersStealthAfterFirstDirectHit => entersStealthAfterFirstDirectHit; // 中文：Enters隐身AfterFirstDirectHit
        public float StealthDuration => Mathf.Max(0.1f, stealthDuration); // 中文：隐身持续时间
        public float SignalRevealDuration => Mathf.Max(0.1f, signalRevealDuration); // 中文：SignalReveal持续时间
        public float HiddenAlpha => Mathf.Clamp(hiddenAlpha, 0.05f, 1f); // 中文：Hidden透明度
        public EnemyArchetypeId SplitChildType => splitChildType; // 中文：分裂Child类型
        public int SplitChildCount => Mathf.Max(0, splitChildCount); // 中文：分裂Child数量
        public float SplitSpawnRadius => Mathf.Max(0f, splitSpawnRadius); // 中文：分裂出怪半径
    }

    [SerializeField, InspectorName("敌人定义列表")] private EnemyArchetypeDefinition[] definitions = Array.Empty<EnemyArchetypeDefinition>(); // 中文：定义列表

    public EnemyArchetypeDefinition[] Definitions => definitions ?? Array.Empty<EnemyArchetypeDefinition>(); // 中文：定义列表

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
