using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
#if UNITY_EDITOR
    private const string DefaultWaveCatalogAssetPath = "Assets/Resources/TowerDefense/Configs/WaveCatalog.asset";
    private const string DefaultEnemyCatalogAssetPath = "Assets/Resources/TowerDefense/Configs/EnemyCatalog.asset";
#endif

    private readonly struct SpawnGroupRuntime
    {
        public SpawnGroupRuntime(EnemyArchetypeId enemyType, int enemyCount, float spawnInterval)
        {
            EnemyType = enemyType;
            EnemyCount = enemyCount;
            SpawnInterval = spawnInterval;
        }

        public EnemyArchetypeId EnemyType { get; }
        public int EnemyCount { get; }
        public float SpawnInterval { get; }
    }

    private readonly struct WaveRuntime
    {
        public WaveRuntime(string displayName, SpawnGroupRuntime[] spawnGroups)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Wave" : displayName;
            SpawnGroups = spawnGroups ?? Array.Empty<SpawnGroupRuntime>();
        }

        public string DisplayName { get; }
        public SpawnGroupRuntime[] SpawnGroups { get; }

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

    [Serializable]
    private sealed class LegacyWaveDefinition
    {
        public int enemyCount = 4;
        public float spawnInterval = 1f;
        public float moveSpeed = 1.8f;
        public int enemyHealth = 3;
        public int enemyScrapReward = 8;
    }

    [Header("Wave Timing")]
    [SerializeField] private float initialDelay = 1.5f;
    [SerializeField] private float delayBetweenWaves = 4f;

    [Header("Route Preview")]
    [Tooltip("敌人路线预告会在正式出怪前多少秒开始显示。只有第一波，或后续路线相对上一波发生变化时，这个时间窗口才会生效。")]
    [Min(0f)]
    [SerializeField] private float routePreviewLeadTime = 2f;

    [Header("Campaign Flow")]
    [SerializeField] private bool continueCampaignAfterClear = true;
    [SerializeField] private KeyCode clearContinuePrimaryKey = KeyCode.Return;
    [SerializeField] private KeyCode clearContinueSecondaryKey = KeyCode.Space;
    [SerializeField] private string clearContinueMessage = "Combat segment secured. Press Enter / Space to continue the story.";

    [Header("Map References")]
    [SerializeField] private BattlefieldMapDefinition battlefieldMapReference;
    [SerializeField] private WaveCatalogAsset waveCatalogAsset;
    [SerializeField] private EnemyCatalogAsset enemyCatalogAsset;

    [Header("Scene References")]
    [SerializeField] private EnemyPath enemyPathReference;
    [SerializeField] private GameObject enemyPrototypeReference;
    [SerializeField] private Transform enemyRootReference;

    [Header("Legacy Wave Fallback")]
    [SerializeField] private LegacyWaveDefinition[] waves;

    private BattlefieldMapDefinition _battlefieldMap;
    private EnemyPath _fallbackEnemyPath;
    private GameObject _enemyPrototype;
    private Transform _enemyRoot;
    private WaveRuntime[] _resolvedWaves = Array.Empty<WaveRuntime>();

    private int _currentWaveIndex;
    private int _currentSpawnGroupIndex;
    private int _spawnedInCurrentGroup;
    private float _spawnTimer;
    private bool _waitingForFirstWave;
    private bool _levelClearMessageShown;
    private int _spawnGateSequence;
    private bool _routePreviewVisible;
    private bool _hasStartedAnyWave;
    private string _lastStartedWaveRouteSignature = string.Empty;
    private readonly List<EnemyPath> _allRoutePreviewPaths = new List<EnemyPath>();
    private readonly List<EnemyPath> _activeRoutePreviewPaths = new List<EnemyPath>();

    private void Start()
    {
        EnsureCatalogAssetsAssigned();
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
                        TowerDefenseGame.Instance.ShowTransientHudNotice("Campaign segment clear. Continue to the next scene when ready.", duration: 3.2f);
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
                    TowerDefenseGame.Instance.SetStatusMessage("Test level complete! The base survived.");
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
                TowerDefenseGame.Instance.SetStatusMessage($"{wave.DisplayName} incoming. Hold the line!");
                _waitingForFirstWave = false;
            }
            else
            {
                TowerDefenseGame.Instance.SetStatusMessage($"{wave.DisplayName} started.");
            }

            TowerDefenseGame.Instance.ShowTransientHudNotice(
                $"{wave.DisplayName}: salvage potential {GetWaveScrapRewardTotal(wave)} SCRAP.",
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
                TowerDefenseGame.Instance.SetStatusMessage($"Wave {_currentWaveIndex} cleared. Prepare for the next one.");
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

        if (waves != null && waves.Length > 0)
        {
            _resolvedWaves = ConvertLegacyWaveEntries(waves);
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
                convertedWaves[waveIndex] = new WaveRuntime("Wave", Array.Empty<SpawnGroupRuntime>());
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

    private static WaveRuntime[] ConvertLegacyWaveEntries(LegacyWaveDefinition[] legacyWaves)
    {
        WaveRuntime[] convertedWaves = new WaveRuntime[legacyWaves.Length];
        for (int waveIndex = 0; waveIndex < legacyWaves.Length; waveIndex++)
        {
            LegacyWaveDefinition legacyWave = legacyWaves[waveIndex];
            SpawnGroupRuntime[] convertedGroups =
            {
                new SpawnGroupRuntime(EnemyArchetypeId.Scavenger, Mathf.Max(0, legacyWave.enemyCount), Mathf.Max(0.05f, legacyWave.spawnInterval))
            };

            convertedWaves[waveIndex] = new WaveRuntime($"Wave {waveIndex + 1:00}", convertedGroups);
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

    private void OnValidate()
    {
        EnsureCatalogAssetsAssigned();
    }

    private void EnsureCatalogAssetsAssigned()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (waveCatalogAsset == null)
            {
                waveCatalogAsset = AssetDatabase.LoadAssetAtPath<WaveCatalogAsset>(DefaultWaveCatalogAssetPath);
            }

            if (enemyCatalogAsset == null)
            {
                enemyCatalogAsset = AssetDatabase.LoadAssetAtPath<EnemyCatalogAsset>(DefaultEnemyCatalogAssetPath);
            }
        }
#endif
    }
}
