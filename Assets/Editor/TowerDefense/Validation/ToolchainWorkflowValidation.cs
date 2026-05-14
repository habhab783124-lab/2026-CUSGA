using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Editor
{
    /// <summary>
    /// End-to-end validation for the current map-authoring toolchain.
    ///
    /// Why this validator exists:
    /// - The toolchain has grown beyond "edit an existing map" and now claims to support
    ///   the full workflow from a fresh scene.
    /// - That claim must be verified against one concrete path, not just inferred from code.
    /// - The validator deliberately drives authoring through tool methods instead of manually
    ///   wiring gameplay objects, so any failure exposes a real workflow gap.
    ///
    /// Scope of the workflow under test:
    /// 1. Create a brand-new scene.
    /// 2. Bootstrap the combat shell from SampleScene.
    /// 3. Create one defense point, one spawn gate, one enemy path through topology tools.
    /// 4. Create path waypoints through the path-authoring tool helpers.
    /// 5. Create one buildable rectangle through the zone-brush helper.
    /// 6. Generate functional road strips through the map toolkit.
    /// 7. Re-sync shared references and probe runtime readiness.
    ///
    /// This gives us a grounded answer to:
    /// "Can the current tools really produce a playable new level without dropping back into
    /// manual component setup?"
    /// </summary>
    public static class ToolchainWorkflowValidation
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ValidationScenePath = "Assets/Scenes/ToolchainWorkflowValidationScene.unity";
        private const string ReportFileName = "toolchain-workflow-validation-report.txt";
        private const string AutoRunMarkerPath = "Temp/toolchain-workflow-validation-autorun.txt";

        private static readonly StringBuilder Report = new StringBuilder();

        [InitializeOnLoadMethod]
        private static void TryAutoRunPendingValidation()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(AutoRunMarkerPath))
                {
                    return;
                }

                try
                {
                    Run();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ToolchainValidation] Auto-run failed: {exception}");
                }
                finally
                {
                    if (File.Exists(AutoRunMarkerPath))
                    {
                        File.Delete(AutoRunMarkerPath);
                    }
                }
            };
        }

        public static void Run()
        {
            Report.Clear();
            AppendLine("[ToolchainValidation] Starting end-to-end toolchain validation.");

            try
            {
                Scene validationScene = CreateFreshValidationScene();
                AppendLine("[ToolchainValidation] Step 1: created a fresh empty validation scene.");

                BootstrapSceneShell(validationScene);
                AppendLine("[ToolchainValidation] Step 2: bootstrapped the combat shell from SampleScene.");
                AppendShellSummary(validationScene, "After shell bootstrap");

                LevelTopologyEditorWindow topologyWindow = ScriptableObject.CreateInstance<LevelTopologyEditorWindow>();
                try
                {
                    InvokePrivateInstanceMethod(topologyWindow, "CreateDefensePoint", validationScene);
                    InvokePrivateInstanceMethod(topologyWindow, "CreateEnemyPath", validationScene);
                    InvokePrivateInstanceMethod(topologyWindow, "CreateSpawnGate", validationScene);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(topologyWindow);
                }

                BattlefieldMapDefinition map = FindFirstComponentInScene<BattlefieldMapDefinition>(validationScene);
                BuildZone buildZone = FindFirstComponentInScene<BuildZone>(validationScene);
                WaveSpawner waveSpawner = FindFirstComponentInScene<WaveSpawner>(validationScene);
                TowerDefenseGame game = FindFirstComponentInScene<TowerDefenseGame>(validationScene);
                EnemyPath createdPath = FindFirstComponentInScene<EnemyPath>(validationScene);

                AppendLine("[ToolchainValidation] Step 3: topology editor created one defense point, one path, and one spawn gate.");
                AppendShellSummary(validationScene, "After topology creation");

                if (createdPath != null)
                {
                    EnemyPathAuthoringUtility.CreateWaypointAtEnd(createdPath, new Vector3(-12f, 4f, 0f), renameWaypoints: true);
                    EnemyPathAuthoringUtility.CreateWaypointAtEnd(createdPath, new Vector3(-2f, 4f, 0f), renameWaypoints: true);
                    EnemyPathAuthoringUtility.CreateWaypointAtEnd(createdPath, new Vector3(-2f, -4f, 0f), renameWaypoints: true);
                    EnemyPathAuthoringUtility.CreateWaypointAtEnd(createdPath, new Vector3(10f, -4f, 0f), renameWaypoints: true);
                }

                AppendLine("[ToolchainValidation] Step 4: created four waypoints through the path-authoring helper.");
                AppendLine($"[ToolchainValidation] Current waypoint count: {EnemyPathAuthoringUtility.GetWaypointChildren(createdPath).Count}");

                if (buildZone != null)
                {
                    Transform zoneRoot = TowerDefenseMapToolkitUtility.EnsureZoneShapeRoot(buildZone);
                    TowerDefenseMapToolkitUtility.CreateBrushRectangle(
                        validationScene,
                        AuthoringBrushMode.BuildZoneShape,
                        zoneRoot,
                        new Rect(-16f, -10f, 32f, 20f),
                        string.Empty,
                        new Color(1f, 0.5f, 0.2f, 0.18f));
                    buildZone.CollectZoneShapeColliders();
                    EditorUtility.SetDirty(buildZone);
                }

                AppendLine("[ToolchainValidation] Step 5: created one buildable area rectangle through the zone-brush helper.");

                TowerDefenseMapToolkitWindow mapToolkitWindow = ScriptableObject.CreateInstance<TowerDefenseMapToolkitWindow>();
                try
                {
                    InvokePrivateInstanceMethod(mapToolkitWindow, "GenerateRoadFromPath", createdPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mapToolkitWindow);
                }

                AppendLine("[ToolchainValidation] Step 6: generated functional road strips from the authored waypoints.");
                AppendLine($"[ToolchainValidation] Generated PathSegment count: {CountPathSegments(validationScene)}");

                ResyncSharedReferences(validationScene);
                if (map != null)
                {
                    map.CollectSceneReferences();
                    EditorUtility.SetDirty(map);
                }

                AppendLine("[ToolchainValidation] Step 7: resynced shared references after path / zone authoring.");

                RuntimeProbeResult runtimeProbe = ProbeRuntimeReadiness(game, waveSpawner, createdPath);
                AppendLine("[ToolchainValidation] Runtime probe summary:");
                AppendLine($"- Relay can be placed in build zone: {runtimeProbe.CanPlaceRelay}");
                AppendLine($"- Runtime wave count: {runtimeProbe.RuntimeWaveCount}");
                AppendLine($"- First enemy spawn succeeded: {runtimeProbe.FirstEnemySpawnSucceeded}");
                if (!string.IsNullOrWhiteSpace(runtimeProbe.PlacementFailureReason))
                {
                    AppendLine($"- Placement failure reason: {runtimeProbe.PlacementFailureReason}");
                }

                EditorSceneManager.SaveScene(validationScene);
                WriteReport();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                AppendLine($"[ToolchainValidation] FAILED: {exception}");
                WriteReport();
                EditorApplication.Exit(1);
                throw;
            }
        }

        private static Scene CreateFreshValidationScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ValidationScenePath);
            return scene;
        }

        private static void BootstrapSceneShell(Scene targetScene)
        {
            Scene sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            try
            {
                TowerDefenseMapToolkitUtility.BootstrapCombatSceneShellFromSample(sampleScene, targetScene, replaceExistingRoots: true);
            }
            finally
            {
                EditorSceneManager.CloseScene(sampleScene, true);
            }
        }

        private static void ResyncSharedReferences(Scene targetScene)
        {
            Scene sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            try
            {
                TowerDefenseMapToolkitUtility.SyncSceneFromSample(sampleScene, targetScene);
            }
            finally
            {
                EditorSceneManager.CloseScene(sampleScene, true);
            }
        }

        private static RuntimeProbeResult ProbeRuntimeReadiness(TowerDefenseGame game, WaveSpawner waveSpawner, EnemyPath path)
        {
            RuntimeProbeResult result = new RuntimeProbeResult();

            if (game != null)
            {
                InvokePrivateInstanceMethod(game, "Awake");
                InvokePrivateInstanceMethod(game, "Start");

                MethodInfo validatePlacementMethod = typeof(TowerDefenseGame)
                    .GetMethod("ValidatePlacementPosition", BindingFlags.Instance | BindingFlags.NonPublic);
                if (validatePlacementMethod != null)
                {
                    object[] arguments = { new Vector3(-8f, 0f, 0f), TowerType.Relay, null };
                    result.CanPlaceRelay = (bool)validatePlacementMethod.Invoke(game, arguments);
                    result.PlacementFailureReason = arguments[2] as string;
                }
            }

            if (waveSpawner != null)
            {
                InvokePrivateInstanceMethod(waveSpawner, "Start");

                FieldInfo runtimeWavesField = typeof(WaveSpawner).GetField("_runtimeWaves", BindingFlags.Instance | BindingFlags.NonPublic);
                IList runtimeWaves = runtimeWavesField != null ? runtimeWavesField.GetValue(waveSpawner) as IList : null;
                result.RuntimeWaveCount = runtimeWaves != null ? runtimeWaves.Count : -1;

                if (runtimeWaves != null && runtimeWaves.Count > 0)
                {
                    object firstWave = runtimeWaves[0];
                    FieldInfo groupsField = firstWave.GetType().GetField("Groups", BindingFlags.Instance | BindingFlags.Public);
                    IList groups = groupsField != null ? groupsField.GetValue(firstWave) as IList : null;
                    if (groups != null && groups.Count > 0)
                    {
                        MethodInfo spawnEnemyMethod = typeof(WaveSpawner).GetMethod("SpawnEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
                        object[] spawnArguments = { groups[0], 1, 1 };
                        result.FirstEnemySpawnSucceeded = spawnEnemyMethod != null && (bool)spawnEnemyMethod.Invoke(waveSpawner, spawnArguments);
                    }
                }
            }

            return result;
        }

        private static void AppendShellSummary(Scene scene, string title)
        {
            AppendLine($"[ToolchainValidation] --- {title} ---");
            AppendLine($"- TowerDefenseGame: {CountComponentsInScene<TowerDefenseGame>(scene)}");
            AppendLine($"- WaveSpawner: {CountComponentsInScene<WaveSpawner>(scene)}");
            AppendLine($"- BattlefieldMapDefinition: {CountComponentsInScene<BattlefieldMapDefinition>(scene)}");
            AppendLine($"- BuildZone: {CountComponentsInScene<BuildZone>(scene)}");
            AppendLine($"- EnemyPath: {CountComponentsInScene<EnemyPath>(scene)}");
            AppendLine($"- EnemySpawnGate: {CountComponentsInScene<EnemySpawnGate>(scene)}");
            AppendLine($"- DefensePointFlag: {CountComponentsInScene<DefensePointFlag>(scene)}");
            AppendLine($"- PathSegment: {CountPathSegments(scene)}");
            AppendLine($"- HUDCanvas present: {TowerDefenseMapToolkitUtility.FindObjectByName(scene, "HUDCanvas") != null}");
            AppendLine($"- PlacedTowers root present: {TowerDefenseMapToolkitUtility.FindObjectByName(scene, "PlacedTowers") != null}");
            AppendLine($"- PlacementPreviewRoot present: {TowerDefenseMapToolkitUtility.FindObjectByName(scene, "PlacementPreviewRoot") != null}");
            AppendLine($"- EnemiesRoot present: {TowerDefenseMapToolkitUtility.FindObjectByName(scene, "EnemiesRoot") != null}");
        }

        private static int CountPathSegments(Scene scene)
        {
            return TowerDefenseMapToolkitUtility.EnumerateSceneObjects(scene)
                .Count(sceneObject => sceneObject != null && sceneObject.name.StartsWith("PathSegment_", StringComparison.Ordinal));
        }

        private static int CountComponentsInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Count(component => component != null);
        }

        private static T FindFirstComponentInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(component => component != null);
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName, params object[] arguments)
        {
            if (target == null)
            {
                return;
            }

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().Name, methodName);
            }

            method.Invoke(target, arguments);
        }

        private static void AppendLine(string line)
        {
            Report.AppendLine(line);
            Debug.Log(line);
        }

        private static void WriteReport()
        {
            string absoluteReportPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp", ReportFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteReportPath) ?? Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp"));
            File.WriteAllText(absoluteReportPath, Report.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[ToolchainValidation] Report saved to: {absoluteReportPath}");
        }

        private sealed class RuntimeProbeResult
        {
            public bool CanPlaceRelay;
            public string PlacementFailureReason;
            public int RuntimeWaveCount = -1;
            public bool FirstEnemySpawnSucceeded;
        }
    }
}
