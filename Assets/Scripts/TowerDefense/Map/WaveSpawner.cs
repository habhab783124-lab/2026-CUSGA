using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `WaveSpawner` 负责把配置好的波次真正刷进战场。
///
/// 这一版开始正式支持“多怪物系统”：
/// - 每一波可以由多个刷怪组组成
/// - 每个刷怪组可以指定不同敌人类型
/// - 敌人的真实属性由 `EnemyCatalogAsset` 统一提供
///
/// 这样波次系统就不再只会刷“同一种小怪的不同数值版本”，
/// 而是能真正表达：
/// - 快速突破怪
/// - 护盾支援怪
/// - 修理支援怪
/// - 隐身怪
/// - 重甲怪
/// - 死亡分裂怪
/// 这些结构差异。
/// </summary>
public sealed class WaveSpawner : MonoBehaviour
{

    private readonly struct SpawnGroupRuntime
    {
        public SpawnGroupRuntime(EnemyArchetypeId enemyType, int enemyCount, float spawnInterval)
        {
            EnemyType = enemyType;
            EnemyCount = enemyCount;
            SpawnInterval = spawnInterval;
        }

        public EnemyArchetypeId EnemyType { get; } // 中文：敌人类型
        public int EnemyCount { get; } // 中文：敌人数量
        public float SpawnInterval { get; } // 中文：出怪间隔
    }

    private readonly struct WaveRuntime
    {
        public WaveRuntime(string displayName, SpawnGroupRuntime[] spawnGroups)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "波次" : displayName;
            SpawnGroups = spawnGroups ?? Array.Empty<SpawnGroupRuntime>();
        }

        public string DisplayName { get; } // 中文：显示名称
        public SpawnGroupRuntime[] SpawnGroups { get; } // 中文：出怪Groups

        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                for (int index = 0; index < SpawnGroups.Length; index++)
                {
                    total += SpawnGroups[index].EnemyCount;
                }

