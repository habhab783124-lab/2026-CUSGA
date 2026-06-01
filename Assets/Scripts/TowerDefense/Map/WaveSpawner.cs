using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// `WaveSpawner` drives enemy wave progression inside one authored combat scene.
///
/// This version deliberately resolves the long-standing split between:
/// - scene-local fallback wave arrays
/// - `WaveCatalogAsset` / `EnemyCatalogAsset` based authoring
///
/// The new rule is:
/// 1. If a valid `WaveCatalogAsset` + `EnemyCatalogAsset` pair is assigned, that asset pipeline is
///    the primary planner workflow and runtime source.
/// 2. If the asset workflow is not wired yet, the older scene-local `waves` array still works as
///    a compatibility fallback so existing levels do not immediately break.
///
/// That gives planners an asset-first workflow without forcing a risky big-bang migration.
/// </summary>
public sealed class WaveSpawner : MonoBehaviour
{
    [Serializable]
    private struct WaveDefinition
    {
        public int enemyCount;
        public float spawnInterval;
        public float moveSpeed;
        public int enemyHealth;
        public int enemyScrapReward;
        public EnemyPath enemyPathReference;
        public int pathSequence;
    }

    /// <summary>
    /// One normalized spawn group used by runtime regardless of authoring source.
    ///
    /// Both scene-array waves and `WaveCatalogAsset` waves are converted into this compact model so
    /// the runtime loop only has one code path to maintain.
    /// </summary>
    private sealed class RuntimeSpawnGroup
    {
        public EnemyArchetypeId EnemyType = EnemyArchetypeId.None;
        public int EnemyCount;
        public float SpawnInterval;
        public float MoveSpeed;
        public int EnemyHealth;
        public int EnemyScrapReward;
        public GameObject RuntimePrefab;
        public EnemyPath PathReference;
        public int PathSequence;
    }

    [Serializable]
    private struct CatalogGroupPathBinding
    {
        public int waveIndex;
        public int groupIndex;
        public EnemyPath enemyPathReference;
    }

    /// <summary>
    /// One normalized runtime wave built from either scene fallback data or the wave catalog.
    /// </summary>
    private sealed class RuntimeWave
    {
        public string DisplayName = "Wave";
        public string DesignerNote = string.Empty;
        public readonly List<RuntimeSpawnGroup> Groups = new List<RuntimeSpawnGroup>();

        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                for (int index = 0; index < Groups.Count; index++)
                {
                    RuntimeSpawnGroup group = Groups[index];
                    if (group != null)
                    {
                        total += Mathf.Max(0, group.EnemyCount);
                    }
                }

