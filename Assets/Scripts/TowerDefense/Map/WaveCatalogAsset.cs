using System;
using UnityEngine;

/// <summary>
/// `WaveCatalogAsset` 把波次内容收成一份共享资产。
///
/// 现在它不再只是“每波一个统一数值壳”，
/// 而是允许你按组配置：
/// - 这波先刷哪种怪
/// - 再刷哪种怪
/// - 每一组刷多少只
/// - 这一组刷怪间隔是多少
///
/// 这样才适合承载真正的多怪物系统。
/// </summary>
[CreateAssetMenu(
    fileName = "WaveCatalog",
    menuName = "Tower Defense/Map/Wave Catalog")]
public sealed class WaveCatalogAsset : ScriptableObject
{
    [Serializable]
    public sealed class SpawnGroup
    {
        [SerializeField] private EnemyArchetypeId enemyType = EnemyArchetypeId.Scavenger;
        [SerializeField] [Min(0)] private int enemyCount = 4;
        [SerializeField] [Min(0.05f)] private float spawnInterval = 1f;

        public EnemyArchetypeId EnemyType => enemyType;
        public int EnemyCount => Mathf.Max(0, enemyCount);
        public float SpawnInterval => Mathf.Max(0.05f, spawnInterval);
    }

    [Serializable]
    public sealed class WaveEntry
    {
        [SerializeField] private string displayName = "Wave 01";
        [SerializeField] [TextArea(2, 4)] private string designerNote = "What should this wave communicate to the player?";
        [SerializeField] private SpawnGroup[] spawnGroups = Array.Empty<SpawnGroup>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Wave" : displayName;
        public string DesignerNote => designerNote ?? string.Empty;
        public SpawnGroup[] SpawnGroups => spawnGroups ?? Array.Empty<SpawnGroup>();

        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                if (spawnGroups != null)
                {
                    for (int index = 0; index < spawnGroups.Length; index++)
                    {
                        SpawnGroup group = spawnGroups[index];
                        if (group != null)
                        {
                            total += group.EnemyCount;
                        }
                    }
                }

                return total;
            }
        }
    }

    [SerializeField] private WaveEntry[] waves = Array.Empty<WaveEntry>();

    public WaveEntry[] Waves => waves ?? Array.Empty<WaveEntry>();
}
