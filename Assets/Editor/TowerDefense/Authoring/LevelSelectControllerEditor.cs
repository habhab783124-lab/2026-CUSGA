using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 关卡选择页控制器的作者检查器。
    ///
    /// 这一版重点增强两件事：
    /// - 直接告诉作者当前是不是已经切到 `LevelSelectCatalogAsset` 主链
    /// - 把页面物化和作者默认值同步动作显式放到 Inspector 上
    /// </summary>
    [CustomEditor(typeof(LevelSelectController))]
    public sealed class LevelSelectControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawCatalogStatus();
            DrawSceneReferenceSummary();
            DrawAuthoringActions();
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCatalogStatus()
        {
            SerializedProperty catalogProperty = serializedObject.FindProperty("levelCatalogAsset");
            if (catalogProperty != null && catalogProperty.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox("当前关卡选择页已经优先走 LevelSelectCatalogAsset 主链。后续改关卡卡片数据，建议直接改这份资产。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("当前没有接 LevelSelectCatalogAsset，仍会回退到控制器内的旧关卡数组。建议尽量接上共享目录资产。", MessageType.Warning);
        }

        private void DrawSceneReferenceSummary()
        {
            string missing = string.Empty;
            AppendMissing(ref missing, "sceneCamera", "主相机");
            AppendMissing(ref missing, "mainCanvas", "Canvas");
            AppendMissing(ref missing, "eventSystem", "EventSystem");
            AppendMissing(ref missing, "pageRoot", "页面根节点");
            AppendMissing(ref missing, "cardsRoot", "卡片根节点");
            AppendMissing(ref missing, "backButton", "返回按钮");

            if (string.IsNullOrWhiteSpace(missing))
            {
                EditorGUILayout.HelpBox("关卡选择页当前关键场景引用已接齐。", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox($"关卡选择页当前还有这些关键缺项：\n{missing}".TrimEnd(), MessageType.Warning);
        }

        private void DrawAuthoringActions()
        {
            LevelSelectController controller = (LevelSelectController)target;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("物化场景 UI"))
            {
                controller.EditorMaterializeSceneUi();
                EditorUtility.SetDirty(controller);
            }

            if (GUILayout.Button("应用作者设置到场景"))
            {
                controller.EditorApplyAuthoringToScene();
                EditorUtility.SetDirty(controller);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6f);
        }

        private void AppendMissing(ref string currentMessage, string propertyName, string displayName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null)
            {
                return;
            }

            currentMessage += $"- {displayName}\n";
        }
    }
}
