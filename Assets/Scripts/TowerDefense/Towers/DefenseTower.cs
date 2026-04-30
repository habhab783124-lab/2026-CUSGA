using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// `DefenseTower` is the shared combat-tower runtime bridge.
///
/// We intentionally keep one scene prototype and let `BuildType` decide the concrete behavior,
/// because this keeps the scene easier to author and also makes later art replacement cheaper:
/// the user can swap sprites and visuals in Inspector without needing a different gameplay script
/// for every tower family.
///
/// This file now owns three things together:
/// 1. Type-specific combat behavior.
/// 2. Type-specific upgrade growth.
/// 3. Lightweight runtime feedback for bombard towers.
///
/// Keeping these three responsibilities together is useful at this stage of the project,
/// because "how a tower upgrades" and "how a tower attacks" are tightly coupled pieces of one design.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(PlacedTower))]
public class DefenseTower : MonoBehaviour
{
    /// <summary>
    /// `CombatTuning` groups the authoring knobs for one combat family.
    ///
    /// It is nested on purpose:
    /// - the Inspector can still show a clean grouped block per tower family
    /// - scene authors can tune values directly on the prototype
    /// - we avoid introducing a heavier data-asset layer before the design is stable
    /// </summary>
    [System.Serializable]
    private sealed class CombatTuning
    {
        [Header("攻击")]
        [Min(0.1f), InspectorName("攻击范围")] public float attackRange = 2.8f; // 中文：攻击范围
        [InspectorName("每级攻击范围增量")] public float attackRangePerUpgrade = 0.2f; // 中文：攻击范围Per升级
        [Min(0.05f), InspectorName("攻击间隔")] public float attackInterval = 0.65f; // 中文：攻击间隔
        [InspectorName("每级攻击间隔改变量")] public float attackIntervalPerUpgradeDelta = -0.06f; // 中文：攻击间隔Per升级Delta
        [Min(0), InspectorName("基础伤害")] public int baseDamage = 1; // 中文：基础伤害
        [Min(0), InspectorName("每级伤害增量")] public int damagePerUpgrade = 1; // 中文：伤害Per升级
        [InspectorName("弹道 Prefab")] public GameObject shotTracePrefab = null; // 中文：shot轨迹预制体
        [InspectorName("弹道精灵")] public Sprite shotTraceSprite = null; // 中文：shot轨迹精灵
        [InspectorName("弹道颜色")] public Color shotTraceColor = new Color(0.68f, 0.9f, 1f, 0.92f); // 中文：shot轨迹颜色
        [Min(0.02f), InspectorName("弹道粗细")] public float shotTraceThickness = 0.1f; // 中文：shot轨迹Thickness
        [Min(0.02f), InspectorName("弹道持续时间")] public float shotTraceDuration = 0.08f; // 中文：shot轨迹持续时间

        [Header("供电")]
        [Min(0), InspectorName("基础耗电")] public int basePowerRequired = 2; // 中文：基础供电Required
        [Min(0), InspectorName("每级耗电增量")] public int powerRequiredPerUpgrade = 1; // 中文：供电RequiredPer升级

        [Header("升级费用")]
        [Min(0), InspectorName("升级基础费用")] public int upgradeCostBase = 30; // 中文：升级费用基础
        [Min(0), InspectorName("每级升级增量")] public int upgradeCostPerLevel = 15; // 中文：升级费用Per等级

        [Header("减速场")]
        [Range(0.15f, 1f), InspectorName("减速倍率")] public float slowMultiplier = 0.65f; // 中文：减速倍率
        [InspectorName("每级减速倍率改变量")] public float slowMultiplierPerUpgradeDelta = -0.05f; // 中文：减速倍率Per升级Delta
        [Min(0f), InspectorName("减速持续时间")] public float slowDuration = 1.1f; // 中文：减速持续时间
        [InspectorName("每级减速时长增量")] public float slowDurationPerUpgrade = 0.2f; // 中文：减速持续时间Per升级
        [InspectorName("减速脉冲 Prefab")] public GameObject slowPulsePrefab = null; // 中文：减速脉冲预制体
        [InspectorName("减速脉冲精灵")] public Sprite slowPulseSprite = null; // 中文：减速脉冲精灵
        [InspectorName("减速脉冲颜色")] public Color slowPulseColor = new Color(0.36f, 0.95f, 0.84f, 0.28f); // 中文：减速脉冲颜色
        [Min(0.05f), InspectorName("减速脉冲时长")] public float slowPulseDuration = 0.18f; // 中文：减速脉冲持续时间
        [Min(0.05f), InspectorName("减速脉冲起始缩放")] public float slowPulseStartScale = 0.2f; // 中文：减速脉冲开始缩放
        [Min(0.1f), InspectorName("减速脉冲缩放倍率")] public float slowPulseScaleMultiplier = 2.1f; // 中文：减速脉冲缩放倍率

        [Header("炸弹")]
        [Min(0.05f), InspectorName("飞行时间")] public float bombFlightTime = 0.45f; // 中文：炸弹飞行时间
        [InspectorName("每级飞行时间改变量")] public float bombFlightTimePerUpgradeDelta = -0.04f; // 中文：炸弹飞行时间Per升级Delta
        [Min(0.1f), InspectorName("爆炸半径")] public float bombRadius = 1.2f; // 中文：炸弹半径
        [InspectorName("每级爆炸半径增量")] public float bombRadiusPerUpgrade = 0.2f; // 中文：炸弹半径Per升级
        [Min(0f), InspectorName("抛物线高度")] public float bombArcHeight = 0.5f; // 中文：炸弹弧线Height
        [Min(0.05f), InspectorName("投射物缩放")] public float bombProjectileScale = 0.18f; // 中文：炸弹投射物缩放
        [Min(0.05f), InspectorName("爆炸持续时间")] public float bombExplosionDuration = 0.24f; // 中文：炸弹爆炸持续时间
        [Min(0.1f), InspectorName("爆炸缩放倍率")] public float bombExplosionScaleMultiplier = 1.45f; // 中文：炸弹爆炸缩放倍率
        [InspectorName("炸弹投射物 Prefab")] public GameObject bombProjectilePrefab = null; // 中文：炸弹投射物预制体
        [InspectorName("炸弹爆炸 Prefab")] public GameObject bombExplosionPrefab = null; // 中文：炸弹爆炸预制体
        [InspectorName("投射物精灵")] public Sprite bombProjectileSprite = null; // 中文：炸弹投射物精灵
        [InspectorName("爆炸精灵")] public Sprite bombExplosionSprite = null; // 中文：炸弹爆炸精灵
        [InspectorName("投射物颜色")] public Color bombProjectileColor = new Color(1f, 0.76f, 0.34f, 1f); // 中文：炸弹投射物颜色
        [InspectorName("爆炸颜色")] public Color bombExplosionColor = new Color(1f, 0.54f, 0.2f, 0.9f); // 中文：炸弹爆炸颜色

