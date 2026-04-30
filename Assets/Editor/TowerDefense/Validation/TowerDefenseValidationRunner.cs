using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 这个批处理验证器负责在人工检查之外，再拦一层结构性错误。
    ///
    /// 当前重点检查：
    /// 1. MainMenu 是否还保有基础 UI 引用
    /// 2. SampleScene 的原型层级和关键运行时引用是否正确
    /// 3. 三种战斗塔是否都已经有各自独立的运行时 prefab
    /// 4. 继电器覆盖判定是否仍是正方形
    /// </summary>
    public static class TowerDefenseValidationRunner
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity"; // 中文：Sample场景路径
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity"; // 中文：主菜单场景路径
        private const string RelayPrefabPath = "Assets/Prefabs/TowerDefense/Runtime/RelayTowerPrototype.prefab"; // 中文：继电器预制体路径
        private const string SingleTargetPrefabPath = "Assets/Prefabs/TowerDefense/Runtime/SingleTargetTowerPrototype.prefab"; // 中文：单体目标预制体路径
        private const string SlowFieldPrefabPath = "Assets/Prefabs/TowerDefense/Runtime/SlowFieldTowerPrototype.prefab"; // 中文：减速区域预制体路径
        private const string BombardPrefabPath = "Assets/Prefabs/TowerDefense/Runtime/BombardTowerPrototype.prefab"; // 中文：炸弹预制体路径
        private const string EnemyPrefabPath = "Assets/Prefabs/TowerDefense/Runtime/EnemyPrototype.prefab"; // 中文：敌人预制体路径
        private const string ShotTracePrefabPath = "Assets/Prefabs/TowerDefense/Vfx/ShotTrace.prefab"; // 中文：Shot轨迹预制体路径
        private const string SlowPulsePrefabPath = "Assets/Prefabs/TowerDefense/Vfx/SlowPulse.prefab"; // 中文：减速脉冲预制体路径
        private const string BombProjectilePrefabPath = "Assets/Prefabs/TowerDefense/Vfx/BombProjectile.prefab"; // 中文：炸弹投射物预制体路径
        private const string BombExplosionPrefabPath = "Assets/Prefabs/TowerDefense/Vfx/BombExplosion.prefab"; // 中文：炸弹爆炸预制体路径

        public static void RunAll()
        {
            try
            {
                ValidateMainMenuScene();
                ValidatePrefabAssets();
                ValidateSampleSceneStructure();
                ValidateRelayCoverageShape();
                Debug.Log("TowerDefenseValidationRunner: all automated checks passed.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"TowerDefenseValidationRunner failed: {exception}");
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void ValidateMainMenuScene()
        {
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            MainMenuController controller = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            if (controller == null)
            {
                throw new InvalidOperationException("MainMenu scene is missing MainMenuController.");
            }

            Canvas mainCanvas = GetPrivateField<Canvas>(controller, "mainCanvas");
            RectTransform menuRoot = GetPrivateField<RectTransform>(controller, "menuRoot");
            Image backgroundPanel = GetPrivateField<Image>(controller, "backgroundPanel");
            Button startButton = GetPrivateField<Button>(controller, "startButton");
            if (mainCanvas == null || menuRoot == null || backgroundPanel == null || startButton == null)
            {
                throw new InvalidOperationException("MainMenuController still has missing scene references.");
            }
        }

        private static void ValidatePrefabAssets()
        {
            GameObject relayPrefab = LoadRequiredPrefab(RelayPrefabPath);
            GameObject singleTargetPrefab = LoadRequiredPrefab(SingleTargetPrefabPath);
            GameObject slowFieldPrefab = LoadRequiredPrefab(SlowFieldPrefabPath);
            GameObject bombardPrefab = LoadRequiredPrefab(BombardPrefabPath);
            GameObject enemyPrefab = LoadRequiredPrefab(EnemyPrefabPath);

            LoadRequiredPrefab(ShotTracePrefabPath);
            LoadRequiredPrefab(SlowPulsePrefabPath);
            LoadRequiredPrefab(BombProjectilePrefabPath);
            LoadRequiredPrefab(BombExplosionPrefabPath);

            if (relayPrefab.GetComponent<RelayTower>() == null)
            {
                throw new InvalidOperationException("Relay runtime prefab is missing RelayTower.");
            }

            AssertTowerPrefabType(singleTargetPrefab, TowerType.SingleTarget, "single-target");
            AssertTowerPrefabType(slowFieldPrefab, TowerType.SlowField, "slow-field");
            AssertTowerPrefabType(bombardPrefab, TowerType.Bombard, "bombard");

            if (enemyPrefab.GetComponent<Enemy>() == null)
            {
                throw new InvalidOperationException("Enemy runtime prefab is missing Enemy.");
            }
        }

        private static void ValidateSampleSceneStructure()
        {
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            TowerDefenseGame gameController = UnityEngine.Object.FindFirstObjectByType<TowerDefenseGame>();
            WaveSpawner waveSpawner = UnityEngine.Object.FindFirstObjectByType<WaveSpawner>();
            DefenseTower prototype = UnityEngine.Object.FindObjectsByType<DefenseTower>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(tower => tower.name == "DefenseTowerPrototype");
            RelayTower relayPrototype = UnityEngine.Object.FindObjectsByType<RelayTower>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(relay => relay.name == "RelayTowerPrototype");

            if (gameController == null || waveSpawner == null || prototype == null || relayPrototype == null)
            {
                throw new InvalidOperationException("SampleScene is missing one or more runtime prototypes.");
            }

            Transform feedbackRoot = GetPrivateField<Transform>(prototype, "feedbackRootReference");
            Transform typeSignatureRoot = GetPrivateField<Transform>(prototype, "typeSignatureRootReference");
            Transform levelMarkerRoot = GetPrivateField<Transform>(prototype, "levelMarkerRootReference");
            Transform relayVisualRoot = GetPrivateField<Transform>(relayPrototype, "visualRootReference");
            if (feedbackRoot == null || typeSignatureRoot == null || levelMarkerRoot == null || relayVisualRoot == null)
            {
                throw new InvalidOperationException("SampleScene prototype visual roots are still missing references.");
            }

            AssertPrefabAssetReference(GetPrivateField<UnityEngine.Object>(gameController, "relayTowerPrototypeReference"), RelayPrefabPath);
            AssertPrefabAssetReference(GetPrivateField<UnityEngine.Object>(gameController, "singleTargetTowerPrototypeReference"), SingleTargetPrefabPath);
            AssertPrefabAssetReference(GetPrivateField<UnityEngine.Object>(gameController, "slowFieldTowerPrototypeReference"), SlowFieldPrefabPath);
            AssertPrefabAssetReference(GetPrivateField<UnityEngine.Object>(gameController, "bombardTowerPrototypeReference"), BombardPrefabPath);
            AssertPrefabAssetReference(GetPrivateField<UnityEngine.Object>(waveSpawner, "enemyPrototypeReference"), EnemyPrefabPath);

            SerializedObject serializedPrototype = new SerializedObject(prototype);
            AssertSerializedAssetReference(serializedPrototype, "singleTargetTuning.shotTracePrefab", ShotTracePrefabPath);
            AssertSerializedAssetReference(serializedPrototype, "slowFieldTuning.slowPulsePrefab", SlowPulsePrefabPath);
            AssertSerializedAssetReference(serializedPrototype, "bombardTuning.bombProjectilePrefab", BombProjectilePrefabPath);
            AssertSerializedAssetReference(serializedPrototype, "bombardTuning.bombExplosionPrefab", BombExplosionPrefabPath);

            DefenseTower singleTargetInstance = UnityEngine.Object.Instantiate(prototype);
            DefenseTower slowFieldInstance = UnityEngine.Object.Instantiate(prototype);
            DefenseTower bombardInstance = UnityEngine.Object.Instantiate(prototype);

            try
            {
                singleTargetInstance.ConfigureBuildType(TowerType.SingleTarget);
                slowFieldInstance.ConfigureBuildType(TowerType.SlowField);
                bombardInstance.ConfigureBuildType(TowerType.Bombard);

                AssertOnlyExpectedFeedbackRoot(singleTargetInstance, "SingleTargetFeedbackRoot");
                AssertOnlyExpectedFeedbackRoot(slowFieldInstance, "SlowFieldFeedbackRoot");
                AssertOnlyExpectedFeedbackRoot(bombardInstance, "BombardFeedbackRoot");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(singleTargetInstance.gameObject);
                UnityEngine.Object.DestroyImmediate(slowFieldInstance.gameObject);
                UnityEngine.Object.DestroyImmediate(bombardInstance.gameObject);
            }
        }

        private static void ValidateRelayCoverageShape()
        {
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            RelayTower relayPrototype = UnityEngine.Object.FindObjectsByType<RelayTower>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(relay => relay.name == "RelayTowerPrototype");
            if (relayPrototype == null)
            {
                throw new InvalidOperationException("SampleScene is missing RelayTowerPrototype.");
            }

            float supplyRange = relayPrototype.SupplyRange;
            Vector3 center = relayPrototype.transform.position;
            Vector3 insideCorner = center + new Vector3(supplyRange * 0.95f, supplyRange * 0.95f, 0f);
            Vector3 outsideEdge = center + new Vector3(supplyRange * 1.05f, 0f, 0f);

            if (!relayPrototype.ContainsPoint(insideCorner))
            {
                throw new InvalidOperationException("Relay coverage is still not accepting square-corner points.");
            }

            if (relayPrototype.ContainsPoint(outsideEdge))
            {
                throw new InvalidOperationException("Relay coverage is still accepting points outside the square edge.");
            }
        }

        private static void AssertOnlyExpectedFeedbackRoot(DefenseTower tower, string expectedChildName)
        {
            Transform feedbackRoot = GetPrivateField<Transform>(tower, "feedbackRootReference");
            if (feedbackRoot == null)
            {
                throw new InvalidOperationException("DefenseTower instance is missing feedbackRootReference.");
            }

            string[] childNames = Enumerable.Range(0, feedbackRoot.childCount)
                .Select(index => feedbackRoot.GetChild(index).name)
                .ToArray();

            if (childNames.Length != 1 || childNames[0] != expectedChildName)
            {
                throw new InvalidOperationException(
                    $"DefenseTower feedback roots are not isolated correctly. Expected only '{expectedChildName}', got [{string.Join(", ", childNames)}].");
            }
        }

        private static GameObject LoadRequiredPrefab(string assetPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Missing required prefab asset: {assetPath}");
            }

            return prefab;
        }

        private static void AssertTowerPrefabType(GameObject prefab, TowerType expectedType, string label)
        {
            DefenseTower tower = prefab.GetComponent<DefenseTower>();
            if (tower == null)
            {
                throw new InvalidOperationException($"{label} runtime prefab is missing DefenseTower.");
            }

            if (tower.BuildType != expectedType)
            {
                throw new InvalidOperationException(
                    $"Expected {label} runtime prefab to use tower type '{expectedType}', but found '{tower.BuildType}'.");
            }
        }

        private static void AssertPrefabAssetReference(UnityEngine.Object value, string expectedAssetPath)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"Expected prefab asset reference '{expectedAssetPath}', but the reference is null.");
            }

            string actualAssetPath = AssetDatabase.GetAssetPath(value);
            if (!string.Equals(actualAssetPath, expectedAssetPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expected prefab asset reference '{expectedAssetPath}', but found '{actualAssetPath}'.");
            }
        }

        private static void AssertSerializedAssetReference(SerializedObject target, string propertyPath, string expectedAssetPath)
        {
            SerializedProperty property = target.FindProperty(propertyPath);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property '{propertyPath}'.");
            }

            AssertPrefabAssetReference(property.objectReferenceValue, expectedAssetPath);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().Name, fieldName);
            }

            return (T)field.GetValue(target);
        }
    }
}
