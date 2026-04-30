using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 这个作者工具负责把 `SampleScene` 里的运行时原型整理成真正的运行时 Prefab 资产。
    ///
    /// 当前版本的重点和旧版不同：
    /// - 继电器、敌人仍然各自一个独立 prefab
    /// - 三种战斗塔不再共用一个塔 prefab
    /// - 改成：
    ///   - `SingleTargetTowerPrototype.prefab`
    ///   - `SlowFieldTowerPrototype.prefab`
    ///   - `BombardTowerPrototype.prefab`
    ///
    /// 这样后续你就可以直接分别改三种塔的 prefab，而不是继续依赖“一个共用塔 prefab + 运行时切类型”。
    /// </summary>
    public static class TowerDefensePrefabAuthoringTool
    {
        private const float DefaultRelayPlacementRadius = 0.52f; // 中文：默认继电器放置半径
        private const float DefaultCombatTowerPlacementRadius = 0.58f; // 中文：默认Combat塔放置半径

        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity"; // 中文：Sample场景路径
        private static readonly string[] GameplayScenePaths = // 中文：Gameplay场景路径
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/Scenes/Level02.unity",
            "Assets/Scenes/Level03.unity",
            "Assets/Scenes/Level04.unity",
            "Assets/Scenes/Level05.unity"
        };

        private const string PrefabRootFolder = "Assets/Prefabs"; // 中文：预制体根节点文件夹
        private const string TowerDefensePrefabFolder = "Assets/Prefabs/TowerDefense"; // 中文：塔防御预制体文件夹
        private const string RuntimePrefabFolder = "Assets/Prefabs/TowerDefense/Runtime"; // 中文：运行时预制体文件夹
        private const string VfxPrefabFolder = "Assets/Prefabs/TowerDefense/Vfx"; // 中文：Vfx预制体文件夹

        [MenuItem("Tools/Tower Defense/重建分类运行时 Prefab")]
        public static void BatchCreateOrUpdateRuntimePrefabs()
        {
            EnsureFolder(PrefabRootFolder);
            EnsureFolder(TowerDefensePrefabFolder);
            EnsureFolder(RuntimePrefabFolder);
            EnsureFolder(VfxPrefabFolder);

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            RelayTower relayPrototype = FindDisabledPrototype<RelayTower>("RelayTowerPrototype");
            DefenseTower defensePrototype = FindDisabledPrototype<DefenseTower>("DefenseTowerPrototype");
            Enemy enemyPrototype = FindDisabledPrototype<Enemy>("EnemyPrototype");
            if (relayPrototype == null || defensePrototype == null || enemyPrototype == null)
            {
                throw new System.InvalidOperationException("TowerDefensePrefabAuthoringTool 未找到所需的场景原型对象。");
            }

            string relayPrefabPath = $"{RuntimePrefabFolder}/RelayTowerPrototype.prefab";
            string singleTargetPrefabPath = $"{RuntimePrefabFolder}/SingleTargetTowerPrototype.prefab";
            string slowFieldPrefabPath = $"{RuntimePrefabFolder}/SlowFieldTowerPrototype.prefab";
            string bombardPrefabPath = $"{RuntimePrefabFolder}/BombardTowerPrototype.prefab";
            string enemyPrefabPath = $"{RuntimePrefabFolder}/EnemyPrototype.prefab";

            EnsurePlacedStructureComponents(relayPrototype.gameObject, DefaultRelayPlacementRadius);
            GameObject relayPrefab = PrefabUtility.SaveAsPrefabAsset(relayPrototype.gameObject, relayPrefabPath);
            GameObject enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyPrototype.gameObject, enemyPrefabPath);

            GameObject tracePrefab = CreateOrUpdateFeedbackPrefab(
                $"{VfxPrefabFolder}/ShotTrace.prefab",
                "ShotTrace",
                defensePrototype.GetComponent<SpriteRenderer>() != null ? defensePrototype.GetComponent<SpriteRenderer>().sprite : null,
                new Color(0.78f, 0.94f, 1f, 1f),
                new Vector3(1f, 0.18f, 1f));

            Sprite placementRingSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/placement-ring.png");
            GameObject slowPulsePrefab = CreateOrUpdateFeedbackPrefab(
                $"{VfxPrefabFolder}/SlowPulse.prefab",
                "SlowPulse",
                placementRingSprite,
                new Color(0.36f, 0.95f, 0.84f, 0.4f),
                new Vector3(0.36f, 0.36f, 1f));

            GameObject bombProjectilePrefab = CreateOrUpdateFeedbackPrefab(
                $"{VfxPrefabFolder}/BombProjectile.prefab",
                "BombProjectile",
                defensePrototype.GetComponent<SpriteRenderer>() != null ? defensePrototype.GetComponent<SpriteRenderer>().sprite : null,
                new Color(1f, 0.78f, 0.42f, 1f),
                new Vector3(0.32f, 0.32f, 1f));

            GameObject bombExplosionPrefab = CreateOrUpdateFeedbackPrefab(
                $"{VfxPrefabFolder}/BombExplosion.prefab",
                "BombExplosion",
                placementRingSprite,
                new Color(1f, 0.58f, 0.24f, 1f),
                new Vector3(0.42f, 0.42f, 1f));

            AssignSerializedReference(defensePrototype, "singleTargetTuning.shotTracePrefab", tracePrefab);
            AssignSerializedReference(defensePrototype, "slowFieldTuning.slowPulsePrefab", slowPulsePrefab);
            AssignSerializedReference(defensePrototype, "bombardTuning.bombProjectilePrefab", bombProjectilePrefab);
            AssignSerializedReference(defensePrototype, "bombardTuning.bombExplosionPrefab", bombExplosionPrefab);

            GameObject singleTargetPrefab = SaveTypedTowerPrefab(defensePrototype, TowerType.SingleTarget, "SingleTargetTowerPrototype", singleTargetPrefabPath);
            GameObject slowFieldPrefab = SaveTypedTowerPrefab(defensePrototype, TowerType.SlowField, "SlowFieldTowerPrototype", slowFieldPrefabPath);
            GameObject bombardPrefab = SaveTypedTowerPrefab(defensePrototype, TowerType.Bombard, "BombardTowerPrototype", bombardPrefabPath);

            for (int sceneIndex = 0; sceneIndex < GameplayScenePaths.Length; sceneIndex++)
            {
                string scenePath = GameplayScenePaths[sceneIndex];
                if (!File.Exists(scenePath))
                {
                    continue;
                }

                WireGameplayScene(scenePath, relayPrefab, singleTargetPrefab, slowFieldPrefab, bombardPrefab, enemyPrefab);
            }

            // 保持 SampleScene 里的作者原型回到单体塔默认态，方便你之后继续从场景里做对照编辑。
            defensePrototype.ConfigureBuildType(TowerType.SingleTarget);
            EditorSceneManager.MarkSceneDirty(defensePrototype.gameObject.scene);
            EditorSceneManager.SaveScene(defensePrototype.gameObject.scene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"TowerDefensePrefabAuthoringTool rebuilt typed runtime prefabs successfully. Single='{singleTargetPrefab.name}', Slow='{slowFieldPrefab.name}', Bombard='{bombardPrefab.name}'.");
        }

        private static T FindDisabledPrototype<T>(string objectName) where T : Component
        {
            T[] candidates = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null && candidates[i].name == objectName)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parentPath) && !AssetDatabase.IsValidFolder(parentPath))
            {
                EnsureFolder(parentPath);
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static GameObject SaveTypedTowerPrefab(DefenseTower sourcePrototype, TowerType towerType, string rootName, string assetPath)
        {
            GameObject temporaryClone = Object.Instantiate(sourcePrototype.gameObject);
            try
            {
                temporaryClone.name = rootName;
                temporaryClone.transform.SetParent(null, false);

                DefenseTower tower = temporaryClone.GetComponent<DefenseTower>();
                if (tower == null)
                {
                    throw new System.InvalidOperationException($"Temporary clone for '{towerType}' is missing DefenseTower.");
                }

                tower.ConfigureBuildType(towerType);
                EnsurePlacedStructureComponents(temporaryClone, DefaultCombatTowerPlacementRadius);
                return PrefabUtility.SaveAsPrefabAsset(temporaryClone, assetPath);
            }
            finally
            {
                Object.DestroyImmediate(temporaryClone);
            }
        }

        private static GameObject CreateOrUpdateFeedbackPrefab(string assetPath, string objectName, Sprite sprite, Color color, Vector3 localScale)
        {
            GameObject tempRoot = new GameObject(objectName);
            try
            {
                SpriteRenderer renderer = tempRoot.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = color;
                tempRoot.transform.localScale = localScale;

                return PrefabUtility.SaveAsPrefabAsset(tempRoot, assetPath);
            }
            finally
            {
                Object.DestroyImmediate(tempRoot);
            }
        }

        private static void AssignSerializedReference(Object target, string propertyPath, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                throw new System.InvalidOperationException($"Could not find serialized property '{propertyPath}' on {target.name}.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 保证运行时塔 Prefab 自己就是一份“可落地实例资产”。
        ///
        /// 也就是说：
        /// - 不要再依赖 `TowerPlacementBuildExecutor` 现场给 Prefab 补组件
        /// - Collider 和 PlacedTower 应该直接存在于 Prefab 上
        ///
        /// 这样作者以后打开 Prefab 时，就能直接看到完整运行链依赖。
        /// </summary>
        private static void EnsurePlacedStructureComponents(GameObject target, float placementRadius)
        {
            if (target == null)
            {
                return;
            }

            CircleCollider2D circleCollider = target.GetComponent<CircleCollider2D>();
            if (circleCollider == null)
            {
                circleCollider = target.AddComponent<CircleCollider2D>();
            }

            circleCollider.isTrigger = true;
            circleCollider.radius = placementRadius;

            if (target.GetComponent<PlacedTower>() == null)
            {
                target.AddComponent<PlacedTower>();
            }
        }

        private static void WireGameplayScene(
            string scenePath,
            GameObject relayPrefab,
            GameObject singleTargetPrefab,
            GameObject slowFieldPrefab,
            GameObject bombardPrefab,
            GameObject enemyPrefab)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            TowerDefenseGame gameController = Object.FindFirstObjectByType<TowerDefenseGame>();
            WaveSpawner waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();
            if (gameController == null || waveSpawner == null)
            {
                throw new System.InvalidOperationException($"Scene '{scenePath}' is missing TowerDefenseGame or WaveSpawner.");
            }

            AssignSerializedReference(gameController, "relayTowerPrototypeReference", relayPrefab);
            AssignSerializedReference(gameController, "singleTargetTowerPrototypeReference", singleTargetPrefab);
            AssignSerializedReference(gameController, "slowFieldTowerPrototypeReference", slowFieldPrefab);
            AssignSerializedReference(gameController, "bombardTowerPrototypeReference", bombardPrefab);
            AssignSerializedReference(waveSpawner, "enemyPrototypeReference", enemyPrefab);

            EditorSceneManager.MarkSceneDirty(gameController.gameObject.scene);
            EditorSceneManager.SaveScene(gameController.gameObject.scene);
        }
    }
}
