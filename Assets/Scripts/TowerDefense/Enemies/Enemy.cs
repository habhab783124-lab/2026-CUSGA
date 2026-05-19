using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `Enemy` is the runtime bridge for path-following, health, and hit feedback.
///
/// At this stage of the project, the enemy still intentionally stays lightweight:
/// - it follows a fixed path
/// - it takes damage from towers
/// - it damages the base if it reaches the endpoint
///
/// The extra layer added in this pass is "readability feedback":
/// we want the player to immediately see the difference between
/// a precise hit, a slow-field application, and a bombard blast.
///
/// This script therefore owns:
/// 1. Movement along `EnemyPath`
/// 2. Health and health-bar updates
/// 3. Lightweight body flash / slow tint / scale pulse feedback
///
/// We keep the feedback inside the enemy instead of scattering it across towers,
/// because the enemy is the one object that knows how it should visually react when struck.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    /// <summary>
    /// `DamageFeedbackType` lets towers tell the enemy what kind of hit just happened.
    ///
    /// This keeps feedback intent explicit:
    /// - single-target tower: precise direct hit
    /// - slow-field tower: control hit
    /// - bombard tower: heavier blast hit
    /// </summary>
    public enum DamageFeedbackType
    {
        Standard,
        SlowField,
        Bombard
    }

    /// <summary>
    /// Active enemies are tracked in a flat list so towers can scan targets cheaply.
    /// This is still a very reasonable structure for a prototype tower-defense scale.
    /// </summary>
    private static readonly List<Enemy> ActiveEnemies = new List<Enemy>();

    [Header("Movement")]

    /// <summary>
    /// Distance tolerance used to decide whether the enemy has effectively reached a waypoint.
    /// </summary>
    [SerializeField] private float reachWaypointDistance = 0.05f;

    [Header("Body Look")]

    /// <summary>
    /// 敌人本体的主渲染器。
    ///
    /// 如果后续敌人做成多层结构，
    /// 这里可以显式指定哪一层代表“主体受击颜色反馈”。
    /// </summary>
    [SerializeField] private SpriteRenderer bodyRendererReference;

    /// <summary>
    /// 负责承载受击脉冲缩放的视觉根节点。
    ///
    /// 这样后续如果你想让血条或别的挂件不跟着一起缩放，
    /// 就可以单独把这层指定到真正的敌人视觉根上。
    /// </summary>
    [SerializeField] private Transform visualScaleRootReference;

    /// <summary>
    /// Base body color when the enemy is in a neutral state.
    ///
    /// 这套字段最早是为了“占位怪物也能靠纯色区分种类”而存在。
    /// 但现在项目已经接入了正式怪物 sprite 动画，
    /// 如果继续把常态主体直接染成强烈的纯色，
    /// 反而会把真实怪物画面压扁成“红黄小色块”的占位观感。
    ///
    /// 因此当前默认值改成白色，表示：
    /// - 常态下尽量尊重原始 sprite 外观
    /// - 只在减速 / 受击等事件发生时，短时叠加临时反馈色
    /// </summary>
    [SerializeField] private Color bodyColor = Color.white;

    /// <summary>
    /// While the enemy is slowed, we blend the body toward this color.
    /// This is a simple, art-replacement-friendly way to show control status.
    /// </summary>
    [SerializeField] private Color slowTintColor = new Color(0.42f, 0.95f, 0.9f, 1f);

    /// <summary>
    /// Precise hits use a bright flash so the player can read single-target focus fire.
    /// </summary>
    [SerializeField] private Color standardHitFlashColor = new Color(1f, 0.96f, 0.9f, 1f);

    /// <summary>
    /// Bombard hits use a warmer flash so blast impact reads heavier than a normal shot.
    /// </summary>
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
    [SerializeField] private Color healthBarBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Sprite healthBarFillSpriteOverride;
    [SerializeField] private Sprite healthBarBackgroundSpriteOverride;

    [Header("Health Bar References")]

    /// <summary>
    /// Scene-authored explicit reference chain for the health bar.
    /// This keeps the enemy prefab resilient against hierarchy renames.
    /// </summary>
    [SerializeField] private Transform healthBarRootReference;
    [SerializeField] private Transform healthBarFillReference;
    [SerializeField] private SpriteRenderer healthBarFillRendererReference;
    [SerializeField] private SpriteRenderer healthBarBackgroundRendererReference;

    [Header("Animation Runtime")]
    [SerializeField] private float animatorSpeedBaseline = 1.8f;
    [SerializeField] private float animatorSpeedMultiplier = 1f;
    [SerializeField] private float minAnimatorSpeed = 0.75f;
    [SerializeField] private float maxAnimatorSpeed = 2.2f;
    [SerializeField] private bool randomizeAnimationPhaseOnSpawn = true;

    [Header("Runtime Diagnostics")]
    [SerializeField] private bool enableRuntimeVisualDiagnostics = true;

    private SpriteRenderer _spriteRenderer;
    private Transform _healthBarRoot;
    private Transform _healthBarFill;
    private SpriteRenderer _healthBarFillRenderer;
    private SpriteRenderer _healthBarBackgroundRenderer;
    private Animator _bodyAnimator;

    private EnemyPath _path;
    private EnemyCatalogAsset _enemyCatalog;
    private EnemyCatalogAsset.EnemyArchetypeDefinition _currentDefinition;
    private EnemyArchetypeId _currentArchetypeId = EnemyArchetypeId.None;
    private Transform _enemyRoot;
    private float _moveSpeed;
    private float _slowMultiplier = 1f;
    private float _slowTimer;
    private int _maxHealth;
    private int _currentHealth;
    private int _scrapRewardOnDeath;
    private int _targetWaypointIndex;
    private bool _hasReachedBase;

    private Vector3 _baseScale = Vector3.one;
    private float _hitFlashTimer;
    private float _hitFlashDuration;
    private Color _hitFlashColor = Color.white;
    private float _pulseTimer;
    private float _pulseDuration;
    private float _pulseScaleMultiplier = 1f;
    private bool _hasLoggedRuntimeVisualSnapshot;
    private bool _hasInitializedAnimatorPhase;

    public static int ActiveEnemyCount => ActiveEnemies.Count;
    public bool IsAlive => _currentHealth > 0 && !_hasReachedBase;
    public EnemyCatalogAsset.EnemyArchetypeDefinition CurrentDefinition => _currentDefinition;
    public EnemyPath CurrentPath => _path;
    public Transform EnemyRoot => _enemyRoot != null ? _enemyRoot : transform.parent;
    public int CurrentWaypointIndex => Mathf.Max(0, _targetWaypointIndex - 1);
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool CanReceiveMechanicRepair => _currentDefinition != null ? _currentDefinition.CanBeRepairedByMechanic : true;

    public static Enemy GetActiveEnemy(int index)
    {
        return ActiveEnemies[index];
    }

    /// <summary>
    /// This remains a simple presentation hook for end-state cleanup.
    /// </summary>
    public void SetHealthBarVisible(bool visible)
    {
        CacheReferences();

        if (_healthBarRoot != null)
        {
            _healthBarRoot.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Editor-time helper: if the fill transform is assigned but its renderer is not,
    /// we can safely derive that reference without relying on object names.
    /// </summary>
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
        _baseScale = (visualScaleRootReference != null ? visualScaleRootReference : transform).localScale;
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

    /// <summary>
    /// Spawn-time runtime setup.
    /// We intentionally reuse one prototype and inject path / speed / health / scrap reward per wave.
    /// </summary>
    public void Initialize(EnemyPath path, float moveSpeed, int maxHealth, int scrapRewardOnDeath = 0)
    {
        CacheReferences();
        _baseScale = (visualScaleRootReference != null ? visualScaleRootReference : transform).localScale;
        _enemyRoot = transform.parent;
        if (_enemyCatalog == null)
        {
            _currentDefinition = null;
            _currentArchetypeId = EnemyArchetypeId.None;
        }

        ApplyVisualTheme();

        _path = path;
        _moveSpeed = moveSpeed;
        _maxHealth = Mathf.Max(1, maxHealth);
        _currentHealth = _maxHealth;
        _scrapRewardOnDeath = Mathf.Max(0, scrapRewardOnDeath);
        _targetWaypointIndex = 1;
        _hasReachedBase = false;
        _slowMultiplier = 1f;
        _slowTimer = 0f;
        _hitFlashTimer = 0f;
        _pulseTimer = 0f;
        _pulseScaleMultiplier = 1f;
        _hasLoggedRuntimeVisualSnapshot = false;
        _hasInitializedAnimatorPhase = false;

        if (_path != null)
        {
            transform.position = _path.GetSpawnPosition();
        }

        ApplyAnimatorRuntimeSettings();
        RefreshBodyVisualState();
        UpdateHealthBar();
    }

    /// <summary>
    /// 这是给新版“敌人目录 + 机制模块”工作流准备的兼容初始化入口。
    ///
    /// 当前第一关主链仍然可以继续走旧的 `Initialize(path, moveSpeed, health, reward)`，
    /// 但当后续关卡或新 prefab 已经开始依赖 `EnemyCatalogAsset` 时，
    /// 它们也能通过这个重载把目录定义、安全地桥接进这份旧敌人实现。
    /// </summary>
    public void Initialize(
        EnemyPath path,
        EnemyCatalogAsset enemyCatalog,
        EnemyArchetypeId archetypeId,
        GameObject enemyPrototypePrefab,
        Transform enemyRoot)
    {
        CacheReferences();
        _enemyCatalog = enemyCatalog;
        _currentArchetypeId = archetypeId;
        _currentDefinition = enemyCatalog != null ? enemyCatalog.GetDefinition(archetypeId) : null;
        _enemyRoot = enemyRoot != null ? enemyRoot : transform.parent;

        float moveSpeed = _currentDefinition != null ? _currentDefinition.MoveSpeed : 1.8f;
        int maxHealth = _currentDefinition != null ? _currentDefinition.MaxHealth : 3;
        int scrapReward = _currentDefinition != null ? _currentDefinition.ScrapReward : 0;

        if (_currentDefinition != null)
        {
            // 目录里的 BodyColor 过去主要服务“占位怪物纯色区分”。
            // 现在正式怪物已经有自己的 sprite 动画，为了让 Play 视图显示真实外观，
            // 这里不再把目录颜色直接当作常态主体染色。
            // 真正需要强调状态时，仍然走 slow / hit flash 这些临时反馈色。
            bodyColor = Color.white;

            // 运行时这里不能再假设“当前刷出来的 prefab 一定已经是正确怪物本体”。
            //
            // 我们已经抓到过真实日志：运行时实例的 archetype 是对的，
            // 但实际 bodyRenderer 仍然拿着 `Square` 这张占位 sprite。
            // 这说明运行时有时候仍会落回通用 EnemyPrototype，然后再套上敌人目录数据。
            //
            // 所以这里要做一层更鲁棒的桥接：
            // - 只要目录里已经知道这个 archetype 对应哪个 RuntimePrefab，
            //   就主动把“那只 prefab 的可视外观配置”拷到当前实例上。
            //
            // 这样即使底层刷出来的是通用原型，当前实例也会被立即换成对应怪物的
            // sprite / animator / 血条布局，而不是继续显示占位方块。
            ApplyRuntimeVisualFromDefinitionPrefab(_currentDefinition);

            if (_currentDefinition.BodySpriteOverride != null && bodyRendererReference != null)
            {
                bodyRendererReference.sprite = _currentDefinition.BodySpriteOverride;
            }

            Transform scaleTarget = visualScaleRootReference != null ? visualScaleRootReference : transform;
            scaleTarget.localScale = _baseScale * _currentDefinition.BodyScaleMultiplier;
            _baseScale = scaleTarget.localScale;
        }

        Initialize(path, moveSpeed, maxHealth, scrapReward);
        BindMechanicModules();
    }

    private void ApplyRuntimeVisualFromDefinitionPrefab(EnemyCatalogAsset.EnemyArchetypeDefinition definition)
    {
        if (definition == null)
        {
            return;
        }
        Transform currentVisualRoot = visualScaleRootReference != null ? visualScaleRootReference : transform;
        SpriteRenderer currentBodyRenderer = bodyRendererReference != null ? bodyRendererReference : GetComponent<SpriteRenderer>();

        if (currentBodyRenderer != null)
        {
            if (definition.RuntimeBodySprite != null)
            {
                currentBodyRenderer.sprite = definition.RuntimeBodySprite;
            }
        }

        if (currentVisualRoot != null && definition.RuntimeAnimatorController != null)
        {
            Animator currentAnimator = currentVisualRoot.GetComponent<Animator>();
            if (currentAnimator == null)
            {
                currentAnimator = currentVisualRoot.gameObject.AddComponent<Animator>();
            }

            currentAnimator.runtimeAnimatorController = definition.RuntimeAnimatorController;
        }

        if (definition.RuntimePrefab != null)
        {
            Enemy sourceEnemy = definition.RuntimePrefab.GetComponent<Enemy>();
            SpriteRenderer sourceBodyRenderer = sourceEnemy != null && sourceEnemy.bodyRendererReference != null
                ? sourceEnemy.bodyRendererReference
                : definition.RuntimePrefab.GetComponent<SpriteRenderer>();

            Transform sourceVisualRoot = sourceEnemy != null && sourceEnemy.visualScaleRootReference != null
                ? sourceEnemy.visualScaleRootReference
                : definition.RuntimePrefab.transform;

            Animator sourceAnimator = sourceVisualRoot != null ? sourceVisualRoot.GetComponent<Animator>() : null;

            if (currentBodyRenderer != null && sourceBodyRenderer != null)
            {
                currentBodyRenderer.sortingLayerID = sourceBodyRenderer.sortingLayerID;
                currentBodyRenderer.sortingOrder = sourceBodyRenderer.sortingOrder;
                currentBodyRenderer.flipX = sourceBodyRenderer.flipX;
                currentBodyRenderer.flipY = sourceBodyRenderer.flipY;
            }

            if (currentVisualRoot != null && sourceAnimator != null)
            {
                Animator currentAnimator = currentVisualRoot.GetComponent<Animator>();
                if (currentAnimator == null)
                {
                    currentAnimator = currentVisualRoot.gameObject.AddComponent<Animator>();
                }

                if (definition.RuntimeAnimatorController == null)
                {
                    currentAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
                }

                currentAnimator.applyRootMotion = sourceAnimator.applyRootMotion;
                currentAnimator.cullingMode = sourceAnimator.cullingMode;
                currentAnimator.updateMode = sourceAnimator.updateMode;
            }

            if (healthBarRootReference != null && sourceEnemy != null && sourceEnemy.healthBarRootReference != null)
            {
                healthBarRootReference.localPosition = sourceEnemy.healthBarRootReference.localPosition;
                healthBarRootReference.localScale = sourceEnemy.healthBarRootReference.localScale;
            }
        }
    }

    /// <summary>
    /// `Update()` keeps movement and presentation feedback in one place.
    ///
    /// We advance timers first, so body tint / hit flash / pulse stay responsive,
    /// then continue with waypoint motion if the match is still active.
    /// </summary>
    private void Update()
    {
        AdvanceFeedbackTimers();
        ApplyAnimatorRuntimeSettings();
        RefreshBodyVisualState();
        TryLogRuntimeVisualSnapshot();

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

    /// <summary>
    /// Standard damage entry point kept for compatibility with existing callers.
    /// </summary>
    public void TakeDamage(int amount)
    {
        TakeDamage(amount, DamageFeedbackType.Standard);
    }

    /// <summary>
    /// Extended damage entry point with explicit feedback intent.
    /// Towers use this overload when they want the enemy to react differently to different hit families.
    /// </summary>
    public void TakeDamage(int amount, DamageFeedbackType feedbackType)
    {
        if (amount <= 0 || _currentHealth <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        TriggerHitFeedback(feedbackType);
        UpdateHealthBar();

        if (_currentHealth == 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Applying slow now also counts as a visible combat event.
    /// This matters because the slow tower intentionally starts with zero damage,
    /// so the player still needs some immediate feedback that the control effect landed.
    /// </summary>
    public void ApplySlow(float slowMultiplier, float duration)
    {
        if (_currentHealth <= 0)
        {
            return;
        }

        _slowMultiplier = Mathf.Clamp(Mathf.Min(_slowMultiplier, slowMultiplier), 0.15f, 1f);
        _slowTimer = Mathf.Max(_slowTimer, duration);
        TriggerHitFeedback(DamageFeedbackType.SlowField);
    }

    private void ReachBase()
    {
        if (_hasReachedBase)
        {
            return;
        }

        _hasReachedBase = true;
        TowerDefenseGame.Instance?.DamageBase(1);
        Destroy(gameObject);
    }

    private void Die()
    {
        NotifyModulesBeforeDeath();

        // Enemy death is now part of the economy loop:
        // after a legal kill, the current wave definition can immediately feed scrap back into the player's resource pool.
        if (_scrapRewardOnDeath > 0 && TowerDefenseGame.Instance != null && !TowerDefenseGame.Instance.IsGameOver)
        {
            TowerDefenseGame.Instance.AddScrap(_scrapRewardOnDeath);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 新版特殊机制模块会在敌人正式销毁前收到一次统一通知。
    /// 旧第一关里就算场景上没有任何模块，这个循环也只会零成本略过。
    /// </summary>
    private void NotifyModulesBeforeDeath()
    {
        EnemyMechanicModule[] modules = GetComponents<EnemyMechanicModule>();
        for (int index = 0; index < modules.Length; index++)
        {
            if (modules[index] != null)
            {
                modules[index].OnBeforeDeath();
            }
        }
    }

    private void BindMechanicModules()
    {
        EnemyMechanicModule[] modules = GetComponents<EnemyMechanicModule>();
        for (int index = 0; index < modules.Length; index++)
        {
            if (modules[index] != null)
            {
                modules[index].BindOwner(this);
            }
        }
    }

    /// <summary>
    /// 供新版护盾光环模块调用。
    /// 当前旧第一关并没有完整护盾系统，所以这里先采用一个最保守的兼容实现：
    /// - 只要目标还活着，就把它当作一次“轻度修复/护盾补偿”事件处理
    /// - 不引入额外状态字段，避免反向改坏旧关卡的伤害链
    /// </summary>
    public void ApplyShieldIfWeaker(int shieldAmount)
    {
        if (!IsAlive || shieldAmount <= 0)
        {
            return;
        }

        ReceiveRepair(shieldAmount);
    }

    /// <summary>
    /// 供新版机械师模块调用的兼容回血入口。
    /// </summary>
    public void ReceiveRepair(int amount)
    {
        if (!IsAlive || amount <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        UpdateHealthBar();
    }

    /// <summary>
    /// 供新版死亡分裂模块调用的兼容子怪生成入口。
    /// 如果当前没有目录定义或 prefab 根本不存在，就安静跳过，不把旧第一关主链炸掉。
    /// </summary>
    public void SpawnConfiguredChild(
        EnemyArchetypeId childArchetypeId,
        Vector3 spawnPosition,
        int waypointIndex,
        string childName)
    {
        if (_enemyCatalog == null)
        {
            return;
        }

        EnemyCatalogAsset.EnemyArchetypeDefinition childDefinition = _enemyCatalog.GetDefinition(childArchetypeId);
        if (childDefinition == null || childDefinition.RuntimePrefab == null || _path == null)
        {
            return;
        }

        GameObject childObject = Instantiate(
            childDefinition.RuntimePrefab,
            spawnPosition,
            Quaternion.identity,
            EnemyRoot);
        childObject.name = string.IsNullOrWhiteSpace(childName) ? childDefinition.DisplayName : childName;

        Enemy childEnemy = childObject.GetComponent<Enemy>();
        if (childEnemy == null)
        {
            return;
        }

        childEnemy.Initialize(_path, _enemyCatalog, childArchetypeId, childDefinition.RuntimePrefab, EnemyRoot);
        childEnemy.SetWaypointIndexForSpawn(Mathf.Max(waypointIndex, 0));
    }

    private void SetWaypointIndexForSpawn(int waypointIndex)
    {
        _targetWaypointIndex = Mathf.Max(1, waypointIndex + 1);
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
    }

    private void CacheReferences()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = bodyRendererReference != null ? bodyRendererReference : GetComponent<SpriteRenderer>();
            bodyRendererReference = _spriteRenderer;
        }

        if (_bodyAnimator == null)
        {
            Transform visualRoot = visualScaleRootReference != null ? visualScaleRootReference : transform;
            _bodyAnimator = visualRoot != null ? visualRoot.GetComponent<Animator>() : null;
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

    private void ApplyVisualTheme()
    {
        if (_healthBarFillRenderer != null)
        {
            if (healthBarFillSpriteOverride != null)
            {
                _healthBarFillRenderer.sprite = healthBarFillSpriteOverride;
            }

            _healthBarFillRenderer.color = healthBarFillColor;
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

    /// <summary>
    /// A single helper makes the combat reaction readable without introducing a heavyweight status-effect system.
    /// </summary>
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

    /// <summary>
    /// Body feedback is layered in a deterministic order:
    /// 1. base color
    /// 2. slow tint
    /// 3. temporary hit flash
    ///
    /// This gives us readable combat feedback without needing per-hit child effects on every enemy.
    /// </summary>
    private void RefreshBodyVisualState()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        Color bodyResult = bodyColor;
        if (_slowTimer > 0f)
        {
            bodyResult = Color.Lerp(bodyResult, slowTintColor, 0.42f);
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
        scaleTarget.localScale = _baseScale * pulseScale;
    }

    private void ApplyAnimatorRuntimeSettings()
    {
        if (_bodyAnimator == null)
        {
            return;
        }

        float safeBaseline = Mathf.Max(0.05f, animatorSpeedBaseline);
        float normalizedMoveSpeed = _moveSpeed > 0f ? _moveSpeed / safeBaseline : 1f;
        float animationSpeed = Mathf.Clamp(normalizedMoveSpeed * animatorSpeedMultiplier, minAnimatorSpeed, maxAnimatorSpeed);
        _bodyAnimator.speed = animationSpeed;

        if (!_hasInitializedAnimatorPhase && randomizeAnimationPhaseOnSpawn)
        {
            AnimatorStateInfo stateInfo = _bodyAnimator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.length > 0f || _bodyAnimator.runtimeAnimatorController != null)
            {
                _bodyAnimator.Play(0, 0, Random.value);
                _hasInitializedAnimatorPhase = true;
            }
        }
        else if (!_hasInitializedAnimatorPhase)
        {
            _hasInitializedAnimatorPhase = true;
        }
    }

    private void TryLogRuntimeVisualSnapshot()
    {
        if (!enableRuntimeVisualDiagnostics || _hasLoggedRuntimeVisualSnapshot)
        {
            return;
        }

        if (_spriteRenderer == null)
        {
            return;
        }

        Transform visualRoot = visualScaleRootReference != null ? visualScaleRootReference : transform;
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        SpriteRenderer visualRootRenderer = visualRoot != null ? visualRoot.GetComponent<SpriteRenderer>() : null;

        string activeSpriteName = _spriteRenderer.sprite != null ? _spriteRenderer.sprite.name : "null";
        string rootSpriteName = rootRenderer != null && rootRenderer.sprite != null ? rootRenderer.sprite.name : "null";
        string visualRootSpriteName = visualRootRenderer != null && visualRootRenderer.sprite != null ? visualRootRenderer.sprite.name : "null";
        string activeTextureName = _spriteRenderer.sprite != null && _spriteRenderer.sprite.texture != null ? _spriteRenderer.sprite.texture.name : "null";
        Vector2 activeTextureSize = _spriteRenderer.sprite != null && _spriteRenderer.sprite.texture != null
            ? new Vector2(_spriteRenderer.sprite.texture.width, _spriteRenderer.sprite.texture.height)
            : Vector2.zero;
        string definitionRuntimeBodySpriteName = _currentDefinition != null && _currentDefinition.RuntimeBodySprite != null
            ? _currentDefinition.RuntimeBodySprite.name
            : "null";
        string definitionRuntimeBodyTextureName = _currentDefinition != null &&
                                                  _currentDefinition.RuntimeBodySprite != null &&
                                                  _currentDefinition.RuntimeBodySprite.texture != null
            ? _currentDefinition.RuntimeBodySprite.texture.name
            : "null";

        Debug.Log(
            $"[EnemyVisualDiag] enemy={name} archetype={_currentArchetypeId} " +
            $"bodyRenderer={_spriteRenderer.gameObject.name} activeSprite={activeSpriteName} color={_spriteRenderer.color} " +
            $"sortingOrder={_spriteRenderer.sortingOrder} enabled={_spriteRenderer.enabled} worldPos={_spriteRenderer.transform.position} " +
            $"rootSprite={rootSpriteName} visualRootSprite={visualRootSpriteName} " +
            $"activeTexture={activeTextureName} textureSize={activeTextureSize} " +
            $"definitionRuntimeBodySprite={definitionRuntimeBodySpriteName} definitionRuntimeBodyTexture={definitionRuntimeBodyTextureName} " +
            $"rootScale={transform.localScale} visualRootScale={(visualRoot != null ? visualRoot.localScale.ToString() : "null")}",
            this);

        _hasLoggedRuntimeVisualSnapshot = true;
    }
}