        [Header("外观")]
        [InspectorName("主体精灵")] public Sprite bodySprite = null; // 中文：主体精灵
        [InspectorName("通电颜色")] public Color poweredColor = new Color(0.2f, 0.55f, 1f, 1f); // 中文：powered颜色

        [Header("塔型签名")]
        [InspectorName("签名精灵")] public Sprite signatureSprite = null; // 中文：签名精灵
        [InspectorName("签名颜色")] public Color signatureColor = new Color(1f, 1f, 1f, 0.9f); // 中文：签名颜色
        [InspectorName("签名偏移")] public Vector2 signatureOffset = Vector2.zero; // 中文：签名偏移
        [InspectorName("签名基础缩放")] public Vector2 signatureBaseScale = new Vector2(0.25f, 0.25f); // 中文：签名基础缩放
        [InspectorName("每范围缩放增量")] public Vector2 signatureScalePerRange = Vector2.zero; // 中文：签名缩放Per范围
        [InspectorName("签名初始旋转角")] public float signatureRotationDegrees = 0f; // 中文：签名旋转Degrees
        [InspectorName("签名旋转速度")] public float signatureRotationSpeed = 0f; // 中文：签名旋转速度
        [InspectorName("签名脉冲幅度")] public float signaturePulseAmplitude = 0f; // 中文：签名脉冲Amplitude
        [InspectorName("签名脉冲速度")] public float signaturePulseSpeed = 2f; // 中文：签名脉冲速度
        [InspectorName("签名上下浮动幅度")] public float signatureVerticalBobAmplitude = 0f; // 中文：签名VerticalBobAmplitude
        [InspectorName("签名上下浮动速度")] public float signatureVerticalBobSpeed = 2f; // 中文：签名VerticalBob速度
    }

    [Header("塔型")]
    [SerializeField, InspectorName("建造类型")] private TowerType buildType = TowerType.SingleTarget; // 中文：建造类型

    [Header("参数配置")]
    [SerializeField, InspectorName("单体塔参数")] private CombatTuning singleTargetTuning = new CombatTuning // 中文：单体目标Tuning
    {
        attackRange = 2.8f,
        attackRangePerUpgrade = 0.25f,
        attackInterval = 0.72f,
        attackIntervalPerUpgradeDelta = -0.08f,
        baseDamage = 1,
        damagePerUpgrade = 1,
        basePowerRequired = 2,
        powerRequiredPerUpgrade = 1,
        upgradeCostBase = 26,
        upgradeCostPerLevel = 14,
        poweredColor = new Color(0.2f, 0.55f, 1f, 1f),
        signatureColor = new Color(0.42f, 0.86f, 1f, 0.92f),
        signatureOffset = new Vector2(0f, -0.5f),
        signatureBaseScale = new Vector2(0.55f, 0.08f),
        signaturePulseAmplitude = 0.08f,
        signaturePulseSpeed = 5.2f
    };

    [SerializeField, InspectorName("减速塔参数")] private CombatTuning slowFieldTuning = new CombatTuning // 中文：减速区域Tuning
    {
        attackRange = 2.35f,
        attackRangePerUpgrade = 0.3f,
        attackInterval = 1.0f,
        attackIntervalPerUpgradeDelta = -0.05f,
        baseDamage = 0,
        damagePerUpgrade = 1,
        basePowerRequired = 3,
        powerRequiredPerUpgrade = 1,
        upgradeCostBase = 34,
        upgradeCostPerLevel = 16,
        slowMultiplier = 0.7f,
        slowMultiplierPerUpgradeDelta = -0.08f,
        slowDuration = 1.25f,
        slowDurationPerUpgrade = 0.25f,
        poweredColor = new Color(0.32f, 0.92f, 0.82f, 1f),
        signatureColor = new Color(0.3f, 0.95f, 0.84f, 0.18f),
        signatureOffset = new Vector2(0f, -0.04f),
        signatureBaseScale = new Vector2(0.45f, 0.45f),
        signatureScalePerRange = new Vector2(0.48f, 0.48f),
        signaturePulseAmplitude = 0.1f,
        signaturePulseSpeed = 2.4f
    };

    [SerializeField, InspectorName("炸弹塔参数")] private CombatTuning bombardTuning = new CombatTuning // 中文：炸弹Tuning
    {
        attackRange = 3.4f,
        attackRangePerUpgrade = 0.35f,
        attackInterval = 1.5f,
        attackIntervalPerUpgradeDelta = -0.12f,
        baseDamage = 2,
        damagePerUpgrade = 2,
        basePowerRequired = 4,
        powerRequiredPerUpgrade = 1,
        upgradeCostBase = 44,
        upgradeCostPerLevel = 20,
        bombFlightTime = 0.55f,
        bombFlightTimePerUpgradeDelta = -0.05f,
        bombRadius = 1.15f,
        bombRadiusPerUpgrade = 0.3f,
        bombArcHeight = 0.6f,
        bombProjectileScale = 0.2f,
        bombExplosionDuration = 0.28f,
        bombExplosionScaleMultiplier = 1.6f,
        bombProjectileColor = new Color(1f, 0.74f, 0.34f, 1f),
        bombExplosionColor = new Color(1f, 0.5f, 0.22f, 0.92f),
        poweredColor = new Color(1f, 0.56f, 0.24f, 1f),
        signatureColor = new Color(1f, 0.72f, 0.36f, 0.92f),
        signatureOffset = new Vector2(0f, 0.56f),
        signatureBaseScale = new Vector2(0.22f, 0.22f),
        signatureRotationDegrees = 45f,
        signatureRotationSpeed = 46f,
        signaturePulseAmplitude = 0.12f,
        signaturePulseSpeed = 3.6f,
        signatureVerticalBobAmplitude = 0.05f,
        signatureVerticalBobSpeed = 3.1f
    };

    [Header("成长")]
    [SerializeField, InspectorName("当前等级")] private int currentLevel = 1; // 中文：当前等级
    [SerializeField, InspectorName("最大等级")] private int maxLevel = 3; // 中文：最大等级

    [Header("视觉引用")]

    /// <summary>
    /// 塔本体的主渲染器。
    ///
    /// 如果你后续把塔做成更复杂的层级，
    /// 这里可以显式指定“哪一个 SpriteRenderer 才代表主塔身”。
    /// </summary>
    [SerializeField, InspectorName("主体渲染器")] private SpriteRenderer bodyRendererReference; // 中文：主体Renderer引用

    /// <summary>
    /// 所有运行时反馈对象的挂点。
    ///
    /// 这样炸弹、爆炸、脉冲和 tracer 不会再默认挂到塔根节点上乱长，
    /// 也更方便后续整体替换或隐藏这一层效果。
    /// </summary>
    [SerializeField, InspectorName("反馈根节点")] private Transform feedbackRootReference; // 中文：反馈根节点引用

