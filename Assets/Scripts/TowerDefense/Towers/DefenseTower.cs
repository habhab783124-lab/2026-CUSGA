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
        [Header("Attack")]
        [Min(0.1f)] public float attackRange = 2.8f;
        public float attackRangePerUpgrade = 0.2f;
        [Min(0.05f)] public float attackInterval = 0.65f;
        public float attackIntervalPerUpgradeDelta = -0.06f;
        [Min(0)] public int baseDamage = 1;
        [Min(0)] public int damagePerUpgrade = 1;
        public GameObject shotTracePrefab = null;
        public Sprite shotTraceSprite = null;
        public Color shotTraceColor = new Color(0.68f, 0.9f, 1f, 0.92f);
        [Min(0.02f)] public float shotTraceThickness = 0.1f;
        [Min(0.02f)] public float shotTraceDuration = 0.08f;

        [Header("Power")]
        [Min(0)] public int basePowerRequired = 2;
        [Min(0)] public int powerRequiredPerUpgrade = 1;

        [Header("Upgrade Cost")]
        [Min(0)] public int upgradeCostBase = 30;
        [Min(0)] public int upgradeCostPerLevel = 15;

        [Header("Slow Field")]
        [Range(0.15f, 1f)] public float slowMultiplier = 0.65f;
        public float slowMultiplierPerUpgradeDelta = -0.05f;
        [Min(0f)] public float slowDuration = 1.1f;
        public float slowDurationPerUpgrade = 0.2f;
        public GameObject slowPulsePrefab = null;
        public Sprite slowPulseSprite = null;
        public Color slowPulseColor = new Color(0.36f, 0.95f, 0.84f, 0.28f);
        [Min(0.05f)] public float slowPulseDuration = 0.18f;
        [Min(0.05f)] public float slowPulseStartScale = 0.2f;
        [Min(0.1f)] public float slowPulseScaleMultiplier = 2.1f;

        [Header("Bombard")]
        [Min(0.05f)] public float bombFlightTime = 0.45f;
        public float bombFlightTimePerUpgradeDelta = -0.04f;
        [Min(0.1f)] public float bombRadius = 1.2f;
        public float bombRadiusPerUpgrade = 0.2f;
        [Min(0f)] public float bombArcHeight = 0.5f;
        [Min(0.05f)] public float bombProjectileScale = 0.18f;
        [Min(0.05f)] public float bombExplosionDuration = 0.24f;
        [Min(0.1f)] public float bombExplosionScaleMultiplier = 1.45f;
        public GameObject bombProjectilePrefab = null;
        public GameObject bombExplosionPrefab = null;
        public Sprite bombProjectileSprite = null;
        public Sprite bombExplosionSprite = null;
        public Color bombProjectileColor = new Color(1f, 0.76f, 0.34f, 1f);
        public Color bombExplosionColor = new Color(1f, 0.54f, 0.2f, 0.9f);

        [Header("Look")]
        public Sprite bodySprite = null;
        public Color poweredColor = Color.white;

        [Header("Type Signature")]
        public Sprite signatureSprite = null;
        public Color signatureColor = new Color(1f, 1f, 1f, 0.9f);
        public Vector2 signatureOffset = Vector2.zero;
        public Vector2 signatureBaseScale = new Vector2(0.25f, 0.25f);
        public Vector2 signatureScalePerRange = Vector2.zero;
        public float signatureRotationDegrees = 0f;
        public float signatureRotationSpeed = 0f;
        public float signaturePulseAmplitude = 0f;
        public float signaturePulseSpeed = 2f;
        public float signatureVerticalBobAmplitude = 0f;
        public float signatureVerticalBobSpeed = 2f;
    }

    [Header("Type")]
    [SerializeField] private TowerType buildType = TowerType.SingleTarget;

    [Header("Tunings")]
    [SerializeField] private CombatTuning singleTargetTuning = new CombatTuning
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
        poweredColor = Color.white,
        signatureColor = new Color(0.42f, 0.86f, 1f, 0.92f),
        signatureOffset = new Vector2(0f, -0.5f),
        signatureBaseScale = new Vector2(0.55f, 0.08f),
        signaturePulseAmplitude = 0.08f,
        signaturePulseSpeed = 5.2f
    };

    [SerializeField] private CombatTuning slowFieldTuning = new CombatTuning
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
        poweredColor = Color.white,
        signatureColor = new Color(0.3f, 0.95f, 0.84f, 0.18f),
        signatureOffset = new Vector2(0f, -0.04f),
        signatureBaseScale = new Vector2(0.45f, 0.45f),
        signatureScalePerRange = new Vector2(0.48f, 0.48f),
        signaturePulseAmplitude = 0.1f,
        signaturePulseSpeed = 2.4f
    };

    [SerializeField] private CombatTuning bombardTuning = new CombatTuning
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
        poweredColor = Color.white,
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

    [Header("Progression")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int maxLevel = 3;

    [Header("Visual References")]

    /// <summary>
    /// 塔本体的主渲染器。
    ///
    /// 如果你后续把塔做成更复杂的层级，
    /// 这里可以显式指定“哪一个 SpriteRenderer 才代表主塔身”。
    /// </summary>
    [SerializeField] private SpriteRenderer bodyRendererReference;

    /// <summary>
    /// 所有运行时反馈对象的挂点。
    ///
    /// 这样炸弹、爆炸、脉冲和 tracer 不会再默认挂到塔根节点上乱长，
    /// 也更方便后续整体替换或隐藏这一层效果。
    /// </summary>
    [SerializeField] private Transform feedbackRootReference;

    /// <summary>
    /// 单体塔反馈的专用挂点。
    /// 这样 `tracer` 的起点可以独立调整，而不是总从塔中心生硬飞出。
    /// </summary>
    [SerializeField] private Transform singleTargetFeedbackRootReference;

    /// <summary>
    /// 减速塔反馈的专用挂点。
    /// </summary>
    [SerializeField] private Transform slowFieldFeedbackRootReference;

    /// <summary>
    /// 炸弹塔反馈的专用挂点。
    /// 这样投射物和爆炸可以围绕更明确的视觉锚点展开。
    /// </summary>
    [SerializeField] private Transform bombardFeedbackRootReference;

    /// <summary>
    /// 塔型签名的挂点。
    /// </summary>
    [SerializeField] private Transform typeSignatureRootReference;

    /// <summary>
    /// 等级标记的挂点。
    /// </summary>
    [SerializeField] private Transform levelMarkerRootReference;

    [Header("Shared Visuals")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private Color offlineColor = Color.white;
    [SerializeField] private float flashDuration = 0.06f;
    [SerializeField] private Color upgradeFlashColor = Color.white;
    [SerializeField] private float upgradePulseDuration = 0.18f;
    [SerializeField] private float upgradeScaleMultiplier = 1.14f;
    [SerializeField] private Material feedbackMaterial;

    [Header("Level Marker")]
    [SerializeField] private Sprite levelPipSprite = null;
    [SerializeField] private Color levelPipColor = new Color(0.98f, 0.96f, 0.78f, 1f);
    [SerializeField] private Vector2 levelPipOffset = new Vector2(0f, -0.65f);
    [SerializeField] private float levelPipSpacing = 0.22f;
    [SerializeField] private float levelPipScale = 0.12f;
    [SerializeField] private int levelPipSortingOffset = 3;

    private static Sprite s_runtimeFallbackSprite;

    private readonly List<GameObject> _activeFeedbackObjects = new List<GameObject>(4);
    private readonly List<SpriteRenderer> _levelPipRenderers = new List<SpriteRenderer>(4);
    private SpriteRenderer _spriteRenderer;
    private SpriteRenderer _typeSignatureRenderer;
    private float _attackTimer;
    private Sprite _defaultBodySprite;

    public int TowerNumber { get; private set; } = 100;
    public TowerType BuildType => buildType;
    public int CurrentLevel => Mathf.Max(1, currentLevel);
    public int MaxLevel => Mathf.Max(1, maxLevel);
    public int DamagePerShot => EvaluateDamage(CurrentLevel);
    public int PowerRequired => EvaluatePowerRequired(CurrentLevel);
    public float AttackRange => EvaluateAttackRange(CurrentLevel);
    public float AttackInterval => EvaluateAttackInterval(CurrentLevel);
    public float SlowMultiplier => EvaluateSlowMultiplier(CurrentLevel);
    public float SlowDuration => EvaluateSlowDuration(CurrentLevel);
    public float BombFlightTime => EvaluateBombFlightTime(CurrentLevel);
    public float BombRadius => EvaluateBombRadius(CurrentLevel);
    public bool IsPowered { get; private set; } = true;
    public RelayTower AssignedRelay { get; private set; }
    public string PowerStatusMessage { get; private set; } = "Awaiting power evaluation.";

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
            ? (isPowered ? "Powered and operational." : "Offline.")
            : powerStatusMessage;
        RefreshVisualState();
        RefreshLevelMarkerVisual();
    }

    public bool CanUpgrade => CurrentLevel < MaxLevel;

    public int GetUpgradeCost()
    {
        return ActiveTuning.upgradeCostBase + (CurrentLevel - 1) * ActiveTuning.upgradeCostPerLevel;
    }

    /// <summary>
    /// 给删除返还逻辑提供一个“这座塔到当前为止一共花过多少升级费”的可靠入口。
    ///
    /// 这样总控只需要关心返还比例，不需要再复制一份和塔内部升级规则耦合的成本计算。
    /// </summary>
    public int CalculateAccumulatedUpgradeCost()
    {
        int totalUpgradeCost = 0;
        for (int level = 1; level < CurrentLevel; level++)
        {
            totalUpgradeCost += ActiveTuning.upgradeCostBase + (level - 1) * ActiveTuning.upgradeCostPerLevel;
        }

        return Mathf.Max(0, totalUpgradeCost);
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
                return $"DMG {DamagePerShot} / SLOW {GetSlowPercent(SlowMultiplier):0}% / DUR {SlowDuration:0.00}s / RATE {AttackInterval:0.00}s / RNG {AttackRange:0.0}";

            case TowerType.Bombard:
                return $"DMG {DamagePerShot} / BLAST {BombRadius:0.0} / FLIGHT {BombFlightTime:0.00}s / RATE {AttackInterval:0.00}s / RNG {AttackRange:0.0}";

            default:
                return $"DMG {DamagePerShot} / RATE {AttackInterval:0.00}s / RNG {AttackRange:0.0}";
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
            return "At max level.";
        }

        switch (buildType)
        {
            case TowerType.SlowField:
                return
                    $"Next DMG {PreviewUpgradedDamagePerShot()} / SLOW {GetSlowPercent(PreviewUpgradedSlowMultiplier()):0}% / DUR {PreviewUpgradedSlowDuration():0.00}s / RATE {PreviewUpgradedAttackInterval():0.00}s / PWR {PreviewUpgradedPowerRequired()}";

            case TowerType.Bombard:
                return
                    $"Next DMG {PreviewUpgradedDamagePerShot()} / BLAST {PreviewUpgradedBombRadius():0.0} / FLIGHT {PreviewUpgradedBombFlightTime():0.00}s / RATE {PreviewUpgradedAttackInterval():0.00}s / PWR {PreviewUpgradedPowerRequired()}";

            default:
                return
                    $"Next DMG {PreviewUpgradedDamagePerShot()} / RATE {PreviewUpgradedAttackInterval():0.00}s / RNG {PreviewUpgradedAttackRange():0.0} / PWR {PreviewUpgradedPowerRequired()}";
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

        target.TakeDamage(DamagePerShot, Enemy.DamageFeedbackType.Standard);
        TowerDefenseGame.Instance?.PlaySingleTargetImpactSound();
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

            enemy.ApplySlow(SlowMultiplier, SlowDuration);
            enemy.TakeDamage(DamagePerShot, Enemy.DamageFeedbackType.SlowField);
            affectedAnyEnemy = true;
        }

        if (affectedAnyEnemy)
        {
            TowerDefenseGame.Instance?.PlaySlowFieldImpactSound();
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

            enemy.TakeDamage(DamagePerShot, Enemy.DamageFeedbackType.Bombard);
            hitCount++;
        }

        if (explosion != null)
        {
            yield return PlayExplosionFeedback(explosion, targetPosition);
        }

        if (hitCount > 0)
        {
            TowerDefenseGame.Instance?.PlayBombardImpactSound();
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
            _spriteRenderer.color = Color.white;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            transform.localScale = Vector3.Lerp(baseScale * upgradeScaleMultiplier, baseScale, progress);
            _spriteRenderer.color = Color.white;
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
        _spriteRenderer.color = Color.white;
        RefreshTypeSignatureStyle();
        TowerRenderSorting.ApplyPlacedTowerTopmostSorting(transform, _spriteRenderer);
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

        if (rootReference != null && rootReference.Equals(null))
        {
            rootReference = null;
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

        if (typedReference != null && typedReference.Equals(null))
        {
            typedReference = null;
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
        TowerRenderSorting.ApplyPlacedTowerAdornmentSorting(_typeSignatureRenderer, _spriteRenderer, 1);
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
            TowerRenderSorting.ApplyPlacedTowerAdornmentSorting(pipRenderer, _spriteRenderer, levelPipSortingOffset);
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

        TowerRenderSorting.ApplyPlacedTowerEffectSorting(feedbackRenderer, _spriteRenderer, sortingOffset);
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
        TowerRenderSorting.ApplyPlacedTowerEffectSorting(renderers, _spriteRenderer, sortingOffset);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            SpriteRenderer renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            renderer.color = color;
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
