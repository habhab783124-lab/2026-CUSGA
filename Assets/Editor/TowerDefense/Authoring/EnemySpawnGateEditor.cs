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
        private SerializedProperty _readabilityRootReferenceProperty; // 中文：可读性根节点引用Property
        private SerializedProperty _autoCreateReadabilityRootProperty; // 中文：自动创建可读性根节点Property

        private void OnEnable()
        {
            _readabilityRootReferenceProperty = serializedObject.FindProperty("readabilityRootReference");
            _autoCreateReadabilityRootProperty = serializedObject.FindProperty("autoCreateReadabilityRoot");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EnemySpawnGate spawnGate = (EnemySpawnGate)target;
            string routeName = spawnGate.EnemyPath != null ? spawnGate.EnemyPath.name : "未设置";
            string defensePointName = spawnGate.TargetDefensePoint != null ? spawnGate.TargetDefensePoint.name : "未设置";
            bool proceduralMarker = serializedObject.FindProperty("proceduralReadabilityMarker")?.boolValue ?? true;
            string readabilityMode = proceduralMarker ? "程序化标记" : "仅使用作者根节点";

            EditorGUILayout.HelpBox(
                $"出怪口：{spawnGate.DisplayName}\n敌人路线：{routeName}\n目标防御点：{defensePointName}\n可读性模式：{readabilityMode}",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("指定 / 创建可读性根节点"))
            {
                AssignOrCreateReadabilityRoot(spawnGate, "__SpawnGateReadability");
            }

            if (GUILayout.Button("刷新标记"))
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
                Undo.RegisterCreatedObjectUndo(rootObject, "创建出怪口可读性根节点");
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
