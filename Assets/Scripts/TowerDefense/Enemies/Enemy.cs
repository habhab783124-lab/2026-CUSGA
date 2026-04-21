using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `Enemy` 现在不再只是“唯一一种小怪”的运行时壳，
/// 而是升级成了“多怪物类型的统一运行桥”。
///
/// 这意味着它要同时承接三层职责：
/// 1. 通用移动与血条逻辑。
/// 2. 由敌人目录资产驱动的基础属性。
/// 3. 各类特殊机制：
///    - 护盾
///    - 修理
///    - 护甲减伤
///    - 隐身与探测
///    - 死亡分裂
///
/// 之所以继续把这些机制留在同一个 `Enemy` 运行桥里，
/// 是因为当前项目仍然处在“原型向正式玩法过渡”的阶段：
/// - 先把怪物主链跑通，比一开始就拆很多策略类更重要
/// - 后续如果怪物系统继续扩张，再按类型拆分会更稳
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    /// <summary>
    /// `DamageFeedbackType` 继续保留为“受击反馈语气”枚举。
    /// 这层和怪物具体机制解耦：
    /// - 单体塔命中：精确直击
    /// - 减速塔命中：控制命中
    /// - 炸弹塔命中：爆炸重击
    /// </summary>
    public enum DamageFeedbackType
    {
        Standard,
        SlowField,
        Bombard
    }

    private static readonly List<Enemy> ActiveEnemies = new List<Enemy>();

    [Header("Movement")]
    [SerializeField] private float reachWaypointDistance = 0.05f;

    [Header("Body Look")]
    [SerializeField] private SpriteRenderer bodyRendererReference;
    [SerializeField] private Transform visualScaleRootReference;
    [SerializeField] private Color bodyColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color shieldTintColor = new Color(0.42f, 0.9f, 1f, 1f);
    [SerializeField] private Color slowTintColor = new Color(0.42f, 0.95f, 0.9f, 1f);
    [SerializeField] private Color standardHitFlashColor = new Color(1f, 0.96f, 0.9f, 1f);
    [SerializeField] private Color bombardHitFlashColor = new Color(1f, 0.74f, 0.45f, 1f);

    [Header("Body Feedback Timing")]
    [SerializeField] private float standardHitFlashDuration = 0.08f;
    [SerializeField] private float bombardHitFlashDuration = 0.16f;
    [SerializeField] private float slowFeedbackFlashDuration = 0.1f;
    [SerializeField] private float standardHitPulseScale = 1.05f;
    [SerializeField] private float bombardHitPulseScale = 1.13f;
    [SerializeField] private float slowHitPulseScale = 1.04f;
    [SerializeField] private float hitPulseDuration = 0.12f;

    [Header("Health Bar Visuals")]
    [SerializeField] private Color healthBarFillColor = new Color(0.2f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color healthBarShieldColor = new Color(0.44f, 0.9f, 1f, 1f);
    [SerializeField] private Color healthBarBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Sprite healthBarFillSpriteOverride;
    [SerializeField] private Sprite healthBarBackgroundSpriteOverride;

    [Header("Health Bar References")]
    [SerializeField] private Transform healthBarRootReference;
    [SerializeField] private Transform healthBarFillReference;
    [SerializeField] private SpriteRenderer healthBarFillRendererReference;
    [SerializeField] private SpriteRenderer healthBarBackgroundRendererReference;

    private SpriteRenderer _spriteRenderer;
    private Transform _healthBarRoot;
    private Transform _healthBarFill;
    private SpriteRenderer _healthBarFillRenderer;
    private SpriteRenderer _healthBarBackgroundRenderer;

    private EnemyPath _path;
    private EnemyCatalogAsset _enemyCatalog;
    private EnemyCatalogAsset.EnemyArchetypeDefinition _definition;
    private GameObject _enemyPrototypePrefab;
    private Transform _enemyRoot;

    private float _moveSpeed;
    private float _slowMultiplier = 1f;
    private float _slowTimer;
    private int _maxHealth;
    private int _currentHealth;
    private int _currentShield;
    private int _scrapRewardOnDeath;
    private int _baseDamageToBase = 1;
    private int _targetWaypointIndex;
    private bool _hasReachedBase;

    private EnemyArmorTier _armorTier = EnemyArmorTier.None;
    private float _nonPiercingDamageMultiplier = 1f;
    private bool _ignoresSlowEffects;
    private bool _canBeRepairedByMechanic;

    private bool _stealthTriggered;
    private float _stealthTimer;
    private float _revealTimer;

    private float _shieldAuraTimer;
    private float _repairTimer;

    private Vector3 _nativeScale = Vector3.one;
    private Vector3 _configuredScale = Vector3.one;
    private float _bodyScaleMultiplier = 1f;
    private float _hitFlashTimer;
    private float _hitFlashDuration;
    private Color _hitFlashColor = Color.white;
    private float _pulseTimer;
    private float _pulseDuration;
    private float _pulseScaleMultiplier = 1f;

    public static int ActiveEnemyCount => ActiveEnemies.Count;

    public static Enemy GetActiveEnemy(int index)
    {
        return ActiveEnemies[index];
    }

    public EnemyArchetypeId ArchetypeId => _definition != null ? _definition.ArchetypeId : EnemyArchetypeId.None;
    public bool CanBeDirectlyTargeted => _stealthTimer <= 0f || _revealTimer > 0f;

    public void SetHealthBarVisible(bool visible)
    {
        CacheReferences();
        if (_healthBarRoot != null)
        {
            _healthBarRoot.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 旧接口继续保留，避免当前项目里其它调用点瞬间失效。
    /// 不过这一版真正推荐的初始化方式，是走“敌人目录资产 + 敌人类型”的新入口。
    /// </summary>
    public void Initialize(EnemyPath path, float moveSpeed, int maxHealth, int scrapRewardOnDeath = 0)
    {
        InitializeInternal(
            path: path,
            moveSpeed: moveSpeed,
            maxHealth: maxHealth,
            scrapRewardOnDeath: scrapRewardOnDeath,
            baseDamageToBase: 1,
            armorTier: EnemyArmorTier.None,
            nonPiercingDamageMultiplier: 1f,
            ignoresSlowEffects: false,
            canBeRepairedByMechanic: false,
            bodySprite: null,
            configuredBodyColor: bodyColor,
            bodyScaleMultiplier: 1f,
            spawnPositionOverride: null,
            targetWaypointIndexOverride: 1);
    }

    /// <summary>
    /// 新版初始化入口：按敌人目录资产里的类型定义初始化。
    /// 这条链是后续多怪物系统的正式主链。
    /// </summary>
    public void Initialize(
        EnemyPath path,
        EnemyCatalogAsset enemyCatalog,
        EnemyArchetypeId archetypeId,
        GameObject enemyPrototypePrefab,
        Transform enemyRoot,
        Vector3? spawnPositionOverride = null,
        int targetWaypointIndexOverride = 1)
    {
        _enemyCatalog = enemyCatalog;
        _enemyPrototypePrefab = enemyPrototypePrefab;
        _enemyRoot = enemyRoot;
        _definition = enemyCatalog != null ? enemyCatalog.GetDefinition(archetypeId) : null;

        if (_definition == null)
        {
            Debug.LogWarning($"Enemy 无法找到类型 `{archetypeId}` 的目录定义，回退到基础小怪配置。", this);
            Initialize(path, moveSpeed: 1.8f, maxHealth: 3, scrapRewardOnDeath: 0);
            return;
        }

        InitializeInternal(
            path: path,
            moveSpeed: _definition.MoveSpeed,
            maxHealth: _definition.MaxHealth,
            scrapRewardOnDeath: _definition.ScrapReward,
            baseDamageToBase: _definition.BaseDamageToBase,
            armorTier: _definition.ArmorTier,
            nonPiercingDamageMultiplier: _definition.NonPiercingDamageMultiplier,
            ignoresSlowEffects: _definition.IgnoresSlowEffects,
            canBeRepairedByMechanic: _definition.CanBeRepairedByMechanic,
            bodySprite: _definition.BodySpriteOverride,
            configuredBodyColor: _definition.BodyColor,
            bodyScaleMultiplier: _definition.BodyScaleMultiplier,
            spawnPositionOverride: spawnPositionOverride,
            targetWaypointIndexOverride: targetWaypointIndexOverride);
    }

    private void OnValidate()
    {
        if (bodyRendererReference == null)
        {
            bodyRendererReference = GetComponent<SpriteRenderer>();
        }

        if (visualScaleRootReference == null)
        {
            visualScaleRootReference = transform;
        }

        if (healthBarFillReference != null && healthBarFillRendererReference == null)
        {
            healthBarFillRendererReference = healthBarFillReference.GetComponent<SpriteRenderer>();
        }
    }

    private void Awake()
    {
        CacheReferences();
        CaptureNativeScale();
        ApplyVisualTheme();
        RefreshBodyVisualState();
        SetHealthBarVisible(true);
    }

    private void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
    }

    private void Update()
    {
        AdvanceFeedbackTimers();
        ExecuteSupportAbilities();
        RefreshBodyVisualState();

        if (TowerDefenseGame.Instance != null && TowerDefenseGame.Instance.IsGameOver)
        {
            return;
        }

        if (_path == null || _path.WaypointCount == 0 || _hasReachedBase)
        {
            return;
        }

        if (_targetWaypointIndex >= _path.WaypointCount)
        {
            ReachBase();
            return;
        }

        Vector3 targetPosition = _path.GetWaypointPosition(_targetWaypointIndex);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, _moveSpeed * _slowMultiplier * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) <= reachWaypointDistance)
        {
            _targetWaypointIndex++;
            if (_targetWaypointIndex >= _path.WaypointCount)
            {
                ReachBase();
            }
        }
    }

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, DamageFeedbackType.Standard, isArmorPiercing: false, isAreaDamage: false);
    }

    public void TakeDamage(int amount, DamageFeedbackType feedbackType)
    {
        TakeDamage(amount, feedbackType, isArmorPiercing: false, isAreaDamage: false);
    }

    /// <summary>
    /// 新版伤害入口会显式区分：
    /// - 这次攻击是否穿甲
    /// - 这次攻击是不是范围伤害
    ///
    /// 这是实现重甲怪、隐身怪所需的关键边界。
    /// </summary>
    public void TakeDamage(int amount, DamageFeedbackType feedbackType, bool isArmorPiercing, bool isAreaDamage)
    {
        if (amount <= 0 || _currentHealth <= 0)
        {
            return;
        }

        int adjustedDamage = EvaluateIncomingDamage(amount, isArmorPiercing);
        if (_currentShield > 0)
        {
            int absorbedByShield = Mathf.Min(_currentShield, adjustedDamage);
            _currentShield -= absorbedByShield;
            adjustedDamage -= absorbedByShield;
        }

        if (adjustedDamage > 0)
        {
            _currentHealth = Mathf.Max(0, _currentHealth - adjustedDamage);
        }

        if (_definition != null &&
            _definition.EntersStealthAfterFirstDirectHit &&
            !_stealthTriggered &&
            !isAreaDamage)
        {
            _stealthTriggered = true;
            _stealthTimer = Mathf.Max(_stealthTimer, _definition.StealthDuration);
            _revealTimer = 0f;
        }

        TriggerHitFeedback(feedbackType);
        UpdateHealthBar();

        if (_currentHealth == 0)
        {
            Die();
        }
    }

    public void ApplySlow(float slowMultiplier, float duration)
    {
        if (_currentHealth <= 0)
        {
            return;
        }

        if (_ignoresSlowEffects)
        {
            return;
        }

        _slowMultiplier = Mathf.Clamp(Mathf.Min(_slowMultiplier, slowMultiplier), 0.15f, 1f);
        _slowTimer = Mathf.Max(_slowTimer, duration);
        TriggerHitFeedback(DamageFeedbackType.SlowField);
    }

    /// <summary>
    /// “信号 / 神经干扰塔”对应当前原型里的减速塔，
    /// 所以它也顺带承担“探测隐身怪”的职责。
    /// </summary>
    public void ApplyDetection(float duration)
    {
        if (_currentHealth <= 0 || _definition == null || !_definition.EntersStealthAfterFirstDirectHit)
        {
            return;
        }

        _revealTimer = Mathf.Max(_revealTimer, duration);
    }

    public bool CanReceiveMechanicRepair => _canBeRepairedByMechanic && _currentHealth > 0;

    private void InitializeInternal(
        EnemyPath path,
        float moveSpeed,
        int maxHealth,
        int scrapRewardOnDeath,
        int baseDamageToBase,
        EnemyArmorTier armorTier,
        float nonPiercingDamageMultiplier,
        bool ignoresSlowEffects,
        bool canBeRepairedByMechanic,
        Sprite bodySprite,
        Color configuredBodyColor,
        float bodyScaleMultiplier,
        Vector3? spawnPositionOverride,
        int targetWaypointIndexOverride)
    {
        CacheReferences();
        CaptureNativeScale();
        ApplyVisualTheme();

        _path = path;
        _moveSpeed = Mathf.Max(0.05f, moveSpeed);
        _maxHealth = Mathf.Max(1, maxHealth);
        _currentHealth = _maxHealth;
        _currentShield = 0;
        _scrapRewardOnDeath = Mathf.Max(0, scrapRewardOnDeath);
        _baseDamageToBase = Mathf.Max(1, baseDamageToBase);
        _targetWaypointIndex = Mathf.Max(1, targetWaypointIndexOverride);
        _hasReachedBase = false;

        _armorTier = armorTier;
        _nonPiercingDamageMultiplier = Mathf.Clamp(nonPiercingDamageMultiplier, 0.05f, 1f);
        _ignoresSlowEffects = ignoresSlowEffects;
        _canBeRepairedByMechanic = canBeRepairedByMechanic;

        _slowMultiplier = 1f;
        _slowTimer = 0f;
        _hitFlashTimer = 0f;
        _pulseTimer = 0f;
        _pulseScaleMultiplier = 1f;
        _stealthTriggered = false;
        _stealthTimer = 0f;
        _revealTimer = 0f;
        _shieldAuraTimer = 0f;
        _repairTimer = 0f;

        _bodyScaleMultiplier = Mathf.Max(0.2f, bodyScaleMultiplier);
        _configuredScale = _nativeScale * _bodyScaleMultiplier;

        if (_spriteRenderer != null)
        {
            if (bodySprite != null)
            {
                _spriteRenderer.sprite = bodySprite;
            }

            bodyColor = configuredBodyColor;
        }

        transform.position = spawnPositionOverride ?? (_path != null ? _path.GetSpawnPosition() : transform.position);

        RefreshBodyVisualState();
        UpdateHealthBar();
    }

    private void ReachBase()
    {
        if (_hasReachedBase)
        {
            return;
        }

        _hasReachedBase = true;
        TowerDefenseGame.Instance?.DamageBase(_baseDamageToBase);
        Destroy(gameObject);
    }

    private void Die()
    {
        SpawnSplitChildrenOnDeath();

        if (_scrapRewardOnDeath > 0 && TowerDefenseGame.Instance != null && !TowerDefenseGame.Instance.IsGameOver)
        {
            TowerDefenseGame.Instance.AddScrap(_scrapRewardOnDeath);
        }

        Destroy(gameObject);
    }

    private void SpawnSplitChildrenOnDeath()
    {
        if (_definition == null ||
            _definition.SplitChildType == EnemyArchetypeId.None ||
            _definition.SplitChildCount <= 0 ||
            _enemyRoot == null ||
            _enemyCatalog == null ||
            _path == null)
        {
            return;
        }

        EnemyCatalogAsset.EnemyArchetypeDefinition childDefinition = _enemyCatalog.GetDefinition(_definition.SplitChildType);
        GameObject childPrototype = childDefinition != null && childDefinition.RuntimePrefab != null
            ? childDefinition.RuntimePrefab
            : _enemyPrototypePrefab;
        if (childPrototype == null)
        {
            return;
        }

        for (int childIndex = 0; childIndex < _definition.SplitChildCount; childIndex++)
        {
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * _definition.SplitSpawnRadius;
            Vector3 spawnPosition = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

            GameObject childObject = Instantiate(childPrototype, spawnPosition, Quaternion.identity, _enemyRoot);
            childObject.name = $"{_definition.SplitChildType}_Split_{childIndex + 1}";
            childObject.SetActive(true);

            Enemy childEnemy = childObject.GetComponent<Enemy>();
            if (childEnemy != null)
            {
                childEnemy.Initialize(
                    path: _path,
                    enemyCatalog: _enemyCatalog,
                    archetypeId: _definition.SplitChildType,
                    enemyPrototypePrefab: childPrototype,
                    enemyRoot: _enemyRoot,
                    spawnPositionOverride: spawnPosition,
                    targetWaypointIndexOverride: _targetWaypointIndex);
            }
        }
    }

    private void ExecuteSupportAbilities()
    {
        if (_definition == null || _currentHealth <= 0)
        {
            return;
        }

        if (_definition.ShieldAmount > 0)
        {
            _shieldAuraTimer -= Time.deltaTime;
            if (_shieldAuraTimer <= 0f)
            {
                ApplyShieldAura();
                _shieldAuraTimer = _definition.ShieldRefreshInterval;
            }
        }

        if (_definition.RepairAmount > 0)
        {
            _repairTimer -= Time.deltaTime;
            if (_repairTimer <= 0f)
            {
                TryRepairNearbyMechanicalAlly();
                _repairTimer = _definition.RepairCooldown;
            }
        }
    }

    private void ApplyShieldAura()
    {
        float shieldRadiusSqr = _definition.ShieldAuraRadius * _definition.ShieldAuraRadius;
        for (int enemyIndex = 0; enemyIndex < ActiveEnemyCount; enemyIndex++)
        {
            Enemy ally = GetActiveEnemy(enemyIndex);
            if (ally == null || ally == this || ally._currentHealth <= 0)
            {
                continue;
            }

            float distanceSqr = (ally.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr > shieldRadiusSqr)
            {
                continue;
            }

            ally.ApplyShieldIfWeaker(_definition.ShieldAmount);
        }
    }

    private void TryRepairNearbyMechanicalAlly()
    {
        Enemy bestTarget = null;
        float bestDistanceSqr = float.MaxValue;
        float repairRadiusSqr = _definition.RepairRadius * _definition.RepairRadius;

        for (int enemyIndex = 0; enemyIndex < ActiveEnemyCount; enemyIndex++)
        {
            Enemy ally = GetActiveEnemy(enemyIndex);
            if (ally == null || ally == this || !ally.CanReceiveMechanicRepair || ally._currentHealth >= ally._maxHealth)
            {
                continue;
            }

            float distanceSqr = (ally.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr > repairRadiusSqr || distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestTarget = ally;
        }

        if (bestTarget != null)
        {
            bestTarget.Repair(_definition.RepairAmount);
        }
    }

    private void Repair(int amount)
    {
        if (amount <= 0 || _currentHealth <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        TriggerHitFeedback(DamageFeedbackType.Standard);
        UpdateHealthBar();
    }

    private void ApplyShieldIfWeaker(int shieldAmount)
    {
        if (shieldAmount <= 0 || _currentHealth <= 0)
        {
            return;
        }

        _currentShield = Mathf.Max(_currentShield, shieldAmount);
        UpdateHealthBar();
    }

    private int EvaluateIncomingDamage(int rawDamage, bool isArmorPiercing)
    {
        if (isArmorPiercing || _armorTier == EnemyArmorTier.None)
        {
            return rawDamage;
        }

        int adjustedDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * _nonPiercingDamageMultiplier));
        return adjustedDamage;
    }

    private void UpdateHealthBar()
    {
        if (_healthBarFill == null)
        {
            return;
        }

        float healthRatio = _maxHealth <= 0 ? 0f : (float)_currentHealth / _maxHealth;

        Vector3 fillScale = _healthBarFill.localScale;
        fillScale.x = healthRatio;
        _healthBarFill.localScale = fillScale;

        Vector3 fillPosition = _healthBarFill.localPosition;
        fillPosition.x = (healthRatio - 1f) * 0.5f;
        _healthBarFill.localPosition = fillPosition;

        if (_healthBarFillRenderer != null)
        {
            _healthBarFillRenderer.color = _currentShield > 0 ? healthBarShieldColor : healthBarFillColor;
        }
    }

    private void CacheReferences()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = bodyRendererReference != null ? bodyRendererReference : GetComponent<SpriteRenderer>();
            bodyRendererReference = _spriteRenderer;
        }

        if (_healthBarRoot == null)
        {
            _healthBarRoot = healthBarRootReference;
        }

        if (_healthBarFill == null)
        {
            _healthBarFill = healthBarFillReference;
        }

        if (_healthBarFillRenderer == null)
        {
            _healthBarFillRenderer = healthBarFillRendererReference;
            if (_healthBarFillRenderer == null && _healthBarFill != null)
            {
                _healthBarFillRenderer = _healthBarFill.GetComponent<SpriteRenderer>();
            }
        }

        if (_healthBarBackgroundRenderer == null)
        {
            _healthBarBackgroundRenderer = healthBarBackgroundRendererReference;
        }
    }

    private void CaptureNativeScale()
    {
        Transform scaleTarget = visualScaleRootReference != null ? visualScaleRootReference : transform;
        _nativeScale = scaleTarget.localScale;
        _configuredScale = _nativeScale;
    }

    private void ApplyVisualTheme()
    {
        if (_healthBarFillRenderer != null)
        {
            if (healthBarFillSpriteOverride != null)
            {
                _healthBarFillRenderer.sprite = healthBarFillSpriteOverride;
            }

            _healthBarFillRenderer.color = _currentShield > 0 ? healthBarShieldColor : healthBarFillColor;
        }

        if (_healthBarBackgroundRenderer != null)
        {
            if (healthBarBackgroundSpriteOverride != null)
            {
                _healthBarBackgroundRenderer.sprite = healthBarBackgroundSpriteOverride;
            }

            _healthBarBackgroundRenderer.color = healthBarBackgroundColor;
        }
    }

    private void TriggerHitFeedback(DamageFeedbackType feedbackType)
    {
        switch (feedbackType)
        {
            case DamageFeedbackType.Bombard:
                _hitFlashColor = bombardHitFlashColor;
                _hitFlashDuration = bombardHitFlashDuration;
                _hitFlashTimer = bombardHitFlashDuration;
                _pulseScaleMultiplier = bombardHitPulseScale;
                _pulseDuration = hitPulseDuration;
                _pulseTimer = hitPulseDuration;
                break;

            case DamageFeedbackType.SlowField:
                _hitFlashColor = slowTintColor;
                _hitFlashDuration = slowFeedbackFlashDuration;
                _hitFlashTimer = slowFeedbackFlashDuration;
                _pulseScaleMultiplier = slowHitPulseScale;
                _pulseDuration = hitPulseDuration;
                _pulseTimer = hitPulseDuration;
                break;

            default:
                _hitFlashColor = standardHitFlashColor;
                _hitFlashDuration = standardHitFlashDuration;
                _hitFlashTimer = standardHitFlashDuration;
                _pulseScaleMultiplier = standardHitPulseScale;
                _pulseDuration = hitPulseDuration;
                _pulseTimer = hitPulseDuration;
                break;
        }
    }

    private void AdvanceFeedbackTimers()
    {
        if (_slowTimer > 0f)
        {
            _slowTimer -= Time.deltaTime;
            if (_slowTimer <= 0f)
            {
                _slowTimer = 0f;
                _slowMultiplier = 1f;
            }
        }

        if (_stealthTimer > 0f)
        {
            _stealthTimer = Mathf.Max(0f, _stealthTimer - Time.deltaTime);
        }

        if (_revealTimer > 0f)
        {
            _revealTimer = Mathf.Max(0f, _revealTimer - Time.deltaTime);
        }

        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer = Mathf.Max(0f, _hitFlashTimer - Time.deltaTime);
        }

        if (_pulseTimer > 0f)
        {
            _pulseTimer = Mathf.Max(0f, _pulseTimer - Time.deltaTime);
        }
    }

    private void RefreshBodyVisualState()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        Color bodyResult = bodyColor;
        if (_currentShield > 0)
        {
            bodyResult = Color.Lerp(bodyResult, shieldTintColor, 0.32f);
        }

        if (_slowTimer > 0f)
        {
            bodyResult = Color.Lerp(bodyResult, slowTintColor, 0.42f);
        }

        if (_stealthTimer > 0f && _revealTimer <= 0f && _definition != null && _definition.EntersStealthAfterFirstDirectHit)
        {
            bodyResult.a = Mathf.Min(bodyResult.a, _definition.HiddenAlpha);
        }

        if (_hitFlashTimer > 0f && _hitFlashDuration > 0.0001f)
        {
            float flashStrength = Mathf.Clamp01(_hitFlashTimer / _hitFlashDuration);
            bodyResult = Color.Lerp(bodyResult, _hitFlashColor, flashStrength);
        }

        _spriteRenderer.color = bodyResult;

        float pulseScale = 1f;
        if (_pulseTimer > 0f && _pulseDuration > 0.0001f)
        {
            float pulseProgress = 1f - Mathf.Clamp01(_pulseTimer / _pulseDuration);
            pulseScale = 1f + Mathf.Sin(pulseProgress * Mathf.PI) * (_pulseScaleMultiplier - 1f);
        }

        Transform scaleTarget = visualScaleRootReference != null ? visualScaleRootReference : transform;
        scaleTarget.localScale = _configuredScale * pulseScale;
    }
}
