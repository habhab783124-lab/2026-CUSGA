using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `BattlefieldMapDefinition` 是阶段 A 引入的地图总配置入口。
///
/// 它当前先解决一个很基础但很重要的问题：
/// 把“这张地图有哪些出怪口、有哪些防御点、允许放多少个继电器”
/// 从旧原型里的隐式约定，收口成一个显式场景组件。
///
/// 这样后续做供电系统、地图校验和多出怪口刷怪时，
/// 就能围绕同一个地图对象继续长，而不是各自去猜场景结构。
/// </summary>
public sealed class BattlefieldMapDefinition : MonoBehaviour
{
    private const string DefaultSpawnGateRootName = "SpawnGates";
    private const string DefaultDefensePointRootName = "DefensePoints";

    [Header("Core References")]
    [Tooltip("当前地图真正允许建造的空地区域。后续关卡应优先在 Scene 里显式拖入，而不是运行时再猜。")]
    [SerializeField] private BuildZone buildZoneReference;
    [Tooltip("这张地图会使用到的所有出怪口。顺序会直接影响轮询刷怪顺序。")]
    [SerializeField] private EnemySpawnGate[] spawnGates = new EnemySpawnGate[0];
    [Tooltip("这张地图当前会使用到的所有防御点。现在通常只用第一个，但后续地图允许扩展到多个。")]
    [SerializeField] private DefensePointFlag[] defensePoints = new DefensePointFlag[0];

    [Header("Authoring Helpers")]
    [Tooltip("开启后，脚本会在编辑器里自动从场景层级重新收集 BuildZone / 出怪口 / 防御点引用，减少手工维护数组的成本。")]
    [SerializeField] private bool autoCollectSceneReferences = true;
    [Tooltip("可选的出怪口根节点。指定后，只会在这棵子树下收集 EnemySpawnGate，并按层级顺序写回数组。")]
    [SerializeField] private Transform spawnGateRootReference;
    [Tooltip("可选的防御点根节点。指定后，只会在这棵子树下收集 DefensePointFlag。")]
    [SerializeField] private Transform defensePointRootReference;
    [Tooltip("自动收集时是否包含未激活对象。作者搭关时常会先隐藏一些出怪口或目标点，所以这里默认包含。")]
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Gameplay Limits")]
    [Min(0)]
    [SerializeField] private int relayLimit = 4;

    public BuildZone BuildZone => buildZoneReference;
    public int RelayLimit => Mathf.Max(0, relayLimit);
    public int SpawnGateCount => CollectValidSpawnGates(null);
    public int DefensePointCount => CollectValidDefensePoints(null);
    public Transform SpawnGateRoot => spawnGateRootReference;
    public Transform DefensePointRoot => defensePointRootReference;

    /// <summary>
    /// 当前地图的可读摘要。
    /// 主要用于启动日志和场景契约检查，帮助我们快速知道这一关的骨架到底有没有接好。
    /// </summary>
    public string BuildDebugSummary()
    {
        return $"BuildZone={(buildZoneReference != null ? buildZoneReference.name : "None")}, SpawnGates={SpawnGateCount}, DefensePoints={DefensePointCount}, RelayLimit={RelayLimit}";
    }

    /// <summary>
    /// 这是给作者工作流看的摘要。
    /// 它不只看最终数量，也会顺手告诉你：
    /// - 当前引用是手填的还是从某个显式根节点收集的
    /// - 哪些根节点已经接好
    /// </summary>
    public string BuildAuthoringSummary()
    {
        string spawnGateRootName = spawnGateRootReference != null ? spawnGateRootReference.name : "(Map Root)";
        string defensePointRootName = defensePointRootReference != null ? defensePointRootReference.name : "(Map Root)";
        string buildZoneName = buildZoneReference != null ? buildZoneReference.name : "None";

        return $"BuildZone={buildZoneName} | SpawnGateRoot={spawnGateRootName} | DefensePointRoot={defensePointRootName} | SpawnGates={SpawnGateCount} | DefensePoints={DefensePointCount}";
    }