    /// <summary>
    /// 单体塔反馈的专用挂点。
    /// 这样 `tracer` 的起点可以独立调整，而不是总从塔中心生硬飞出。
    /// </summary>
    [SerializeField, InspectorName("单体塔反馈根")] private Transform singleTargetFeedbackRootReference; // 中文：单体目标反馈根节点引用

    /// <summary>
    /// 减速塔反馈的专用挂点。
    /// </summary>
    [SerializeField, InspectorName("减速塔反馈根")] private Transform slowFieldFeedbackRootReference; // 中文：减速区域反馈根节点引用

    /// <summary>
    /// 炸弹塔反馈的专用挂点。
    /// 这样投射物和爆炸可以围绕更明确的视觉锚点展开。
    /// </summary>
    [SerializeField, InspectorName("炸弹塔反馈根")] private Transform bombardFeedbackRootReference; // 中文：炸弹反馈根节点引用

    /// <summary>
    /// 塔型签名的挂点。
    /// </summary>
    [SerializeField, InspectorName("塔型签名根")] private Transform typeSignatureRootReference; // 中文：类型签名根节点引用

    /// <summary>
    /// 等级标记的挂点。
    /// </summary>
    [SerializeField, InspectorName("等级标记根")] private Transform levelMarkerRootReference; // 中文：等级标记根节点引用

    [Header("共享视觉")]
    [SerializeField, InspectorName("受击闪光颜色")] private Color flashColor = Color.white; // 中文：闪光颜色
    [SerializeField, InspectorName("断电颜色")] private Color offlineColor = new Color(0.24f, 0.28f, 0.36f, 1f); // 中文：离线颜色
    [SerializeField, InspectorName("受击闪光时长")] private float flashDuration = 0.06f; // 中文：闪光持续时间
    [SerializeField, InspectorName("升级闪光颜色")] private Color upgradeFlashColor = new Color(1f, 0.96f, 0.68f, 1f); // 中文：升级闪光颜色
    [SerializeField, InspectorName("升级脉冲时长")] private float upgradePulseDuration = 0.18f; // 中文：升级脉冲持续时间
    [SerializeField, InspectorName("升级缩放倍率")] private float upgradeScaleMultiplier = 1.14f; // 中文：升级缩放倍率
    [SerializeField, InspectorName("反馈材质")] private Material feedbackMaterial; // 中文：反馈材质

    [Header("等级标记")]
    [SerializeField, InspectorName("等级点精灵")] private Sprite levelPipSprite = null; // 中文：等级等级点精灵
    [SerializeField, InspectorName("等级点颜色")] private Color levelPipColor = new Color(0.98f, 0.96f, 0.78f, 1f); // 中文：等级等级点颜色
    [SerializeField, InspectorName("等级点偏移")] private Vector2 levelPipOffset = new Vector2(0f, -0.65f); // 中文：等级等级点偏移
    [SerializeField, InspectorName("等级点间距")] private float levelPipSpacing = 0.22f; // 中文：等级等级点间距
    [SerializeField, InspectorName("等级点缩放")] private float levelPipScale = 0.12f; // 中文：等级等级点缩放
    [SerializeField, InspectorName("等级点排序偏移")] private int levelPipSortingOffset = 3; // 中文：等级等级点Sorting偏移

    private static Sprite s_runtimeFallbackSprite; // 中文：运行时Fallback精灵

    private readonly List<GameObject> _activeFeedbackObjects = new List<GameObject>(4); // 中文：激活反馈Objects
    private readonly List<SpriteRenderer> _levelPipRenderers = new List<SpriteRenderer>(4); // 中文：等级等级点Renderers
    private SpriteRenderer _spriteRenderer; // 中文：精灵Renderer
    private SpriteRenderer _typeSignatureRenderer; // 中文：类型签名Renderer
    private float _attackTimer; // 中文：攻击计时器
    private Sprite _defaultBodySprite; // 中文：默认主体精灵

    public int TowerNumber { get; private set; } = 100; // 中文：塔Number
    public TowerType BuildType => buildType; // 中文：建造类型
    public int CurrentLevel => Mathf.Max(1, currentLevel); // 中文：当前等级
    public int MaxLevel => Mathf.Max(1, maxLevel); // 中文：最大等级
    public int DamagePerShot => EvaluateDamage(CurrentLevel); // 中文：伤害PerShot
    public int PowerRequired => EvaluatePowerRequired(CurrentLevel); // 中文：供电Required
    public float AttackRange => EvaluateAttackRange(CurrentLevel); // 中文：攻击范围
    public float AttackInterval => EvaluateAttackInterval(CurrentLevel); // 中文：攻击间隔
    public float SlowMultiplier => EvaluateSlowMultiplier(CurrentLevel); // 中文：减速倍率
    public float SlowDuration => EvaluateSlowDuration(CurrentLevel); // 中文：减速持续时间
    public float BombFlightTime => EvaluateBombFlightTime(CurrentLevel); // 中文：炸弹飞行时间
    public float BombRadius => EvaluateBombRadius(CurrentLevel); // 中文：炸弹半径
    public bool IsPowered { get; private set; } = true; // 中文：是否Powered
    public RelayTower AssignedRelay { get; private set; } // 中文：已分配继电器
    public string PowerStatusMessage { get; private set; } = "等待供电结算。"; // 中文：供电状态消息

    private CombatTuning ActiveTuning
    {
        get
        {
            switch (buildType)
            {
                case TowerType.SlowField:
                    return slowFieldTuning;

                case TowerType.Bombard:
                    return bombardTuning;

                default:
                    return singleTargetTuning;
            }
        }
    }

