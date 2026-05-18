using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TowerDefense.Editor
{
    /// <summary>
    /// Applies large route blueprints into authored map scenes.
    ///
    /// Why this tool exists:
    /// 1. Hand-editing long Unity YAML scene files is fragile once a route needs many waypoints.
    /// 2. The user wants route complexity to grow in big steps, not by tiny manual nudges.
    /// 3. Scene-authored maps still remain the source of truth, so we generate the scene layout
    ///    directly instead of hiding the result behind runtime code.
    ///
    /// The tool deliberately edits only scene-authored map structure:
    /// - spawn gates
    /// - defense points
    /// - enemy path roots and waypoints
    /// - visible road strips
    /// - build zone size
    /// - camera view size
    ///
    /// It does not touch wave data, tower balance, or HUD wiring.
    /// </summary>
    public static class LevelRouteBlueprintApplier
    {
        private const string Level03ScenePath = "Assets/Scenes/Level03.unity";
        private const string Level04ScenePath = "Assets/Scenes/Level04.unity";
        private const string Level06ScenePath = "Assets/Scenes/Level06.unity";
        private const string AutoRunMarkerPath = "Temp/level_route_blueprint_autorun.txt";
        private const int AutoRunMaxUpdateChecks = 240;

        private const string BattlefieldMapName = "BattlefieldMap";
        private const string BuildZoneName = "BuildZone";
        private const string PathVisualsName = "PathVisuals";
        private const string MainCameraName = "Main Camera";
        private static int autoRunUpdateChecks;

        /// <summary>
        /// A minimal route description for one authored EnemyPath.
        ///
        /// We keep the blueprint data intentionally compact:
        /// a name, one gate, one target defense point, and a waypoint list.
        /// Road strips are derived from the waypoint spans so the scene stays consistent.
        /// </summary>
        private sealed class RouteBlueprint
        {
            public string GateObjectName;
            public string GateId;
            public string GateDisplayName;
            public string PathObjectName;
            public string[] WaypointNames;
            public Vector2[] Waypoints;
            public string DefensePointObjectName;
        }

        /// <summary>
        /// A minimal defense point description.
        /// </summary>
        private sealed class DefenseBlueprint
        {
            public string ObjectName;
            public string PointId;
            public string DisplayName;
            public Vector2 Position;
        }

        /// <summary>
        /// Lets the already-open Unity editor apply the blueprint automatically once.
        ///
        /// Why this exists:
        /// - batchmode can become flaky on large local editor setups
        /// - the user often already has the project open in Unity while reviewing maps
        /// - a marker-file driven one-shot keeps the behavior explicit and reversible
        ///
        /// We only run when the marker file exists, then delete it immediately after the attempt.
        /// That prevents accidental reapplication on every domain reload.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void TryAutoRunPendingBlueprint()
        {
            ScheduleAutoRunMarkerCheck();
        }

        [DidReloadScripts]
        private static void TryAutoRunPendingBlueprintAfterReload()
        {
            ScheduleAutoRunMarkerCheck();
        }

        private static void ScheduleAutoRunMarkerCheck()
        {
            autoRunUpdateChecks = 0;
            EditorApplication.update -= TryConsumeAutoRunMarkerOnEditorUpdate;
            EditorApplication.update += TryConsumeAutoRunMarkerOnEditorUpdate;
            EditorApplication.delayCall -= TryConsumeAutoRunMarkerOnEditorUpdate;
            EditorApplication.delayCall += TryConsumeAutoRunMarkerOnEditorUpdate;
        }

        private static void TryConsumeAutoRunMarkerOnEditorUpdate()
        {
            string markerPath = GetAutoRunMarkerPath();
            if (File.Exists(markerPath))
            {
                EditorApplication.update -= TryConsumeAutoRunMarkerOnEditorUpdate;
                ConsumeAutoRunMarker(markerPath);
                return;
            }

            autoRunUpdateChecks++;
            if (autoRunUpdateChecks >= AutoRunMaxUpdateChecks)
            {
                EditorApplication.update -= TryConsumeAutoRunMarkerOnEditorUpdate;
            }
        }

        private static void ConsumeAutoRunMarker(string markerPath)
        {
            try
            {
                string marker = File.ReadAllText(markerPath).Trim();
                if (string.Equals(marker, "Level06", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyLevel06BlueprintBatch();
                }
                else
                {
                    ApplyLevel03AndLevel04BlueprintsBatch();
                }
                Debug.Log("[LevelRouteBlueprintApplier] Auto-run marker consumed successfully.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LevelRouteBlueprintApplier] Auto-run failed: {exception}");
            }
            finally
            {
                if (File.Exists(markerPath))
                {
                    File.Delete(markerPath);
                }
            }
        }

        private static string GetAutoRunMarkerPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, AutoRunMarkerPath);
        }

        /// <summary>
        /// Batch entry used by `-executeMethod`.
        ///
        /// This lets us update both authored scenes in one Unity batch run while keeping the code
        /// reusable from the normal editor menu.
        /// </summary>
        public static void ApplyLevel03AndLevel04BlueprintsBatch()
        {
            ApplyLevel03AdvancedBlueprint();
            ApplyLevel04ExpandedBlueprint();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// One-shot batch entry for the current level-polish workflow:
        /// rebuild the authored route blueprints, then regenerate decorative road art.
        /// </summary>
        public static void ApplyAndPolishLevel03AndLevel04Batch()
        {
            ApplyLevel03AndLevel04BlueprintsBatch();
            RoadArtAuthoringWindow.GenerateLevel03AndLevel04RoadArtBatch();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Batch entry for the user's current request:
        /// rebuild only Level04 into the larger four-gate, two-defense-point layout.
        /// </summary>
        public static void ApplyLevel04BlueprintBatch()
        {
            ApplyLevel04ExpandedBlueprint();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Tower Defense/Authoring/Apply Level06 Chalkboard Blueprint")]
        public static void ApplyLevel06ChalkboardBlueprintMenu()
        {
            ApplyLevel06ChalkboardBlueprint();
        }

        public static void ApplyLevel06BlueprintBatch()
        {
            ApplyLevel06ChalkboardBlueprint();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ApplyLevel03AdvancedBlueprint()
        {
            Scene scene = EditorSceneManager.OpenScene(Level03ScenePath, OpenSceneMode.Single);
            using (BattlefieldAuthoringGuard.BeginReadabilitySuppressionScope())
            {
                BattlefieldMapDefinition map = FindRequiredComponent<BattlefieldMapDefinition>(scene, BattlefieldMapName);
                BuildZone buildZone = FindRequiredComponent<BuildZone>(scene, BuildZoneName);
                Transform pathVisualsRoot = FindRequiredTransform(scene, PathVisualsName);
                Camera mainCamera = FindRequiredComponent<Camera>(scene, MainCameraName);

                // Level03 should only step slightly beyond Level02:
                // - keep three spawn gates
                // - keep one defense point
                // - add a modest third lane instead of exploding route complexity
                buildZone.transform.localScale = new Vector3(52f, 32f, 1f);
                mainCamera.orthographicSize = 10.8f;

                RouteBlueprint[] routes =
                {
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_Main",
                        GateId = "Gate_A",
                        GateDisplayName = "Upper Main Spawn Gate",
                        PathObjectName = "EnemyPath",
                        DefensePointObjectName = "DefensePoint_Core",
                        WaypointNames = BuildWaypointNames("Waypoint_", 7),
                        Waypoints = new[]
                        {
                            new Vector2(-20f, 8f),
                            new Vector2(-12f, 8f),
                            new Vector2(-12f, 3f),
                            new Vector2(2f, 3f),
                            new Vector2(2f, -1f),
                            new Vector2(16f, -1f),
                            new Vector2(24f, -6f)
                        }
                    },
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_Alt",
                        GateId = "Gate_B",
                        GateDisplayName = "Lower Backstreet Spawn Gate",
                        PathObjectName = "EnemyPath_B",
                        DefensePointObjectName = "DefensePoint_Core",
                        WaypointNames = BuildWaypointNames("Waypoint_B", 6),
                        Waypoints = new[]
                        {
                            new Vector2(-20f, -8f),
                            new Vector2(-20f, -3f),
                            new Vector2(-6f, -3f),
                            new Vector2(-6f, -6f),
                            new Vector2(10f, -6f),
                            new Vector2(24f, -6f)
                        }
                    },
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_Mid",
                        GateId = "Gate_C",
                        GateDisplayName = "Upper Mid Fast Spawn Gate",
                        PathObjectName = "EnemyPath_C",
                        DefensePointObjectName = "DefensePoint_Core",
                        WaypointNames = BuildWaypointNames("Waypoint_C", 6),
                        Waypoints = new[]
                        {
                            new Vector2(-8f, 10f),
                            new Vector2(-8f, 4f),
                            new Vector2(8f, 4f),
                            new Vector2(8f, -2f),
                            new Vector2(20f, -2f),
                            new Vector2(24f, -6f)
                        }
                    }
                };

                DefenseBlueprint[] defenses =
                {
                    new DefenseBlueprint
                    {
                        ObjectName = "DefensePoint_Core",
                        PointId = "Core",
                        DisplayName = "Core Defense Point",
                        Position = new Vector2(24f, -6f)
                    }
                };

                ApplyBlueprintIntoScene(
                    scene,
                    map,
                    buildZone,
                    pathVisualsRoot,
                    routes,
                    defenses,
                    relayLimit: 6);
            }

            RefreshSceneAuthoringVisuals(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ApplyLevel04ExpandedBlueprint()
        {
            Scene scene = EditorSceneManager.OpenScene(Level04ScenePath, OpenSceneMode.Single);
            using (BattlefieldAuthoringGuard.BeginReadabilitySuppressionScope())
            {
                BattlefieldMapDefinition map = FindRequiredComponent<BattlefieldMapDefinition>(scene, BattlefieldMapName);
                BuildZone buildZone = FindRequiredComponent<BuildZone>(scene, BuildZoneName);
                Transform pathVisualsRoot = FindRequiredTransform(scene, PathVisualsName);
                Camera mainCamera = FindRequiredComponent<Camera>(scene, MainCameraName);

                // Level04 should be the next step after Level03, not a giant difficulty spike:
                // - keep four spawn gates and two defense points
                // - simplify the route bends
                // - keep the map only modestly larger than Level03
                buildZone.transform.localScale = new Vector3(68f, 40f, 1f);
                mainCamera.orthographicSize = 13.2f;

                RouteBlueprint[] routes =
                {
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_Main",
                        GateId = "Gate_A",
                        GateDisplayName = "North Outer Spawn Gate",
                        PathObjectName = "EnemyPath",
                        DefensePointObjectName = "DefensePoint_Alpha",
                        WaypointNames = BuildWaypointNames("Waypoint_", 7),
                        Waypoints = new[]
                        {
                            new Vector2(-28f, 12f),
                            new Vector2(-16f, 12f),
                            new Vector2(-16f, 6f),
                            new Vector2(0f, 6f),
                            new Vector2(0f, 10f),
                            new Vector2(18f, 10f),
                            new Vector2(28f, 10f)
                        }
                    },
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_Mid",
                        GateId = "Gate_B",
                        GateDisplayName = "Upper Exchange Spawn Gate",
                        PathObjectName = "EnemyPath_C",
                        DefensePointObjectName = "DefensePoint_Alpha",
                        WaypointNames = BuildWaypointNames("Waypoint_C", 5),
                        Waypoints = new[]
                        {
                            new Vector2(-6f, 14f),
                            new Vector2(-6f, 8f),
                            new Vector2(10f, 8f),
                            new Vector2(10f, 10f),
                            new Vector2(28f, 10f)
                        }
                    },
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_Alt",
                        GateId = "Gate_C",
                        GateDisplayName = "South Outer Spawn Gate",
                        PathObjectName = "EnemyPath_B",
                        DefensePointObjectName = "DefensePoint_Beta",
                        WaypointNames = BuildWaypointNames("Waypoint_B", 7),
                        Waypoints = new[]
                        {
                            new Vector2(-28f, -12f),
                            new Vector2(-16f, -12f),
                            new Vector2(-16f, -6f),
                            new Vector2(0f, -6f),
                            new Vector2(0f, -10f),
                            new Vector2(18f, -10f),
                            new Vector2(28f, -10f)
                        }
                    },
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_LowerMid",
                        GateId = "Gate_D",
                        GateDisplayName = "Lower Exchange Spawn Gate",
                        PathObjectName = "EnemyPath_D",
                        DefensePointObjectName = "DefensePoint_Beta",
                        WaypointNames = BuildWaypointNames("Waypoint_D", 5),
                        Waypoints = new[]
                        {
                            new Vector2(-6f, -14f),
                            new Vector2(-6f, -8f),
                            new Vector2(10f, -8f),
                            new Vector2(10f, -10f),
                            new Vector2(28f, -10f)
                        }
                    }
                };

                DefenseBlueprint[] defenses =
                {
                    new DefenseBlueprint
                    {
                        ObjectName = "DefensePoint_Alpha",
                        PointId = "Alpha",
                        DisplayName = "Upper Core",
                        Position = new Vector2(28f, 10f)
                    },
                    new DefenseBlueprint
                    {
                        ObjectName = "DefensePoint_Beta",
                        PointId = "Beta",
                        DisplayName = "Lower Core",
                        Position = new Vector2(28f, -10f)
                    }
                };

                ApplyBlueprintIntoScene(
                    scene,
                    map,
                    buildZone,
                    pathVisualsRoot,
                    routes,
                    defenses,
                    relayLimit: 7);
            }

            RefreshSceneAuthoringVisuals(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ApplyLevel06ChalkboardBlueprint()
        {
            Scene scene = EditorSceneManager.OpenScene(Level06ScenePath, OpenSceneMode.Single);
            using (BattlefieldAuthoringGuard.BeginReadabilitySuppressionScope())
            {
                BattlefieldMapDefinition map = FindRequiredComponent<BattlefieldMapDefinition>(scene, BattlefieldMapName);
                BuildZone buildZone = FindRequiredComponent<BuildZone>(scene, BuildZoneName);
                Transform pathVisualsRoot = FindRequiredTransform(scene, PathVisualsName);
                Camera mainCamera = FindRequiredComponent<Camera>(scene, MainCameraName);

                // Level06 follows the user's chalkboard sketch: an upper-left attack
                // gate drops into the upper corridor, a lower-left attack gate climbs
                // through the lower corridor, and both routes merge before the right-side home.
                // The point layout intentionally follows the chalkboard drawing more than a grid.
                buildZone.transform.localScale = new Vector3(20.5f, 12f, 1f);
                mainCamera.orthographicSize = 6.9f;

                RouteBlueprint[] routes =
                {
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_Main",
                        GateId = "Gate_A",
                        GateDisplayName = "Upper Chalk Attack Gate",
                        PathObjectName = "EnemyPath",
                        DefensePointObjectName = "DefensePoint_Core",
                        WaypointNames = BuildWaypointNames("Waypoint_", 12),
                        Waypoints = new[]
                        {
                            new Vector2(-7.8f, 3.45f),
                            new Vector2(-7.8f, 2.55f),
                            new Vector2(-7.55f, 1.75f),
                            new Vector2(-6.75f, 1.25f),
                            new Vector2(-4.1f, 1.35f),
                            new Vector2(-1.2f, 1.4f),
                            new Vector2(2.2f, 1.38f),
                            new Vector2(4.15f, 1.18f),
                            new Vector2(5.75f, -0.78f),
                            new Vector2(6.65f, -1.1f),
                            new Vector2(7.75f, -1.1f),
                            new Vector2(8.9f, -1.1f)
                        }
                    },
                    new RouteBlueprint
                    {
                        GateObjectName = "SpawnGate_Alt",
                        GateId = "Gate_B",
                        GateDisplayName = "Lower Chalk Attack Gate",
                        PathObjectName = "EnemyPath_B",
                        DefensePointObjectName = "DefensePoint_Core",
                        WaypointNames = BuildWaypointNames("Waypoint_B", 13),
                        Waypoints = new[]
                        {
                            new Vector2(-8.9f, -2.9f),
                            new Vector2(-7.35f, -2.9f),
                            new Vector2(-6.5f, -2.55f),
                            new Vector2(-5.85f, -1.85f),
                            new Vector2(-5.25f, -1.2f),
                            new Vector2(-4.15f, -0.82f),
                            new Vector2(-2.45f, -0.7f),
                            new Vector2(-0.2f, -0.72f),
                            new Vector2(2.1f, -0.7f),
                            new Vector2(3.95f, -0.78f),
                            new Vector2(5.75f, -0.78f),
                            new Vector2(6.65f, -1.1f),
                            new Vector2(7.75f, -1.1f),
                            new Vector2(8.9f, -1.1f)
                        }
                    }
                };

                DefenseBlueprint[] defenses =
                {
                    new DefenseBlueprint
                    {
                        ObjectName = "DefensePoint_Core",
                        PointId = "Home",
                        DisplayName = "Home",
                        Position = new Vector2(8.9f, -1.1f)
                    }
                };

                ApplyBlueprintIntoScene(
                    scene,
                    map,
                    buildZone,
                    pathVisualsRoot,
                    routes,
                    defenses,
                    relayLimit: 5);
            }

            RefreshSceneAuthoringVisuals(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Applies one whole scene blueprint.
        ///
        /// This method deliberately works in a top-down order:
        /// 1. defense points
        /// 2. enemy paths
        /// 3. spawn gates
        /// 4. road strips
        /// 5. map reference collection
        ///
        /// That order keeps cross-references easy to wire because each later step can safely
        /// look up the scene objects created or renamed by the previous step.
        /// </summary>
        private static void ApplyBlueprintIntoScene(
            Scene scene,
            BattlefieldMapDefinition map,
            BuildZone buildZone,
            Transform pathVisualsRoot,
            IReadOnlyList<RouteBlueprint> routes,
            IReadOnlyList<DefenseBlueprint> defenses,
            int relayLimit)
        {
            Transform mapRoot = map.transform;

            Dictionary<string, DefensePointFlag> defenseByName = new Dictionary<string, DefensePointFlag>();
            DefensePointFlag defenseTemplate = FindFirstComponentInScene<DefensePointFlag>(scene);
            foreach (DefenseBlueprint defenseBlueprint in defenses)
            {
                DefensePointFlag defensePoint = GetOrDuplicateDefensePoint(defenseTemplate, mapRoot, defenseBlueprint.ObjectName);
                ConfigureDefensePoint(defensePoint, defenseBlueprint);
                defenseByName[defenseBlueprint.ObjectName] = defensePoint;
            }

            EnemyPath pathTemplate = FindFirstComponentInScene<EnemyPath>(scene);
            Dictionary<string, EnemyPath> pathByName = new Dictionary<string, EnemyPath>();
            foreach (RouteBlueprint route in routes)
            {
                EnemyPath enemyPath = GetOrDuplicateEnemyPath(pathTemplate, mapRoot, route.PathObjectName);
                ConfigureEnemyPath(enemyPath, route.WaypointNames, route.Waypoints);
                pathByName[route.PathObjectName] = enemyPath;
            }

            EnemySpawnGate gateTemplate = FindFirstComponentInScene<EnemySpawnGate>(scene);
            Dictionary<string, EnemySpawnGate> gateByName = new Dictionary<string, EnemySpawnGate>();
            foreach (RouteBlueprint route in routes)
            {
                EnemySpawnGate spawnGate = GetOrDuplicateSpawnGate(gateTemplate, mapRoot, route.GateObjectName);
                ConfigureSpawnGate(
                    spawnGate,
                    route,
                    pathByName[route.PathObjectName],
                    defenseByName[route.DefensePointObjectName]);
                gateByName[route.GateObjectName] = spawnGate;
            }

            CleanupExtraComponents(mapRoot, routes, defenses);
            RebuildRoadSegmentsFromRoutes(pathVisualsRoot, routes);
            ApplyMapReferences(map, buildZone, routes, defenses, gateByName, defenseByName, relayLimit);
            RewireSharedSceneControllers(scene, map, routes, pathByName);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(buildZone);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// Removes outdated authored objects that are no longer part of the active blueprint.
        ///
        /// This is important when a late-stage map redesign changes the number of
        /// gates / paths / defense points. Without cleanup, the scene can technically run,
        /// but it stops matching the intended difficulty contract and becomes confusing to edit.
        /// </summary>
        private static void CleanupExtraComponents(
            Transform mapRoot,
            IReadOnlyList<RouteBlueprint> routes,
            IReadOnlyList<DefenseBlueprint> defenses)
        {
            HashSet<string> allowedGateNames = new HashSet<string>(routes.Select(route => route.GateObjectName));
            HashSet<string> allowedPathNames = new HashSet<string>(routes.Select(route => route.PathObjectName));
            HashSet<string> allowedDefenseNames = new HashSet<string>(defenses.Select(defense => defense.ObjectName));

            List<EnemySpawnGate> extraGates = mapRoot.GetComponentsInChildren<EnemySpawnGate>(true)
                .Where(gate => gate != null && !allowedGateNames.Contains(gate.name))
                .ToList();
            foreach (EnemySpawnGate extraGate in extraGates)
            {
                Object.DestroyImmediate(extraGate.gameObject);
            }

            List<EnemyPath> extraPaths = mapRoot.GetComponentsInChildren<EnemyPath>(true)
                .Where(path => path != null && !allowedPathNames.Contains(path.name))
                .ToList();
            foreach (EnemyPath extraPath in extraPaths)
            {
                Object.DestroyImmediate(extraPath.gameObject);
            }

            List<DefensePointFlag> extraDefensePoints = mapRoot.GetComponentsInChildren<DefensePointFlag>(true)
                .Where(point => point != null && !allowedDefenseNames.Contains(point.name))
                .ToList();
            foreach (DefensePointFlag extraDefensePoint in extraDefensePoints)
            {
                Object.DestroyImmediate(extraDefensePoint.gameObject);
            }
        }

        /// <summary>
        /// Rebuilds readability overlays once the structural scene edit is complete.
        ///
        /// During the bulk edit we intentionally suppress `OnValidate`-driven helper generation.
        /// That keeps the scene stable while objects are being created, renamed, and rewired.
        /// Once the structure is final, we explicitly refresh every author-facing map marker once.
        /// </summary>
        private static void RefreshSceneAuthoringVisuals(Scene scene)
        {
            foreach (EnemyPath enemyPath in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyPath>(true)))
            {
                enemyPath.EditorRefreshAuthoringState();
                EditorUtility.SetDirty(enemyPath);
            }

            foreach (EnemySpawnGate spawnGate in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemySpawnGate>(true)))
            {
                spawnGate.EditorRefreshAuthoringState();
                EditorUtility.SetDirty(spawnGate);
            }

            foreach (DefensePointFlag defensePoint in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DefensePointFlag>(true)))
            {
                defensePoint.EditorRefreshAuthoringState();
                EditorUtility.SetDirty(defensePoint);
            }
        }

        /// <summary>
        /// Writes the authoritative map arrays in blueprint order instead of relying on incidental
        /// hierarchy enumeration order.
        /// </summary>
        private static void ApplyMapReferences(
            BattlefieldMapDefinition map,
            BuildZone buildZone,
            IReadOnlyList<RouteBlueprint> routes,
            IReadOnlyList<DefenseBlueprint> defenses,
            IReadOnlyDictionary<string, EnemySpawnGate> gateByName,
            IReadOnlyDictionary<string, DefensePointFlag> defenseByName,
            int relayLimit)
        {
            SerializedObject serializedMap = new SerializedObject(map);
            SetObjectProperty(serializedMap, "buildZoneReference", buildZone);

            SerializedProperty spawnGatesProperty = serializedMap.FindProperty("spawnGates");
            if (spawnGatesProperty != null)
            {
                spawnGatesProperty.arraySize = routes.Count;
                for (int index = 0; index < routes.Count; index++)
                {
                    spawnGatesProperty.GetArrayElementAtIndex(index).objectReferenceValue = gateByName[ routes[index].GateObjectName ];
                }
            }

            SerializedProperty defensePointsProperty = serializedMap.FindProperty("defensePoints");
            if (defensePointsProperty != null)
            {
                defensePointsProperty.arraySize = defenses.Count;
                for (int index = 0; index < defenses.Count; index++)
                {
                    defensePointsProperty.GetArrayElementAtIndex(index).objectReferenceValue = defenseByName[ defenses[index].ObjectName ];
                }
            }

            SerializedProperty relayLimitProperty = serializedMap.FindProperty("relayLimit");
            if (relayLimitProperty != null)
            {
                relayLimitProperty.intValue = relayLimit;
            }

            serializedMap.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Rebinds the shared controllers that still keep one scene-level fallback path reference.
        ///
        /// We keep the fallback path aligned to the first authored route so older systems do not
        /// silently point at stale duplicate path roots after a major topology rewrite.
        /// </summary>
        private static void RewireSharedSceneControllers(
            Scene scene,
            BattlefieldMapDefinition map,
            IReadOnlyList<RouteBlueprint> routes,
            IReadOnlyDictionary<string, EnemyPath> pathByName)
        {
            if (routes.Count == 0)
            {
                return;
            }

            EnemyPath primaryPath = pathByName[routes[0].PathObjectName];

            WaveSpawner waveSpawner = FindFirstComponentInScene<WaveSpawner>(scene);
            if (waveSpawner != null)
            {
                SerializedObject serializedWaveSpawner = new SerializedObject(waveSpawner);
                SetObjectProperty(serializedWaveSpawner, "battlefieldMapReference", map);
                SetObjectProperty(serializedWaveSpawner, "enemyPathReference", primaryPath);
                serializedWaveSpawner.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(waveSpawner);
            }

            TowerDefenseGame towerDefenseGame = FindFirstComponentInScene<TowerDefenseGame>(scene);
            if (towerDefenseGame != null)
            {
                SerializedObject serializedGame = new SerializedObject(towerDefenseGame);
                SetObjectProperty(serializedGame, "battlefieldMapReference", map);
                serializedGame.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(towerDefenseGame);
            }
        }

        private static void ConfigureDefensePoint(DefensePointFlag defensePoint, DefenseBlueprint blueprint)
        {
            defensePoint.name = blueprint.ObjectName;
            defensePoint.transform.SetParent(defensePoint.transform.parent, false);
            defensePoint.transform.localPosition = new Vector3(blueprint.Position.x, blueprint.Position.y, 0f);
            defensePoint.transform.localRotation = Quaternion.identity;
            defensePoint.transform.localScale = Vector3.one;

            SerializedObject serializedDefense = new SerializedObject(defensePoint);
            SetStringProperty(serializedDefense, "pointId", blueprint.PointId);
            SetStringProperty(serializedDefense, "displayName", blueprint.DisplayName);
            serializedDefense.ApplyModifiedPropertiesWithoutUndo();

            defensePoint.EditorRefreshAuthoringState();
            EditorUtility.SetDirty(defensePoint);
        }

        private static void ConfigureSpawnGate(
            EnemySpawnGate spawnGate,
            RouteBlueprint route,
            EnemyPath path,
            DefensePointFlag defensePoint)
        {
            spawnGate.name = route.GateObjectName;
            spawnGate.transform.localPosition = new Vector3(route.Waypoints[0].x, route.Waypoints[0].y, 0f);
            spawnGate.transform.localRotation = Quaternion.identity;
            spawnGate.transform.localScale = Vector3.one;

            SerializedObject serializedGate = new SerializedObject(spawnGate);
            SetStringProperty(serializedGate, "gateId", route.GateId);
            SetStringProperty(serializedGate, "displayName", route.GateDisplayName);
            SetObjectProperty(serializedGate, "enemyPathReference", path);
            SetObjectProperty(serializedGate, "targetDefensePointReference", defensePoint);
            serializedGate.ApplyModifiedPropertiesWithoutUndo();

            spawnGate.EditorRefreshAuthoringState();
            EditorUtility.SetDirty(spawnGate);
        }

        private static void ConfigureEnemyPath(EnemyPath enemyPath, IReadOnlyList<string> waypointNames, IReadOnlyList<Vector2> waypoints)
        {
            enemyPath.transform.localPosition = Vector3.zero;
            enemyPath.transform.localRotation = Quaternion.identity;
            enemyPath.transform.localScale = Vector3.one;

            List<Transform> childrenToDelete = enemyPath.transform.Cast<Transform>()
                .Where(child => child != null && !child.name.StartsWith("__", StringComparison.Ordinal))
                .ToList();
            foreach (Transform child in childrenToDelete)
            {
                Object.DestroyImmediate(child.gameObject);
            }

            SerializedObject serializedPath = new SerializedObject(enemyPath);
            SerializedProperty waypointRootProperty = serializedPath.FindProperty("waypointRootReference");
            if (waypointRootProperty != null)
            {
                waypointRootProperty.objectReferenceValue = null;
                serializedPath.ApplyModifiedPropertiesWithoutUndo();
            }

            for (int index = 0; index < waypoints.Count; index++)
            {
                GameObject waypointObject = new GameObject(waypointNames[index]);
                waypointObject.transform.SetParent(enemyPath.transform, false);
                waypointObject.transform.localPosition = new Vector3(waypoints[index].x, waypoints[index].y, 0f);
                waypointObject.transform.localRotation = Quaternion.identity;
                waypointObject.transform.localScale = Vector3.one;
            }

            enemyPath.EditorRefreshAuthoringState();
            EditorUtility.SetDirty(enemyPath);
        }

        private static void RebuildRoadSegmentsFromRoutes(Transform pathVisualsRoot, IReadOnlyList<RouteBlueprint> routes)
        {
            GameObject template = BuildRoadTemplate(pathVisualsRoot);

            List<GameObject> existingSegments = pathVisualsRoot.Cast<Transform>()
                .Where(child => child != null && child.name.StartsWith("PathSegment_", StringComparison.Ordinal))
                .Select(child => child.gameObject)
                .ToList();
            foreach (GameObject existingSegment in existingSegments)
            {
                Object.DestroyImmediate(existingSegment);
            }

            int segmentCounter = 1;
            HashSet<string> createdSegmentKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (RouteBlueprint route in routes)
            {
                for (int index = 0; index < route.Waypoints.Length - 1; index++)
                {
                    Vector2 start = route.Waypoints[index];
                    Vector2 end = route.Waypoints[index + 1];
                    string segmentKey = BuildSegmentKey(start, end);
                    if (!createdSegmentKeys.Add(segmentKey))
                    {
                        continue;
                    }

                    CreateRoadSegment(pathVisualsRoot, template, $"PathSegment_{route.GateId}_{segmentCounter:D2}", start, end);
                    segmentCounter++;
                }
            }

            Object.DestroyImmediate(template);
        }

        private static string BuildSegmentKey(Vector2 start, Vector2 end)
        {
            string first = BuildPointKey(start);
            string second = BuildPointKey(end);
            return string.CompareOrdinal(first, second) <= 0
                ? $"{first}|{second}"
                : $"{second}|{first}";
        }

        private static string BuildPointKey(Vector2 point)
        {
            return $"{Mathf.RoundToInt(point.x * 100f)}:{Mathf.RoundToInt(point.y * 100f)}";
        }

        private static GameObject BuildRoadTemplate(Transform pathVisualsRoot)
        {
            GameObject existingTemplate = pathVisualsRoot.Cast<Transform>()
                .Where(child => child != null && child.name.StartsWith("PathSegment_", StringComparison.Ordinal))
                .Select(child => child.gameObject)
                .FirstOrDefault();
            if (existingTemplate == null)
            {
                throw new InvalidOperationException("No existing PathSegment_ template was found in the scene.");
            }

            GameObject template = Object.Instantiate(existingTemplate);
            template.hideFlags = HideFlags.HideAndDontSave;
            return template;
        }

        private static void CreateRoadSegment(Transform parent, GameObject template, string objectName, Vector2 start, Vector2 end)
        {
            GameObject segmentObject = Object.Instantiate(template, parent);
            segmentObject.hideFlags = HideFlags.None;
            segmentObject.name = objectName;

            float thickness = 1.8f;
            Vector2 direction = end - start;
            float length = direction.magnitude;
            if (length <= 0.001f)
            {
                Object.DestroyImmediate(segmentObject);
                return;
            }

            segmentObject.transform.localPosition = new Vector3((start.x + end.x) * 0.5f, (start.y + end.y) * 0.5f, 0f);
            segmentObject.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            segmentObject.transform.localScale = new Vector3(length + thickness, thickness, 1f);
        }

        private static EnemyPath GetOrDuplicateEnemyPath(EnemyPath template, Transform parent, string objectName)
        {
            return FindOrCreateUniqueComponent(template, parent, objectName);
        }

        private static EnemySpawnGate GetOrDuplicateSpawnGate(EnemySpawnGate template, Transform parent, string objectName)
        {
            return FindOrCreateUniqueComponent(template, parent, objectName);
        }

        private static DefensePointFlag GetOrDuplicateDefensePoint(DefensePointFlag template, Transform parent, string objectName)
        {
            return FindOrCreateUniqueComponent(template, parent, objectName);
        }

        /// <summary>
        /// Finds one authored object by name, destroys same-name duplicates, or duplicates a
        /// template if the scene does not yet contain the requested object.
        ///
        /// Same-name duplicate cleanup is a key part of making blueprint application converge
        /// reliably. Without it, repeated redesign passes can leave stale components behind while
        /// still "looking" correct at a glance because one of the duplicates happens to be wired.
        /// </summary>
        private static T FindOrCreateUniqueComponent<T>(T template, Transform parent, string objectName) where T : Component
        {
            List<T> matchingComponents = parent.GetComponentsInChildren<T>(true)
                .Where(component => component != null && component.name == objectName)
                .ToList();

            if (matchingComponents.Count > 0)
            {
                T retained = matchingComponents[0];
                for (int index = 1; index < matchingComponents.Count; index++)
                {
                    Object.DestroyImmediate(matchingComponents[index].gameObject);
                }

                return retained;
            }

            if (template == null)
            {
                throw new InvalidOperationException(
                    $"Cannot create '{objectName}' because no template of type '{typeof(T).Name}' exists in the scene.");
            }

            T duplicated = Object.Instantiate(template, parent);
            duplicated.name = objectName;
            return duplicated;
        }

        private static T FindRequiredComponent<T>(Scene scene, string objectName) where T : Component
        {
            T component = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.name == objectName);
            if (component == null)
            {
                throw new InvalidOperationException($"Required component '{typeof(T).Name}' on object '{objectName}' was not found in scene '{scene.path}'.");
            }

            return component;
        }

        private static Transform FindRequiredTransform(Scene scene, string objectName)
        {
            Transform transform = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate != null && candidate.name == objectName);
            if (transform == null)
            {
                throw new InvalidOperationException($"Required object '{objectName}' was not found in scene '{scene.path}'.");
            }

            return transform;
        }

        private static T FindFirstComponentInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(candidate => candidate != null);
        }

        private static void SetStringProperty(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetObjectProperty(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static string[] BuildWaypointNames(string prefix, int count)
        {
            string safePrefix = prefix ?? "Waypoint_";
            string[] results = new string[count];
            for (int index = 0; index < count; index++)
            {
                if (safePrefix.EndsWith("_", StringComparison.Ordinal))
                {
                    results[index] = $"{safePrefix}{index + 1:D2}";
                }
                else
                {
                    results[index] = $"{safePrefix}{index + 1:D2}";
                }
            }

            return results;
        }
    }
}
