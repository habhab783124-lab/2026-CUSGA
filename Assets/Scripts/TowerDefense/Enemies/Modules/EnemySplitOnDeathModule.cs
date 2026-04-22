using UnityEngine;

/// <summary>
/// `EnemySplitOnDeathModule` 负责在宿主死亡前生成分裂子怪。
///
/// 这里不直接复制旧敌人的所有内部状态，
/// 而是继续走“按敌人目录重新初始化子怪”的主链，
/// 这样后续你调小拾荒者的属性时，分裂产物也会自动同步到新定义。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class EnemySplitOnDeathModule : EnemyMechanicModule
{
    [Header("Authoring Source")]
    [Tooltip("关闭时优先使用 EnemyCatalogAsset 里的分裂参数；开启后改用当前 prefab 上这组本地参数。")]
    [SerializeField] private bool useLocalOverrides;

    [Header("Local Overrides")]
    [Tooltip("当启用本地覆盖后，这个开关决定该 prefab 是否真的启用死亡分裂。")]
    [SerializeField] private bool localSplitEnabled = true;
    [Tooltip("死亡后生成的子怪类型。")]
    [SerializeField] private EnemyArchetypeId localSplitChildType = EnemyArchetypeId.SmallScavenger;
    [Tooltip("死亡时生成多少个子怪。")]
    [SerializeField] [Min(0)] private int localSplitChildCount = 2;
    [Tooltip("子怪在宿主附近随机刷新的半径。")]
    [SerializeField] [Min(0f)] private float localSplitSpawnRadius = 0.4f;

    private bool UsesSplitOnDeath =>
        useLocalOverrides
            ? localSplitEnabled && ResolveSplitChildType() != EnemyArchetypeId.None && ResolveSplitChildCount() > 0
            : Definition != null && Definition.SplitChildType != EnemyArchetypeId.None && Definition.SplitChildCount > 0;

    public override void OnBeforeDeath()
    {
        if (!UsesSplitOnDeath || Owner == null || Owner.CurrentPath == null || Owner.EnemyRoot == null)
        {
            return;
        }

        EnemyArchetypeId splitChildType = ResolveSplitChildType();
        int splitChildCount = ResolveSplitChildCount();
        float splitSpawnRadius = ResolveSplitSpawnRadius();

        for (int childIndex = 0; childIndex < splitChildCount; childIndex++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * splitSpawnRadius;
            Vector3 spawnPosition = Owner.transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
            Owner.SpawnConfiguredChild(
                splitChildType,
                spawnPosition,
                Owner.CurrentWaypointIndex,
                $"{splitChildType}_Split_{childIndex + 1}");
        }
    }

    private EnemyArchetypeId ResolveSplitChildType()
    {
        if (useLocalOverrides || Definition == null)
        {
            return localSplitChildType;
        }

        return Definition.SplitChildType;
    }

    private int ResolveSplitChildCount()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0, localSplitChildCount);
        }

        return Definition.SplitChildCount;
    }

    private float ResolveSplitSpawnRadius()
    {
        if (useLocalOverrides || Definition == null)
        {
            return Mathf.Max(0f, localSplitSpawnRadius);
        }

        return Definition.SplitSpawnRadius;
    }
}
