using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `BattlefieldMapDefinition` 的自定义检查器。
    ///
    /// 目标很明确：
    /// - 让地图作者一打开 Inspector 就能看到当前地图骨架摘要
    /// - 让“收集场景引用”变成一个明确按钮，而不是要求用户手动维护数组
    /// - 让缺项警告在编辑阶段就更直观
    /// </summary>
    [CustomEditor(typeof(BattlefieldMapDefinition))]
    public sealed class BattlefieldMapDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BattlefieldMapDefinition mapDefinition = (BattlefieldMapDefinition)target;

            EditorGUILayout.HelpBox(mapDefinition.BuildAuthoringSummary(), MessageType.Info);
            DrawConfigurationWarnings(mapDefinition);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("收集场景引用"))
            {
                bool changed = mapDefinition.CollectSceneReferences();
                EditorUtility.SetDirty(mapDefinition);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(mapDefinition.gameObject.scene);
                }

                serializedObject.Update();
            }

            if (GUILayout.Button("输出地图摘要"))
            {
                Debug.Log($"[BattlefieldMapDefinition] {mapDefinition.BuildAuthoringSummary()}", mapDefinition);
                mapDefinition.LogConfigurationWarnings(mapDefinition);
            }

            EditorGUILayout.Space(8f);
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawConfigurationWarnings(BattlefieldMapDefinition mapDefinition)
        {
            bool hasWarning = false;
            string warningMessage = string.Empty;

            if (mapDefinition.BuildZone == null)
            {
                hasWarning = true;
                warningMessage += "- 缺少 BuildZone 引用。\n";
            }

            if (!mapDefinition.HasAnyValidSpawnGate())
            {
                hasWarning = true;
                warningMessage += "- 当前没有有效的 EnemySpawnGate。\n";
            }

            if (!mapDefinition.TryGetPrimaryDefensePoint(out _))
            {
                hasWarning = true;
                warningMessage += "- 当前没有有效的 DefensePointFlag。\n";
            }

            if (!hasWarning)
            {
                EditorGUILayout.HelpBox("地图骨架当前已具备 BuildZone、出怪口和防御点基础结构。", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox($"当前地图还有这些作者缺项：\n{warningMessage}".TrimEnd(), MessageType.Warning);
        }
    }
}

