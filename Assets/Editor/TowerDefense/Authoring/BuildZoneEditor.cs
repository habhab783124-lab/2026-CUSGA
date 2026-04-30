using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `BuildZone` 的作者检查器。
    ///
    /// 这次它的核心价值是把“不规则建造区”变成显式工作流：
    /// - 可选 `ZoneShapes` 根节点
    /// - 可一键收集多个 Collider2D
    /// - 可直接看到当前收集到多少个形状
    /// </summary>
    [CustomEditor(typeof(BuildZone))]
    public sealed class BuildZoneEditor : UnityEditor.Editor
    {
        private SerializedProperty _zoneShapeRootReferenceProperty; // 中文：区域形状根节点引用Property

        private void OnEnable()
        {
            _zoneShapeRootReferenceProperty = serializedObject.FindProperty("zoneShapeRootReference");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BuildZone buildZone = (BuildZone)target;
            EditorGUILayout.HelpBox(buildZone.BuildAuthoringSummary(), MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("指定 / 创建 ZoneShapes 根节点"))
            {
                AssignOrCreateZoneShapeRoot(buildZone);
            }

            if (GUILayout.Button("收集形状碰撞体"))
            {
                bool changed = buildZone.CollectZoneShapeColliders();
                EditorUtility.SetDirty(buildZone);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(buildZone.gameObject.scene);
                }

                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "推荐地图工作流：\n1. 在 BuildZone 下创建 `ZoneShapes`\n2. 在下面摆多个 Box / Polygon / Composite / CircleCollider2D\n3. 点击 `Collect Zone Shape Colliders`\n4. 用这些形状的并集作为真正可建造区",
                MessageType.None);

            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }

        private void AssignOrCreateZoneShapeRoot(BuildZone buildZone)
        {
            Transform existingRoot = buildZone.transform.Find("ZoneShapes");
            if (existingRoot == null)
            {
                GameObject rootObject = new GameObject("ZoneShapes");
                Undo.RegisterCreatedObjectUndo(rootObject, "创建 BuildZone 形状根节点");
                existingRoot = rootObject.transform;
                existingRoot.SetParent(buildZone.transform, false);
                existingRoot.localPosition = Vector3.zero;
                existingRoot.localRotation = Quaternion.identity;
                existingRoot.localScale = Vector3.one;
            }

            _zoneShapeRootReferenceProperty.objectReferenceValue = existingRoot;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(buildZone);
            EditorSceneManager.MarkSceneDirty(buildZone.gameObject.scene);
        }
    }
}
