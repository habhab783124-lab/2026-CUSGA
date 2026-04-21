using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `WaveSpawner` 的作者检查器。
    ///
    /// 重点是把这几个最容易接错的点直接显示出来：
    /// - 地图定义
    /// - 敌人 Prefab
    /// - 敌人根节点
    /// - 波次数量
    /// - 路线预告提前量
    /// </summary>
    [CustomEditor(typeof(WaveSpawner))]
    public sealed class WaveSpawnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSummary();
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSummary()
        {
            SerializedProperty mapProperty = serializedObject.FindProperty("battlefieldMapReference");
            SerializedProperty waveCatalogProperty = serializedObject.FindProperty("waveCatalogAsset");
            SerializedProperty enemyCatalogProperty = serializedObject.FindProperty("enemyCatalogAsset");
            SerializedProperty enemyPrototypeProperty = serializedObject.FindProperty("enemyPrototypeReference");
            SerializedProperty enemyRootProperty = serializedObject.FindProperty("enemyRootReference");
            SerializedProperty wavesProperty = serializedObject.FindProperty("waves");
            SerializedProperty routePreviewProperty = serializedObject.FindProperty("routePreviewLeadTime");

            string message =
                $"Map: {DescribeObject(mapProperty)}\n" +
                $"Wave Catalog: {DescribeObject(waveCatalogProperty)}\n" +
                $"Enemy Catalog: {DescribeObject(enemyCatalogProperty)}\n" +
                $"Enemy Prefab: {DescribeObject(enemyPrototypeProperty)}\n" +
                $"Enemy Root: {DescribeObject(enemyRootProperty)}\n" +
                $"Wave Count: {(wavesProperty != null ? wavesProperty.arraySize.ToString() : "0")}\n" +
                $"Route Preview Lead: {(routePreviewProperty != null ? routePreviewProperty.floatValue.ToString("0.00") + "s" : "0s")}";

            EditorGUILayout.HelpBox(message, MessageType.Info);

            if (mapProperty == null || mapProperty.objectReferenceValue == null ||
                enemyPrototypeProperty == null || enemyPrototypeProperty.objectReferenceValue == null ||
                enemyRootProperty == null || enemyRootProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("WaveSpawner 当前仍有关键引用缺项。由于这条链已经不再做运行时兜底创建，建议在场景里直接补齐。", MessageType.Warning);
            }

            if (waveCatalogProperty == null || waveCatalogProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("WaveSpawner 当前没有接 WaveCatalogAsset，仍会回退到组件内的旧波次数组。建议尽量切到共享波次资产主链。", MessageType.Warning);
            }

            if (enemyCatalogProperty == null || enemyCatalogProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("WaveSpawner 当前没有接 EnemyCatalogAsset，多怪物系统将无法正常工作。", MessageType.Error);
            }
        }

        private static string DescribeObject(SerializedProperty property)
        {
            if (property == null || property.objectReferenceValue == null)
            {
                return "None";
            }

            return property.objectReferenceValue.name;
        }
    }
}
