using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// `PowerGridHudSnapshot` 是 HUD 需要看到的供电域摘要。
///
/// 这里故意不把整套继电器和塔对象直接暴露给 HUD，
/// 而是收口成一份“读数结果”：
/// - 现在有多少继电器。
/// - 现在有多少战斗塔在线 / 离线。
/// - 当前总负载和总供电量分别是多少。
/// - 此刻最值得告诉玩家的一句供电状态提示是什么。
///
/// 这样做以后，HUD 只依赖稳定的数据快照，
/// 不会反过来耦合供电判定过程里的内部容器和中间步骤。
/// </summary>
public readonly struct PowerGridHudSnapshot
{
    public PowerGridHudSnapshot(
        int relayCount,
        int relayLimit,
        int totalTowerCount,
        int poweredTowerCount,
        int offlineTowerCount,
        int assignedLoad,
        int totalCapacity,
        string statusMessage)
    {
        RelayCount = relayCount;
        RelayLimit = relayLimit;
        TotalTowerCount = totalTowerCount;
        PoweredTowerCount = poweredTowerCount;
        OfflineTowerCount = offlineTowerCount;
        AssignedLoad = assignedLoad;
        TotalCapacity = totalCapacity;
        StatusMessage = statusMessage ?? string.Empty;
    }

    public int RelayCount { get; } // 中文：继电器数量

    public int RelayLimit { get; } // 中文：继电器上限

    public int TotalTowerCount { get; } // 中文：总塔数量

    public int PoweredTowerCount { get; } // 中文：Powered塔数量

    public int OfflineTowerCount { get; } // 中文：离线塔数量

    public int AssignedLoad { get; } // 中文：已分配加载

    public int TotalCapacity { get; } // 中文：总容量

    public string StatusMessage { get; } // 中文：状态消息
}

/// <summary>
/// Coordinates relay coverage, relay numbering, tower numbering, and runtime power allocation.
/// The goal is to keep the phase-two power rules out of the main gameplay orchestrator.
/// </summary>
public sealed class TowerPowerGridCoordinator
{
    private sealed class TowerOfflineReason
    {
        public TowerOfflineReason(string message)
        {
            Message = message;
        }

        public string Message { get; } // 中文：消息
    }

    private sealed class RelayEvaluation
    {
        public RelayTower Relay { get; set; } // 中文：继电器
        public List<DefenseTower> WorkingTowers { get; } = new List<DefenseTower>(); // 中文：Working塔列表
        public int RemainingCapacity { get; set; } // 中文：剩余容量
    }

    private readonly Func<BattlefieldMapDefinition> _mapDefinitionQuery; // 中文：地图定义查询
    private readonly Action<string> _logDiagnostic; // 中文：日志诊断
    private readonly Stack<int> _relayNumbers = new Stack<int>(); // 中文：继电器Numbers
    private readonly Stack<int> _towerNumbers = new Stack<int>(); // 中文：塔Numbers

    private Transform _placedTowerRoot; // 中文：已放置塔根节点

    public TowerPowerGridCoordinator(
        Func<BattlefieldMapDefinition> mapDefinitionQuery,
        Action<string> logDiagnostic)
    {
        _mapDefinitionQuery = mapDefinitionQuery;
        _logDiagnostic = logDiagnostic;

        for (int value = 100; value >= 1; value--)
        {
            _relayNumbers.Push(value);
            _towerNumbers.Push(value);
        }
    }

    public int RelayLimit => _mapDefinitionQuery != null && _mapDefinitionQuery() != null // 中文：继电器上限
        ? _mapDefinitionQuery().RelayLimit
        : int.MaxValue;

    public void BindPlacedTowerRoot(Transform placedTowerRoot)
    {
        _placedTowerRoot = placedTowerRoot;
        AssignNumbersToExistingStructures();
    }

    public int GetPlacedRelayCount()
    {
        CollectRuntimeStructures(out List<RelayTower> relays, out _);
        return relays.Count;
    }

