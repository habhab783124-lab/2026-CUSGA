using System;
using UnityEngine;

/// <summary>
/// `Level01TutorialDirector` keeps the tutorial-specific pacing out of the shared combat loop.
/// It only coordinates teaching beats for `Tutorial Level`:
/// - phased tower unlocks
/// - command-center guidance copy
/// - one lightweight side-lane pulse marker
/// </summary>
[DisallowMultipleComponent]
public sealed class Level01TutorialDirector : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float openingDelay = 0.6f;
    [SerializeField] private float evaluationInterval = 0.2f;
    [SerializeField] private float repeatHintInterval = 6f;
    [SerializeField] private float noticeDuration = 3.2f;

    [Header("Opening")]
    [SerializeField] private string openingStatusMessage = "指挥中心：外围供能尚未接通。先在起始部署区架设继电器，建立前线第一段供能链。";
    [SerializeField] private string openingNoticeMessage = "教学 1/4：先建立供能链。";

    [Header("After Relay")]
    [SerializeField] private string relayPlacedStatusMessage = "指挥中心：供能链路已接通。现在在继电器覆盖范围内部署单体塔，准备迎击第一波试探。";
    [SerializeField] private string relayPlacedNoticeMessage = "教学 2/4：单体塔负责稳定点杀。";

    [Header("First Wave")]
    [SerializeField] private string firstTowerPlacedStatusMessage = "指挥中心：火力节点就位。守住第一波，留意击毁目标后回收的废料。";
    [SerializeField] private string firstTowerPlacedNoticeMessage = "第一波来袭，保持火力连续。";
    [SerializeField] private string firstScrapRecoveredStatusMessage = "指挥中心：废料回收成功。击毁目标会补充部署资源，后续扩建依赖这条补给回路。";
    [SerializeField] private string firstScrapRecoveredNoticeMessage = "教学 3/4：回收废料，准备扩线。";

    [Header("Second Wave")]
    [SerializeField] private string secondWaveStatusMessage = "指挥中心：侦测到副路信号抬升。第二波预览已出现，优先在副路附近补上一座减速塔，拉长敌军暴露时间。";
    [SerializeField] private string secondWaveNoticeMessage = "教学 4/4：副路附近部署减速塔。";
    [SerializeField] private string slowTowerPlacedStatusMessage = "指挥中心：减速区已建立。把敌军拖在副路口，主火力就能稳定清线。";
    [SerializeField] private string slowTowerPlacedNoticeMessage = "副路控制完成，继续稳住前线。";

    [Header("Final Wave")]
    [SerializeField] private string finalWaveStatusMessage = "指挥中心：最后一波接近。维持主路火力，不要让副路重新失控。";
    [SerializeField] private string finalWaveNoticeMessage = "守住现有部署，准备收束战斗。";

    [Header("Tutorial Locks")]
    [SerializeField] private string lockedTowerStatusMessage = "指挥中心：当前阶段尚未批准部署该塔型，先完成当前指引。";

    [Header("Side Lane Marker")]
    [SerializeField] private float sideLaneMarkerPathSample = 0.62f;
    [SerializeField] private float sideLaneMarkerRadius = 0.92f;
    [SerializeField] private float sideLaneMarkerLineWidth = 0.12f;
    [SerializeField] private float sideLaneMarkerPulseSpeed = 2.35f;
    [SerializeField] private float sideLaneMarkerPulseAmplitude = 0.24f;
    [SerializeField] private Color sideLaneMarkerColor = new Color(0.36f, 0.95f, 0.84f, 0.72f);
    [SerializeField] private int sideLaneMarkerSortingOrder = 18;

    [Header("Failure Tone")]
    [SerializeField] private string failureCommanderLine = "指挥中心：本次失稳节点已经记录，判断链路没有中断。";
    [SerializeField] private string failureFollowUpLine = "保持冷静，重新校准继电器位置与副路减速区后，我们立刻可以再试一次。";

    private TowerDefenseGame _game;
    private WaveSpawner _waveSpawner;
    private Transform _placedTowerRoot;
    private EnemySpawnGate _sideLaneSpawnGate;
    private TutorialGroundPulseMarker _sideLaneMarker;
    private float _levelStartTime;
    private float _nextEvaluationTime;
    private float _nextRepeatHintTime;
    private int _lastKnownScrap = -1;
    private bool _tutorialSetupApplied;
    private bool _openingShown;
    private bool _relayStepCompleted;
    private bool _singleTowerStepCompleted;
    private bool _scrapStepCompleted;
    private bool _secondWaveBriefed;
    private bool _slowTowerStepCompleted;
    private bool _finalWaveBriefed;

    private void Start()
    {
        _levelStartTime = Time.time;
    }

    private void Update()
    {
        _sideLaneMarker?.Tick(Time.time);

        if (Time.time < _nextEvaluationTime)
        {
            return;
        }

        _nextEvaluationTime = Time.time + Mathf.Max(0.05f, evaluationInterval);
        if (!ResolveReferences() || _game == null)
        {
            return;
        }

        if (!_tutorialSetupApplied)
        {
            ConfigureTutorialRuntime();
        }

        if (_game.IsGameOver)
        {
            HideSideLaneMarker();
            return;
        }

        EvaluateTutorialProgress();
    }

    private void OnDestroy()
    {
        HideSideLaneMarker();
        _sideLaneMarker?.Dispose();
        _sideLaneMarker = null;

        if (_game != null)
        {
            _game.ClearTutorialTowerAvailability();
            _game.ClearTutorialFailureDialogueOverride();
            _game.ClearTutorialStatusMessage();
            _game.ClearTutorialHudNotice();
            _game.ConfigureTutorialSelectionHudSections(showPrimaryOperationSection: true, showPowerGridSection: true);
        }
    }

    private bool ResolveReferences()
    {
        if (_game == null)
        {
            _game = TowerDefenseGame.Instance != null
                ? TowerDefenseGame.Instance
                : FindFirstObjectByType<TowerDefenseGame>();
        }

        if (_waveSpawner == null)
        {
            _waveSpawner = FindFirstObjectByType<WaveSpawner>();
        }

        if (_game != null && _placedTowerRoot == null)
        {
            _placedTowerRoot = _game.PlacedTowerRoot;
        }

        if (_sideLaneSpawnGate == null)
        {
            _sideLaneSpawnGate = ResolveSideLaneSpawnGate();
        }

        if (_sideLaneMarker == null)
        {
            _sideLaneMarker = new TutorialGroundPulseMarker(
                parent: transform,
                markerName: "TutorialSideLaneMarker",
                radius: sideLaneMarkerRadius,
                lineWidth: sideLaneMarkerLineWidth,
                pulseSpeed: sideLaneMarkerPulseSpeed,
                pulseAmplitude: sideLaneMarkerPulseAmplitude,
                color: sideLaneMarkerColor,
                sortingOrder: sideLaneMarkerSortingOrder);
        }

        return _game != null;
    }

    private void ConfigureTutorialRuntime()
    {
        if (_game == null)
        {
            return;
        }

        _game.SetTutorialLockedTowerStatusMessage(lockedTowerStatusMessage);
        _game.SetTutorialFailureDialogueOverride(failureCommanderLine, failureFollowUpLine);
        _game.SetTutorialStatusMessage(openingStatusMessage);
        _game.ConfigureTutorialSelectionHudSections(showPrimaryOperationSection: false, showPowerGridSection: false);
        ApplyOpeningTowerAvailability();
        _lastKnownScrap = _game.CurrentScrap;
        HideSideLaneMarker();
        _tutorialSetupApplied = true;
    }

    private void EvaluateTutorialProgress()
    {
        StructureCounts counts = CollectPlacedStructureCounts();

        if (!_openingShown)
        {
            if (Time.time - _levelStartTime >= openingDelay)
            {
                BroadcastStep(openingStatusMessage, openingNoticeMessage, HudNoticeTone.Warning);
                _openingShown = true;
                _nextRepeatHintTime = Time.time + repeatHintInterval;
            }

            return;
        }

        if (!_relayStepCompleted)
        {
            if (counts.RelayCount > 0)
            {
                BroadcastStep(relayPlacedStatusMessage, relayPlacedNoticeMessage, HudNoticeTone.Positive);
                ApplyPostRelayTowerAvailability();
                _relayStepCompleted = true;
                _nextRepeatHintTime = Time.time + repeatHintInterval;
            }
            else
            {
                RepeatHintIfNeeded(openingStatusMessage);
            }

            return;
        }

        if (!_singleTowerStepCompleted)
        {
            if (counts.SingleTargetTowerCount > 0)
            {
                BroadcastStep(firstTowerPlacedStatusMessage, firstTowerPlacedNoticeMessage, HudNoticeTone.Positive);
                _singleTowerStepCompleted = true;
                _lastKnownScrap = _game.CurrentScrap;
                _nextRepeatHintTime = Time.time + repeatHintInterval;
            }
            else
            {
                RepeatHintIfNeeded(relayPlacedStatusMessage);
            }

            return;
        }

        if (!_scrapStepCompleted && _lastKnownScrap >= 0 && _game.CurrentScrap > _lastKnownScrap)
        {
            BroadcastStep(firstScrapRecoveredStatusMessage, firstScrapRecoveredNoticeMessage, HudNoticeTone.Positive);
            _scrapStepCompleted = true;
            _nextRepeatHintTime = Time.time + repeatHintInterval;
        }

        if (!_secondWaveBriefed && IsSecondWavePreviewVisible())
        {
            BroadcastStep(secondWaveStatusMessage, secondWaveNoticeMessage, HudNoticeTone.Warning);
            ApplySecondWaveTowerAvailability();
            _secondWaveBriefed = true;
            _nextRepeatHintTime = Time.time + repeatHintInterval;
        }

        if (_secondWaveBriefed && !_slowTowerStepCompleted)
        {
            if (counts.SlowFieldTowerCount > 0)
            {
                BroadcastStep(slowTowerPlacedStatusMessage, slowTowerPlacedNoticeMessage, HudNoticeTone.Positive);
                ApplyPostSlowTowerAvailability();
                _slowTowerStepCompleted = true;
                _nextRepeatHintTime = Time.time + repeatHintInterval;
                HideSideLaneMarker();
            }
            else
            {
                ShowSideLaneMarker();
                RepeatHintIfNeeded(secondWaveStatusMessage);
            }
        }
        else
        {
            HideSideLaneMarker();
        }

        if (!_finalWaveBriefed && _game.CurrentWave >= 3)
        {
            BroadcastStep(finalWaveStatusMessage, finalWaveNoticeMessage, HudNoticeTone.Warning);
            _finalWaveBriefed = true;
        }

        _lastKnownScrap = _game.CurrentScrap;
    }

    private void ApplyOpeningTowerAvailability()
    {
        _game?.ApplyTutorialTowerAvailability(
            relayAvailability: TowerTutorialAvailability.Recommended,
            singleTargetAvailability: TowerTutorialAvailability.Locked,
            slowFieldAvailability: TowerTutorialAvailability.Locked,
            bombardAvailability: TowerTutorialAvailability.Locked);
    }

    private void ApplyPostRelayTowerAvailability()
    {
        _game?.ApplyTutorialTowerAvailability(
            relayAvailability: TowerTutorialAvailability.Available,
            singleTargetAvailability: TowerTutorialAvailability.Recommended,
            slowFieldAvailability: TowerTutorialAvailability.Locked,
            bombardAvailability: TowerTutorialAvailability.Locked);
    }

    private void ApplySecondWaveTowerAvailability()
    {
        _game?.ApplyTutorialTowerAvailability(
            relayAvailability: TowerTutorialAvailability.Available,
            singleTargetAvailability: TowerTutorialAvailability.Available,
            slowFieldAvailability: TowerTutorialAvailability.Recommended,
            bombardAvailability: TowerTutorialAvailability.Locked);
    }

    private void ApplyPostSlowTowerAvailability()
    {
        _game?.ApplyTutorialTowerAvailability(
            relayAvailability: TowerTutorialAvailability.Available,
            singleTargetAvailability: TowerTutorialAvailability.Available,
            slowFieldAvailability: TowerTutorialAvailability.Available,
            bombardAvailability: TowerTutorialAvailability.Locked);
    }

    private bool IsSecondWavePreviewVisible()
    {
        return _waveSpawner != null &&
               _waveSpawner.IsRoutePreviewVisible &&
               _waveSpawner.UpcomingWaveNumber == 2;
    }

    private void RepeatHintIfNeeded(string statusMessage)
    {
        if (Time.time < _nextRepeatHintTime || _game == null)
        {
            return;
        }

        _game.SetTutorialStatusMessage(statusMessage);
        _nextRepeatHintTime = Time.time + repeatHintInterval;
    }

    private void BroadcastStep(string statusMessage, string noticeMessage, HudNoticeTone tone)
    {
        if (_game == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            _game.SetTutorialStatusMessage(statusMessage);
        }

        if (!string.IsNullOrWhiteSpace(noticeMessage))
        {
            _game.ShowTutorialHudNotice(noticeMessage, tone);
        }
    }

    private void ShowSideLaneMarker()
    {
        if (_sideLaneMarker == null)
        {
            return;
        }

        if (!TryGetSideLaneMarkerWorldPosition(out Vector3 worldPosition))
        {
            return;
        }

        _sideLaneMarker.Show(worldPosition);
    }

    private void HideSideLaneMarker()
    {
        _sideLaneMarker?.Hide();
    }

    private bool TryGetSideLaneMarkerWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (_sideLaneSpawnGate == null)
        {
            return false;
        }

        EnemyPath sideLanePath = _sideLaneSpawnGate.EnemyPath;
        if (sideLanePath != null && TrySamplePathPosition(sideLanePath, sideLaneMarkerPathSample, out worldPosition))
        {
            return true;
        }

        worldPosition = _sideLaneSpawnGate.transform.position;
        return true;
    }

    private EnemySpawnGate ResolveSideLaneSpawnGate()
    {
        EnemySpawnGate[] spawnGates = FindObjectsByType<EnemySpawnGate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (spawnGates == null || spawnGates.Length == 0)
        {
            return null;
        }

        Array.Sort(
            spawnGates,
            (left, right) => string.CompareOrdinal(
                left != null ? left.GateId : string.Empty,
                right != null ? right.GateId : string.Empty));

        return spawnGates.Length > 1 ? spawnGates[1] : spawnGates[0];
    }

    private static bool TrySamplePathPosition(EnemyPath path, float normalizedDistance, out Vector3 position)
    {
        position = Vector3.zero;
        if (path == null || path.WaypointCount == 0)
        {
            return false;
        }

        if (path.WaypointCount == 1)
        {
            position = path.GetWaypointPosition(0);
            return true;
        }

        float totalLength = 0f;
        for (int i = 0; i < path.WaypointCount - 1; i++)
        {
            totalLength += Vector3.Distance(path.GetWaypointPosition(i), path.GetWaypointPosition(i + 1));
        }

        if (totalLength <= 0.001f)
        {
            position = path.GetWaypointPosition(path.WaypointCount - 1);
            return true;
        }

        float remainingDistance = totalLength * Mathf.Clamp01(normalizedDistance);
        for (int i = 0; i < path.WaypointCount - 1; i++)
        {
            Vector3 segmentStart = path.GetWaypointPosition(i);
            Vector3 segmentEnd = path.GetWaypointPosition(i + 1);
            float segmentLength = Vector3.Distance(segmentStart, segmentEnd);
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            if (remainingDistance <= segmentLength)
            {
                float t = remainingDistance / segmentLength;
                position = Vector3.Lerp(segmentStart, segmentEnd, t);
                return true;
            }

            remainingDistance -= segmentLength;
        }

        position = path.GetWaypointPosition(path.WaypointCount - 1);
        return true;
    }

    private StructureCounts CollectPlacedStructureCounts()
    {
        StructureCounts counts = default;
        if (_placedTowerRoot == null)
        {
            return counts;
        }

        RelayTower[] relays = FindObjectsByType<RelayTower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int index = 0; index < relays.Length; index++)
        {
            RelayTower relay = relays[index];
            if (relay != null && relay.transform.IsChildOf(_placedTowerRoot))
            {
                counts.RelayCount++;
            }
        }

        DefenseTower[] towers = FindObjectsByType<DefenseTower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int index = 0; index < towers.Length; index++)
        {
            DefenseTower tower = towers[index];
            if (tower == null || !tower.transform.IsChildOf(_placedTowerRoot))
            {
                continue;
            }

            switch (tower.BuildType)
            {
                case TowerType.SingleTarget:
                    counts.SingleTargetTowerCount++;
                    break;

                case TowerType.SlowField:
                    counts.SlowFieldTowerCount++;
                    break;
            }
        }

        return counts;
    }

    private struct StructureCounts
    {
        public int RelayCount;
        public int SingleTargetTowerCount;
        public int SlowFieldTowerCount;
    }

    private sealed class TutorialGroundPulseMarker : IDisposable
    {
        private readonly Transform _root;
        private readonly LineRenderer _outerRing;
        private readonly LineRenderer _innerRing;
        private readonly Color _baseColor;
        private readonly float _pulseSpeed;
        private readonly float _pulseAmplitude;

        public TutorialGroundPulseMarker(
            Transform parent,
            string markerName,
            float radius,
            float lineWidth,
            float pulseSpeed,
            float pulseAmplitude,
            Color color,
            int sortingOrder)
        {
            _root = BattlefieldReadabilityVisualUtility.EnsureChild(parent, markerName);
            _root.localPosition = Vector3.zero;
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one;
            _baseColor = color;
            _pulseSpeed = Mathf.Max(0.1f, pulseSpeed);
            _pulseAmplitude = Mathf.Max(0f, pulseAmplitude);

            _outerRing = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
                _root,
                "OuterRing",
                sortingOrder,
                lineWidth,
                color,
                loop: true);
            BattlefieldReadabilityVisualUtility.SetCircle(_outerRing, radius, 28, lineWidth, color);

            _innerRing = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
                _root,
                "InnerRing",
                sortingOrder + 1,
                lineWidth * 0.72f,
                color,
                loop: true);
            BattlefieldReadabilityVisualUtility.SetCircle(_innerRing, radius * 0.62f, 24, lineWidth * 0.72f, color);

            _root.gameObject.SetActive(false);
        }

        public void Show(Vector3 worldPosition)
        {
            _root.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
            }

            Tick(Time.time);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        public void Tick(float time)
        {
            if (_root == null || !_root.gameObject.activeSelf)
            {
                return;
            }

            float pulse = 0.5f + (Mathf.Sin(time * _pulseSpeed) * 0.5f);
            float outerScale = 1f + (pulse * _pulseAmplitude);
            float innerScale = 0.78f + (pulse * _pulseAmplitude * 0.46f);
            _outerRing.transform.localScale = Vector3.one * outerScale;
            _innerRing.transform.localScale = Vector3.one * innerScale;

            Color outerColor = _baseColor;
            outerColor.a = Mathf.Lerp(_baseColor.a * 0.34f, _baseColor.a, pulse);
            _outerRing.startColor = outerColor;
            _outerRing.endColor = outerColor;

            Color innerColor = _baseColor;
            innerColor.a = Mathf.Lerp(_baseColor.a * 0.22f, _baseColor.a * 0.82f, pulse);
            _innerRing.startColor = innerColor;
            _innerRing.endColor = innerColor;
        }

        public void Dispose()
        {
            if (_root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_root.gameObject);
            }
        }
    }
}
