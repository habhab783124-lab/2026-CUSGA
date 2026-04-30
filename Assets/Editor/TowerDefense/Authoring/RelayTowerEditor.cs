using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `RelayTower` 的作者检查器。
    ///
    /// 主要强化两件事：
    /// - 作者一眼看见当前供电核心参数和视觉入口
    /// - Play 模式下直接看到当前运行负载和剩余容量
    /// </summary>
    [CustomEditor(typeof(RelayTower))]
    public sealed class RelayTowerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            RelayTower relayTower = (RelayTower)target;
            EditorGUILayout.HelpBox(
                $"供电范围：{relayTower.SupplyRange:0.0}\n供电容量：{relayTower.SupplyCapacity}\n视觉根节点：{GetVisualRootName()}",
                MessageType.Info);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    $"继电器 #{relayTower.RelayNumber}\n当前负载：{relayTower.CurrentAssignedLoad}\n剩余容量：{relayTower.RemainingCapacity}",
                    MessageType.None);
            }

            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }

        private string GetVisualRootName()
        {
            SerializedProperty property = serializedObject.FindProperty("visualRootReference");
            if (property == null || property.objectReferenceValue == null)
            {
                return "（根对象）";
            }

            return property.objectReferenceValue.name;
        }
    }
}