    public bool CanPlaceRelay(out string invalidReason)
    {
        int relayLimit = RelayLimit;
        int placedRelayCount = GetPlacedRelayCount();
        if (placedRelayCount >= relayLimit)
        {
            invalidReason = $"继电器数量已达上限。本地图最多允许放置 {relayLimit} 个继电器。";
            return false;
        }

        invalidReason = string.Empty;
        return true;
    }

    public bool IsWithinAnyRelayCoverage(Vector3 worldPosition)
    {
        CollectRuntimeStructures(out List<RelayTower> relays, out _);
        for (int i = 0; i < relays.Count; i++)
        {
            if (relays[i].ContainsPoint(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    public Bounds GetRelayCoverageBounds()
    {
        CollectRuntimeStructures(out List<RelayTower> relays, out _);
        if (relays.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds bounds = relays[0].CoverageBounds;
        for (int i = 1; i < relays.Count; i++)
        {
            bounds.Encapsulate(relays[i].CoverageBounds.min);
            bounds.Encapsulate(relays[i].CoverageBounds.max);
        }

        return bounds;
    }

    /// <summary>
    /// 组装一份给 HUD 使用的供电摘要。
    ///
    /// 这一步的重点不是“再次参与供电判定”，
    /// 而是把当前已经生效的结果翻译成玩家能快速读懂的局势信息。
    /// </summary>
    public PowerGridHudSnapshot GetHudSnapshot()
    {
        CollectRuntimeStructures(out List<RelayTower> relays, out List<DefenseTower> towers);

        int relayCount = relays.Count;
        int totalTowerCount = towers.Count;
        int poweredTowerCount = towers.Count(tower => tower != null && tower.IsPowered);
        int offlineTowerCount = Mathf.Max(0, totalTowerCount - poweredTowerCount);
        int totalCapacity = relays.Sum(relay => relay != null ? relay.SupplyCapacity : 0);
        int assignedLoad = relays.Sum(relay => relay != null ? relay.CurrentAssignedLoad : 0);

        string statusMessage;
        if (relayCount == 0)
        {
            statusMessage = totalTowerCount > 0
                ? "当前没有任何继电器供电，已部署的战斗塔都会离线。"
                : "先放置一个继电器，打开供电网络。";
        }
        else if (totalTowerCount == 0)
        {
            statusMessage = relayCount >= RelayLimit
                ? "供电网络已就绪，但继电器数量已达上限。"
                : "供电网络已就绪。请在继电器覆盖范围内放置战斗塔。";
        }
        else if (offlineTowerCount > 0)
        {
            statusMessage = $"有 {offlineTowerCount} 座塔离线。请扩充容量或调整后续部署。";
        }
        else if (relayCount >= RelayLimit)
        {
            statusMessage = "所有塔都已通电。后续扩张需要优先升级现有继电器。";
        }
        else if (totalCapacity > 0 && assignedLoad >= totalCapacity)
        {
            statusMessage = "电网已满载。新的建造或升级可能会立刻断电。";
        }
        else
        {
            statusMessage = "电网稳定，当前继电器容量足以覆盖所有已部署塔。";
        }

        return new PowerGridHudSnapshot(
            relayCount: relayCount,
            relayLimit: RelayLimit,
            totalTowerCount: totalTowerCount,
            poweredTowerCount: poweredTowerCount,
            offlineTowerCount: offlineTowerCount,
            assignedLoad: assignedLoad,
            totalCapacity: totalCapacity,
            statusMessage: statusMessage);
    }

    public void RegisterPlacedStructure(GameObject structureObject, TowerType towerType)
    {
        if (structureObject == null)
        {
            return;
        }

        switch (towerType)
        {
            case TowerType.Relay:
            {
                RelayTower relayTower = structureObject.GetComponent<RelayTower>();
                if (relayTower != null && relayTower.RelayNumber >= 100 && _relayNumbers.Count > 0)
                {
                    relayTower.AssignRelayNumber(_relayNumbers.Pop());
                }

                break;
            }

            case TowerType.SingleTarget:
            case TowerType.SlowField:
            case TowerType.Bombard:
            {
                DefenseTower defenseTower = structureObject.GetComponent<DefenseTower>();
                if (defenseTower != null && defenseTower.TowerNumber >= 100 && _towerNumbers.Count > 0)
                {
                    defenseTower.AssignTowerNumber(_towerNumbers.Pop());
                }

                break;
            }
        }

        RecalculatePowerDistribution();
    }

    public void NotifyTopologyChanged()
    {
        RecalculatePowerDistribution();
    }

    public bool CanUpgradeRelay(RelayTower relay, int availableEnergy, out int upgradeCost, out string invalidReason)
    {
        upgradeCost = 0;
        invalidReason = string.Empty;

        if (relay == null)
        {
            invalidReason = "当前没有选中继电器。";
            return false;
        }

        if (!relay.CanUpgrade)
        {
            invalidReason = "该继电器已经满级。";
            return false;
        }

        upgradeCost = relay.GetUpgradeCost();
        if (availableEnergy < upgradeCost)
        {
            invalidReason = $"废料不足，升级需要 {upgradeCost} 废料。";
            return false;
        }

        return true;
    }

    public bool CanUpgradeDefenseTower(DefenseTower tower, int availableEnergy, out int upgradeCost, out string invalidReason)
    {
        upgradeCost = 0;
        invalidReason = string.Empty;

        if (tower == null)
        {
            invalidReason = "当前没有选中战斗塔。";
            return false;
        }

        if (!tower.CanUpgrade)
        {
            invalidReason = "该战斗塔已经满级。";
            return false;
        }

        upgradeCost = tower.GetUpgradeCost();
        if (availableEnergy < upgradeCost)
        {
            invalidReason = $"废料不足，升级需要 {upgradeCost} 废料。";
            return false;
        }

        CollectRuntimeStructures(out List<RelayTower> relays, out List<DefenseTower> towers);
        SimulationResult currentSimulation = Simulate(relays, towers, null, null);

        Dictionary<DefenseTower, int> towerPowerOverrides = new Dictionary<DefenseTower, int>
        {
            [tower] = tower.PreviewUpgradedPowerRequired()
        };

        SimulationResult upgradedSimulation = Simulate(relays, towers, null, towerPowerOverrides);
        if (!upgradedSimulation.Assignments.ContainsKey(tower))
        {
            Dictionary<DefenseTower, TowerOfflineReason> upgradeReasons = BuildOfflineReasons(
                relays,
                towers,
                upgradedSimulation.Evaluations,
                upgradedSimulation.Assignments);
            invalidReason = upgradeReasons.TryGetValue(tower, out TowerOfflineReason reason)
                ? $"升级被阻止：{reason.Message}"
                : "升级被阻止：这座塔升级后会失去供电。";
            return false;
        }

        foreach (KeyValuePair<DefenseTower, RelayTower> currentAssignment in currentSimulation.Assignments)
        {
            if (!upgradedSimulation.Assignments.ContainsKey(currentAssignment.Key))
            {
                invalidReason = $"升级被阻止：塔 #{currentAssignment.Key.TowerNumber} 会因此被迫断电。";
                return false;
            }
        }

        return true;
    }

    public void ApplyRelayUpgrade(RelayTower relay)
    {
        if (relay == null)
        {
            return;
        }

        relay.ApplyUpgrade();
        RecalculatePowerDistribution();
    }

    public void ApplyDefenseTowerUpgrade(DefenseTower tower)
    {
        if (tower == null)
        {
            return;
        }

        tower.ApplyUpgrade();
        RecalculatePowerDistribution();
    }

    public void RecalculatePowerDistribution()
    {
        CollectRuntimeStructures(out List<RelayTower> relays, out List<DefenseTower> towers);

        for (int i = 0; i < relays.Count; i++)
        {
            relays[i].ResetRuntimeLoad();
        }

        for (int i = 0; i < towers.Count; i++)
        {
            towers[i].SetPowerState(false, null, "等待供电结算。");
        }

        if (relays.Count == 0 || towers.Count == 0)
        {
            return;
        }

        relays.Sort((a, b) => a.RelayNumber.CompareTo(b.RelayNumber));
        towers.Sort((a, b) => a.TowerNumber.CompareTo(b.TowerNumber));

        SimulationResult simulation = Simulate(relays, towers, null, null);
        Dictionary<DefenseTower, TowerOfflineReason> offlineReasons = BuildOfflineReasons(relays, towers, simulation.Evaluations, simulation.Assignments);
        ApplyAssignments(relays, towers, simulation.Assignments, offlineReasons);
    }

    private SimulationResult Simulate(
        List<RelayTower> relays,
        List<DefenseTower> towers,
        Dictionary<RelayTower, int> relayCapacityOverrides,
        Dictionary<DefenseTower, int> towerPowerOverrides)
    {
        Dictionary<RelayTower, HashSet<DefenseTower>> exclusions = relays.ToDictionary(
            relay => relay,
            relay => new HashSet<DefenseTower>());

        Dictionary<RelayTower, RelayEvaluation> evaluations = EvaluateAllRelays(relays, towers, exclusions, relayCapacityOverrides, towerPowerOverrides);

        // This loop is the runtime equivalent of the documented "3号过程":
        // overlapping towers are finally owned by the smallest relay number that can currently support them,
        // and the other relays rerun their local 2号过程 with that tower excluded.
        bool changed;
        int guard = relays.Count * Math.Max(1, towers.Count);
        do
        {
            changed = false;

            for (int towerIndex = 0; towerIndex < towers.Count; towerIndex++)
            {
                DefenseTower tower = towers[towerIndex];
                List<RelayTower> supportingRelays = relays
                    .Where(relay => evaluations.TryGetValue(relay, out RelayEvaluation evaluation) && evaluation.WorkingTowers.Contains(tower))
                    .OrderBy(relay => relay.RelayNumber)
                    .ToList();

                if (supportingRelays.Count <= 1)
                {
                    continue;
                }

                for (int relayIndex = 1; relayIndex < supportingRelays.Count; relayIndex++)
                {
                    RelayTower relay = supportingRelays[relayIndex];
                    if (exclusions[relay].Add(tower))
                    {
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                evaluations = EvaluateAllRelays(relays, towers, exclusions, relayCapacityOverrides, towerPowerOverrides);
            }
        }
        while (changed && --guard > 0);

        Dictionary<DefenseTower, RelayTower> assignments = BuildPreferredAssignments(relays, evaluations, towers);
        return new SimulationResult
        {
            Evaluations = evaluations,
            Assignments = assignments
        };
    }

    private Dictionary<RelayTower, RelayEvaluation> EvaluateAllRelays(
        List<RelayTower> relays,
        List<DefenseTower> towers,
        Dictionary<RelayTower, HashSet<DefenseTower>> exclusions,
        Dictionary<RelayTower, int> relayCapacityOverrides,
        Dictionary<DefenseTower, int> towerPowerOverrides)
    {
        Dictionary<RelayTower, RelayEvaluation> evaluations = new Dictionary<RelayTower, RelayEvaluation>();
        for (int relayIndex = 0; relayIndex < relays.Count; relayIndex++)
        {
            RelayTower relay = relays[relayIndex];
            HashSet<DefenseTower> relayExclusions = exclusions.TryGetValue(relay, out HashSet<DefenseTower> existingExclusions)
                ? existingExclusions
                : null;
            evaluations[relay] = EvaluateSingleRelay(relay, towers, relayExclusions, relayCapacityOverrides, towerPowerOverrides);
        }

        return evaluations;
    }

    private static RelayEvaluation EvaluateSingleRelay(
        RelayTower relay,
        List<DefenseTower> towers,
        HashSet<DefenseTower> exclusions,
        Dictionary<RelayTower, int> relayCapacityOverrides,
        Dictionary<DefenseTower, int> towerPowerOverrides)
    {
        RelayEvaluation evaluation = new RelayEvaluation
        {
            Relay = relay,
            RemainingCapacity = GetRelayCapacity(relay, relayCapacityOverrides)
        };

        for (int towerIndex = 0; towerIndex < towers.Count; towerIndex++)
        {
            DefenseTower tower = towers[towerIndex];
            if (!relay.ContainsPoint(tower.transform.position))
            {
                continue;
            }

            if (exclusions != null && exclusions.Contains(tower))
            {
                continue;
            }

            int powerRequired = GetTowerPowerRequired(tower, towerPowerOverrides);
            if (powerRequired > evaluation.RemainingCapacity)
            {
                break;
            }

            evaluation.WorkingTowers.Add(tower);
            evaluation.RemainingCapacity -= powerRequired;
        }

        return evaluation;
    }

    private static Dictionary<DefenseTower, RelayTower> BuildPreferredAssignments(
        List<RelayTower> relays,
        Dictionary<RelayTower, RelayEvaluation> evaluations,
        List<DefenseTower> towers)
    {
        Dictionary<DefenseTower, RelayTower> assignments = new Dictionary<DefenseTower, RelayTower>();

        for (int towerIndex = 0; towerIndex < towers.Count; towerIndex++)
        {
            DefenseTower tower = towers[towerIndex];
            RelayTower bestRelay = null;

            for (int relayIndex = 0; relayIndex < relays.Count; relayIndex++)
            {
                RelayTower relay = relays[relayIndex];
                if (!evaluations.TryGetValue(relay, out RelayEvaluation evaluation) || !evaluation.WorkingTowers.Contains(tower))
                {
                    continue;
                }

                if (bestRelay == null || relay.RelayNumber < bestRelay.RelayNumber)
                {
                    bestRelay = relay;
                }
            }

            if (bestRelay != null)
            {
                assignments[tower] = bestRelay;
            }
        }

        return assignments;
    }

    private void ApplyAssignments(
        List<RelayTower> relays,
        List<DefenseTower> towers,
        Dictionary<DefenseTower, RelayTower> assignments,
        Dictionary<DefenseTower, TowerOfflineReason> offlineReasons)
    {
        HashSet<DefenseTower> poweredTowers = new HashSet<DefenseTower>();

        foreach (RelayTower relay in relays)
        {
            int remainingCapacity = relay.SupplyCapacity;
            int assignedLoad = 0;

            List<DefenseTower> relayTowers = towers
                .Where(tower => assignments.TryGetValue(tower, out RelayTower assignedRelay) && assignedRelay == relay)
                .OrderBy(tower => tower.TowerNumber)
                .ToList();

            for (int towerIndex = 0; towerIndex < relayTowers.Count; towerIndex++)
            {
                DefenseTower tower = relayTowers[towerIndex];
                if (tower.PowerRequired > remainingCapacity)
                {
                    tower.SetPowerState(false, null, $"继电器 #{relay.RelayNumber} 的剩余容量已不足。");
                    continue;
                }

                remainingCapacity -= tower.PowerRequired;
                assignedLoad += tower.PowerRequired;
                poweredTowers.Add(tower);
                tower.SetPowerState(true, relay, $"由继电器 #{relay.RelayNumber} 供电。该继电器剩余容量：{remainingCapacity}。");
            }

            relay.SetRuntimeLoad(assignedLoad);
        }

        for (int towerIndex = 0; towerIndex < towers.Count; towerIndex++)
        {
            DefenseTower tower = towers[towerIndex];
            if (poweredTowers.Contains(tower))
            {
                continue;
            }

            string message = offlineReasons != null && offlineReasons.TryGetValue(tower, out TowerOfflineReason reason)
                ? reason.Message
                : "该塔当前离线。";
            tower.SetPowerState(false, null, message);
        }

        int poweredTowerCount = towers.Count(tower => tower.IsPowered);
        int unpoweredTowerCount = towers.Count - poweredTowerCount;
        _logDiagnostic?.Invoke(
            $"Power grid recalculated: relays={relays.Count} powered={poweredTowerCount} offline={unpoweredTowerCount} relayLimit={RelayLimit}");
    }

    private static Dictionary<DefenseTower, TowerOfflineReason> BuildOfflineReasons(
        List<RelayTower> relays,
        List<DefenseTower> towers,
        Dictionary<RelayTower, RelayEvaluation> evaluations,
        Dictionary<DefenseTower, RelayTower> assignments)
    {
        Dictionary<DefenseTower, TowerOfflineReason> reasons = new Dictionary<DefenseTower, TowerOfflineReason>();

        for (int towerIndex = 0; towerIndex < towers.Count; towerIndex++)
        {
            DefenseTower tower = towers[towerIndex];
            if (assignments.ContainsKey(tower))
            {
                continue;
            }

            List<RelayTower> coveringRelays = relays
                .Where(relay => relay.ContainsPoint(tower.transform.position))
                .OrderBy(relay => relay.RelayNumber)
                .ToList();

            if (coveringRelays.Count == 0)
            {
                reasons[tower] = new TowerOfflineReason("断电：当前不在任何继电器覆盖范围内。");
                continue;
            }

            RelayTower lowestRelay = coveringRelays[0];
            if (coveringRelays.Count == 1)
            {
                reasons[tower] = new TowerOfflineReason(
                    $"断电：轮到这座塔时，继电器 #{lowestRelay.RelayNumber} 的容量已经耗尽。");
                continue;
            }

            bool anyRelayHasWorkingTower = coveringRelays.Any(relay =>
                evaluations.TryGetValue(relay, out RelayEvaluation evaluation) && evaluation.WorkingTowers.Count > 0);

            reasons[tower] = anyRelayHasWorkingTower
                ? new TowerOfflineReason(
                    $"断电：虽然处于多个继电器覆盖内，但更高优先级的塔先耗尽了所有可用供电。")
                : new TowerOfflineReason(
                    $"断电：虽然处于继电器覆盖内，但当前没有任何继电器能为这座塔预留足够容量。");
        }

        return reasons;
    }

    private static int GetRelayCapacity(RelayTower relay, Dictionary<RelayTower, int> relayCapacityOverrides)
    {
        if (relayCapacityOverrides != null && relayCapacityOverrides.TryGetValue(relay, out int overriddenCapacity))
        {
            return Mathf.Max(0, overriddenCapacity);
        }

        return relay != null ? relay.SupplyCapacity : 0;
    }

    private static int GetTowerPowerRequired(DefenseTower tower, Dictionary<DefenseTower, int> towerPowerOverrides)
    {
        if (towerPowerOverrides != null && towerPowerOverrides.TryGetValue(tower, out int overriddenPowerRequired))
        {
            return Mathf.Max(0, overriddenPowerRequired);
        }

        return tower != null ? tower.PowerRequired : 0;
    }

    private void CollectRuntimeStructures(out List<RelayTower> relays, out List<DefenseTower> towers)
    {
        relays = new List<RelayTower>();
        towers = new List<DefenseTower>();

        if (_placedTowerRoot == null)
        {
            return;
        }

        for (int index = 0; index < _placedTowerRoot.childCount; index++)
        {
            Transform child = _placedTowerRoot.GetChild(index);
            if (child == null || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            RelayTower relayTower = child.GetComponent<RelayTower>();
            if (relayTower != null)
            {
                relays.Add(relayTower);
            }

            DefenseTower defenseTower = child.GetComponent<DefenseTower>();
            if (defenseTower != null)
            {
                towers.Add(defenseTower);
            }
        }
    }

    private void AssignNumbersToExistingStructures()
    {
        CollectRuntimeStructures(out List<RelayTower> relays, out List<DefenseTower> towers);

        relays.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        towers.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        for (int relayIndex = 0; relayIndex < relays.Count; relayIndex++)
        {
            if (relays[relayIndex].RelayNumber >= 100 && _relayNumbers.Count > 0)
            {
                relays[relayIndex].AssignRelayNumber(_relayNumbers.Pop());
            }
        }

        for (int towerIndex = 0; towerIndex < towers.Count; towerIndex++)
        {
            if (towers[towerIndex].TowerNumber >= 100 && _towerNumbers.Count > 0)
            {
                towers[towerIndex].AssignTowerNumber(_towerNumbers.Pop());
            }
        }
    }

    private sealed class SimulationResult
    {
        public Dictionary<RelayTower, RelayEvaluation> Evaluations { get; set; } // 中文：Evaluations
        public Dictionary<DefenseTower, RelayTower> Assignments { get; set; } // 中文：Assignments
    }
}
