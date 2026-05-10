using System;
using System.Collections;
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
    /// Small runtime-style probe for authored combat scenes.
    ///
    /// This helper exists for one reason: when a level "looks" wired in YAML but still does not
    /// behave like `SampleScene`, we need a precise answer about which runtime layer is failing.
    ///
    /// It answers two concrete questions per scene:
    /// 1. Can `TowerDefenseGame` find at least one valid relay placement point?
    /// 2. Can `WaveSpawner` build runtime waves and successfully spawn the first enemy?
    ///
    /// The probe intentionally uses reflection instead of duplicating private runtime rules in
    /// editor code, because we want diagnostics against the real implementation path.
    /// </summary>
    public static class LevelSceneSanityProbe
    {
        private static readonly StringBuilder ProbeLog = new StringBuilder();

        public static void ProbeLevel02()
        {
            ProbeScene("Assets/Scenes/Level02.unity");
        }

        public static void ProbeLevel03()
        {
            ProbeScene("Assets/Scenes/Level03.unity");
        }

        public static void ProbeLevel04()
        {
            ProbeScene("Assets/Scenes/Level04.unity");
        }

        private static void ProbeScene(string scenePath)
        {
            ProbeLog.Clear();
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            AppendLine($"[SceneProbe] Probing scene: {scenePath}");

            TowerDefenseGame game = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TowerDefenseGame>(true))
                .FirstOrDefault(component => component != null);
            WaveSpawner waveSpawner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WaveSpawner>(true))
                .FirstOrDefault(component => component != null);

            if (game != null)
            {
                InvokePrivateInstanceMethod(game, "Awake");
                InvokePrivateInstanceMethod(game, "Start");
                ProbePlacement(game);
            }
            else
            {
                AppendLine("[SceneProbe] TowerDefenseGame not found.");
            }

            if (waveSpawner != null)
            {
                InvokePrivateInstanceMethod(waveSpawner, "Start");
                ProbeSpawner(waveSpawner);
            }
            else
            {
                AppendLine("[SceneProbe] WaveSpawner not found.");
            }

            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "scene-probe.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, ProbeLog.ToString(), Encoding.UTF8);
            Debug.Log(ProbeLog.ToString());
        }

        private static void ProbePlacement(TowerDefenseGame game)
        {
            MethodInfo validatePlacementMethod = typeof(TowerDefenseGame)
                .GetMethod("ValidatePlacementPosition", BindingFlags.Instance | BindingFlags.NonPublic);
            if (validatePlacementMethod == null)
            {
                Debug.LogWarning("[SceneProbe] ValidatePlacementPosition method not found.");
                return;
            }

            Vector3[] samplePoints =
            {
                new Vector3(-6.5f, -2.25f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(-2f, -2f, 0f),
                new Vector3(4f, -4f, 0f)
            };

            foreach (Vector3 samplePoint in samplePoints)
            {
                object[] arguments = { samplePoint, TowerType.Relay, null };
                bool valid = (bool)validatePlacementMethod.Invoke(game, arguments);
                string invalidReason = arguments[2] as string;
                AppendLine($"[SceneProbe] Relay placement @ {samplePoint}: valid={valid} reason={invalidReason}");
            }
        }

        private static void ProbeSpawner(WaveSpawner waveSpawner)
        {
            FieldInfo runtimeWavesField = typeof(WaveSpawner).GetField("_runtimeWaves", BindingFlags.Instance | BindingFlags.NonPublic);
            IList runtimeWaves = runtimeWavesField != null ? runtimeWavesField.GetValue(waveSpawner) as IList : null;
            AppendLine($"[SceneProbe] Runtime waves count: {(runtimeWaves != null ? runtimeWaves.Count : -1)}");

            if (runtimeWaves == null || runtimeWaves.Count == 0)
            {
                return;
            }

            object firstWave = runtimeWaves[0];
            FieldInfo groupsField = firstWave.GetType().GetField("Groups", BindingFlags.Instance | BindingFlags.Public);
            IList groups = groupsField != null ? groupsField.GetValue(firstWave) as IList : null;
            AppendLine($"[SceneProbe] First wave group count: {(groups != null ? groups.Count : -1)}");
            if (groups == null || groups.Count == 0)
            {
                return;
            }

            MethodInfo spawnEnemyMethod = typeof(WaveSpawner).GetMethod("SpawnEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
            object firstGroup = groups[0];
            object[] spawnArguments = { firstGroup, 1, 1 };
            bool spawned = spawnEnemyMethod != null && (bool)spawnEnemyMethod.Invoke(waveSpawner, spawnArguments);
            AppendLine($"[SceneProbe] First enemy spawn attempt result: {spawned}");
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void AppendLine(string message)
        {
            ProbeLog.AppendLine(message);
        }
    }
}
