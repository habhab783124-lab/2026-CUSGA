using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `EnemySpawnGate` 的自定义检查器。
    ///
    /// 目标是把“出怪口表现层”也做成显式作者工作流：
    /// - 先明确看见当前路径和防御点是否接好了
    /// - 需要时一键创建可读性根
    /// - 再显式刷新表现层，而不是只能靠脚本静默自动补
    /// </summary>
    [CustomEditor(typeof(EnemySpawnGate))]
    public sealed class EnemySpawnGateEditor : UnityEditor.Editor
    {
        private SerializedProperty _readabilityRootReferenceProperty;
        private SerializedProperty _autoCreateReadabilityRootProperty;

        private void OnEnable()
        {
            _readabilityRootReferenceProperty = serializedObject.FindProperty("readabilityRootReference");
            _autoCreateReadabilityRootProperty = serializedObject.FindProperty("autoCreateReadabilityRoot");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EnemySpawnGate spawnGate = (EnemySpawnGate)target;
            string routeName = spawnGate.EnemyPath != null ? spawnGate.EnemyPath.name : "None";
            string defensePointName = spawnGate.TargetDefensePoint != null ? spawnGate.TargetDefensePoint.name : "None";

            EditorGUILayout.HelpBox(
                $"Gate: {spawnGate.DisplayName}\nEnemyPath: {routeName}\nTargetDefensePoint: {defensePointName}",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Assign / Create Readability Root"))
            {
                AssignOrCreateReadabilityRoot(spawnGate, "__SpawnGateReadability");
            }

            if (GUILayout.Button("Refresh Marker"))
            {
                spawnGate.EditorRefreshAuthoringState();
                EditorUtility.SetDirty(spawnGate);
                EditorSceneManager.MarkSceneDirty(spawnGate.gameObject.scene);
            }
            EditorGUILayout.EndHorizontal();

            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }

        private void AssignOrCreateReadabilityRoot(EnemySpawnGate spawnGate, string rootName)
        {
            Transform existingRoot = spawnGate.transform.Find(rootName);
            if (existingRoot == null)
            {
                GameObject rootObject = new GameObject(rootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Spawn Gate Readability Root");
                existingRoot = rootObject.transform;
                existingRoot.SetParent(spawnGate.transform, false);
                existingRoot.localPosition = Vector3.zero;
                existingRoot.localRotation = Quaternion.identity;
                existingRoot.localScale = Vector3.one;
            }

            _readabilityRootReferenceProperty.objectReferenceValue = existingRoot;
            if (_autoCreateReadabilityRootProperty != null)
            {
                _autoCreateReadabilityRootProperty.boolValue = false;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            spawnGate.EditorRefreshAuthoringState();
            EditorUtility.SetDirty(spawnGate);
            EditorSceneManager.MarkSceneDirty(spawnGate.gameObject.scene);
        }
    }
}