    /// <summary>
    /// 把这张地图当前最明显的配置缺口打印成警告。
    ///
    /// 阶段 A 的目标不是立刻让所有规则完整运行，
    /// 但至少要让“地图骨架哪里没接好”变得可见，避免后面排查时还要先猜场景是否缺对象。
    /// </summary>
    public void LogConfigurationWarnings(Object context)
    {
        if (buildZoneReference == null)
        {
            Debug.LogWarning("BattlefieldMapDefinition is missing BuildZone reference.", context);
        }

        if (SpawnGateCount == 0)
        {
            Debug.LogWarning("BattlefieldMapDefinition has no valid EnemySpawnGate configured.", context);
        }

        if (DefensePointCount == 0)
        {
            Debug.LogWarning("BattlefieldMapDefinition has no DefensePointFlag configured.", context);
        }
    }

    public bool HasAnyValidSpawnGate()
    {
        return SpawnGateCount > 0;
    }

    public bool TryGetSpawnGateBySequence(int sequenceIndex, out EnemySpawnGate spawnGate)
    {
        List<EnemySpawnGate> validSpawnGates = new List<EnemySpawnGate>();
        CollectValidSpawnGates(validSpawnGates);

        if (validSpawnGates.Count == 0)
        {
            spawnGate = null;
            return false;
        }

        int normalizedIndex = Mathf.Abs(sequenceIndex) % validSpawnGates.Count;
        spawnGate = validSpawnGates[normalizedIndex];
        return spawnGate != null;
    }

    public bool TryGetPrimaryDefensePoint(out DefensePointFlag defensePoint)
    {
        List<DefensePointFlag> validDefensePoints = new List<DefensePointFlag>();
        CollectValidDefensePoints(validDefensePoints);

        if (validDefensePoints.Count == 0)
        {
            defensePoint = null;
            return false;
        }

        defensePoint = validDefensePoints[0];
        return true;
    }

    /// <summary>
    /// 显式重新收集当前地图的关键场景引用。
    ///
    /// 这一步之所以做成一个公开作者入口，是因为：
    /// - 关卡作者后面主要直接在 Scene 里摆对象
    /// - 与其让人每次手改数组，不如让脚本按层级顺序帮忙回填
    ///
    /// 返回值表示这次是否真的改动了序列化数据。
    /// </summary>
    public bool CollectSceneReferences()
    {
        bool changed = false;
        changed |= TryAssignBuildZoneReference();
        changed |= TryAssignAuthoringRoots();
        changed |= RebuildSpawnGateReferences();
        changed |= RebuildDefensePointReferences();
        return changed;
    }

    private void OnValidate()
    {
        relayLimit = Mathf.Max(0, relayLimit);

        if (autoCollectSceneReferences)
        {
            CollectSceneReferences();
        }
    }

    [ContextMenu("Collect Scene References")]
    private void ContextCollectSceneReferences()
    {
        CollectSceneReferences();
    }

    [ContextMenu("Log Map Summary")]
    private void ContextLogMapSummary()
    {
        Debug.Log($"[BattlefieldMapDefinition] {BuildAuthoringSummary()}", this);
        LogConfigurationWarnings(this);
    }

    private int CollectValidSpawnGates(List<EnemySpawnGate> output)
    {
        int count = 0;
        if (spawnGates == null)
        {
            return 0;
        }

        for (int i = 0; i < spawnGates.Length; i++)
        {
            EnemySpawnGate spawnGate = spawnGates[i];
            if (spawnGate == null || !spawnGate.IsConfigured)
            {
                continue;
            }

            count++;
            output?.Add(spawnGate);
        }

        return count;
    }

