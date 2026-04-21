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
        private SerializedProperty _waypointRootReferenceProperty;

        private void OnEnable()
        {
            _waypointRootReferenceProperty = serializedObject.FindProperty("waypointRootReference");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EnemyPath enemyPath = (EnemyPath)target;

            string waypointRootName = enemyPath.WaypointRoot != null ? enemyPath.WaypointRoot.name : "(Direct Children)";
            EditorGUILayout.HelpBox($"Waypoint Count: {enemyPath.WaypointCount}\nWaypoint Root: {waypointRootName}", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Assign / Create Waypoints Root"))
            {
                AssignOrCreateWaypointRoot(enemyPath);
                serializedObject.Update();
            }

            if (GUILayout.Button("Refresh Path Visuals"))
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
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Waypoints Root");
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

                Undo.SetTransformParent(child, existingRoot, "Move Path Waypoint Under Waypoints Root");
            }

            _waypointRootReferenceProperty.objectReferenceValue = existingRoot;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            enemyPath.EditorRefreshAuthoringState();
            EditorUtility.SetDirty(enemyPath);
            EditorSceneManager.MarkSceneDirty(enemyPath.gameObject.scene);
        }
    }
}