    private static Sprite RuntimeFallbackSprite
    {
        get
        {
            if (s_runtimeFallbackSprite == null)
            {
                Texture2D sourceTexture = Texture2D.whiteTexture;
                s_runtimeFallbackSprite = Sprite.Create(
                    sourceTexture,
                    new Rect(0f, 0f, sourceTexture.width, sourceTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                s_runtimeFallbackSprite.name = "RuntimeFallbackSprite";
            }

            return s_runtimeFallbackSprite;
        }
    }

    /// <summary>
    /// The prototype keeps a stable default color until a specific combat family is assigned.
    /// </summary>
    private void Awake()
    {
        _spriteRenderer = bodyRendererReference != null ? bodyRendererReference : GetComponent<SpriteRenderer>();
        bodyRendererReference = _spriteRenderer;
        _defaultBodySprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
        EnsureTypeSignatureRenderer();
        RefreshVisualState();
        RefreshLevelMarkerVisual();
    }

    /// <summary>
    /// 在编辑器里尽量把最关键的视觉引用自动补齐。
    /// 这样用户后续调整塔层级时，也更容易看清该拖哪些入口。
    /// </summary>
    private void OnValidate()
    {
        if (bodyRendererReference == null)
        {
            bodyRendererReference = GetComponent<SpriteRenderer>();
        }

        if (!Application.isPlaying && bodyRendererReference != null && _defaultBodySprite == null)
        {
            _defaultBodySprite = bodyRendererReference.sprite;
        }

        CleanupUnusedFeedbackRoots(immediate: true);
    }

    /// <summary>
    /// `Update()` only advances combat when the tower is both online and the match is still active.
    /// This keeps the offline rule very explicit: the tower stays in the scene, but its attack loop stops.
    /// </summary>
    private void Update()
    {
        UpdateTypeSignatureVisual();

        if (TowerDefenseGame.Instance == null || TowerDefenseGame.Instance.IsGameOver || !IsPowered)
        {
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer < AttackInterval)
        {
            return;
        }

        _attackTimer -= AttackInterval;

        switch (buildType)
        {
            case TowerType.SlowField:
                ExecuteSlowFieldAttack();
                break;

            case TowerType.Bombard:
                ExecuteBombardAttack();
                break;

            default:
                ExecuteSingleTargetAttack();
                break;
        }
    }

    public void AssignTowerNumber(int towerNumber)
    {
        TowerNumber = Mathf.Clamp(towerNumber, 1, 100);
    }

    public void ConfigureBuildType(TowerType towerType)
    {
        buildType = TowerTypeUtility.IsCombatTower(towerType) ? towerType : TowerType.SingleTarget;
        CleanupUnusedFeedbackRoots(immediate: false);
        EnsureTypedFeedbackRoot(buildType);
        RefreshVisualState();
        RefreshLevelMarkerVisual();
    }

    public void SetPowerState(bool isPowered, RelayTower assignedRelay, string powerStatusMessage)
    {
        IsPowered = isPowered;
        AssignedRelay = assignedRelay;
        PowerStatusMessage = string.IsNullOrWhiteSpace(powerStatusMessage)
            ? (isPowered ? "已通电并正常运作。" : "当前离线。")
            : powerStatusMessage;
        RefreshVisualState();
        RefreshLevelMarkerVisual();
    }

    public bool CanUpgrade => CurrentLevel < MaxLevel; // 中文：能否升级

    public int GetUpgradeCost()
    {
        return ActiveTuning.upgradeCostBase + (CurrentLevel - 1) * ActiveTuning.upgradeCostPerLevel;
    }

    public int PreviewUpgradedPowerRequired()
    {
        return CanUpgrade ? EvaluatePowerRequired(CurrentLevel + 1) : PowerRequired;
    }

    public int PreviewUpgradedDamagePerShot()
    {
        return CanUpgrade ? EvaluateDamage(CurrentLevel + 1) : DamagePerShot;
    }

    public float PreviewUpgradedAttackRange()
    {
        return CanUpgrade ? EvaluateAttackRange(CurrentLevel + 1) : AttackRange;
    }

    public float PreviewUpgradedAttackInterval()
    {
        return CanUpgrade ? EvaluateAttackInterval(CurrentLevel + 1) : AttackInterval;
    }

    public float PreviewUpgradedSlowMultiplier()
    {
        return CanUpgrade ? EvaluateSlowMultiplier(CurrentLevel + 1) : SlowMultiplier;
    }

    public float PreviewUpgradedSlowDuration()
    {
        return CanUpgrade ? EvaluateSlowDuration(CurrentLevel + 1) : SlowDuration;
    }

    public float PreviewUpgradedBombFlightTime()
    {
        return CanUpgrade ? EvaluateBombFlightTime(CurrentLevel + 1) : BombFlightTime;
    }

    public float PreviewUpgradedBombRadius()
    {
        return CanUpgrade ? EvaluateBombRadius(CurrentLevel + 1) : BombRadius;
    }

    /// <summary>
    /// The HUD asks the tower itself for its current combat summary,
    /// so the selection panel does not need to duplicate tower-type branching logic.
    /// </summary>
    public string BuildCurrentCombatSummary()
    {
        switch (buildType)
        {
            case TowerType.SlowField:
                return $"伤害 {DamagePerShot} / 减速 {GetSlowPercent(SlowMultiplier):0}% / 持续 {SlowDuration:0.00}s / 攻速 {AttackInterval:0.00}s / 射程 {AttackRange:0.0}";

            case TowerType.Bombard:
                return $"伤害 {DamagePerShot} / 爆炸 {BombRadius:0.0} / 飞行 {BombFlightTime:0.00}s / 攻速 {AttackInterval:0.00}s / 射程 {AttackRange:0.0}";

            default:
                return $"伤害 {DamagePerShot} / 攻速 {AttackInterval:0.00}s / 射程 {AttackRange:0.0}";
        }
    }

    /// <summary>
    /// The next-level summary is also type-aware.
    /// This makes upgrade interaction clearer: the player sees what actually changes for this family.
    /// </summary>
    public string BuildUpgradePreviewSummary()
    {
        if (!CanUpgrade)
        {
            return "已经达到最大等级。";
        }

        switch (buildType)
        {
            case TowerType.SlowField:
                return
                    $"升级后伤害 {PreviewUpgradedDamagePerShot()} / 减速 {GetSlowPercent(PreviewUpgradedSlowMultiplier()):0}% / 持续 {PreviewUpgradedSlowDuration():0.00}s / 攻速 {PreviewUpgradedAttackInterval():0.00}s / 耗电 {PreviewUpgradedPowerRequired()}";

            case TowerType.Bombard:
                return
                    $"升级后伤害 {PreviewUpgradedDamagePerShot()} / 爆炸 {PreviewUpgradedBombRadius():0.0} / 飞行 {PreviewUpgradedBombFlightTime():0.00}s / 攻速 {PreviewUpgradedAttackInterval():0.00}s / 耗电 {PreviewUpgradedPowerRequired()}";

            default:
                return
                    $"升级后伤害 {PreviewUpgradedDamagePerShot()} / 攻速 {PreviewUpgradedAttackInterval():0.00}s / 射程 {PreviewUpgradedAttackRange():0.0} / 耗电 {PreviewUpgradedPowerRequired()}";
        }
    }

    public void ApplyUpgrade()
    {
        if (!CanUpgrade)
        {
            return;
        }

        currentLevel++;
        RefreshVisualState();
        RefreshLevelMarkerVisual();
        StartCoroutine(UpgradePulseRoutine());
    }

    private void ExecuteSingleTargetAttack()
    {
        Enemy target = FindClosestTarget(AttackRange);
        if (target == null)
        {
            return;
        }

        target.TakeDamage(
            DamagePerShot,
            Enemy.DamageFeedbackType.Standard,
            isArmorPiercing: DoesCurrentTowerUseArmorPiercingDamage(),
            isAreaDamage: false);
        StartCoroutine(FlashRoutine());
        StartCoroutine(PlayTracerFeedback(target.transform.position));
    }

    private void ExecuteSlowFieldAttack()
    {
        bool affectedAnyEnemy = false;
        float maxDistanceSqr = AttackRange * AttackRange;

        for (int enemyIndex = 0; enemyIndex < Enemy.ActiveEnemyCount; enemyIndex++)
        {
            Enemy enemy = Enemy.GetActiveEnemy(enemyIndex);
            if (enemy == null)
            {
                continue;
            }

            float distanceSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr)
            {
                continue;
            }

            enemy.ApplyDetection(SlowDuration);
            enemy.ApplySlow(SlowMultiplier, SlowDuration);
            enemy.TakeDamage(
                DamagePerShot,
                Enemy.DamageFeedbackType.SlowField,
                isArmorPiercing: false,
                isAreaDamage: true);
            affectedAnyEnemy = true;
        }

        if (affectedAnyEnemy)
        {
            StartCoroutine(FlashRoutine());
            StartCoroutine(PlaySlowPulseFeedback());
        }
    }

    private void ExecuteBombardAttack()
    {
        Enemy target = FindClosestTarget(AttackRange);
        if (target == null)
        {
            return;
        }

        StartCoroutine(FlashRoutine());
        StartCoroutine(BombardRoutine(target.transform.position));
    }

    private IEnumerator BombardRoutine(Vector3 targetPosition)
    {
        GameObject projectile = CreateFeedbackObject(
            "BombProjectile",
            ActiveTuning.bombProjectilePrefab,
            ActiveTuning.bombProjectileSprite,
            ActiveTuning.bombProjectileColor,
            ActiveTuning.bombProjectileScale,
            12,
            GetCurrentFeedbackRoot(),
            GetCurrentFeedbackOrigin());

        Vector3 projectileStart = GetCurrentFeedbackOrigin();
        float flightDuration = BombFlightTime;
        if (projectile != null)
        {
            float elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.deltaTime;
                float progress = flightDuration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / flightDuration);
                Vector3 flatPosition = Vector3.Lerp(projectileStart, targetPosition, progress);
                float arcOffset = Mathf.Sin(progress * Mathf.PI) * ActiveTuning.bombArcHeight;
                projectile.transform.position = flatPosition + Vector3.up * arcOffset;
                yield return null;
            }

            projectile.transform.position = targetPosition;
            DestroyFeedbackObject(projectile);
        }
        else if (flightDuration > 0f)
        {
            yield return new WaitForSeconds(flightDuration);
        }

