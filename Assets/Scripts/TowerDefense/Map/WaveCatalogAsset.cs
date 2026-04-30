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
        [SerializeField, InspectorName("敌人类型")] private EnemyArchetypeId enemyType = EnemyArchetypeId.Scavenger; // 中文：敌人类型
        [SerializeField, Min(0), InspectorName("数量")] private int enemyCount = 4; // 中文：敌人数量
        [SerializeField, Min(0.05f), InspectorName("刷新间隔")] private float spawnInterval = 1f; // 中文：出怪间隔

        public EnemyArchetypeId EnemyType => enemyType; // 中文：敌人类型
        public int EnemyCount => Mathf.Max(0, enemyCount); // 中文：敌人数量
        public float SpawnInterval => Mathf.Max(0.05f, spawnInterval); // 中文：出怪间隔
    }

    [Serializable]
    public sealed class WaveEntry
    {
        [SerializeField, InspectorName("波次名称")] private string displayName = "第 01 波"; // 中文：显示名称
        [SerializeField, TextArea(2, 4), InspectorName("设计备注")] private string designerNote = "说明这一波想让玩家感受到什么压力或机制。"; // 中文：designer备注
        [SerializeField, InspectorName("刷怪组")] private SpawnGroup[] spawnGroups = Array.Empty<SpawnGroup>(); // 中文：出怪Groups

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "波次" : displayName; // 中文：显示名称
        public string DesignerNote => designerNote ?? string.Empty; // 中文：Designer备注
        public SpawnGroup[] SpawnGroups => spawnGroups ?? Array.Empty<SpawnGroup>(); // 中文：出怪Groups

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

    [SerializeField, InspectorName("波次列表")] private WaveEntry[] waves = Array.Empty<WaveEntry>(); // 中文：波次列表

    public WaveEntry[] Waves => waves ?? Array.Empty<WaveEntry>(); // 中文：波次列表
}
