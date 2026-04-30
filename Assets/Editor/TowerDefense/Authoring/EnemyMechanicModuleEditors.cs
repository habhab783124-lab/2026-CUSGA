using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 这一组检查器不是为了“隐藏参数”，而是为了把敌人机制模块的作者入口整理得更清楚。
    ///
    /// 当前敌人模块都遵循同一条工作流：
    /// 1. 默认吃 `EnemyCatalogAsset` 的全局参数
    /// 2. 勾选 `useLocalOverrides` 后，当前 prefab 上的模块参数优先生效
    ///
    /// 所以这组检查器最核心的职责，是把“当前到底走哪条参数来源”明确说清楚，
    /// 避免作者打开 Inspector 时看到一堆字段，却不确定它们现在有没有真的生效。
    /// </summary>
    internal static class EnemyMechanicModuleEditorUtility
    {
        public static void DrawSourceSection(
            SerializedObject serializedObject,
            string catalogSummary,
            string localSummary,
            params string[] localPropertyNames)
        {
            SerializedProperty useLocalOverridesProperty = serializedObject.FindProperty("useLocalOverrides");
            if (useLocalOverridesProperty == null)
            {
                DrawRemainingProperties(serializedObject);
                return;
            }

            EditorGUILayout.PropertyField(useLocalOverridesProperty);
            EditorGUILayout.Space(4f);

            if (useLocalOverridesProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    $"当前 prefab 正在使用本地覆盖参数。\n{localSummary}",
                    MessageType.Info);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("本地覆盖参数", EditorStyles.boldLabel);
                    DrawProperties(serializedObject, localPropertyNames);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"当前 prefab 正在使用 EnemyCatalogAsset 中的默认参数。\n{catalogSummary}",
                    MessageType.None);

                using (new EditorGUI.DisabledScope(true))
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("本地覆盖预览", EditorStyles.boldLabel);
                    DrawProperties(serializedObject, localPropertyNames);
                }
            }
        }

        private static void DrawProperties(SerializedObject serializedObject, params string[] propertyNames)
        {
            for (int index = 0; index < propertyNames.Length; index++)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyNames[index]);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, includeChildren: true);
                }
            }
        }

        private static void DrawRemainingProperties(SerializedObject serializedObject)
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (iterator.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, includeChildren: true);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(iterator, includeChildren: true);
                }

                enterChildren = false;
            }
        }
    }

    /// <summary>
    /// 隐身模块检查器重点回答两件事：
    /// - 当前是否走目录默认值，还是当前 prefab 本地覆盖
    /// - 当地覆盖启用后，隐身持续、显形持续和透明度倍率分别是多少
    /// </summary>
    [CustomEditor(typeof(EnemyStealthModule))]
    public sealed class EnemyStealthModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "隐身模块负责首次直接受击后的隐身、被探测后的临时显形，以及目标锁定可见性。",
                MessageType.Info);

            EnemyMechanicModuleEditorUtility.DrawSourceSection(
                serializedObject,
                catalogSummary: "目录字段：EntersStealthAfterFirstDirectHit / StealthDuration / SignalRevealDuration / HiddenAlpha",
                localSummary: "你现在可以直接在当前敌人 prefab 上单独微调隐身机制。",
                "localStealthEnabled",
                "localStealthDuration",
                "localRevealDuration",
                "localHiddenAlpha");

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// 护盾光环模块检查器重点突出：
    /// - 护盾值
    /// - 作用半径
    /// - 刷新间隔
    /// </summary>
    [CustomEditor(typeof(EnemyShieldAuraModule))]
    public sealed class EnemyShieldAuraModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "护盾光环模块会周期性扫描附近敌人，并把护盾值抬到指定下限。",
                MessageType.Info);

            EnemyMechanicModuleEditorUtility.DrawSourceSection(
                serializedObject,
                catalogSummary: "目录字段：ShieldAmount / ShieldAuraRadius / ShieldRefreshInterval",
                localSummary: "你现在可以直接在当前敌人 prefab 上单独微调护盾光环参数。",
                "localShieldAuraEnabled",
                "localShieldAmount",
                "localShieldRadius",
                "localRefreshInterval");

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// 修理模块检查器把“修多少、修多远、多久修一次”放到最直观的位置。
    /// </summary>
    [CustomEditor(typeof(EnemyRepairModule))]
    public sealed class EnemyRepairModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "修理模块会周期性寻找最近的可修理友军，并恢复其生命值。",
                MessageType.Info);

            EnemyMechanicModuleEditorUtility.DrawSourceSection(
                serializedObject,
                catalogSummary: "目录字段：RepairAmount / RepairRadius / RepairCooldown",
                localSummary: "你现在可以直接在当前敌人 prefab 上单独微调修理行为。",
                "localRepairEnabled",
                "localRepairAmount",
                "localRepairRadius",
                "localRepairCooldown");

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// 死亡分裂模块检查器把“分裂成谁、分裂几个、刷在多大范围内”集中展示。
    /// </summary>
    [CustomEditor(typeof(EnemySplitOnDeathModule))]
    public sealed class EnemySplitOnDeathModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "死亡分裂模块会在宿主销毁前生成子怪，并继续沿当前路径前进。",
                MessageType.Info);

            EnemyMechanicModuleEditorUtility.DrawSourceSection(
                serializedObject,
                catalogSummary: "目录字段：SplitChildType / SplitChildCount / SplitSpawnRadius",
                localSummary: "你现在可以直接在当前敌人 prefab 上单独微调死亡分裂参数。",
                "localSplitEnabled",
                "localSplitChildType",
                "localSplitChildCount",
                "localSplitSpawnRadius");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
