using UnityEngine;

/// <summary>
/// `EnemyRepairModule` 负责让“机械师”这类支援敌人周期性修理最近的可修理单位。
///
/// 这里继续坚持一个边界：
/// - 选谁来修、多久修一次：属于修理模块
/// - 真正回血并刷新血条：仍然交给 `Enemy` 自己
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class EnemyRepairModule : EnemyMechanicModule
{
    [Header("参数来源")]
    [Tooltip("关闭时优先使用 EnemyCatalogAsset 里的修理参数；开启后改用当前 prefab 上这组本地参数。")]
    [SerializeField, InspectorName("启用本地覆盖")] private bool useLocalOverrides; // 中文：使用本地覆盖

    [Header("本地覆盖")]
    [Tooltip("当启用本地覆盖后，这个开关决定该 prefab 是否真的启用修理支援。")]
    [SerializeField, InspectorName("启用修理支援")] private bool localRepairEnabled = true; // 中文：本地修理Enabled
    [Tooltip("每次修理恢复的生命值。")]
    [SerializeField, Min(0), InspectorName("单次修理量")] private int localRepairAmount = 1; // 中文：本地修理数量
    [Tooltip("可修理友军的搜索半径。")]
    [SerializeField, Min(0.1f), InspectorName("搜索半径")] private float localRepairRadius = 2.1f; // 中文：本地修理半径
    [Tooltip("每次修理之间的冷却。")]
    [SerializeField, Min(0.1f), InspectorName("修理冷却")] private float localRepairCooldown = 2.5f; // 中文：本地修理冷却

    private float _repairTimer; // 中文：修理计时器

    private bool UsesRepairSupport => // 中文：Uses修理支持
        useLocalOverrides
            ? localRepairEnabled && ResolveRepairAmount() > 0
            : Definition != null && Definition.RepairAmount > 0;

    public override void ResetRuntimeState()
    {
        _repairTimer = 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (!UsesRepairSupport || Owner == null || !Owner.IsAlive)
        {
            return;
        }

        _repairTimer -= deltaTime;
        if (_repairTimer > 0f)
        {
            return;
        }

        TryRepairNearbyMechanicalAlly();
        _repairTimer = ResolveRepairCooldown();
    }

    private void TryRepairNearbyMechanicalAlly()
    {
        Enemy bestTarget = null;
        float bestDistanceSqr = float.MaxValue;
        int repairAmount = ResolveRepairAmount();
        float repairRadius = ResolveRepairRadius();
        float repairRadiusSqr = repairRadius * repairRadius;

        for (int enemyIndex = 0; enemyIndex < Enemy.ActiveEnemyCount; enemyIndex++)
        {
            Enemy ally = Enemy.GetActiveEnemy(enemyIndex);
            if (ally == null ||
                ally == Owner ||
                !ally.CanReceiveMechanicRepair ||
                ally.CurrentHealth >= ally.MaxHealth)
            {
                continue;
            }

            float distanceSqr = (ally.transform.position - Owner.transform.position).sqrMagnitude;
            if (distanceSqr > repairRadiusSqr || distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestTarget = ally;
        }

        if (bestTarget != null)
        {
            bestTarget.ReceiveRepair(repairAmount);
        }
    }

    private int ResolveRepairAmount()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0, localRepairAmount);
        }

        return Definition.RepairAmount;
    }

    private float ResolveRepairRadius()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0.1f, localRepairRadius);
        }

        return Definition.RepairRadius;
    }

    private float ResolveRepairCooldown()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0.1f, localRepairCooldown);
        }

        return Definition.RepairCooldown;
    }
}