                return total;
            }
        }
    }

    [Header("波次时序")]
    [SerializeField, InspectorName("首波延迟")] private float initialDelay = 1.5f; // 中文：initialDelay
    [SerializeField, InspectorName("波次间隔")] private float delayBetweenWaves = 4f; // 中文：delayBetween波次列表

    [Header("路线预告")]
    [Tooltip("敌人路线预告会在正式出怪前多少秒开始显示。只有第一波，或后续路线相对上一波发生变化时，这个时间窗口才会生效。")]
    [Min(0f)]
    [SerializeField, InspectorName("预告提前秒数")] private float routePreviewLeadTime = 2f; // 中文：路线预览Lead时间

    [Header("战役流程")]
    [SerializeField, InspectorName("通关后继续战役")] private bool continueCampaignAfterClear = true; // 中文：继续战役After清除
    [SerializeField, InspectorName("继续主按键")] private KeyCode clearContinuePrimaryKey = KeyCode.Return; // 中文：清除继续主Key
    [SerializeField, InspectorName("继续副按键")] private KeyCode clearContinueSecondaryKey = KeyCode.Space; // 中文：清除继续副Key
    [SerializeField, InspectorName("通关提示文案")] private string clearContinueMessage = "战斗段已完成。按 Enter / Space 继续后续剧情。"; // 中文：清除继续消息

    [Header("地图引用")]
    [SerializeField, InspectorName("战场地图定义")] private BattlefieldMapDefinition battlefieldMapReference; // 中文：战场地图引用
    [SerializeField, InspectorName("波次目录资产")] private WaveCatalogAsset waveCatalogAsset; // 中文：波次目录资产
    [SerializeField, InspectorName("敌人目录资产")] private EnemyCatalogAsset enemyCatalogAsset; // 中文：敌人目录资产

    [Header("场景引用")]
    [SerializeField, InspectorName("后备路线")] private EnemyPath enemyPathReference; // 中文：敌人路径引用
    [SerializeField, InspectorName("敌人后备 Prefab")] private GameObject enemyPrototypeReference; // 中文：敌人原型引用
    [SerializeField, InspectorName("敌人根节点")] private Transform enemyRootReference; // 中文：敌人根节点引用

    private BattlefieldMapDefinition _battlefieldMap; // 中文：战场地图
    private EnemyPath _fallbackEnemyPath; // 中文：fallback敌人路径
    private GameObject _enemyPrototype; // 中文：敌人原型
    private Transform _enemyRoot; // 中文：敌人根节点
    private WaveRuntime[] _resolvedWaves = Array.Empty<WaveRuntime>(); // 中文：resolved波次列表

    private int _currentWaveIndex; // 中文：当前波次Index
    private int _currentSpawnGroupIndex; // 中文：当前出怪GroupIndex
    private int _spawnedInCurrentGroup; // 中文：spawnedIn当前Group
    private float _spawnTimer; // 中文：出怪计时器
    private bool _waitingForFirstWave; // 中文：waitingForFirst波次
    private bool _levelClearMessageShown; // 中文：等级清除消息Shown
    private int _spawnGateSequence; // 中文：出怪出怪口Sequence
    private bool _routePreviewVisible; // 中文：路线预览Visible
    private bool _hasStartedAnyWave; // 中文：是否有StartedAny波次
    private string _lastStartedWaveRouteSignature = string.Empty; // 中文：lastStarted波次路线签名
    private readonly List<EnemyPath> _allRoutePreviewPaths = new List<EnemyPath>(); // 中文：all路线预览路径
    private readonly List<EnemyPath> _activeRoutePreviewPaths = new List<EnemyPath>(); // 中文：激活路线预览路径

    private void Start()
    {
        EnsureWaveData();

        _battlefieldMap = battlefieldMapReference;
        _fallbackEnemyPath = enemyPathReference;
        _enemyPrototype = enemyPrototypeReference;
        _enemyRoot = enemyRootReference;

        CacheRoutePreviewPaths();

        if (_battlefieldMap != null)
        {
            _battlefieldMap.LogConfigurationWarnings(this);
            Debug.Log($"[WaveSpawner] Stage-A map summary: {_battlefieldMap.BuildDebugSummary()}", this);
        }

        if (_battlefieldMap == null)
        {
            Debug.LogError("WaveSpawner 缺少 BattlefieldMapDefinition 显式引用。当前版本不再按类型自动查找地图定义。", this);
            enabled = false;
            return;
        }

        if (_enemyRoot == null)
        {
            Debug.LogError("WaveSpawner 缺少 enemyRootReference。当前版本不再运行时自动创建 EnemiesRoot。", this);
            enabled = false;
            return;
        }

        if (_enemyPrototype == null)
        {
            Debug.LogError("WaveSpawner 缺少 EnemyPrototype 引用。", this);
            enabled = false;
            return;
        }

        if (enemyCatalogAsset == null)
        {
            Debug.LogError("WaveSpawner 缺少 EnemyCatalogAsset。当前多怪物主链必须依赖敌人目录资产。", this);
            enabled = false;
            return;
        }

        if (!HasAnySpawnSource())
        {
            Debug.LogError("WaveSpawner 缺少有效出怪来源。请检查 BattlefieldMapDefinition / EnemyPath 接线。", this);
            enabled = false;
            return;
        }

        if (_resolvedWaves.Length == 0)
        {
            Debug.LogError("WaveSpawner 当前没有任何有效波次配置。", this);
            enabled = false;
            return;
        }

        _spawnTimer = initialDelay;
        _waitingForFirstWave = true;
        UpdateRoutePreviewVisibility(force: true);
    }

    private void Update()
    {
        if (TowerDefenseGame.Instance != null && TowerDefenseGame.Instance.IsGameOver)
        {
            UpdateRoutePreviewVisibility(force: true, overrideVisible: false);
            return;
        }

        if (_currentWaveIndex >= _resolvedWaves.Length)
        {
            UpdateRoutePreviewVisibility(force: true, overrideVisible: false);

            if (Enemy.ActiveEnemyCount == 0 && TowerDefenseGame.Instance != null)
            {
                if (continueCampaignAfterClear && CampaignFlowController.HasActiveCampaign)
                {
                    if (!_levelClearMessageShown)
                    {
                        TowerDefenseGame.Instance.SetStatusMessage(clearContinueMessage);
                        TowerDefenseGame.Instance.ShowTransientHudNotice("当前战役节点已完成，准备好后可进入下一段。", duration: 3.2f);
                        _levelClearMessageShown = true;
                    }

                    if (Input.GetKeyDown(clearContinuePrimaryKey) || Input.GetKeyDown(clearContinueSecondaryKey))
                    {
                        CampaignFlowController.AdvanceToNextStep();
                        return;
                    }
                }
                else if (!_levelClearMessageShown)
                {
                    TowerDefenseGame.Instance.SetStatusMessage("本关完成，基地成功守住了。");
                    _levelClearMessageShown = true;
                }
            }

            return;
        }

        _spawnTimer -= Time.deltaTime;
        UpdateRoutePreviewVisibility(force: false);
        if (_spawnTimer > 0f)
        {
            return;
        }

        WaveRuntime wave = _resolvedWaves[_currentWaveIndex];
        if (_currentSpawnGroupIndex < 0 || _currentSpawnGroupIndex >= wave.SpawnGroups.Length)
        {
            CompleteCurrentWave();
            return;
        }

        SpawnGroupRuntime spawnGroup = wave.SpawnGroups[_currentSpawnGroupIndex];
        if (spawnGroup.EnemyCount <= 0)
        {
            AdvanceToNextSpawnGroupOrWave(wave);
            return;
        }

        if (_spawnedInCurrentGroup == 0 && _currentSpawnGroupIndex == 0 && TowerDefenseGame.Instance != null)
        {
            TowerDefenseGame.Instance.SetWaveProgress(_currentWaveIndex + 1, _resolvedWaves.Length);
            MarkCurrentWaveRouteAsStarted();

            if (_waitingForFirstWave)
            {
                TowerDefenseGame.Instance.SetStatusMessage($"{wave.DisplayName} 即将到来，准备迎战。");
                _waitingForFirstWave = false;
            }
            else
            {
                TowerDefenseGame.Instance.SetStatusMessage($"{wave.DisplayName} 已开始。");
            }

            TowerDefenseGame.Instance.ShowTransientHudNotice(
                $"{wave.DisplayName}：本波最多可回收 {GetWaveScrapRewardTotal(wave)} 废料。",
                duration: 3.4f);
        }

        if (!SpawnEnemy(spawnGroup.EnemyType, _currentWaveIndex + 1, _spawnedInCurrentGroup + 1))
        {
            enabled = false;
            return;
        }

        _spawnedInCurrentGroup++;
        UpdateRoutePreviewVisibility(force: true);

        if (_spawnedInCurrentGroup < spawnGroup.EnemyCount)
        {
            _spawnTimer = spawnGroup.SpawnInterval;
            return;
        }

        AdvanceToNextSpawnGroupOrWave(wave);
    }

    private void AdvanceToNextSpawnGroupOrWave(WaveRuntime currentWave)
    {
        _currentSpawnGroupIndex++;
        _spawnedInCurrentGroup = 0;

        if (_currentSpawnGroupIndex < currentWave.SpawnGroups.Length)
        {
            _spawnTimer = currentWave.SpawnGroups[_currentSpawnGroupIndex].SpawnInterval;
            return;
        }

        CompleteCurrentWave();
    }

    private void CompleteCurrentWave()
    {
        _currentWaveIndex++;
        _currentSpawnGroupIndex = 0;
        _spawnedInCurrentGroup = 0;

        if (_currentWaveIndex < _resolvedWaves.Length)
        {
            _spawnTimer = delayBetweenWaves;
            UpdateRoutePreviewVisibility(force: true);

            if (TowerDefenseGame.Instance != null)
            {
                TowerDefenseGame.Instance.SetStatusMessage($"第 {_currentWaveIndex} 波已清空，准备迎接下一波。");
            }
        }
        else
        {
            _spawnTimer = 0f;
            UpdateRoutePreviewVisibility(force: true, overrideVisible: false);
        }
    }

    private void EnsureWaveData()
    {
        if (waveCatalogAsset != null && waveCatalogAsset.Waves.Length > 0)
        {
            _resolvedWaves = ConvertWaveEntries(waveCatalogAsset.Waves);
            return;
        }

        _resolvedWaves = Array.Empty<WaveRuntime>();
    }

    private static WaveRuntime[] ConvertWaveEntries(WaveCatalogAsset.WaveEntry[] waveEntries)
    {
        WaveRuntime[] convertedWaves = new WaveRuntime[waveEntries.Length];
        for (int waveIndex = 0; waveIndex < waveEntries.Length; waveIndex++)
        {
            WaveCatalogAsset.WaveEntry waveEntry = waveEntries[waveIndex];
            if (waveEntry == null)
            {
                convertedWaves[waveIndex] = new WaveRuntime("波次", Array.Empty<SpawnGroupRuntime>());
                continue;
            }

            WaveCatalogAsset.SpawnGroup[] sourceGroups = waveEntry.SpawnGroups;
            SpawnGroupRuntime[] convertedGroups = new SpawnGroupRuntime[sourceGroups.Length];
            for (int groupIndex = 0; groupIndex < sourceGroups.Length; groupIndex++)
            {
                WaveCatalogAsset.SpawnGroup sourceGroup = sourceGroups[groupIndex];
                convertedGroups[groupIndex] = sourceGroup == null
                    ? new SpawnGroupRuntime(EnemyArchetypeId.Scavenger, 0, 1f)
                    : new SpawnGroupRuntime(sourceGroup.EnemyType, sourceGroup.EnemyCount, sourceGroup.SpawnInterval);
            }

            convertedWaves[waveIndex] = new WaveRuntime(waveEntry.DisplayName, convertedGroups);
        }

        return convertedWaves;
    }

    private bool SpawnEnemy(EnemyArchetypeId enemyType, int waveNumber, int enemyNumber)
    {
        EnemyPath spawnPath = ResolveSpawnPath(out EnemySpawnGate spawnGate);
        if (spawnPath == null)
        {
            Debug.LogWarning("WaveSpawner could not resolve a valid EnemyPath for the next spawn.", this);
            return false;
        }

        EnemyCatalogAsset.EnemyArchetypeDefinition enemyDefinition = enemyCatalogAsset != null
            ? enemyCatalogAsset.GetDefinition(enemyType)
            : null;
        if (enemyDefinition == null)
        {
            Debug.LogWarning($"WaveSpawner 无法找到敌人类型 `{enemyType}` 对应的定义。", this);
            return false;
        }

        GameObject enemyPrefab = enemyDefinition.RuntimePrefab != null ? enemyDefinition.RuntimePrefab : _enemyPrototype;
        if (enemyPrefab == null)
        {
            Debug.LogWarning($"WaveSpawner 找到了敌人类型 `{enemyType}`，但它没有可用的运行时 Prefab。", this);
            return false;
        }

        GameObject enemyObject = Instantiate(enemyPrefab, spawnPath.GetSpawnPosition(), Quaternion.identity, _enemyRoot);
        enemyObject.name = spawnGate != null
            ? $"{enemyDefinition.DisplayName}_{spawnGate.GateId}_W{waveNumber}_{enemyNumber}"
            : $"{enemyDefinition.DisplayName}_W{waveNumber}_{enemyNumber}";
        enemyObject.SetActive(true);

        Enemy enemy = enemyObject.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.Initialize(
                path: spawnPath,
                enemyCatalog: enemyCatalogAsset,
                archetypeId: enemyType,
                enemyPrototypePrefab: enemyPrefab,
                enemyRoot: _enemyRoot);
        }

        return true;
    }

    private bool HasAnySpawnSource()
    {
        return (_battlefieldMap != null && _battlefieldMap.HasAnyValidSpawnGate()) || _fallbackEnemyPath != null;
    }

    private int GetWaveScrapRewardTotal(WaveRuntime wave)
    {
        if (enemyCatalogAsset == null)
        {
            return 0;
        }

        int total = 0;
        for (int groupIndex = 0; groupIndex < wave.SpawnGroups.Length; groupIndex++)
        {
            SpawnGroupRuntime group = wave.SpawnGroups[groupIndex];
            EnemyCatalogAsset.EnemyArchetypeDefinition definition = enemyCatalogAsset.GetDefinition(group.EnemyType);
            if (definition == null)
            {
                continue;
            }

            total += Mathf.Max(0, group.EnemyCount) * definition.ScrapReward;
        }

        return total;
    }

    private EnemyPath ResolveSpawnPath(out EnemySpawnGate spawnGate)
    {
        spawnGate = null;

        if (_battlefieldMap != null && _battlefieldMap.TryGetSpawnGateBySequence(_spawnGateSequence, out spawnGate))
        {
            _spawnGateSequence++;
            return spawnGate != null ? spawnGate.EnemyPath : null;
        }

        return _fallbackEnemyPath;
    }

    private void CacheRoutePreviewPaths()
    {
        _allRoutePreviewPaths.Clear();

        if (_battlefieldMap != null && _battlefieldMap.HasAnyValidSpawnGate())
        {
            for (int i = 0; i < _battlefieldMap.SpawnGateCount; i++)
            {
                if (!_battlefieldMap.TryGetSpawnGateBySequence(i, out EnemySpawnGate spawnGate) || spawnGate == null)
                {
                    continue;
                }

                EnemyPath path = spawnGate.EnemyPath;
                if (path != null && !_allRoutePreviewPaths.Contains(path))
                {
                    _allRoutePreviewPaths.Add(path);
                }
            }
        }

        if (_allRoutePreviewPaths.Count == 0 && _fallbackEnemyPath != null)
        {
            _allRoutePreviewPaths.Add(_fallbackEnemyPath);
        }
    }

    private void UpdateRoutePreviewVisibility(bool force, bool? overrideVisible = null)
    {
        bool shouldShow = overrideVisible ?? ShouldShowRoutePreview();
        if (!force && _routePreviewVisible == shouldShow)
        {
            return;
        }

        BuildActiveRoutePreviewPaths(_activeRoutePreviewPaths);
        _routePreviewVisible = shouldShow;

        for (int i = 0; i < _allRoutePreviewPaths.Count; i++)
        {
            EnemyPath path = _allRoutePreviewPaths[i];
            if (path != null)
            {
                bool pathShouldShow = shouldShow && _activeRoutePreviewPaths.Contains(path);
                path.SetRuntimeReadabilityVisible(pathShouldShow);
            }
        }
    }

    private bool ShouldShowRoutePreview()
    {
        if (_currentWaveIndex >= _resolvedWaves.Length)
        {
            return false;
        }

        if (_spawnedInCurrentGroup > 0)
        {
            return false;
        }

        if (_spawnTimer > Mathf.Max(0f, routePreviewLeadTime))
        {
            return false;
        }

        string upcomingSignature = BuildWaveRouteSignature(_currentWaveIndex, _spawnGateSequence);
        if (!_hasStartedAnyWave)
        {
            return true;
        }

        return !string.Equals(upcomingSignature, _lastStartedWaveRouteSignature, StringComparison.Ordinal);
    }

    private void MarkCurrentWaveRouteAsStarted()
    {
        _lastStartedWaveRouteSignature = BuildWaveRouteSignature(_currentWaveIndex, _spawnGateSequence);
        _hasStartedAnyWave = true;
    }

    private void BuildActiveRoutePreviewPaths(List<EnemyPath> output)
    {
        output.Clear();

        if (_currentWaveIndex >= _resolvedWaves.Length)
        {
            return;
        }

        WaveRuntime wave = _resolvedWaves[_currentWaveIndex];
        int totalEnemyCount = wave.TotalEnemyCount;

        if (_battlefieldMap != null && _battlefieldMap.HasAnyValidSpawnGate())
        {
            for (int enemyIndex = 0; enemyIndex < totalEnemyCount; enemyIndex++)
            {
                int gateSequence = _spawnGateSequence + enemyIndex;
                if (!_battlefieldMap.TryGetSpawnGateBySequence(gateSequence, out EnemySpawnGate spawnGate) || spawnGate == null)
                {
                    continue;
                }

                EnemyPath path = spawnGate.EnemyPath;
                if (path != null && !output.Contains(path))
                {
                    output.Add(path);
                }
            }
        }

        if (output.Count == 0 && _fallbackEnemyPath != null)
        {
            output.Add(_fallbackEnemyPath);
        }
    }

    private string BuildWaveRouteSignature(int waveIndex, int spawnGateSequenceAtWaveStart)
    {
        if (waveIndex < 0 || waveIndex >= _resolvedWaves.Length)
        {
            return string.Empty;
        }

        List<int> instanceIds = new List<int>();
        int totalEnemyCount = _resolvedWaves[waveIndex].TotalEnemyCount;

        if (_battlefieldMap != null && _battlefieldMap.HasAnyValidSpawnGate())
        {
            for (int enemyIndex = 0; enemyIndex < totalEnemyCount; enemyIndex++)
            {
                int gateSequence = spawnGateSequenceAtWaveStart + enemyIndex;
                if (!_battlefieldMap.TryGetSpawnGateBySequence(gateSequence, out EnemySpawnGate spawnGate) || spawnGate == null)
                {
                    continue;
                }

                EnemyPath path = spawnGate.EnemyPath;
                if (path == null)
                {
                    continue;
                }

                int instanceId = path.GetInstanceID();
                if (!instanceIds.Contains(instanceId))
                {
                    instanceIds.Add(instanceId);
                }
            }
        }
        else if (_fallbackEnemyPath != null)
        {
            instanceIds.Add(_fallbackEnemyPath.GetInstanceID());
        }

        instanceIds.Sort();
        return string.Join("|", instanceIds);
    }

}
