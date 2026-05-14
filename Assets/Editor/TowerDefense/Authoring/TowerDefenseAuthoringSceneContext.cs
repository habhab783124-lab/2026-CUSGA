using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Editor
{
    /// <summary>
    /// Shared editor-only scene context used by multiple tower defense authoring windows.
    ///
    /// Why this exists:
    /// 1. Several map tools had their own "adopt current scene" logic.
    /// 2. That forced the user to repeat the same action in each window.
    /// 3. It also increased drift risk because each tool could hold slightly different scene references.
    ///
    /// This shared container lets one window capture the active scene context once, and the rest
    /// of the toolchain can then read the same references.
    /// </summary>
    internal sealed class TowerDefenseAuthoringSceneContext : ScriptableObject
    {
        private const string AssetPath = "Assets/Editor/TowerDefense/Authoring/TowerDefenseAuthoringSceneContext.asset";

        [SerializeField] private string scenePath;
        [SerializeField] private string sceneName;
        [SerializeField] private TowerDefenseGame currentGame;
        [SerializeField] private WaveSpawner currentWaveSpawner;
        [SerializeField] private BattlefieldMapDefinition currentMap;
        [SerializeField] private BuildZone currentBuildZone;
        [SerializeField] private Camera currentCamera;
        [SerializeField] private EnemyPath currentPath;

        internal string ScenePath => scenePath;
        internal string SceneName => sceneName;
        internal TowerDefenseGame CurrentGame => currentGame;
        internal WaveSpawner CurrentWaveSpawner => currentWaveSpawner;
        internal BattlefieldMapDefinition CurrentMap => currentMap;
        internal BuildZone CurrentBuildZone => currentBuildZone;
        internal Camera CurrentCamera => currentCamera;
        internal EnemyPath CurrentPath => currentPath;

        internal string BuildSummary()
        {
            string safeSceneName = string.IsNullOrWhiteSpace(sceneName) ? "(未记录场景)" : sceneName;
            return
                $"当前共享上下文来自：{safeSceneName}\n" +
                $"地图：{(currentMap != null ? currentMap.name : "(未找到)")}\n" +
                $"刷怪器：{(currentWaveSpawner != null ? currentWaveSpawner.name : "(未找到)")}\n" +
                $"路径：{(currentPath != null ? currentPath.name : "(未找到)")}";
        }

        internal static TowerDefenseAuthoringSceneContext GetOrCreate()
        {
            TowerDefenseAuthoringSceneContext context = AssetDatabase.LoadAssetAtPath<TowerDefenseAuthoringSceneContext>(AssetPath);
            if (context != null)
            {
                return context;
            }

            context = CreateInstance<TowerDefenseAuthoringSceneContext>();
            AssetDatabase.CreateAsset(context, AssetPath);
            AssetDatabase.SaveAssets();
            return context;
        }

        internal static TowerDefenseAuthoringSceneContext CaptureActiveSceneContext()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            TowerDefenseAuthoringSceneContext context = GetOrCreate();
            context.scenePath = activeScene.path;
            context.sceneName = activeScene.name;
            context.currentGame = FindFirstComponentInScene<TowerDefenseGame>(activeScene);
            context.currentWaveSpawner = FindFirstComponentInScene<WaveSpawner>(activeScene);
            context.currentMap = FindFirstComponentInScene<BattlefieldMapDefinition>(activeScene);
            context.currentBuildZone = FindFirstComponentInScene<BuildZone>(activeScene);
            context.currentCamera = FindFirstComponentInScene<Camera>(activeScene);
            context.currentPath = FindFirstComponentInScene<EnemyPath>(activeScene);
            EditorUtility.SetDirty(context);
            AssetDatabase.SaveAssets();
            return context;
        }

        internal static T FindFirstComponentInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid())
            {
                return null;
            }

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(component => component != null);
        }
    }
}