        GameObject explosion = CreateFeedbackObject(
            "BombExplosion",
            ActiveTuning.bombExplosionPrefab,
            ActiveTuning.bombExplosionSprite,
            ActiveTuning.bombExplosionColor,
            Mathf.Max(0.1f, BombRadius * 0.35f),
            13,
            GetCurrentFeedbackRoot(),
            targetPosition);

        int hitCount = 0;
        float bombRadiusSqr = BombRadius * BombRadius;

        for (int enemyIndex = 0; enemyIndex < Enemy.ActiveEnemyCount; enemyIndex++)
        {
            Enemy enemy = Enemy.GetActiveEnemy(enemyIndex);
            if (enemy == null)
            {
                continue;
            }

            float distanceSqr = (enemy.transform.position - targetPosition).sqrMagnitude;
            if (distanceSqr > bombRadiusSqr)
            {
                continue;
            }

            enemy.TakeDamage(
                DamagePerShot,
                Enemy.DamageFeedbackType.Bombard,
                isArmorPiercing: false,
                isAreaDamage: true);
            hitCount++;
        }

        if (explosion != null)
        {
            yield return PlayExplosionFeedback(explosion, targetPosition);
        }

        if (hitCount > 0)
        {
            StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator PlayExplosionFeedback(GameObject explosionObject, Vector3 targetPosition)
    {
        SpriteRenderer renderer = explosionObject != null ? explosionObject.GetComponent<SpriteRenderer>() : null;
        if (explosionObject == null || renderer == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.05f, ActiveTuning.bombExplosionDuration);
        float startScale = Mathf.Max(0.1f, BombRadius * 0.35f);
        float endScale = Mathf.Max(startScale, BombRadius * ActiveTuning.bombExplosionScaleMultiplier);
        Color startColor = ActiveTuning.bombExplosionColor;
        float elapsed = 0f;

        explosionObject.transform.position = targetPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(startScale, endScale, progress);
            explosionObject.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, progress));
            yield return null;
        }

        DestroyFeedbackObject(explosionObject);
    }

    /// <summary>
    /// Single-target towers should read as precise and immediate.
    /// A short-lived tracer is a cheap but clear feedback layer, and if the user later assigns a bespoke sprite
    /// it will automatically replace the fallback without changing code.
    /// </summary>
    private IEnumerator PlayTracerFeedback(Vector3 targetPosition)
    {
        GameObject tracerObject = CreateFeedbackObject(
            "ShotTrace",
            ActiveTuning.shotTracePrefab,
            ActiveTuning.shotTraceSprite,
            ActiveTuning.shotTraceColor,
            1f,
            10,
            GetCurrentFeedbackRoot(),
            GetCurrentFeedbackOrigin());

        if (tracerObject == null)
        {
            yield break;
        }

        SpriteRenderer tracerRenderer = tracerObject.GetComponent<SpriteRenderer>();
        Vector3 origin = GetCurrentFeedbackOrigin();
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        tracerObject.transform.position = (origin + targetPosition) * 0.5f;
        tracerObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        tracerObject.transform.localScale = new Vector3(
            Mathf.Max(0.05f, distance),
            ActiveTuning.shotTraceThickness,
            1f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.02f, ActiveTuning.shotTraceDuration);
        Color startColor = ActiveTuning.shotTraceColor;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (tracerRenderer != null)
            {
                tracerRenderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, progress));
            }

            yield return null;
        }

        DestroyFeedbackObject(tracerObject);
    }

    /// <summary>
    /// Slow-field towers should read as "area control" rather than a point hit.
    /// The expanding pulse gives the player a quick spatial reminder of the zone that was just applied.
    /// </summary>
    private IEnumerator PlaySlowPulseFeedback()
    {
        GameObject pulseObject = CreateFeedbackObject(
            "SlowPulse",
            ActiveTuning.slowPulsePrefab,
            ActiveTuning.slowPulseSprite,
            ActiveTuning.slowPulseColor,
            ActiveTuning.slowPulseStartScale,
            9,
            GetCurrentFeedbackRoot(),
            GetCurrentFeedbackOrigin());

        if (pulseObject == null)
        {
            yield break;
        }

        SpriteRenderer pulseRenderer = pulseObject.GetComponent<SpriteRenderer>();
        pulseObject.transform.position = GetCurrentFeedbackOrigin();

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, ActiveTuning.slowPulseDuration);
        float startScale = ActiveTuning.slowPulseStartScale;
        float endScale = Mathf.Max(startScale, AttackRange * ActiveTuning.slowPulseScaleMultiplier);
        Color startColor = ActiveTuning.slowPulseColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(startScale, endScale, progress);
            pulseObject.transform.localScale = new Vector3(scale, scale, 1f);

            if (pulseRenderer != null)
            {
                pulseRenderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, progress));
            }

            yield return null;
        }

        DestroyFeedbackObject(pulseObject);
    }

    /// <summary>
    /// Upgrade feedback is intentionally light and generic.
    /// It makes level-up moments readable now, while still letting future bespoke art replace it later.
    /// </summary>
    private IEnumerator UpgradePulseRoutine()
    {
        if (_spriteRenderer == null)
        {
            yield break;
        }

        Vector3 baseScale = transform.localScale;
        float duration = Mathf.Max(0.05f, upgradePulseDuration);
        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            transform.localScale = Vector3.Lerp(baseScale, baseScale * upgradeScaleMultiplier, progress);
            _spriteRenderer.color = Color.Lerp(ActiveTuning.poweredColor, upgradeFlashColor, progress);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            transform.localScale = Vector3.Lerp(baseScale * upgradeScaleMultiplier, baseScale, progress);
            _spriteRenderer.color = Color.Lerp(upgradeFlashColor, IsPowered ? ActiveTuning.poweredColor : offlineColor, progress);
            yield return null;
        }

        transform.localScale = baseScale;
        RefreshVisualState();
    }

    private Enemy FindClosestTarget(float range)
    {
        float maxDistanceSqr = range * range;
        float closestDistanceSqr = float.MaxValue;
        Enemy bestTarget = null;

        for (int i = 0; i < Enemy.ActiveEnemyCount; i++)
        {
            Enemy candidate = Enemy.GetActiveEnemy(i);
            if (candidate == null)
            {
                continue;
            }

            if (!candidate.CanBeDirectlyTargeted)
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr || distanceSqr >= closestDistanceSqr)
            {
                continue;
            }

            closestDistanceSqr = distanceSqr;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    /// <summary>
    /// 当前原型里还没有真正的“激光塔 / 电磁炮塔”，
    /// 但为了先把重甲怪完整接入主玩法链，这里做一个明确的过渡约定：
    /// - 单体塔先承担穿甲伤害角色
    /// - 减速塔和炸弹塔仍然视为非穿甲
    ///
    /// 后续等真正的穿甲塔并入时，只需要把这条映射调整掉即可。
    /// </summary>
    private bool DoesCurrentTowerUseArmorPiercingDamage()
    {
        return buildType == TowerType.SingleTarget;
    }

    private IEnumerator FlashRoutine()
    {
        if (_spriteRenderer == null || !IsPowered)
        {
            yield break;
        }

        _spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);

        if (_spriteRenderer != null)
        {
            RefreshVisualState();
        }
    }

    private void RefreshVisualState()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        _spriteRenderer.sprite = ResolveBodySprite();
        _spriteRenderer.color = IsPowered ? ActiveTuning.poweredColor : offlineColor;
        RefreshTypeSignatureStyle();
    }

    /// <summary>
    /// 解析当前塔型真正应该显示的主塔身 Sprite。
    ///
    /// 现在三类战斗塔已经支持各自独立的主体外观：
    /// - 单体塔可以配自己的主身体 Sprite
    /// - 减速塔可以配自己的主身体 Sprite
    /// - 炸弹塔可以配自己的主身体 Sprite
    ///
    /// 如果某一类暂时还没指定专属美术，就安全回退到原型体原本的默认 Sprite，
    /// 保证老场景不会因为这次结构升级而突然变成空白。
    /// </summary>
    private Sprite ResolveBodySprite()
    {
        if (ActiveTuning.bodySprite != null)
        {
            return ActiveTuning.bodySprite;
        }

        if (_defaultBodySprite != null)
        {
            return _defaultBodySprite;
        }

        if (bodyRendererReference != null && bodyRendererReference.sprite != null)
        {
            return bodyRendererReference.sprite;
        }

        return RuntimeFallbackSprite;
    }

    private Transform GetCurrentFeedbackRoot()
    {
        switch (buildType)
        {
            case TowerType.SlowField:
                return EnsureTypedFeedbackRoot(TowerType.SlowField);
            case TowerType.Bombard:
                return EnsureTypedFeedbackRoot(TowerType.Bombard);
            default:
                return EnsureTypedFeedbackRoot(TowerType.SingleTarget);
        }
    }

    private Vector3 GetCurrentFeedbackOrigin()
    {
        Transform feedbackRoot = GetCurrentFeedbackRoot();
        return feedbackRoot != null ? feedbackRoot.position : transform.position;
    }

    /// <summary>
    /// 只为当前塔型保留自己的反馈挂点。
    ///
    /// 这样运行时实例在层级上更干净：
    /// - 单体塔不再挂着减速塔 / 炸弹塔的空反馈根
    /// - 炸弹塔也不再挂着不属于自己的那两套空根节点
    ///
    /// 这比之前“原型里预先摆三套空子物体”更符合作者直觉。
    /// </summary>
    private void CleanupUnusedFeedbackRoots(bool immediate)
    {
        if (feedbackRootReference == null)
        {
            return;
        }

        CleanupFeedbackRootForType(TowerType.SingleTarget, ref singleTargetFeedbackRootReference, buildType == TowerType.SingleTarget, immediate);
        CleanupFeedbackRootForType(TowerType.SlowField, ref slowFieldFeedbackRootReference, buildType == TowerType.SlowField, immediate);
        CleanupFeedbackRootForType(TowerType.Bombard, ref bombardFeedbackRootReference, buildType == TowerType.Bombard, immediate);
    }

    private void CleanupFeedbackRootForType(TowerType towerType, ref Transform rootReference, bool shouldKeep, bool immediate)
    {
        if (feedbackRootReference == null)
        {
            rootReference = null;
            return;
        }

        if (rootReference == null)
        {
            Transform existing = feedbackRootReference.Find(GetFeedbackRootName(towerType));
            if (existing != null)
            {
                rootReference = existing;
            }
        }

        if (shouldKeep)
        {
            return;
        }

        if (rootReference != null)
        {
            if (immediate && !Application.isPlaying)
            {
                DestroyImmediate(rootReference.gameObject);
            }
            else
            {
                Destroy(rootReference.gameObject);
            }
        }

        rootReference = null;
    }

    private Transform EnsureTypedFeedbackRoot(TowerType towerType)
    {
        Transform parent = feedbackRootReference != null ? feedbackRootReference : transform;
        if (parent == null)
        {
            return transform;
        }

        ref Transform typedReference = ref singleTargetFeedbackRootReference;
        switch (towerType)
        {
            case TowerType.SlowField:
                typedReference = ref slowFieldFeedbackRootReference;
                break;
            case TowerType.Bombard:
                typedReference = ref bombardFeedbackRootReference;
                break;
        }

        if (typedReference == null)
        {
            Transform existing = parent.Find(GetFeedbackRootName(towerType));
            if (existing != null)
            {
                typedReference = existing;
            }
        }

        if (typedReference != null)
        {
            return typedReference;
        }

        GameObject rootObject = new GameObject(GetFeedbackRootName(towerType));
        typedReference = rootObject.transform;
        typedReference.SetParent(parent, false);
        typedReference.localPosition = GetFeedbackRootLocalOffset(towerType);
        typedReference.localRotation = Quaternion.identity;
        typedReference.localScale = Vector3.one;
        return typedReference;
    }

    private static string GetFeedbackRootName(TowerType towerType)
    {
        switch (towerType)
        {
            case TowerType.SlowField:
                return "SlowFieldFeedbackRoot";
            case TowerType.Bombard:
                return "BombardFeedbackRoot";
            default:
                return "SingleTargetFeedbackRoot";
        }
    }

    private static Vector3 GetFeedbackRootLocalOffset(TowerType towerType)
    {
        switch (towerType)
        {
            case TowerType.Bombard:
                return new Vector3(0f, 0.2f, 0f);
            case TowerType.SingleTarget:
                return new Vector3(0f, 0.12f, 0f);
            default:
                return Vector3.zero;
        }
    }

    /// <summary>
    /// Persistent type signatures make the three families readable even while idle:
    /// - single-target: compact underside rail
    /// - slow-field: broad low-alpha field plate
    /// - bombard: floating rotating diamond
    ///
    /// This keeps the difference visible without requiring final art to exist yet.
    /// </summary>
    private void UpdateTypeSignatureVisual()
    {
        EnsureTypeSignatureRenderer();
        if (_typeSignatureRenderer == null)
        {
            return;
        }

        CombatTuning tuning = ActiveTuning;
        float pulse = 1f;
        if (tuning.signaturePulseAmplitude > 0.0001f)
        {
            pulse += Mathf.Sin(Time.time * Mathf.Max(0.01f, tuning.signaturePulseSpeed)) * tuning.signaturePulseAmplitude;
        }

        float bobOffset = 0f;
        if (tuning.signatureVerticalBobAmplitude > 0.0001f)
        {
            bobOffset = Mathf.Sin(Time.time * Mathf.Max(0.01f, tuning.signatureVerticalBobSpeed)) * tuning.signatureVerticalBobAmplitude;
        }

        Vector2 scaleVector = tuning.signatureBaseScale + tuning.signatureScalePerRange * AttackRange;
        scaleVector *= pulse;
        scaleVector.x = Mathf.Max(0.02f, scaleVector.x);
        scaleVector.y = Mathf.Max(0.02f, scaleVector.y);

        Transform signatureTransform = _typeSignatureRenderer.transform;
        signatureTransform.localPosition = new Vector3(
            tuning.signatureOffset.x,
            tuning.signatureOffset.y + bobOffset,
            0f);
        signatureTransform.localScale = new Vector3(scaleVector.x, scaleVector.y, 1f);
        signatureTransform.localRotation = Quaternion.Euler(0f, 0f, tuning.signatureRotationDegrees + Time.time * tuning.signatureRotationSpeed);
    }

    private void EnsureTypeSignatureRenderer()
    {
        if (_typeSignatureRenderer != null)
        {
            return;
        }

        Transform signatureParent = typeSignatureRootReference != null ? typeSignatureRootReference : transform;
        Transform existingTransform = signatureParent.Find("TypeSignature");
        if (existingTransform != null)
        {
            _typeSignatureRenderer = existingTransform.GetComponent<SpriteRenderer>();
        }

        if (_typeSignatureRenderer != null)
        {
            return;
        }

        GameObject signatureObject = new GameObject("TypeSignature");
        signatureObject.transform.SetParent(signatureParent, false);
        _typeSignatureRenderer = signatureObject.AddComponent<SpriteRenderer>();
    }

    private void RefreshTypeSignatureStyle()
    {
        EnsureTypeSignatureRenderer();
        if (_typeSignatureRenderer == null)
        {
            return;
        }

        CombatTuning tuning = ActiveTuning;
        _typeSignatureRenderer.sprite = tuning.signatureSprite != null ? tuning.signatureSprite : RuntimeFallbackSprite;
        _typeSignatureRenderer.color = IsPowered
            ? tuning.signatureColor
            : new Color(offlineColor.r, offlineColor.g, offlineColor.b, Mathf.Max(0.16f, tuning.signatureColor.a * 0.75f));
        _typeSignatureRenderer.sortingOrder = (_spriteRenderer != null ? _spriteRenderer.sortingOrder : 0) + 1;
        _typeSignatureRenderer.gameObject.SetActive(TowerTypeUtility.IsCombatTower(buildType));
    }

    /// <summary>
    /// Upgrade feedback should not only be a one-frame pulse.
    /// These small level pips give the player a persistent read of how far a tower has been upgraded,
    /// while still staying cheap and art-replacement-friendly.
    /// </summary>
    private void RefreshLevelMarkerVisual()
    {
        if (!TowerTypeUtility.IsCombatTower(buildType))
        {
            HideAllLevelPips();
            return;
        }

        int pipCount = Mathf.Clamp(CurrentLevel, 1, MaxLevel);
        EnsureLevelPipPool(MaxLevel);

        float centeredOffset = (pipCount - 1) * 0.5f;
        for (int index = 0; index < _levelPipRenderers.Count; index++)
        {
            SpriteRenderer pipRenderer = _levelPipRenderers[index];
            if (pipRenderer == null)
            {
                continue;
            }

            bool shouldShow = index < pipCount;
            pipRenderer.gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                continue;
            }

            Transform pipTransform = pipRenderer.transform;
            pipTransform.localPosition = new Vector3(
                levelPipOffset.x + (index - centeredOffset) * levelPipSpacing,
                levelPipOffset.y,
                0f);
            pipTransform.localScale = new Vector3(levelPipScale, levelPipScale, 1f);
            pipRenderer.color = IsPowered ? levelPipColor : new Color(offlineColor.r, offlineColor.g, offlineColor.b, 0.92f);
            pipRenderer.sortingOrder = (_spriteRenderer != null ? _spriteRenderer.sortingOrder : 0) + levelPipSortingOffset;
        }
    }

    private void EnsureLevelPipPool(int desiredCount)
    {
        desiredCount = Mathf.Max(0, desiredCount);
        Transform levelMarkerParent = levelMarkerRootReference != null ? levelMarkerRootReference : transform;
        while (_levelPipRenderers.Count < desiredCount)
        {
            GameObject pipObject = new GameObject($"LevelPip_{_levelPipRenderers.Count + 1}");
            pipObject.transform.SetParent(levelMarkerParent, false);
            SpriteRenderer pipRenderer = pipObject.AddComponent<SpriteRenderer>();
            pipRenderer.sprite = levelPipSprite != null ? levelPipSprite : RuntimeFallbackSprite;
            _levelPipRenderers.Add(pipRenderer);
        }

        for (int index = 0; index < _levelPipRenderers.Count; index++)
        {
            SpriteRenderer pipRenderer = _levelPipRenderers[index];
            if (pipRenderer == null)
            {
                continue;
            }

            pipRenderer.sprite = levelPipSprite != null ? levelPipSprite : RuntimeFallbackSprite;
        }
    }

    private void HideAllLevelPips()
    {
        for (int index = 0; index < _levelPipRenderers.Count; index++)
        {
            if (_levelPipRenderers[index] != null)
            {
                _levelPipRenderers[index].gameObject.SetActive(false);
            }
        }
    }

    private int EvaluateDamage(int level)
    {
        return Mathf.Max(0, ActiveTuning.baseDamage + (level - 1) * ActiveTuning.damagePerUpgrade);
    }

    private int EvaluatePowerRequired(int level)
    {
        return Mathf.Max(0, ActiveTuning.basePowerRequired + (level - 1) * ActiveTuning.powerRequiredPerUpgrade);
    }

    private float EvaluateAttackRange(int level)
    {
        return Mathf.Max(0.1f, ActiveTuning.attackRange + (level - 1) * ActiveTuning.attackRangePerUpgrade);
    }

    private float EvaluateAttackInterval(int level)
    {
        return Mathf.Max(0.08f, ActiveTuning.attackInterval + (level - 1) * ActiveTuning.attackIntervalPerUpgradeDelta);
    }

    private float EvaluateSlowMultiplier(int level)
    {
        return Mathf.Clamp(
            ActiveTuning.slowMultiplier + (level - 1) * ActiveTuning.slowMultiplierPerUpgradeDelta,
            0.15f,
            1f);
    }

    private float EvaluateSlowDuration(int level)
    {
        return Mathf.Max(0f, ActiveTuning.slowDuration + (level - 1) * ActiveTuning.slowDurationPerUpgrade);
    }

    private float EvaluateBombFlightTime(int level)
    {
        return Mathf.Max(0.05f, ActiveTuning.bombFlightTime + (level - 1) * ActiveTuning.bombFlightTimePerUpgradeDelta);
    }

    private float EvaluateBombRadius(int level)
    {
        return Mathf.Max(0.1f, ActiveTuning.bombRadius + (level - 1) * ActiveTuning.bombRadiusPerUpgrade);
    }

    private static float GetSlowPercent(float slowMultiplier)
    {
        return (1f - Mathf.Clamp01(slowMultiplier)) * 100f;
    }

    /// <summary>
    /// Feedback objects are runtime-only and fully optional.
    /// If the user later assigns bespoke sprites, those take priority; otherwise we fall back
    /// to a generated white sprite so gameplay feedback still exists without art dependencies.
    /// </summary>
    private GameObject CreateFeedbackObject(
        string objectName,
        GameObject prefabAsset,
        Sprite preferredSprite,
        Color color,
        float scale,
        int sortingOffset,
        Transform feedbackParentOverride = null,
        Vector3? worldPositionOverride = null)
    {
        if (prefabAsset != null)
        {
            return InstantiateFeedbackPrefab(
                objectName,
                prefabAsset,
                color,
                scale,
                sortingOffset,
                feedbackParentOverride,
                worldPositionOverride ?? transform.position);
        }

        Sprite spriteToUse = preferredSprite != null ? preferredSprite : RuntimeFallbackSprite;
        if (spriteToUse == null)
        {
            return null;
        }

        GameObject feedbackObject = new GameObject(objectName);
        SpriteRenderer feedbackRenderer = feedbackObject.AddComponent<SpriteRenderer>();
        feedbackRenderer.sprite = spriteToUse;
        feedbackRenderer.color = color;
        if (feedbackMaterial != null)
        {
            feedbackRenderer.sharedMaterial = feedbackMaterial;
        }

        feedbackRenderer.sortingOrder = (_spriteRenderer != null ? _spriteRenderer.sortingOrder : 0) + sortingOffset;
        Transform feedbackParent = feedbackParentOverride != null
            ? feedbackParentOverride
            : (feedbackRootReference != null ? feedbackRootReference : transform);
        feedbackObject.transform.SetParent(feedbackParent, false);
        feedbackObject.transform.position = worldPositionOverride ?? transform.position;
        feedbackObject.transform.localScale = new Vector3(scale, scale, 1f);
        _activeFeedbackObjects.Add(feedbackObject);
        return feedbackObject;
    }

    /// <summary>
    /// 如果作者已经提供了专用反馈 Prefab，就优先实例化它。
    ///
    /// 这样后续你要替换成真正的投掷物 / 爆炸 / tracer 美术时，
    /// 就不需要再改这套玩法逻辑，只需要换 Prefab 资源本身。
    /// </summary>
    private GameObject InstantiateFeedbackPrefab(
        string objectName,
        GameObject prefabAsset,
        Color color,
        float scale,
        int sortingOffset,
        Transform feedbackParent,
        Vector3 worldPosition)
    {
        if (prefabAsset == null)
        {
            return null;
        }

        Transform parent = feedbackParent != null ? feedbackParent : transform;
        GameObject instance = Instantiate(prefabAsset, worldPosition, Quaternion.identity, parent);
        instance.name = objectName;
        instance.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        int baseSortingOrder = (_spriteRenderer != null ? _spriteRenderer.sortingOrder : 0) + sortingOffset;
        int firstRendererOrder = renderers.Length > 0 ? renderers[0].sortingOrder : 0;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            SpriteRenderer renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            renderer.color = color;
            renderer.sortingOrder = baseSortingOrder + (renderer.sortingOrder - firstRendererOrder);
            if (feedbackMaterial != null)
            {
                renderer.sharedMaterial = feedbackMaterial;
            }
        }

        _activeFeedbackObjects.Add(instance);
        return instance;
    }

    private void DestroyFeedbackObject(GameObject feedbackObject)
    {
        if (feedbackObject == null)
        {
            return;
        }

        _activeFeedbackObjects.Remove(feedbackObject);
        Destroy(feedbackObject);
    }

    private void OnDestroy()
    {
        for (int index = 0; index < _activeFeedbackObjects.Count; index++)
        {
            if (_activeFeedbackObjects[index] != null)
            {
                Destroy(_activeFeedbackObjects[index]);
            }
        }

        _activeFeedbackObjects.Clear();

        for (int index = 0; index < _levelPipRenderers.Count; index++)
        {
            if (_levelPipRenderers[index] != null)
            {
                Destroy(_levelPipRenderers[index].gameObject);
            }
        }

        _levelPipRenderers.Clear();

        if (TowerDefenseGame.Instance != null)
        {
            TowerDefenseGame.Instance.NotifyStructureTopologyChanged();
        }
    }

    private void OnMouseDown()
    {
        if (TowerDefenseGame.Instance == null)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        TowerDefenseGame.Instance.SelectPlacedStructure(this);
    }
}
