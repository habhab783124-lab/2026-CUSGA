using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `Level04RingGuide` 的 Scene 视图辅助绘制器。
    ///
    /// 它只做作者可读性，不介入玩法：
    /// - 在 Scene 里直接标出外环 / 中环 / 内环
    /// - 用和塔位颜色一致的文字帮助快速理解布局层次
    /// </summary>
    [CustomEditor(typeof(Level04RingGuide))]
    public sealed class Level04RingGuideEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "这个组件只服务于关卡作者可读性。\n它会在 Scene 视图里标出外环 / 中环 / 内环，帮助你继续手改塔位层次。",
                MessageType.Info);

            DrawDefaultInspector();
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawSceneLabels(Level04RingGuide guide, GizmoType gizmoType)
        {
            if (guide == null || !guide.ShowSceneLabels)
            {
                return;
            }

            DrawLabel(guide.OuterRingLabelWorldPosition, guide.OuterRingLabel, guide.OuterRingColor);
            DrawLabel(guide.MidRingLabelWorldPosition, guide.MidRingLabel, guide.MidRingColor);
            DrawLabel(guide.InnerRingLabelWorldPosition, guide.InnerRingLabel, guide.InnerRingColor);
        }

        private static void DrawLabel(Vector3 worldPosition, string text, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(worldPosition, Vector3.forward, 0.28f);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = color },
                fontSize = 12
            };

            Handles.Label(worldPosition + new Vector3(0.22f, 0.22f, 0f), text, style);
        }
    }
}
