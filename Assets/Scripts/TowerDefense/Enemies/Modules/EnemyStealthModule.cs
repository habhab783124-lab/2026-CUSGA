using UnityEngine;

/// <summary>
/// `EnemyStealthModule` 负责承接“受首次直接命中后进入隐身、被探测后暂时显形”这条机制链。
///
/// 注意这里故意不去接管敌人的移动或受伤主链，
/// 它只回答三件事：
/// 1. 这只敌人当前能不能被直接锁定
/// 2. 这只敌人当前应不应该降低透明度
/// 3. 何时进入隐身、何时被探测显形
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class EnemyStealthModule : EnemyMechanicModule
{
    [Header("参数来源")]
    [Tooltip("关闭时优先使用 EnemyCatalogAsset 里的默认隐身参数；开启后改用当前 prefab 上这组本地参数。")]
    [SerializeField, InspectorName("启用本地覆盖")] private bool useLocalOverrides; // 中文：使用本地覆盖

    [Header("本地覆盖")]
    [Tooltip("当启用本地覆盖后，这个开关决定该 prefab 是否真的启用隐身机制。")]
    [SerializeField, InspectorName("启用隐身")] private bool localStealthEnabled = true; // 中文：本地隐身Enabled
    [Tooltip("首次被直接命中后，进入隐身状态的持续时间。")]
    [SerializeField, Min(0.1f), InspectorName("隐身持续时间")] private float localStealthDuration = 1.8f; // 中文：本地隐身持续时间
    [Tooltip("被探测或显形后，维持可被锁定状态的时间。")]
    [SerializeField, Min(0.1f), InspectorName("显形持续时间")] private float localRevealDuration = 1.2f; // 中文：本地Reveal持续时间
    [Tooltip("隐身时身体最低透明度倍率。值越小越难看见。")]
    [SerializeField, Range(0.05f, 1f), InspectorName("隐身透明度")] private float localHiddenAlpha = 0.22f; // 中文：本地Hidden透明度

    private bool _stealthTriggered; // 中文：隐身Triggered
    private float _stealthTimer; // 中文：隐身计时器
    private float _revealTimer; // 中文：reveal计时器

    private bool UsesStealth => // 中文：Uses隐身
        useLocalOverrides
            ? localStealthEnabled
            : Definition != null && Definition.EntersStealthAfterFirstDirectHit;

    public override bool CanBeDirectlyTargeted => !IsHidden; // 中文：能否BeDirectlyTargeted

    public override float BodyAlphaMultiplier => IsHidden ? ResolveHiddenAlpha() : 1f; // 中文：主体透明度倍率

    private bool IsHidden => UsesStealth && _stealthTimer > 0f && _revealTimer <= 0f; // 中文：是否Hidden

    public override void ResetRuntimeState()
    {
        _stealthTriggered = false;
        _stealthTimer = 0f;
        _revealTimer = 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (!UsesStealth)
        {
            return;
        }

        if (_stealthTimer > 0f)
        {
            _stealthTimer = Mathf.Max(0f, _stealthTimer - deltaTime);
        }

        if (_revealTimer > 0f)
        {
            _revealTimer = Mathf.Max(0f, _revealTimer - deltaTime);
        }
    }

    public override void OnDamageResolved(bool isAreaDamage)
    {
        if (!UsesStealth || _stealthTriggered || isAreaDamage || Owner == null || !Owner.IsAlive)
        {
            return;
        }

        _stealthTriggered = true;
        _stealthTimer = Mathf.Max(_stealthTimer, ResolveStealthDuration());
        _revealTimer = 0f;
    }

    public override void ApplyDetection(float duration)
    {
        if (!UsesStealth || Owner == null || !Owner.IsAlive)
        {
            return;
        }

        float resolvedDuration = Mathf.Max(duration, ResolveRevealDuration());
        _revealTimer = Mathf.Max(_revealTimer, resolvedDuration);
    }

    /// <summary>
    /// 目录资产仍然是这类敌人的全局默认值来源，
    /// 但只要 prefab 勾选了本地覆盖，这里就会优先吃 prefab 参数。
    /// </summary>
    private float ResolveStealthDuration()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0.1f, localStealthDuration);
        }

        return Definition.StealthDuration;
    }

    private float ResolveRevealDuration()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0.1f, localRevealDuration);
        }

        return Definition.SignalRevealDuration;
    }

    private float ResolveHiddenAlpha()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Clamp(localHiddenAlpha, 0.05f, 1f);
        }

        return Definition.HiddenAlpha;
    }
}
