using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 主菜单控制器的作者检查器。
    ///
    /// 重点不是重做页面，
    /// 而是把你现在最常需要的作者操作直接放到 Inspector 上：
    /// - 物化默认页面骨架
    /// - 把作者默认主题同步到当前场景对象
    /// - 快速看到哪些场景引用还没接好
    /// </summary>
    [CustomEditor(typeof(MainMenuController))]
    public sealed class MainMenuControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSceneReferenceSummary();
            DrawAuthoringActions();
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSceneReferenceSummary()
        {
            DrawCampaignFlowSummary();

            string missing = string.Empty;
            AppendMissing(ref missing, "sceneCamera", "主相机");
            AppendMissing(ref missing, "mainCanvas", "Canvas");
            AppendMissing(ref missing, "eventSystem", "EventSystem");
            AppendMissing(ref missing, "menuRoot", "菜单根节点");
            AppendMissing(ref missing, "startButton", "开始按钮");
            AppendMissing(ref missing, "titleText", "标题文本");

            if (string.IsNullOrWhiteSpace(missing))
            {
                EditorGUILayout.HelpBox("主菜单当前关键场景引用已接齐。你可以直接在 Scene 中继续调布局和样式。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"主菜单当前还有这些关键缺项：\n{missing}".TrimEnd(), MessageType.Warning);
        }

        private void DrawCampaignFlowSummary()
        {
            SerializedProperty useCampaignFlowProperty = serializedObject.FindProperty("useCampaignFlowOnStart");
            SerializedProperty campaignFlowAssetProperty = serializedObject.FindProperty("campaignFlowAsset");

            bool useCampaignFlow = useCampaignFlowProperty != null && useCampaignFlowProperty.boolValue;
            string assetName = campaignFlowAssetProperty != null && campaignFlowAssetProperty.objectReferenceValue != null
                ? campaignFlowAssetProperty.objectReferenceValue.name
                : "未设置";

            if (useCampaignFlow)
            {
                EditorGUILayout.HelpBox($"当前主菜单启动时将优先进入剧情-塔防流程。\nCampaign 资产：{assetName}", MessageType.Info);
                if (assetName == "未设置")
                {
                    EditorGUILayout.HelpBox("已经启用剧情流程模式，但还没有接 CampaignFlowAsset。当前按下 Start 时会回退到原始场景入口。", MessageType.Warning);
                }

                return;
            }

            EditorGUILayout.HelpBox($"当前主菜单仍然走旧的直接场景入口。\nCampaign 资产：{assetName}", MessageType.None);
        }

        private void DrawAuthoringActions()
        {
            MainMenuController controller = (MainMenuController)target;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("物化场景 UI"))
            {
                controller.EditorMaterializeDefaultSceneUi();
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