                return total;
            }
        }

        public int TotalScrapReward
        {
            get
            {
                int total = 0;
                for (int index = 0; index < Groups.Count; index++)
                {
                    RuntimeSpawnGroup group = Groups[index];
                    if (group != null)
                    {
                        total += Mathf.Max(0, group.EnemyCount) * Mathf.Max(0, group.EnemyScrapReward);
                    }
                }

                return total;
            }
        }
    }

    [Header("Wave Timing")]
    [SerializeField] private float initialDelay = 1.5f;
    [SerializeField] private float delayBetweenWaves = 4f;

    [Header("Route Preview")]
    [Tooltip("Route preview becomes visible this many seconds before a wave actually starts.")]
    [Min(0f)]
    [SerializeField] private float routePreviewLeadTime = 2f;

    [Header("Map References")]
    [SerializeField] private BattlefieldMapDefinition battlefieldMapReference;

    [Header("Asset Content (Primary)")]
    [SerializeField] private WaveCatalogAsset waveCatalogAsset;
    [SerializeField] private EnemyCatalogAsset enemyCatalogAsset;
    [SerializeField] private bool continueCampaignAfterClear;
    [SerializeField] private string fallbackNextSceneNameAfterClear;
    [SerializeField] private float fadeOutToBlackDurationAfterClear = 0.75f;
    [SerializeField] private float fadeInFromBlackDurationAfterClear = 0.75f;

    [Header("Scene References (Fallback)")]
    [SerializeField] private EnemyPath enemyPathReference;
    [SerializeField] private GameObject enemyPrototypeReference;
    [SerializeField] private Transform enemyRootReference;

    [Header("Scene Object Names")]
    [SerializeField] private string enemyRootName = "EnemiesRoot";

    [Header("Wave Content (Fallback)")]
    [SerializeField] private WaveDefinition[] waves;

    [Header("Scene Path Bindings (Catalog Runtime)")]
    [SerializeField] private CatalogGroupPathBinding[] catalogGroupPathBindings = Array.Empty<CatalogGroupPathBinding>();

    private BattlefieldMapDefinition _battlefieldMap;
    private EnemyPath _fallbackEnemyPath;
    private GameObject _enemyPrototype;
    private Transform _enemyRoot;
    private readonly List<RuntimeWave> _runtimeWaves = new List<RuntimeWave>();

    private int _currentWaveIndex;
    private int _currentSpawnGroupIndex;
    private int _spawnedInCurrentWave;
    private int _spawnedInCurrentGroup;
    private float _spawnTimer;
    private bool _waitingForFirstWave;
    private bool _levelClearMessageShown;
    private bool _campaignAdvanceTriggered;
    private bool _fallbackSceneAdvanceTriggered;
    private bool _routePreviewVisible;
    private bool _hasStartedAnyWave;
    private string _lastStartedWaveRouteSignature = string.Empty;
    private readonly List<EnemyPath> _allRoutePreviewPaths = new List<EnemyPath>();
    private readonly List<EnemyPath> _activeRoutePreviewPaths = new List<EnemyPath>();

    public WaveCatalogAsset WaveCatalogAsset => waveCatalogAsset;
    public EnemyCatalogAsset EnemyCatalogAsset => enemyCatalogAsset;
    public bool UsesWaveCatalog => waveCatalogAsset != null && enemyCatalogAsset != null && waveCatalogAsset.Waves.Length > 0;
    public bool IsRoutePreviewVisible => _routePreviewVisible;
    public int UpcomingWaveNumber => _currentWaveIndex < _runtimeWaves.Count ? _currentWaveIndex + 1 : 0;

    private void Start()
    {
        EnsureFallbackWaveData();

        _battlefieldMap = battlefieldMapReference != null
            ? battlefieldMapReference
            : UnityEngine.Object.FindFirstObjectByType<BattlefieldMapDefinition>();
        battlefieldMapReference = _battlefieldMap;

        _fallbackEnemyPath = enemyPathReference;
        _enemyPrototype = enemyPrototypeReference;
        _enemyRoot = enemyRootReference != null ? enemyRootReference : new GameObject(enemyRootName).transform;
        enemyRootReference = _enemyRoot;

        BuildRuntimeWaves();
        EnsureRuntimeWaveSourceIsUsable();
        CacheRoutePreviewPaths();

        if (_battlefieldMap != null)
        {
            _battlefieldMap.LogConfigurationWarnings(this);
            Debug.Log($"[WaveSpawner] Map summary: {_battlefieldMap.BuildDebugSummary()}", this);
        }

        if (_runtimeWaves.Count == 0)
        {
            Debug.LogWarning("WaveSpawner has no effective wave content. Assign a WaveCatalogAsset or keep fallback scene waves.", this);
            enabled = false;
            return;
        }

        if (_enemyPrototype == null && !UsesWaveCatalog)
        {
            Debug.LogWarning("WaveSpawner is missing fallback enemyPrototypeReference for scene-array waves.", this);
            enabled = false;
            return;
        }

        if (!HasAnySpawnSource())
        {
            Debug.LogWarning("WaveSpawner is missing a valid spawn source. Check BattlefieldMapDefinition / EnemyPath wiring.", this);
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

        if (_currentWaveIndex >= _runtimeWaves.Count)
        {
            UpdateRoutePreviewVisibility(force: true, overrideVisible: false);

            if (!_levelClearMessageShown && Enemy.ActiveEnemyCount == 0 && TowerDefenseGame.Instance != null)
            {
                TowerDefenseGame.Instance.SetStatusMessage("Test level complete! The base survived.");
                _levelClearMessageShown = true;
            }

            if (!_campaignAdvanceTriggered && Enemy.ActiveEnemyCount == 0)
            {
                if (TowerDefenseGame.Instance != null)
                {
                    if (TowerDefenseGame.Instance.BeginLevelClearSequence(
                            continueCampaignAfterClear,
                            fallbackNextSceneNameAfterClear,
                            fadeOutToBlackDurationAfterClear,
                            fadeInFromBlackDurationAfterClear,
                            startOpaqueOnContinue: true))
                    {
                        _campaignAdvanceTriggered = true;
                    }
                }
                else if (!_fallbackSceneAdvanceTriggered && !string.IsNullOrWhiteSpace(fallbackNextSceneNameAfterClear))
                {
                    _fallbackSceneAdvanceTriggered = true;
                    Time.timeScale = 1f;
                    ScreenFadeTransition.Play(
                        fallbackNextSceneNameAfterClear,
                        fadeOutToBlackDurationAfterClear,
                        fadeInFromBlackDurationAfterClear,
                        startOpaque: false);
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

        RuntimeWave runtimeWave = _runtimeWaves[_currentWaveIndex];
        RuntimeSpawnGroup runtimeGroup = runtimeWave.Groups[_currentSpawnGroupIndex];

        if (_spawnedInCurrentWave == 0 && TowerDefenseGame.Instance != null)
        {
            TowerDefenseGame.Instance.SetWaveProgress(_currentWaveIndex + 1, _runtimeWaves.Count);
            MarkCurrentWaveRouteAsStarted();

            string waveLabel = string.IsNullOrWhiteSpace(runtimeWave.DisplayName)
                ? $"Wave {_currentWaveIndex + 1}"
                : runtimeWave.DisplayName;

            if (_waitingForFirstWave)
            {
                TowerDefenseGame.Instance.SetStatusMessage($"{waveLabel} incoming. Hold the line!");
                _waitingForFirstWave = false;
            }
            else
            {
                TowerDefenseGame.Instance.SetStatusMessage($"{waveLabel} started.");
            }

            TowerDefenseGame.Instance.ShowTransientHudNotice(
                $"{waveLabel}: salvage potential {runtimeWave.TotalScrapReward} SCRAP.",
                duration: 3.4f);
        }

        if (!SpawnEnemy(runtimeGroup, _currentWaveIndex + 1, _spawnedInCurrentWave + 1))
        {
            enabled = false;
            return;
        }

        _spawnedInCurrentWave++;
        _spawnedInCurrentGroup++;
        UpdateRoutePreviewVisibility(force: true);

        if (_spawnedInCurrentGroup < runtimeGroup.EnemyCount)
        {
            _spawnTimer = runtimeGroup.SpawnInterval;
            return;
        }

        _currentSpawnGroupIndex++;
        _spawnedInCurrentGroup = 0;

        if (_currentSpawnGroupIndex < runtimeWave.Groups.Count)
        {
            RuntimeSpawnGroup nextGroup = runtimeWave.Groups[_currentSpawnGroupIndex];
            _spawnTimer = nextGroup.SpawnInterval;
            return;
        }

        _currentWaveIndex++;
        _currentSpawnGroupIndex = 0;
        _spawnedInCurrentWave = 0;

        if (_currentWaveIndex < _runtimeWaves.Count)
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

    /// <summary>
    /// Keeps older scenes playable even before they migrate to `WaveCatalogAsset`.
    /// </summary>
    private void EnsureFallbackWaveData()
    {
        if (waves != null && waves.Length > 0)
        {
            return;
        }

        waves = new[]
        {
            new WaveDefinition { enemyCount = 4, spawnInterval = 1.0f, moveSpeed = 1.8f, enemyHealth = 3, enemyScrapReward = 8, enemyPathReference = null, pathSequence = 0 },
            new WaveDefinition { enemyCount = 6, spawnInterval = 0.85f, moveSpeed = 2.1f, enemyHealth = 4, enemyScrapReward = 11, enemyPathReference = null, pathSequence = 0 },
            new WaveDefinition { enemyCount = 8, spawnInterval = 0.70f, moveSpeed = 2.4f, enemyHealth = 6, enemyScrapReward = 15, enemyPathReference = null, pathSequence = 0 }
        };
    }

    /// <summary>
    /// Builds the unified runtime wave list from the preferred authoring source.
    /// </summary>
    private void BuildRuntimeWaves()
    {
        _runtimeWaves.Clear();

        if (UsesWaveCatalog)
        {
            BuildRuntimeWavesFromCatalog();
            if (_runtimeWaves.Count > 0)
            {
                return;
            }
        }

        BuildRuntimeWavesFromFallbackSceneData();
    }

    private void BuildRuntimeWavesFromCatalog()
    {
        if (waveCatalogAsset == null || enemyCatalogAsset == null)
        {
            return;
        }

        WaveCatalogAsset.WaveEntry[] authoredWaves = waveCatalogAsset.Waves;
        for (int waveIndex = 0; waveIndex < authoredWaves.Length; waveIndex++)
        {
            WaveCatalogAsset.WaveEntry authoredWave = authoredWaves[waveIndex];
            if (authoredWave == null)
            {
                continue;
            }

            RuntimeWave runtimeWave = new RuntimeWave
            {
                DisplayName = authoredWave.DisplayName,
                DesignerNote = authoredWave.DesignerNote
            };

            WaveCatalogAsset.SpawnGroup[] authoredGroups = authoredWave.SpawnGroups;
            for (int groupIndex = 0; groupIndex < authoredGroups.Length; groupIndex++)
            {
                WaveCatalogAsset.SpawnGroup authoredGroup = authoredGroups[groupIndex];
                if (authoredGroup == null)
                {
                    continue;
                }

                if (!enemyCatalogAsset.TryGetDefinition(authoredGroup.EnemyType, out EnemyCatalogAsset.EnemyArchetypeDefinition definition) || definition == null)
                {
                    Debug.LogWarning(
                        $"WaveSpawner skipped group {groupIndex + 1} of wave '{authoredWave.DisplayName}' because enemy type '{authoredGroup.EnemyType}' is missing from EnemyCatalogAsset.",
                        this);
                    continue;
                }

                runtimeWave.Groups.Add(new RuntimeSpawnGroup
                {
                    EnemyType = authoredGroup.EnemyType,
                    EnemyCount = authoredGroup.EnemyCount,
                    SpawnInterval = authoredGroup.SpawnInterval,
                    MoveSpeed = definition.MoveSpeed,
                    EnemyHealth = definition.MaxHealth,
                    EnemyScrapReward = definition.ScrapReward,
                    RuntimePrefab = definition.RuntimePrefab != null ? definition.RuntimePrefab : enemyPrototypeReference,
                    PathReference = authoredGroup.EnemyPathReference != null
                        ? authoredGroup.EnemyPathReference
                        : ResolveCatalogGroupPathReference(waveIndex, groupIndex),
                    PathSequence = authoredGroup.PathSequence
                });
            }

            if (runtimeWave.Groups.Count > 0)
            {
                _runtimeWaves.Add(runtimeWave);
            }
        }
    }

    private void BuildRuntimeWavesFromFallbackSceneData()
    {
        if (waves == null)
        {
            return;
        }

        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            WaveDefinition authoredWave = waves[waveIndex];
            RuntimeWave runtimeWave = new RuntimeWave
            {
                DisplayName = $"Wave {waveIndex + 1:D2}",
                DesignerNote = "Scene fallback wave"
            };

            runtimeWave.Groups.Add(new RuntimeSpawnGroup
            {
                EnemyType = EnemyArchetypeId.None,
                EnemyCount = Mathf.Max(0, authoredWave.enemyCount),
                SpawnInterval = Mathf.Max(0.05f, authoredWave.spawnInterval),
                MoveSpeed = Mathf.Max(0.05f, authoredWave.moveSpeed),
                EnemyHealth = Mathf.Max(1, authoredWave.enemyHealth),
                EnemyScrapReward = Mathf.Max(0, authoredWave.enemyScrapReward),
                RuntimePrefab = enemyPrototypeReference,
                PathReference = authoredWave.enemyPathReference,
                PathSequence = Mathf.Max(0, authoredWave.pathSequence)
            });

            _runtimeWaves.Add(runtimeWave);
        }
    }

    private bool SpawnEnemy(RuntimeSpawnGroup runtimeGroup, int waveNumber, int enemyNumber)
    {
        EnemyPath spawnPath = ResolveSpawnPath(runtimeGroup, out EnemySpawnGate spawnGate);
        if (spawnPath == null)
        {
            Debug.LogWarning("WaveSpawner could not resolve a valid EnemyPath for the next spawn.", this);
            return false;
        }

        GameObject prototype = ResolveUsableEnemyPrototype(runtimeGroup);
        if (prototype == null)
        {
            Debug.LogWarning("WaveSpawner could not resolve a runtime enemy prefab for the next spawn.", this);
            return false;
        }

        GameObject enemyObject = Instantiate(prototype, spawnPath.GetSpawnPosition(), Quaternion.identity, _enemyRoot);
        enemyObject.name = spawnGate != null
            ? $"Enemy_{spawnGate.GateId}_W{waveNumber}_{enemyNumber}"
            : $"Enemy_W{waveNumber}_{enemyNumber}";
        enemyObject.SetActive(true);

        Enemy enemy = enemyObject.GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogWarning($"WaveSpawner spawned '{prototype.name}', but it does not contain an Enemy component.", this);
            return false;
        }

        if (UsesWaveCatalog && runtimeGroup.EnemyType != EnemyArchetypeId.None)
        {
            enemy.Initialize(spawnPath, enemyCatalogAsset, runtimeGroup.EnemyType, prototype, _enemyRoot);
        }
        else
        {
            enemy.Initialize(spawnPath, runtimeGroup.MoveSpeed, runtimeGroup.EnemyHealth, runtimeGroup.EnemyScrapReward);
        }

        return true;
    }

    /// <summary>
    /// Keeps authored catalog scenes playable even if part of the catalog wiring is incomplete.
    ///
    /// The planner-facing asset workflow is the preferred path, but gameplay should not fully
    /// collapse just because one level still has a stale prefab or an empty path reference.
    /// If the asset-built runtime waves cannot actually be used, we fall back to the older scene
    /// array so the level at least remains testable.
    /// </summary>
    private void EnsureRuntimeWaveSourceIsUsable()
    {
        if (_runtimeWaves.Count == 0)
        {
            return;
        }

        if (CanUseCurrentRuntimeWaves())
        {
            return;
        }

        Debug.LogWarning("WaveSpawner detected an unusable primary wave source and is falling back to scene-local waves.", this);
        _runtimeWaves.Clear();
        BuildRuntimeWavesFromFallbackSceneData();
    }

    private bool CanUseCurrentRuntimeWaves()
    {
        for (int waveIndex = 0; waveIndex < _runtimeWaves.Count; waveIndex++)
        {
            RuntimeWave runtimeWave = _runtimeWaves[waveIndex];
            if (runtimeWave == null || runtimeWave.Groups.Count == 0)
            {
                continue;
            }

            RuntimeSpawnGroup firstGroup = runtimeWave.Groups[0];
            if (ResolveUsableEnemyPrototype(firstGroup) == null)
            {
                return false;
            }

            EnemyPath probePath = ResolveFirstUsableSpawnPath();
            if (probePath == null)
            {
                return false;
            }
        }

        return true;
    }

    private GameObject ResolveUsableEnemyPrototype(RuntimeSpawnGroup runtimeGroup)
    {
        if (runtimeGroup != null && TryGetUsableEnemyPrototype(runtimeGroup.RuntimePrefab, out GameObject groupPrototype))
        {
            return groupPrototype;
        }

        if (TryGetUsableEnemyPrototype(_enemyPrototype, out GameObject fallbackPrototype))
        {
            return fallbackPrototype;
        }

        return null;
    }

    /// <summary>
    /// Validates whether a candidate prefab reference is still safe to use for spawning.
    ///
    /// This extra wrapper is important because Unity object references can enter an awkward state
    /// where the backing object has already been destroyed, but an old serialized wrapper still
    /// exists long enough for direct member access such as `GetComponent<T>()` to throw
    /// `MissingReferenceException`.
    ///
    /// Instead of letting that exception tear down the whole wave system for the level, we treat
    /// that candidate as unusable and cleanly fall back to the next available prototype source.
    /// </summary>
    private static bool TryGetUsableEnemyPrototype(GameObject candidate, out GameObject usablePrototype)
    {
        usablePrototype = null;
        if (candidate == null)
        {
            return false;
        }

        try
        {
            if (candidate.GetComponent<Enemy>() == null)
            {
                return false;
            }

            usablePrototype = candidate;
            return true;
        }
        catch (MissingReferenceException)
        {
            return false;
        }
    }

    private bool HasAnySpawnSource()
    {
        return (_battlefieldMap != null && _battlefieldMap.HasAnyValidSpawnGate()) || _fallbackEnemyPath != null;
    }

    private EnemyPath ResolveSpawnPath(RuntimeSpawnGroup runtimeGroup, out EnemySpawnGate spawnGate)
    {
        spawnGate = null;

        if (runtimeGroup != null && IsUsablePath(runtimeGroup.PathReference))
        {
            EnemySpawnGate directGate = ResolveSpawnGateForPath(runtimeGroup.PathReference);
            if (directGate != null)
            {
                spawnGate = directGate;
            }

            return runtimeGroup.PathReference;
        }

        int requestedPathSequence = runtimeGroup != null ? Mathf.Max(0, runtimeGroup.PathSequence) : 0;

        if (_battlefieldMap != null && _battlefieldMap.TryGetSpawnGateBySequence(requestedPathSequence, out spawnGate))
        {
            EnemyPath gatePath = spawnGate != null ? spawnGate.EnemyPath : null;
            if (IsUsablePath(gatePath))
            {
                return gatePath;
            }
        }

        if (IsUsablePath(_fallbackEnemyPath))
        {
            return _fallbackEnemyPath;
        }

        return ResolveFirstUsableSpawnPath();
    }

    private EnemyPath ResolveFirstUsableSpawnPath()
    {
        if (_battlefieldMap != null && _battlefieldMap.HasAnyValidSpawnGate())
        {
            for (int gateIndex = 0; gateIndex < _battlefieldMap.SpawnGateCount; gateIndex++)
            {
                if (_battlefieldMap.TryGetSpawnGateBySequence(gateIndex, out EnemySpawnGate spawnGate) && spawnGate != null && IsUsablePath(spawnGate.EnemyPath))
                {
                    return spawnGate.EnemyPath;
                }
            }
        }

        return IsUsablePath(_fallbackEnemyPath) ? _fallbackEnemyPath : null;
    }

    private EnemySpawnGate ResolveSpawnGateForPath(EnemyPath enemyPath)
    {
        if (_battlefieldMap == null || enemyPath == null || !_battlefieldMap.HasAnyValidSpawnGate())
        {
            return null;
        }

        for (int gateIndex = 0; gateIndex < _battlefieldMap.SpawnGateCount; gateIndex++)
        {
            if (_battlefieldMap.TryGetSpawnGateBySequence(gateIndex, out EnemySpawnGate spawnGate) && spawnGate != null && spawnGate.EnemyPath == enemyPath)
            {
                return spawnGate;
            }
        }

        return null;
    }

    private EnemyPath ResolveCatalogGroupPathReference(int waveIndex, int groupIndex, bool requireUsablePath = true)
    {
        for (int bindingIndex = 0; bindingIndex < catalogGroupPathBindings.Length; bindingIndex++)
        {
            CatalogGroupPathBinding binding = catalogGroupPathBindings[bindingIndex];
            if (binding.waveIndex != waveIndex || binding.groupIndex != groupIndex)
            {
                continue;
            }

            if (binding.enemyPathReference == null)
            {
                return null;
            }

            if (!requireUsablePath || IsUsablePath(binding.enemyPathReference))
            {
                return binding.enemyPathReference;
            }

            return null;
        }

        return null;
    }

    private static bool IsUsablePath(EnemyPath enemyPath)
    {
        return enemyPath != null && enemyPath.WaypointCount > 0;
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
        if (_currentWaveIndex >= _runtimeWaves.Count)
        {
            return false;
        }

        if (_spawnedInCurrentWave > 0)
        {
            return false;
        }

        if (_spawnTimer > Mathf.Max(0f, routePreviewLeadTime))
        {
            return false;
        }

        string upcomingSignature = BuildWaveRouteSignature(_currentWaveIndex);
        if (!_hasStartedAnyWave)
        {
            return true;
        }

        return !string.Equals(upcomingSignature, _lastStartedWaveRouteSignature, StringComparison.Ordinal);
    }

    private void MarkCurrentWaveRouteAsStarted()
    {
        _lastStartedWaveRouteSignature = BuildWaveRouteSignature(_currentWaveIndex);
        _hasStartedAnyWave = true;
    }

    private void BuildActiveRoutePreviewPaths(List<EnemyPath> output)
    {
        output.Clear();

        if (_currentWaveIndex >= _runtimeWaves.Count)
        {
            return;
        }

        if (_battlefieldMap != null && _battlefieldMap.HasAnyValidSpawnGate())
        {
            RuntimeWave runtimeWave = _runtimeWaves[_currentWaveIndex];
            for (int groupIndex = 0; groupIndex < runtimeWave.Groups.Count; groupIndex++)
            {
                RuntimeSpawnGroup spawnGroup = runtimeWave.Groups[groupIndex];
                EnemyPath path = null;
                if (spawnGroup != null && IsUsablePath(spawnGroup.PathReference))
                {
                    path = spawnGroup.PathReference;
                }
                else
                {
                    int gateSequence = spawnGroup != null ? Mathf.Max(0, spawnGroup.PathSequence) : 0;
                    if (_battlefieldMap.TryGetSpawnGateBySequence(gateSequence, out EnemySpawnGate spawnGate) && spawnGate != null)
                    {
                        path = spawnGate.EnemyPath;
                    }
                }

                if (path == null)
                {
                    continue;
                }

                if (!output.Contains(path))
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

    private string BuildWaveRouteSignature(int waveIndex)
    {
        if (waveIndex < 0 || waveIndex >= _runtimeWaves.Count)
        {
            return string.Empty;
        }

        List<int> instanceIds = new List<int>();

        if (_battlefieldMap != null && _battlefieldMap.HasAnyValidSpawnGate())
        {
            RuntimeWave runtimeWave = _runtimeWaves[waveIndex];
            for (int groupIndex = 0; groupIndex < runtimeWave.Groups.Count; groupIndex++)
            {
                RuntimeSpawnGroup runtimeGroup = runtimeWave.Groups[groupIndex];
                EnemyPath path = null;
                if (runtimeGroup != null && IsUsablePath(runtimeGroup.PathReference))
                {
                    path = runtimeGroup.PathReference;
                }
                else
                {
                    int gateSequence = runtimeGroup != null ? Mathf.Max(0, runtimeGroup.PathSequence) : 0;
                    if (_battlefieldMap.TryGetSpawnGateBySequence(gateSequence, out EnemySpawnGate spawnGate) && spawnGate != null)
                    {
                        path = spawnGate.EnemyPath;
                    }
                }

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

    public EnemyPath ResolveCatalogBinding(int waveIndex, int groupIndex)
    {
        return ResolveCatalogGroupPathReference(waveIndex, groupIndex, requireUsablePath: false);
    }

    public void AssignCatalogBinding(int waveIndex, int groupIndex, EnemyPath enemyPath)
    {
        for (int bindingIndex = 0; bindingIndex < catalogGroupPathBindings.Length; bindingIndex++)
        {
            if (catalogGroupPathBindings[bindingIndex].waveIndex != waveIndex || catalogGroupPathBindings[bindingIndex].groupIndex != groupIndex)
            {
                continue;
            }

            CatalogGroupPathBinding existing = catalogGroupPathBindings[bindingIndex];
            existing.enemyPathReference = enemyPath;
            catalogGroupPathBindings[bindingIndex] = existing;
            return;
        }

        Array.Resize(ref catalogGroupPathBindings, catalogGroupPathBindings.Length + 1);
        catalogGroupPathBindings[catalogGroupPathBindings.Length - 1] = new CatalogGroupPathBinding
        {
            waveIndex = waveIndex,
            groupIndex = groupIndex,
            enemyPathReference = enemyPath
        };
    }
}
