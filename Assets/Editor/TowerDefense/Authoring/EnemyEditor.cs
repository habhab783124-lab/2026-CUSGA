using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `Enemy` 的作者检查器。
    ///
    /// 当前重点是把敌人 prefab 最容易混淆的三件事直接说明白：
    /// 1. 基础视觉引用是否接齐
    /// 2. 当前 prefab 在 `EnemyCatalogAsset` 里匹配到的是哪一类敌人
    /// 3. 目录要求的机制模块是否已经真实挂在 prefab 上
    ///
    /// 这样像 `Wolf`、`HeavyArmoredMachine` 这种没有特殊模块但有明显被动特征的敌人，
    /// 也能在 Inspector 顶部直接看到自己的玩法摘要；
    /// 而像 `BannerScavenger`、`Mechanic`、`StealthStalker`、`Abomination` 这种需要模块的敌人，
    /// 也能马上看出 prefab 是否漏挂了对应组件。
    /// </summary>
    [CustomEditor(typeof(Enemy))]
    public sealed class EnemyEditor : UnityEditor.Editor
    {
        private const string DefaultEnemyCatalogAssetPath = "Assets/Resources/TowerDefense/Configs/EnemyCatalog.asset"; // 中文：默认敌人目录资产路径

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Enemy enemy = (Enemy)target;
            EnemyCatalogAsset.EnemyArchetypeDefinition definition = ResolveCatalogDefinition(enemy, out string prefabAssetPath);

            DrawReferenceSummary();
            DrawCatalogSummary(definition, prefabAssetPath);
            DrawMechanicSummary(enemy, definition);
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferenceSummary()
        {
            string bodyRenderer = DescribeObject("bodyRendererReference");
            string scaleRoot = DescribeObject("visualScaleRootReference");
            string healthBarRoot = DescribeObject("healthBarRootReference");
            string healthBarFill = DescribeObject("healthBarFillReference");

            EditorGUILayout.HelpBox(
                $"主体渲染器：{bodyRenderer}\n" +
                $"视觉缩放根：{scaleRoot}\n" +
                $"血条根节点：{healthBarRoot}\n" +
                $"血条填充：{healthBarFill}",
                MessageType.Info);

            if (bodyRenderer == "未设置" || healthBarRoot == "未设置" || healthBarFill == "未设置")
            {
                EditorGUILayout.HelpBox(
                    "当前敌人 prefab 还有视觉引用缺项。为了后续继续替换正式美术，建议把主体与血条链路都显式接齐。",
                    MessageType.Warning);
            }
        }

        private void DrawCatalogSummary(EnemyCatalogAsset.EnemyArchetypeDefinition definition, string prefabAssetPath)
        {
            if (definition == null)
            {
                EditorGUILayout.HelpBox(
                    $"当前没有在 EnemyCatalogAsset 里找到与这个 prefab 对应的敌人定义。\nPrefab Path: {prefabAssetPath}",
                    MessageType.Warning);
                return;
            }

            string armorSummary = definition.ArmorTier == EnemyArmorTier.None
                ? "无甲"
                : $"{definition.ArmorTier}（非穿甲 {definition.NonPiercingDamageMultiplier:0.00}x）";

            EditorGUILayout.HelpBox(
                $"目录匹配：{definition.DisplayName}\n" +
                $"敌人类型：{definition.ArchetypeId}\n" +
                $"生命：{definition.MaxHealth}  速度：{definition.MoveSpeed:0.00}  废料：{definition.ScrapReward}\n" +
                $"到点伤害：{definition.BaseDamageToBase}\n" +
                $"护甲：{armorSummary}\n" +
                $"被动特征：{BuildPassiveTraitSummary(definition)}",
                MessageType.Info);
        }

        private void DrawMechanicSummary(Enemy enemy, EnemyCatalogAsset.EnemyArchetypeDefinition definition)
        {
            bool hasStealthModule = enemy.GetComponent<EnemyStealthModule>() != null;
            bool hasShieldAuraModule = enemy.GetComponent<EnemyShieldAuraModule>() != null;
            bool hasRepairModule = enemy.GetComponent<EnemyRepairModule>() != null;
            bool hasSplitModule = enemy.GetComponent<EnemySplitOnDeathModule>() != null;

            EditorGUILayout.HelpBox(
                $"机制模块\n" +
                $"- 隐身：{ToYesNo(hasStealthModule)}\n" +
                $"- 护盾光环：{ToYesNo(hasShieldAuraModule)}\n" +
                $"- 修理：{ToYesNo(hasRepairModule)}\n" +
                $"- 死亡分裂：{ToYesNo(hasSplitModule)}",
                MessageType.None);

            if (hasStealthModule || hasShieldAuraModule || hasRepairModule || hasSplitModule)
            {
                EditorGUILayout.HelpBox(
                    "这些敌人机制模块是挂在同一个 prefab 根对象上的独立组件。继续往下滚动 Inspector，就能看到它们各自的参数面板。",
                    MessageType.Info);
            }

            if (definition == null)
            {
                return;
            }

            bool needsStealthModule = definition.EntersStealthAfterFirstDirectHit && !hasStealthModule;
            bool needsShieldAuraModule = definition.ShieldAmount > 0 && !hasShieldAuraModule;
            bool needsRepairModule = definition.RepairAmount > 0 && !hasRepairModule;
            bool needsSplitModule = definition.SplitChildType != EnemyArchetypeId.None && definition.SplitChildCount > 0 && !hasSplitModule;

            string mismatchMessage = string.Empty;
            AppendMissingMechanicWarning(ref mismatchMessage, needsStealthModule, "目录定义启用了隐身，但 prefab 上没有 `EnemyStealthModule`。");
            AppendMissingMechanicWarning(ref mismatchMessage, needsShieldAuraModule, "目录定义启用了护盾光环，但 prefab 上没有 `EnemyShieldAuraModule`。");
            AppendMissingMechanicWarning(ref mismatchMessage, needsRepairModule, "目录定义启用了修理支援，但 prefab 上没有 `EnemyRepairModule`。");
            AppendMissingMechanicWarning(ref mismatchMessage, needsSplitModule, "目录定义启用了死亡分裂，但 prefab 上没有 `EnemySplitOnDeathModule`。");

            if (!string.IsNullOrWhiteSpace(mismatchMessage))
            {
                EditorGUILayout.HelpBox(mismatchMessage.TrimEnd(), MessageType.Warning);

                if (GUILayout.Button("补挂目录要求的模块"))
                {
                    AttachMissingCatalogModules(enemy, definition);
                }
            }
        }

        private string DescribeObject(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                return "未设置";
            }

            return property.objectReferenceValue.name;
        }

        private EnemyCatalogAsset.EnemyArchetypeDefinition ResolveCatalogDefinition(Enemy enemy, out string prefabAssetPath)
        {
            prefabAssetPath = ResolvePrefabAssetPath(enemy != null ? enemy.gameObject : null);
            if (string.IsNullOrWhiteSpace(prefabAssetPath))
            {
                return null;
            }

            EnemyCatalogAsset catalogAsset = ResolveCatalogAsset();
            if (catalogAsset == null)
            {
                return null;
            }

            EnemyCatalogAsset.EnemyArchetypeDefinition[] definitions = catalogAsset.Definitions;
            for (int index = 0; index < definitions.Length; index++)
            {
                EnemyCatalogAsset.EnemyArchetypeDefinition definition = definitions[index];
                if (definition == null || definition.RuntimePrefab == null)
                {
                    continue;
                }

                string runtimePrefabPath = AssetDatabase.GetAssetPath(definition.RuntimePrefab);
                if (string.IsNullOrWhiteSpace(runtimePrefabPath))
                {
                    continue;
                }

                if (string.Equals(runtimePrefabPath, prefabAssetPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        private EnemyCatalogAsset ResolveCatalogAsset()
        {
            EnemyCatalogAsset catalogAsset = AssetDatabase.LoadAssetAtPath<EnemyCatalogAsset>(DefaultEnemyCatalogAssetPath);
            if (catalogAsset != null)
            {
                return catalogAsset;
            }

            string[] candidateGuids = AssetDatabase.FindAssets("t:EnemyCatalogAsset");
            for (int index = 0; index < candidateGuids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(candidateGuids[index]);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                catalogAsset = AssetDatabase.LoadAssetAtPath<EnemyCatalogAsset>(assetPath);
                if (catalogAsset != null)
                {
                    return catalogAsset;
                }
            }

            return null;
        }

        private static string ResolvePrefabAssetPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            string prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            if (!string.IsNullOrWhiteSpace(prefabAssetPath))
            {
                return prefabAssetPath;
            }

            prefabAssetPath = AssetDatabase.GetAssetPath(gameObject);
            return prefabAssetPath ?? string.Empty;
        }

        private static string BuildPassiveTraitSummary(EnemyCatalogAsset.EnemyArchetypeDefinition definition)
        {
            string summary = definition.IgnoresSlowEffects ? "免疫减速" : "会受到减速影响";

            if (definition.CanBeRepairedByMechanic)
            {
                summary += " / 可被机械师修理";
            }

            if (definition.ShieldAmount <= 0 &&
                definition.RepairAmount <= 0 &&
                !definition.EntersStealthAfterFirstDirectHit &&
                definition.SplitChildType == EnemyArchetypeId.None)
            {
                summary += " / 不需要额外主动机制模块";
            }

            return summary;
        }

        private static string ToYesNo(bool value)
        {
            return value ? "是" : "否";
        }

        private static void AppendMissingMechanicWarning(ref string currentMessage, bool shouldAppend, string message)
        {
            if (!shouldAppend)
            {
                return;
            }

            currentMessage += $"- {message}\n";
        }

        private static void AttachMissingCatalogModules(Enemy enemy, EnemyCatalogAsset.EnemyArchetypeDefinition definition)
        {
            if (enemy == null || definition == null)
            {
                return;
            }

            if (definition.EntersStealthAfterFirstDirectHit && enemy.GetComponent<EnemyStealthModule>() == null)
            {
                Undo.AddComponent<EnemyStealthModule>(enemy.gameObject);
            }

            if (definition.ShieldAmount > 0 && enemy.GetComponent<EnemyShieldAuraModule>() == null)
            {
                Undo.AddComponent<EnemyShieldAuraModule>(enemy.gameObject);
            }

            if (definition.RepairAmount > 0 && enemy.GetComponent<EnemyRepairModule>() == null)
            {
                Undo.AddComponent<EnemyRepairModule>(enemy.gameObject);
            }

            if (definition.SplitChildType != EnemyArchetypeId.None &&
                definition.SplitChildCount > 0 &&
                enemy.GetComponent<EnemySplitOnDeathModule>() == null)
            {
                Undo.AddComponent<EnemySplitOnDeathModule>(enemy.gameObject);
            }

            EditorUtility.SetDirty(enemy.gameObject);
            AssetDatabase.SaveAssets();
        }
    }
}
