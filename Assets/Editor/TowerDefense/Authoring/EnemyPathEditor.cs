using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `EnemyPath` 的自定义检查器。
    ///
    /// 它主要强化两件事：
    /// - 把“路径点根节点”这件事做成更明确的作者工作流
    /// - 给路径表现层提供一个显式刷新入口
    /// </summary>
    [CustomEditor(typeof(EnemyPath))]
    public sealed class EnemyPathEditor : UnityEditor.Editor
    {
        private SerializedProperty _waypointRootReferenceProperty; // 中文：路径点根节点引用Property

        private void OnEnable()
        {
            _waypointRootReferenceProperty = serializedObject.FindProperty("waypointRootReference");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EnemyPath enemyPath = (EnemyPath)target;

            string waypointRootName = enemyPath.WaypointRoot != null ? enemyPath.WaypointRoot.name : "(Direct Children)";
            bool proceduralOverlay = serializedObject.FindProperty("proceduralReadabilityOverlay")?.boolValue ?? true;
            string readabilityMode = proceduralOverlay ? "程序化覆盖层" : "仅使用作者根节点";
            EditorGUILayout.HelpBox($"路径点数量：{enemyPath.WaypointCount}\n路径点根节点：{waypointRootName}\n可读性模式：{readabilityMode}", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("指定 / 创建 Waypoints 根节点"))
            {
                AssignOrCreateWaypointRoot(enemyPath);
                serializedObject.Update();
            }

            if (GUILayout.Button("刷新路径表现"))
            {
                enemyPath.EditorRefreshAuthoringState();
                EditorUtility.SetDirty(enemyPath);
                EditorSceneManager.MarkSceneDirty(enemyPath.gameObject.scene);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8f);

            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }

        private void AssignOrCreateWaypointRoot(EnemyPath enemyPath)
        {
            Transform existingRoot = enemyPath.transform.Find("Waypoints");
            if (existingRoot == null)
            {
                GameObject rootObject = new GameObject("Waypoints");
                Undo.RegisterCreatedObjectUndo(rootObject, "创建 Waypoints 根节点");
                existingRoot = rootObject.transform;
                existingRoot.SetParent(enemyPath.transform, false);
                existingRoot.localPosition = Vector3.zero;
                existingRoot.localRotation = Quaternion.identity;
                existingRoot.localScale = Vector3.one;
            }

            for (int childIndex = enemyPath.transform.childCount - 1; childIndex >= 0; childIndex--)
            {
                Transform child = enemyPath.transform.GetChild(childIndex);
                if (child == null || child == existingRoot || child.name == "__PathReadability")
                {
                    continue;
                }

                Undo.SetTransformParent(child, existingRoot, "把路径点移入 Waypoints 根节点");
            }

            _waypointRootReferenceProperty.objectReferenceValue = existingRoot;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            enemyPath.EditorRefreshAuthoringState();
            EditorUtility.SetDirty(enemyPath);
            EditorSceneManager.MarkSceneDirty(enemyPath.gameObject.scene);
        }
    }
}
