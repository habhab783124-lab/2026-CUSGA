using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Editor
{
    /// <summary>
    /// Builds a planner-facing level report from the currently authored scene.
    ///
    /// The report is intentionally different from the health-check export:
    /// - health check answers "what is broken?"
    /// - level report answers "what is this level asking the player to handle?"
    ///
    /// This keeps map production and balance communication practical for larger scenes.
    /// </summary>
    internal static class LevelDesignReportBuilder
    {
        private sealed class RouteMetrics
        {
            public EnemyPath Path;
            public float Length;
            public int TurnCount;
            public Vector3 Start;
            public Vector3 End;
        }

        private sealed class WavePressureSummary
        {
            public int TotalWaves;
            public int TotalEnemies;
            public int TotalHealthBudget;
            public int TotalScrapBudget;
            public readonly Dictionary<string, int> GateEnemyTotals = new Dictionary<string, int>();
            public readonly List<string> WaveBreakdowns = new List<string>();
        }

        private readonly struct OrthogonalSpan
        {
            public OrthogonalSpan(EnemyPath ownerPath, Vector3 start, Vector3 end)
            {
                OwnerPath = ownerPath;
                Start = start;
                End = end;
            }

            public EnemyPath OwnerPath { get; }
            public Vector3 Start { get; }
            public Vector3 End { get; }
            public bool IsHorizontal => Mathf.Abs(Start.y - End.y) <= 0.001f;
        }

        internal static string BuildMarkdown(Scene scene, BattlefieldMapDefinition mapDefinition, WaveSpawner waveSpawner)
        {
            List<EnemySpawnGate> spawnGates = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EnemySpawnGate>(true))
                .Where(gate => gate != null)
                .Distinct()
                .OrderBy(gate => gate.name, StringComparer.Ordinal)
                .ToList();

            List<DefensePointFlag> defensePoints = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DefensePointFlag>(true))
                .Where(point => point != null)
                .Distinct()
                .OrderBy(point => point.name, StringComparer.Ordinal)
                .ToList();

            List<EnemyPath> enemyPaths = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EnemyPath>(true))
                .Where(path => path != null)
                .Distinct()
                .OrderBy(path => path.name, StringComparer.Ordinal)
                .ToList();

            List<RouteMetrics> routeMetrics = enemyPaths.Select(BuildRouteMetrics).ToList();
            float buildZoneArea = EstimateBuildZoneArea(mapDefinition != null ? mapDefinition.BuildZone : null);
            int mergeCount = CountSharedRouteSpans(enemyPaths);
            WavePressureSummary waveSummary = BuildWavePressureSummary(mapDefinition, waveSpawner);

            float averageRouteLength = routeMetrics.Count > 0 ? routeMetrics.Average(metric => metric.Length) : 0f;
            int totalTurnCount = routeMetrics.Sum(metric => metric.TurnCount);
            float difficultyScore = CalculateDifficultyScore(
                spawnGates.Count,
                defensePoints.Count,
                averageRouteLength,
                totalTurnCount,
                mergeCount,
                buildZoneArea,
                waveSummary.TotalEnemies,
                waveSummary.TotalHealthBudget);

            string difficultyBand = difficultyScore switch
            {
                < 45f => "Simple",
                < 80f => "Standard",
                < 125f => "Hard",
                _ => "Extreme"
            };

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"# {scene.name} Level Design Report");
            builder.AppendLine();
            builder.AppendLine($"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"- Scene Path: {scene.path}");
            builder.AppendLine($"- Difficulty Score: {difficultyScore:0.0} ({difficultyBand})");
            builder.AppendLine();

            builder.AppendLine("## Scene Summary");
            builder.AppendLine();
            builder.AppendLine($"- Spawn Gates: {spawnGates.Count}");
            builder.AppendLine($"- Defense Points: {defensePoints.Count}");
            builder.AppendLine($"- Enemy Paths: {enemyPaths.Count}");
            builder.AppendLine($"- Build Zone Area (estimated): {buildZoneArea:0.0}");
            builder.AppendLine($"- Shared Route Merge Count: {mergeCount}");
            builder.AppendLine();

            builder.AppendLine("## Route Metrics");
            builder.AppendLine();
            foreach (RouteMetrics metric in routeMetrics)
            {
                string drivenGateNames = string.Join(", ", spawnGates
                    .Where(gate => gate != null && gate.EnemyPath == metric.Path)
                    .Select(gate => gate.DisplayName));
                string targetNames = string.Join(", ", spawnGates
                    .Where(gate => gate != null && gate.EnemyPath == metric.Path && gate.TargetDefensePoint != null)
                    .Select(gate => gate.TargetDefensePoint.DisplayName)
                    .Distinct());

                builder.AppendLine($"- {metric.Path.name}");
                builder.AppendLine($"  - Length: {metric.Length:0.0}");
                builder.AppendLine($"  - Turns: {metric.TurnCount}");
                builder.AppendLine($"  - Start: {metric.Start}");
                builder.AppendLine($"  - End: {metric.End}");
                builder.AppendLine($"  - Driven By Gates: {(string.IsNullOrWhiteSpace(drivenGateNames) ? "(None)" : drivenGateNames)}");
                builder.AppendLine($"  - Target Points: {(string.IsNullOrWhiteSpace(targetNames) ? "(None)" : targetNames)}");
            }
            builder.AppendLine();

            builder.AppendLine("## Gate Pressure Distribution");
            builder.AppendLine();
            foreach (EnemySpawnGate spawnGate in spawnGates)
            {
                string gateKey = spawnGate.DisplayName;
                int gateEnemyCount = waveSummary.GateEnemyTotals.TryGetValue(gateKey, out int count) ? count : 0;
                string targetName = spawnGate.TargetDefensePoint != null ? spawnGate.TargetDefensePoint.DisplayName : "(None)";
                builder.AppendLine($"- {gateKey}: {gateEnemyCount} enemies routed toward {targetName}");
            }
            builder.AppendLine();

            builder.AppendLine("## Wave Pressure Summary");
            builder.AppendLine();
            builder.AppendLine($"- Total Waves: {waveSummary.TotalWaves}");
            builder.AppendLine($"- Total Enemies: {waveSummary.TotalEnemies}");
            builder.AppendLine($"- Total Health Budget: {waveSummary.TotalHealthBudget}");
            builder.AppendLine($"- Total Scrap Budget: {waveSummary.TotalScrapBudget}");
            builder.AppendLine();
            foreach (string line in waveSummary.WaveBreakdowns)
            {
                builder.AppendLine($"- {line}");
            }
            builder.AppendLine();

            builder.AppendLine("## Difficulty Notes");
            builder.AppendLine();
            builder.AppendLine($"- Average Route Length: {averageRouteLength:0.0}");
            builder.AppendLine($"- Total Turn Count: {totalTurnCount}");
            builder.AppendLine($"- Multiple-Core Pressure: {(defensePoints.Count > 1 ? "Yes" : "No")}");
            builder.AppendLine($"- Wide Entry Pressure: {(spawnGates.Count >= 3 ? "Yes" : "No")}");
            builder.AppendLine("- Difficulty score is heuristic and should be treated as a comparison aid between scenes, not as an absolute truth.");

            return builder.ToString();
        }

        private static RouteMetrics BuildRouteMetrics(EnemyPath enemyPath)
        {
            List<Transform> waypoints = EnemyPathAuthoringUtility.GetWaypointChildren(enemyPath);
            float totalLength = 0f;
            int turnCount = 0;

            for (int index = 0; index < waypoints.Count - 1; index++)
            {
                if (waypoints[index] == null || waypoints[index + 1] == null)
                {
                    continue;
                }

                totalLength += Vector2.Distance(waypoints[index].position, waypoints[index + 1].position);
            }

            for (int index = 1; index < waypoints.Count - 1; index++)
            {
                if (waypoints[index - 1] == null || waypoints[index] == null || waypoints[index + 1] == null)
                {
                    continue;
                }

                Vector3 previous = (waypoints[index].position - waypoints[index - 1].position).normalized;
                Vector3 next = (waypoints[index + 1].position - waypoints[index].position).normalized;
                if (Vector3.Angle(previous, next) > 1f)
                {
                    turnCount++;
                }
            }

            return new RouteMetrics
            {
                Path = enemyPath,
                Length = totalLength,
                TurnCount = turnCount,
                Start = waypoints.Count > 0 && waypoints[0] != null ? waypoints[0].position : enemyPath.transform.position,
                End = waypoints.Count > 0 && waypoints[waypoints.Count - 1] != null ? waypoints[waypoints.Count - 1].position : enemyPath.transform.position
            };
        }

        private static float EstimateBuildZoneArea(BuildZone buildZone)
        {
            if (buildZone == null)
            {
                return 0f;
            }

            List<Collider2D> colliders = new List<Collider2D>();
            if (buildZone.ZoneShapeRoot != null)
            {
                colliders.AddRange(buildZone.ZoneShapeRoot.GetComponentsInChildren<Collider2D>(true));
            }

            if (colliders.Count == 0)
            {
                BoxCollider2D fallback = buildZone.GetComponent<BoxCollider2D>();
                if (fallback != null)
                {
                    colliders.Add(fallback);
                }
            }

            float totalArea = 0f;
            HashSet<Collider2D> uniqueColliders = new HashSet<Collider2D>(colliders.Where(collider => collider != null));
            foreach (Collider2D collider in uniqueColliders)
            {
                Bounds bounds = collider.bounds;
                totalArea += Mathf.Abs(bounds.size.x * bounds.size.y);
            }

            return totalArea;
        }

        private static int CountSharedRouteSpans(List<EnemyPath> enemyPaths)
        {
            List<OrthogonalSpan> spans = new List<OrthogonalSpan>();
            for (int pathIndex = 0; pathIndex < enemyPaths.Count; pathIndex++)
            {
                List<Transform> waypoints = EnemyPathAuthoringUtility.GetWaypointChildren(enemyPaths[pathIndex]);
                for (int index = 0; index < waypoints.Count - 1; index++)
                {
                    Transform start = waypoints[index];
                    Transform end = waypoints[index + 1];
                    if (start == null || end == null)
                    {
                        continue;
                    }

                    spans.Add(new OrthogonalSpan(enemyPaths[pathIndex], start.position, end.position));
                }
            }

            int mergeCount = 0;
            for (int index = 0; index < spans.Count; index++)
            {
                for (int compareIndex = index + 1; compareIndex < spans.Count; compareIndex++)
                {
                    OrthogonalSpan left = spans[index];
                    OrthogonalSpan right = spans[compareIndex];
                    if (left.OwnerPath == right.OwnerPath || left.IsHorizontal != right.IsHorizontal)
                    {
                        continue;
                    }

                    if (left.IsHorizontal)
                    {
                        if (Mathf.Abs(left.Start.y - right.Start.y) > 0.001f)
                        {
                            continue;
                        }

                        float overlap = CalculateAxisOverlap(left.Start.x, left.End.x, right.Start.x, right.End.x);
                        if (overlap > 0.5f)
                        {
                            mergeCount++;
                        }
                    }
                    else
                    {
                        if (Mathf.Abs(left.Start.x - right.Start.x) > 0.001f)
                        {
                            continue;
                        }

                        float overlap = CalculateAxisOverlap(left.Start.y, left.End.y, right.Start.y, right.End.y);
                        if (overlap > 0.5f)
                        {
                            mergeCount++;
                        }
                    }
                }
            }

            return mergeCount;
        }

        private static float CalculateAxisOverlap(float a0, float a1, float b0, float b1)
        {
            float left = Mathf.Max(Mathf.Min(a0, a1), Mathf.Min(b0, b1));
            float right = Mathf.Min(Mathf.Max(a0, a1), Mathf.Max(b0, b1));
            return Mathf.Max(0f, right - left);
        }

        private static WavePressureSummary BuildWavePressureSummary(BattlefieldMapDefinition mapDefinition, WaveSpawner waveSpawner)
        {
            WavePressureSummary summary = new WavePressureSummary();
            if (waveSpawner == null)
            {
                return summary;
            }

            if (waveSpawner.WaveCatalogAsset != null && waveSpawner.EnemyCatalogAsset != null && waveSpawner.WaveCatalogAsset.Waves.Length > 0)
            {
                int gateSequence = 0;
                WaveCatalogAsset.WaveEntry[] waves = waveSpawner.WaveCatalogAsset.Waves;
                summary.TotalWaves = waves.Length;
                for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
                {
                    WaveCatalogAsset.WaveEntry wave = waves[waveIndex];
                    int waveEnemies = 0;
                    int waveHealthBudget = 0;
                    int waveScrapBudget = 0;
                    Dictionary<string, int> gateBreakdown = new Dictionary<string, int>();

                    foreach (WaveCatalogAsset.SpawnGroup spawnGroup in wave.SpawnGroups)
                    {
                        if (spawnGroup == null)
                        {
                            continue;
                        }

                        if (!waveSpawner.EnemyCatalogAsset.TryGetDefinition(spawnGroup.EnemyType, out EnemyCatalogAsset.EnemyArchetypeDefinition definition) || definition == null)
                        {
                            continue;
                        }

                        for (int enemyIndex = 0; enemyIndex < spawnGroup.EnemyCount; enemyIndex++)
                        {
                            string gateName = ResolveGateName(mapDefinition, gateSequence);
                            gateBreakdown[gateName] = gateBreakdown.TryGetValue(gateName, out int existing) ? existing + 1 : 1;
                            summary.GateEnemyTotals[gateName] = summary.GateEnemyTotals.TryGetValue(gateName, out int totalExisting) ? totalExisting + 1 : 1;
                            gateSequence++;
                        }

                        waveEnemies += spawnGroup.EnemyCount;
                        waveHealthBudget += spawnGroup.EnemyCount * definition.MaxHealth;
                        waveScrapBudget += spawnGroup.EnemyCount * definition.ScrapReward;
                    }

                    summary.TotalEnemies += waveEnemies;
                    summary.TotalHealthBudget += waveHealthBudget;
                    summary.TotalScrapBudget += waveScrapBudget;
                    string gateBreakdownSummary = string.Join(" / ", gateBreakdown.Select(pair => $"{pair.Key}: {pair.Value}"));
                    summary.WaveBreakdowns.Add(
                        $"{ResolveWaveDisplayName(wave, waveIndex)} | Enemies: {waveEnemies} | Health Budget: {waveHealthBudget} | Gates: {gateBreakdownSummary}");
                }

                return summary;
            }

            SerializedObject serializedSpawner = new SerializedObject(waveSpawner);
            SerializedProperty wavesProperty = serializedSpawner.FindProperty("waves");
            if (wavesProperty == null || !wavesProperty.isArray)
            {
                return summary;
            }

            int fallbackGateSequence = 0;
            summary.TotalWaves = wavesProperty.arraySize;
            for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
            {
                SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
                int enemyCount = Mathf.Max(0, waveProperty.FindPropertyRelative("enemyCount")?.intValue ?? 0);
                int enemyHealth = Mathf.Max(0, waveProperty.FindPropertyRelative("enemyHealth")?.intValue ?? 0);
                int scrapReward = Mathf.Max(0, waveProperty.FindPropertyRelative("enemyScrapReward")?.intValue ?? 0);
                Dictionary<string, int> gateBreakdown = new Dictionary<string, int>();

                for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
                {
                    string gateName = ResolveGateName(mapDefinition, fallbackGateSequence);
                    gateBreakdown[gateName] = gateBreakdown.TryGetValue(gateName, out int existing) ? existing + 1 : 1;
                    summary.GateEnemyTotals[gateName] = summary.GateEnemyTotals.TryGetValue(gateName, out int totalExisting) ? totalExisting + 1 : 1;
                    fallbackGateSequence++;
                }

                summary.TotalEnemies += enemyCount;
                summary.TotalHealthBudget += enemyCount * enemyHealth;
                summary.TotalScrapBudget += enemyCount * scrapReward;
                string fallbackGateBreakdownSummary = string.Join(" / ", gateBreakdown.Select(pair => $"{pair.Key}: {pair.Value}"));
                summary.WaveBreakdowns.Add(
                    $"Wave {waveIndex + 1:D2} | Enemies: {enemyCount} | Health Budget: {enemyCount * enemyHealth} | Gates: {fallbackGateBreakdownSummary}");
            }

            return summary;
        }

        private static string ResolveWaveDisplayName(WaveCatalogAsset.WaveEntry wave, int waveIndex)
        {
            string displayName = wave != null ? wave.DisplayName : string.Empty;
            return string.IsNullOrWhiteSpace(displayName) ? $"Wave {waveIndex + 1:D2}" : displayName;
        }

        private static string ResolveGateName(BattlefieldMapDefinition mapDefinition, int gateSequence)
        {
            if (mapDefinition != null && mapDefinition.TryGetSpawnGateBySequence(gateSequence, out EnemySpawnGate spawnGate) && spawnGate != null)
            {
                return spawnGate.DisplayName;
            }

            return "FallbackPath";
        }

        private static float CalculateDifficultyScore(
            int spawnGateCount,
            int defensePointCount,
            float averageRouteLength,
            int totalTurnCount,
            int mergeCount,
            float buildZoneArea,
            int totalEnemies,
            int totalHealthBudget)
        {
            float score = 0f;
            score += spawnGateCount * 4f;
            score += defensePointCount * 7f;
            score += averageRouteLength * 0.35f;
            score += totalTurnCount * 1.8f;
            score += mergeCount * 2.8f;
            score += totalEnemies * 0.5f;
            score += totalHealthBudget * 0.06f;
            score -= Mathf.Min(buildZoneArea * 0.01f, 18f);
            return Mathf.Max(0f, score);
        }
    }
}
