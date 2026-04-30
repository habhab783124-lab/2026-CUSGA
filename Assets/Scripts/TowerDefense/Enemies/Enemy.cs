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

    private static readonly List<Enemy> ActiveEnemies = new List<Enemy>(); // 中文：激活敌人列表

    [Header("Movement")]
    [SerializeField] private float reachWaypointDistance = 0.05f; // 中文：reach路径点Distance

    [Header("Body Look")]
    [SerializeField] private SpriteRenderer bodyRendererReference; // 中文：主体Renderer引用
    [SerializeField] private Transform visualScaleRootReference; // 中文：视觉缩放根节点引用
    [SerializeField] private Color bodyColor = new Color(0.9f, 0.25f, 0.25f, 1f); // 中文：主体颜色
    [SerializeField] private Color shieldTintColor = new Color(0.42f, 0.9f, 1f, 1f); // 中文：护盾Tint颜色
    [SerializeField] private Color slowTintColor = new Color(0.42f, 0.95f, 0.9f, 1f); // 中文：减速Tint颜色
    [SerializeField] private Color standardHitFlashColor = new Color(1f, 0.96f, 0.9f, 1f); // 中文：standardHit闪光颜色
    [SerializeField] private Color bombardHitFlashColor = new Color(1f, 0.74f, 0.45f, 1f); // 中文：炸弹Hit闪光颜色

    [Header("Body Feedback Timing")]
    [SerializeField] private float standardHitFlashDuration = 0.08f; // 中文：standardHit闪光持续时间
    [SerializeField] private float bombardHitFlashDuration = 0.16f; // 中文：炸弹Hit闪光持续时间
    [SerializeField] private float slowFeedbackFlashDuration = 0.1f; // 中文：减速反馈闪光持续时间
    [SerializeField] private float standardHitPulseScale = 1.05f; // 中文：standardHit脉冲缩放
    [SerializeField] private float bombardHitPulseScale = 1.13f; // 中文：炸弹Hit脉冲缩放
    [SerializeField] private float slowHitPulseScale = 1.04f; // 中文：减速Hit脉冲缩放
    [SerializeField] private float hitPulseDuration = 0.12f; // 中文：hit脉冲持续时间

    [Header("Health Bar Visuals")]
    [SerializeField] private Color healthBarFillColor = new Color(0.2f, 0.9f, 0.35f, 1f); // 中文：生命BarFill颜色
    [SerializeField] private Color healthBarShieldColor = new Color(0.44f, 0.9f, 1f, 1f); // 中文：生命Bar护盾颜色
    [SerializeField] private Color healthBarBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f); // 中文：生命Bar背景颜色
    [SerializeField] private Sprite healthBarFillSpriteOverride; // 中文：生命BarFill精灵覆盖
    [SerializeField] private Sprite healthBarBackgroundSpriteOverride; // 中文：生命Bar背景精灵覆盖

    [Header("Health Bar References")]
    [SerializeField] private Transform healthBarRootReference; // 中文：生命Bar根节点引用
    [SerializeField] private Transform healthBarFillReference; // 中文：生命BarFill引用
    [SerializeField] private SpriteRenderer healthBarFillRendererReference; // 中文：生命BarFillRenderer引用
    [SerializeField] private SpriteRenderer healthBarBackgroundRendererReference; // 中文：生命Bar背景Renderer引用

    private SpriteRenderer _spriteRenderer; // 中文：精灵Renderer
    private Transform _healthBarRoot; // 中文：生命Bar根节点
    private Transform _healthBarFill; // 中文：生命BarFill
    private SpriteRenderer _healthBarFillRenderer; // 中文：生命BarFillRenderer
    private SpriteRenderer _healthBarBackgroundRenderer; // 中文：生命Bar背景Renderer
    private EnemyMechanicModule[] _mechanicModules = new EnemyMechanicModule[0]; // 中文：机制模块列表
    private EnemyStealthModule _stealthModule; // 中文：隐身模块

    private EnemyPath _path; // 中文：路径
    private EnemyCatalogAsset _enemyCatalog; // 中文：敌人目录
    private EnemyCatalogAsset.EnemyArchetypeDefinition _definition; // 中文：定义
    private GameObject _enemyPrototypePrefab; // 中文：敌人原型预制体
    private Transform _enemyRoot; // 中文：敌人根节点

    private float _moveSpeed; // 中文：move速度
    private float _slowMultiplier = 1f; // 中文：减速倍率
    private float _slowTimer; // 中文：减速计时器
    private int _maxHealth; // 中文：最大生命
    private int _currentHealth; // 中文：当前生命
    private int _currentShield; // 中文：当前护盾
    private int _scrapRewardOnDeath; // 中文：废料RewardOn死亡
    private int _baseDamageToBase = 1; // 中文：基础伤害到基础
    private int _targetWaypointIndex; // 中文：目标路径点Index
    private bool _hasReachedBase; // 中文：是否有Reached基础

    private EnemyArmorTier _armorTier = EnemyArmorTier.None; // 中文：armorTier
    private float _nonPiercingDamageMultiplier = 1f; // 中文：nonPiercing伤害倍率
    private bool _ignoresSlowEffects; // 中文：ignores减速Effects
    private bool _canBeRepairedByMechanic; // 中文：能否BeRepairedBy机制

    private Vector3 _nativeScale = Vector3.one; // 中文：native缩放
    private Vector3 _configuredScale = Vector3.one; // 中文：configured缩放
    private float _bodyScaleMultiplier = 1f; // 中文：主体缩放倍率
    private float _hitFlashTimer; // 中文：hit闪光计时器
    private float _hitFlashDuration; // 中文：hit闪光持续时间
    private Color _hitFlashColor = Color.white; // 中文：hit闪光颜色
    private float _pulseTimer; // 中文：脉冲计时器
    private float _pulseDuration; // 中文：脉冲持续时间
    private float _pulseScaleMultiplier = 1f; // 中文：脉冲缩放倍率

    public static int ActiveEnemyCount => ActiveEnemies.Count; // 中文：激活敌人数量

    public static Enemy GetActiveEnemy(int index)
    {
        return ActiveEnemies[index];
    }

    public EnemyArchetypeId ArchetypeId => _definition != null ? _definition.ArchetypeId : EnemyArchetypeId.None; // 中文：原型类别标识
    public bool CanBeDirectlyTargeted => _stealthModule == null || _stealthModule.CanBeDirectlyTargeted; // 中文：能否BeDirectlyTargeted
    public int CurrentHealth => _currentHealth; // 中文：当前生命
    public int MaxHealth => _maxHealth; // 中文：最大生命
    public bool IsAlive => _currentHealth > 0; // 中文：是否Alive
    public bool CanReceiveMechanicRepair => _canBeRepairedByMechanic && _currentHealth > 0; // 中文：能否Receive机制修理
    internal EnemyCatalogAsset.EnemyArchetypeDefinition CurrentDefinition => _definition; // 中文：当前定义
    internal EnemyPath CurrentPath => _path; // 中文：当前路径
    internal Transform EnemyRoot => _enemyRoot; // 中文：敌人根节点
    internal int CurrentWaypointIndex => _targetWaypointIndex; // 中文：当前路径点Index

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

        CacheMechanicModules();
    }

    private void Awake()
    {
        CacheReferences();
        CacheMechanicModules();
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
        TickMechanicModules(Time.deltaTime);
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

        NotifyModulesDamageResolved(isAreaDamage);

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
        if (_currentHealth <= 0 || _stealthModule == null)
        {
            return;
        }

        _stealthModule.ApplyDetection(duration);
    }

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
        CacheMechanicModules();
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

        BindMechanicModules();
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

    /// <summary>
    /// 按当前敌人目录继续生成某个子类型敌人。
    ///
    /// 这个入口主要给死亡分裂模块使用，
    /// 让模块不必知道“应该怎么找目录、怎么选 prefab、怎么初始化子怪”这些基础细节。
    /// </summary>
    internal void SpawnConfiguredChild(
        EnemyArchetypeId childType,
        Vector3 spawnPosition,
        int targetWaypointIndexOverride,
        string objectName)
    {
        if (_enemyCatalog == null || _enemyRoot == null || _path == null || childType == EnemyArchetypeId.None)
        {
            return;
        }

        EnemyCatalogAsset.EnemyArchetypeDefinition childDefinition = _enemyCatalog.GetDefinition(childType);
        GameObject childPrototype = childDefinition != null && childDefinition.RuntimePrefab != null
            ? childDefinition.RuntimePrefab
            : _enemyPrototypePrefab;
        if (childPrototype == null)
        {
            return;
        }

        GameObject childObject = Instantiate(childPrototype, spawnPosition, Quaternion.identity, _enemyRoot);
        childObject.name = string.IsNullOrWhiteSpace(objectName) ? childType.ToString() : objectName;
        childObject.SetActive(true);

        Enemy childEnemy = childObject.GetComponent<Enemy>();
        if (childEnemy == null)
        {
            return;
        }

        childEnemy.Initialize(
            path: _path,
            enemyCatalog: _enemyCatalog,
            archetypeId: childType,
            enemyPrototypePrefab: childPrototype,
            enemyRoot: _enemyRoot,
            spawnPositionOverride: spawnPosition,
            targetWaypointIndexOverride: targetWaypointIndexOverride);
    }

    private void Die()
    {
        NotifyModulesBeforeDeath();

        if (_scrapRewardOnDeath > 0 && TowerDefenseGame.Instance != null && !TowerDefenseGame.Instance.IsGameOver)
        {
            TowerDefenseGame.Instance.AddScrap(_scrapRewardOnDeath);
        }

        Destroy(gameObject);
    }

    internal void ReceiveRepair(int amount)
    {
        if (amount <= 0 || _currentHealth <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        TriggerHitFeedback(DamageFeedbackType.Standard);
        UpdateHealthBar();
    }

    internal void ApplyShieldIfWeaker(int shieldAmount)
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

    /// <summary>
    /// 缓存当前敌人 prefab 上实际挂着的机制模块。
    ///
    /// 这里刻意不通过敌人类型名字去猜有哪些能力，
    /// 而是以 prefab 上真实挂载的组件为准。
    /// 这样以后你自己做新敌人时，也会更符合 Unity 的编辑器工作流：
    /// 看组件就能知道这只怪到底拥有哪些额外机制。
    /// </summary>
    private void CacheMechanicModules()
    {
        _mechanicModules = GetComponents<EnemyMechanicModule>();
        _stealthModule = null;

        for (int moduleIndex = 0; moduleIndex < _mechanicModules.Length; moduleIndex++)
        {
            if (_mechanicModules[moduleIndex] is EnemyStealthModule stealthModule)
            {
                _stealthModule = stealthModule;
            }
        }
    }

    /// <summary>
    /// 当敌人的目录定义和运行时上下文已经准备好后，
    /// 统一把它们绑定给当前 prefab 上挂着的机制模块。
    /// </summary>
    private void BindMechanicModules()
    {
        for (int moduleIndex = 0; moduleIndex < _mechanicModules.Length; moduleIndex++)
        {
            if (_mechanicModules[moduleIndex] != null)
            {
                _mechanicModules[moduleIndex].BindOwner(this);
            }
        }
    }

    /// <summary>
    /// 每帧把时间推进委托给机制模块。
    /// 这样持续运行的特殊能力就不再继续堆进 `Enemy.Update()` 自己体内。
    /// </summary>
    private void TickMechanicModules(float deltaTime)
    {
        if (_currentHealth <= 0)
        {
            return;
        }

        for (int moduleIndex = 0; moduleIndex < _mechanicModules.Length; moduleIndex++)
        {
            if (_mechanicModules[moduleIndex] != null && _mechanicModules[moduleIndex].enabled)
            {
                _mechanicModules[moduleIndex].Tick(deltaTime);
            }
        }
    }

    /// <summary>
    /// 伤害结算完成后，把命中上下文继续通知给机制模块。
    /// 目前主要是给隐身模块判断“首次直接命中后进入隐身”。
    /// </summary>
    private void NotifyModulesDamageResolved(bool isAreaDamage)
    {
        for (int moduleIndex = 0; moduleIndex < _mechanicModules.Length; moduleIndex++)
        {
            if (_mechanicModules[moduleIndex] != null && _mechanicModules[moduleIndex].enabled)
            {
                _mechanicModules[moduleIndex].OnDamageResolved(isAreaDamage);
            }
        }
    }

    /// <summary>
    /// 在敌人正式死亡销毁前，先给机制模块一个收尾机会。
    /// 例如死亡分裂就在这里生成子怪。
    /// </summary>
    private void NotifyModulesBeforeDeath()
    {
        for (int moduleIndex = 0; moduleIndex < _mechanicModules.Length; moduleIndex++)
        {
            if (_mechanicModules[moduleIndex] != null && _mechanicModules[moduleIndex].enabled)
            {
                _mechanicModules[moduleIndex].OnBeforeDeath();
            }
        }
    }

    /// <summary>
    /// 汇总所有机制模块对身体透明度的影响。
    /// 当前主要是隐身模块会把这个值压低。
    /// </summary>
    private float GetMechanicBodyAlphaMultiplier()
    {
        float alphaMultiplier = 1f;
        for (int moduleIndex = 0; moduleIndex < _mechanicModules.Length; moduleIndex++)
        {
            if (_mechanicModules[moduleIndex] == null || !_mechanicModules[moduleIndex].enabled)
            {
                continue;
            }

            alphaMultiplier = Mathf.Min(alphaMultiplier, _mechanicModules[moduleIndex].BodyAlphaMultiplier);
        }

        return Mathf.Clamp(alphaMultiplier, 0.05f, 1f);
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
        bodyResult.a = Mathf.Min(bodyResult.a, GetMechanicBodyAlphaMultiplier());

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
