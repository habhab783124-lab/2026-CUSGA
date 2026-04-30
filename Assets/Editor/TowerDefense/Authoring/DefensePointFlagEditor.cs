using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `DefensePointFlag` 的自定义检查器。
    ///
    /// 目的和出怪口一致：
    /// 把目标表现层从“脚本偷偷创建”进一步收成“作者可显式物化并刷新”的 Scene 工作流。
    /// </summary>
    [CustomEditor(typeof(DefensePointFlag))]
    public sealed class DefensePointFlagEditor : UnityEditor.Editor
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

            DefensePointFlag defensePoint = (DefensePointFlag)target;
            bool proceduralMarker = serializedObject.FindProperty("proceduralReadabilityMarker")?.boolValue ?? true;
            string readabilityMode = proceduralMarker ? "程序化标记" : "仅使用作者根节点";
            EditorGUILayout.HelpBox(
                $"防御点：{defensePoint.DisplayName}\n点位 ID：{defensePoint.PointId}\n可读性模式：{readabilityMode}",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("指定 / 创建可读性根节点"))
            {
                AssignOrCreateReadabilityRoot(defensePoint, "__DefensePointReadability");
            }

            if (GUILayout.Button("刷新标记"))
            {
                defensePoint.EditorRefreshAuthoringState();
                EditorUtility.SetDirty(defensePoint);
                EditorSceneManager.MarkSceneDirty(defensePoint.gameObject.scene);
            }
            EditorGUILayout.EndHorizontal();

            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }

        private void AssignOrCreateReadabilityRoot(DefensePointFlag defensePoint, string rootName)
        {
            Transform existingRoot = defensePoint.transform.Find(rootName);
            if (existingRoot == null)
            {
                GameObject rootObject = new GameObject(rootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "创建防御点可读性根节点");
                existingRoot = rootObject.transform;
                existingRoot.SetParent(defensePoint.transform, false);
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
            defensePoint.EditorRefreshAuthoringState();
            EditorUtility.SetDirty(defensePoint);
            EditorSceneManager.MarkSceneDirty(defensePoint.gameObject.scene);
        }
    }
}
