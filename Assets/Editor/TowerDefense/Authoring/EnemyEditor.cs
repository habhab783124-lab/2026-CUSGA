using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `Enemy` 的作者检查器。
    ///
    /// 它主要帮助作者快速确认：
    /// - 主体渲染器有没有接上
    /// - 血条链路有没有接上
    /// - 视觉缩放根有没有和血条层级分开
    /// </summary>
    [CustomEditor(typeof(Enemy))]
    public sealed class EnemyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawReferenceSummary();
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
                $"Body Renderer: {bodyRenderer}\nVisual Scale Root: {scaleRoot}\nHealth Bar Root: {healthBarRoot}\nHealth Bar Fill: {healthBarFill}",
                MessageType.Info);

            if (bodyRenderer == "None" || healthBarRoot == "None" || healthBarFill == "None")
            {
                EditorGUILayout.HelpBox("Enemy 当前仍有视觉引用缺项。为了后续换正式美术更稳，建议把主体和血条链路都显式接齐。", MessageType.Warning);
            }
        }

        private string DescribeObject(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                return "None";
            }

            return property.objectReferenceValue.name;
        }
    }
}