    private int CollectValidDefensePoints(List<DefensePointFlag> output)
    {
        int count = 0;
        if (defensePoints == null)
        {
            return 0;
        }

        HashSet<DefensePointFlag> deduplicatedPoints = new HashSet<DefensePointFlag>();
        for (int i = 0; i < defensePoints.Length; i++)
        {
            DefensePointFlag defensePoint = defensePoints[i];
            if (defensePoint == null || !deduplicatedPoints.Add(defensePoint))
            {
                continue;
            }

            count++;
            output?.Add(defensePoint);
        }

        return count;
    }

    private bool TryAssignBuildZoneReference()
    {
        if (buildZoneReference != null)
        {
            return false;
        }

        BuildZone discoveredBuildZone = GetComponentInChildren<BuildZone>(includeInactiveChildren);
        if (discoveredBuildZone == null)
        {
            return false;
        }

        buildZoneReference = discoveredBuildZone;
        return true;
    }

    private bool TryAssignAuthoringRoots()
    {
        bool changed = false;

        if (spawnGateRootReference == null)
        {
            Transform existingSpawnRoot = transform.Find(DefaultSpawnGateRootName);
            if (existingSpawnRoot != null)
            {
                spawnGateRootReference = existingSpawnRoot;
                changed = true;
            }
        }

        if (defensePointRootReference == null)
        {
            Transform existingDefenseRoot = transform.Find(DefaultDefensePointRootName);
            if (existingDefenseRoot != null)
            {
                defensePointRootReference = existingDefenseRoot;
                changed = true;
            }
        }

        return changed;
    }

    private bool RebuildSpawnGateReferences()
    {
        List<EnemySpawnGate> collectedSpawnGates = new List<EnemySpawnGate>();
        CollectComponentsInHierarchyOrder(
            spawnGateRootReference != null ? spawnGateRootReference : transform,
            includeInactiveChildren,
            collectedSpawnGates);

        return AssignReferenceArray(ref spawnGates, collectedSpawnGates);
    }

    private bool RebuildDefensePointReferences()
    {
        List<DefensePointFlag> collectedDefensePoints = new List<DefensePointFlag>();
        CollectComponentsInHierarchyOrder(
            defensePointRootReference != null ? defensePointRootReference : transform,
            includeInactiveChildren,
            collectedDefensePoints);

        return AssignReferenceArray(ref defensePoints, collectedDefensePoints);
    }

    private static bool AssignReferenceArray<T>(ref T[] targetArray, List<T> collectedItems) where T : Component
    {
        if (targetArray == null)
        {
            targetArray = new T[collectedItems.Count];
            for (int index = 0; index < collectedItems.Count; index++)
            {
                targetArray[index] = collectedItems[index];
            }

            return true;
        }

        bool changed = targetArray.Length != collectedItems.Count;
        if (!changed)
        {
            for (int index = 0; index < targetArray.Length; index++)
            {
                if (targetArray[index] != collectedItems[index])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return false;
        }

        targetArray = new T[collectedItems.Count];
        for (int index = 0; index < collectedItems.Count; index++)
        {
            targetArray[index] = collectedItems[index];
        }

        return true;
    }

    private static void CollectComponentsInHierarchyOrder<T>(Transform root, bool includeInactive, List<T> output) where T : Component
    {
        output.Clear();
        if (root == null)
        {
            return;
        }

        HashSet<T> deduplicated = new HashSet<T>();
        TraverseHierarchy(root, includeInactive, output, deduplicated);
    }

    private static void TraverseHierarchy<T>(Transform current, bool includeInactive, List<T> output, HashSet<T> deduplicated) where T : Component
    {
        if (current == null)
        {
            return;
        }

        bool isActive = includeInactive || current.gameObject.activeInHierarchy;
        if (!isActive)
        {
            return;
        }

        T component = current.GetComponent<T>();
        if (component != null && deduplicated.Add(component))
        {
            output.Add(component);
        }

        for (int childIndex = 0; childIndex < current.childCount; childIndex++)
        {
            TraverseHierarchy(current.GetChild(childIndex), includeInactive, output, deduplicated);
        }
    }
}
