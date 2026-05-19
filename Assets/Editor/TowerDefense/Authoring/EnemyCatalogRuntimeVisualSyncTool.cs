using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 把怪物动画产物同步回 `EnemyCatalog.asset`，让运行时显示链不再依赖
    /// “当前实例到底是不是正确 prefab”这种脆弱前提。
    ///
    /// 当前运行时我们已经抓到一种失败模式：
    /// - archetype 是对的
    /// - 但真实刷出来的实例主体纹理仍然是 `Square`
    ///
    /// 这意味着仅靠 `RuntimePrefab` 一层引用还不够稳，
    /// 因为某些运行时路径下，当前实例可能仍然来自通用原型，再套一层敌人目录数据。
    ///
    /// 所以这里把“这个 archetype 真正应该显示哪张默认 sprite、哪套 animator controller”
    /// 明确写回 `EnemyCatalog.asset`。
    /// 之后 `Enemy.Initialize(...)` 可以直接按 archetype 接管运行时外观，
    /// 不再被实例来源污染。
    /// </summary>
    public static class EnemyCatalogRuntimeVisualSyncTool
    {
        private const string EnemyCatalogAssetPath = "Assets/Resources/TowerDefense/Configs/EnemyCatalog.asset";
        private const string RuntimeEnemyFolder = "Assets/Prefabs/TowerDefense/Runtime/Enemies";
        private const string ControllerFolder = "Assets/Animations/TowerDefense/Enemies/Controllers";
        private const string SpriteSheetFolder = "Assets/Art/Enemy/移动/NoBG";

        private static readonly RuntimeVisualBinding[] Bindings =
        {
            new RuntimeVisualBinding(EnemyArchetypeId.Scavenger, "ScavengerEnemy", "ScavengerMove", "拾荒者"),
            new RuntimeVisualBinding(EnemyArchetypeId.Wolf, "WolfEnemy", "WolfMove", "狼"),
            new RuntimeVisualBinding(EnemyArchetypeId.BannerScavenger, "BannerScavengerEnemy", "BannerScavengerMove", "旗帜拾荒者"),
            new RuntimeVisualBinding(EnemyArchetypeId.Mechanic, "MechanicEnemy", "MechanicMove", "机械师"),
            new RuntimeVisualBinding(EnemyArchetypeId.HeavyArmoredMachine, "HeavyArmoredMachineEnemy", "HeavyArmoredMachineMove", "重甲机械兵"),
            new RuntimeVisualBinding(EnemyArchetypeId.StealthStalker, "StealthStalkerEnemy", "StealthStalkerMove", "隐身人"),
            new RuntimeVisualBinding(EnemyArchetypeId.Abomination, "AbominationEnemy", "AbominationMove", "憎恶"),
            new RuntimeVisualBinding(EnemyArchetypeId.SmallScavenger, "SmallScavengerEnemy", "SmallScavengerMove", "幼年拾荒者")
        };

        [MenuItem("Tools/Tower Defense/Authoring/同步怪物目录运行时外观")]
        public static void SyncEnemyCatalogRuntimeVisuals()
        {
            EnemyCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalogAsset>(EnemyCatalogAssetPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Missing EnemyCatalog asset: {EnemyCatalogAssetPath}");
            }

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty definitionsProperty = serializedCatalog.FindProperty("definitions");
            if (definitionsProperty == null || !definitionsProperty.isArray)
            {
                throw new InvalidOperationException("EnemyCatalog definitions property is missing or invalid.");
            }

            Dictionary<EnemyArchetypeId, RuntimeVisualBinding> bindingMap = new Dictionary<EnemyArchetypeId, RuntimeVisualBinding>(Bindings.Length);
            for (int index = 0; index < Bindings.Length; index++)
            {
                bindingMap[Bindings[index].ArchetypeId] = Bindings[index];
            }

            for (int index = 0; index < definitionsProperty.arraySize; index++)
            {
                SerializedProperty definitionProperty = definitionsProperty.GetArrayElementAtIndex(index);
                if (definitionProperty == null)
                {
                    continue;
                }

                SerializedProperty archetypeProperty = definitionProperty.FindPropertyRelative("archetypeId");
                if (archetypeProperty == null)
                {
                    continue;
                }

                EnemyArchetypeId archetypeId = (EnemyArchetypeId)archetypeProperty.enumValueIndex;
                if (!bindingMap.TryGetValue(archetypeId, out RuntimeVisualBinding binding))
                {
                    continue;
                }

                GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RuntimeEnemyFolder}/{binding.PrefabName}.prefab");
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{ControllerFolder}/{binding.ControllerName}.controller");
                Sprite firstFrame = LoadFirstFrameFromSpriteSheet($"{SpriteSheetFolder}/{binding.FrameFolderName}.png");

                AssignObjectReference(definitionProperty, "runtimePrefab", runtimePrefab);
                AssignObjectReference(definitionProperty, "runtimeBodySprite", firstFrame);
                AssignObjectReference(definitionProperty, "runtimeAnimatorController", controller);
            }

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void AssignObjectReference(SerializedProperty definitionProperty, string relativePropertyName, UnityEngine.Object value)
        {
            SerializedProperty property = definitionProperty.FindPropertyRelative(relativePropertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing EnemyCatalog property: {relativePropertyName}");
            }

            property.objectReferenceValue = value;
        }

        private static Sprite LoadFirstFrameFromSpriteSheet(string spriteSheetPath)
        {
            UnityEngine.Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
            List<Sprite> sprites = new List<Sprite>(10);
            for (int index = 0; index < assetsAtPath.Length; index++)
            {
                if (assetsAtPath[index] is Sprite sprite && !string.IsNullOrWhiteSpace(sprite.name))
                {
                    sprites.Add(sprite);
                }
            }

            sprites.Sort((left, right) => CompareSpriteFrameNames(left.name, right.name));
            if (sprites.Count == 0)
            {
                throw new InvalidOperationException($"No sliced sprites found in sprite sheet: {spriteSheetPath}");
            }

            return sprites[0];
        }

        private static int CompareSpriteFrameNames(string left, string right)
        {
            int leftFrame = ExtractTrailingFrameIndex(left);
            int rightFrame = ExtractTrailingFrameIndex(right);
            if (leftFrame != rightFrame)
            {
                return leftFrame.CompareTo(rightFrame);
            }

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int ExtractTrailingFrameIndex(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return int.MaxValue;
            }

            int underscoreIndex = spriteName.LastIndexOf('_');
            if (underscoreIndex >= 0 &&
                underscoreIndex < spriteName.Length - 1 &&
                int.TryParse(spriteName.Substring(underscoreIndex + 1), out int frameIndex))
            {
                return frameIndex;
            }

            return int.MaxValue;
        }

        private readonly struct RuntimeVisualBinding
        {
            public RuntimeVisualBinding(EnemyArchetypeId archetypeId, string prefabName, string controllerName, string frameFolderName)
            {
                ArchetypeId = archetypeId;
                PrefabName = prefabName;
                ControllerName = controllerName;
                FrameFolderName = frameFolderName;
            }

            public EnemyArchetypeId ArchetypeId { get; }
            public string PrefabName { get; }
            public string ControllerName { get; }
            public string FrameFolderName { get; }
        }
    }
}
