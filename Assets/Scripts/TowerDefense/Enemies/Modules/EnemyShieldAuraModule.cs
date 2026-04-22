using UnityEngine;

/// <summary>
/// `EnemyShieldAuraModule` 负责周期性给附近敌人补护盾。
///
/// 护盾值和范围等数值仍然来自 `EnemyCatalogAsset`，
/// 这样你以后改平衡时，优先改目录资产即可；
/// prefab 上的这个模块更像是“声明这类敌人确实拥有护盾光环能力”。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class EnemyShieldAuraModule : EnemyMechanicModule
{
    [Header("Authoring Source")]
    [Tooltip("关闭时优先使用 EnemyCatalogAsset 里的护盾光环参数；开启后改用当前 prefab 上这组本地参数。")]
    [SerializeField] private bool useLocalOverrides;

    [Header("Local Overrides")]
    [Tooltip("当启用本地覆盖后，这个开关决定该 prefab 是否真的启用护盾光环。")]
    [SerializeField] private bool localShieldAuraEnabled = true;
    [Tooltip("每次刷新时，给范围内敌人补上的护盾值。")]
    [SerializeField] [Min(0)] private int localShieldAmount = 1;
    [Tooltip("护盾光环的作用半径。")]
    [SerializeField] [Min(0.1f)] private float localShieldRadius = 1.8f;
    [Tooltip("护盾光环每次结算之间的间隔。")]
    [SerializeField] [Min(0.1f)] private float localRefreshInterval = 0.45f;

    private float _shieldAuraTimer;

    private bool UsesShieldAura =>
        useLocalOverrides
            ? localShieldAuraEnabled && ResolveShieldAmount() > 0
            : Definition != null && Definition.ShieldAmount > 0;

    public override void ResetRuntimeState()
    {
        _shieldAuraTimer = 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (!UsesShieldAura || Owner == null || !Owner.IsAlive)
        {
            return;
        }

        _shieldAuraTimer -= deltaTime;
        if (_shieldAuraTimer > 0f)
        {
            return;
        }

        ApplyShieldAura();
        _shieldAuraTimer = ResolveRefreshInterval();
    }

    private void ApplyShieldAura()
    {
        int shieldAmount = ResolveShieldAmount();
        float shieldRadius = ResolveShieldRadius();
        float shieldRadiusSqr = shieldRadius * shieldRadius;
        for (int enemyIndex = 0; enemyIndex < Enemy.ActiveEnemyCount; enemyIndex++)
        {
            Enemy ally = Enemy.GetActiveEnemy(enemyIndex);
            if (ally == null || ally == Owner || !ally.IsAlive)
            {
                continue;
            }

            float distanceSqr = (ally.transform.position - Owner.transform.position).sqrMagnitude;
            if (distanceSqr > shieldRadiusSqr)
            {
                continue;
            }

            ally.ApplyShieldIfWeaker(shieldAmount);
        }
    }

    private int ResolveShieldAmount()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0, localShieldAmount);
        }

        return Definition.ShieldAmount;
    }

    private float ResolveShieldRadius()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0.1f, localShieldRadius);
        }

        return Definition.ShieldAuraRadius;
    }

    private float ResolveRefreshInterval()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0.1f, localRefreshInterval);
        }

        return Definition.ShieldRefreshInterval;
    }
}
