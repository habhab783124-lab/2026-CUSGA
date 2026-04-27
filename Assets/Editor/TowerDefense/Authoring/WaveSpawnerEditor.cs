using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `WaveSpawner` 的作者检查器。
    ///
    /// 这次重点不是再帮它兜底找资产，
    /// 而是把当前作者工作流说清楚：
    /// - 地图结构在 Scene 中做
    /// - 波次内容在 `WaveCatalogAsset` 中做
    /// - 敌人类型在 `EnemyCatalogAsset` 中做
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
            SerializedProperty routePreviewProperty = serializedObject.FindProperty("routePreviewLeadTime");
            SerializedProperty continueCampaignProperty = serializedObject.FindProperty("continueCampaignAfterClear");

            string message =
                $"Map: {DescribeObject(mapProperty)}\n" +
                $"Wave Catalog: {DescribeObject(waveCatalogProperty)}\n" +
                $"Enemy Catalog: {DescribeObject(enemyCatalogProperty)}\n" +
                $"Enemy Prefab Fallback: {DescribeObject(enemyPrototypeProperty)}\n" +
                $"Enemy Root: {DescribeObject(enemyRootProperty)}\n" +
                $"Route Preview Lead: {(routePreviewProperty != null ? routePreviewProperty.floatValue.ToString("0.00") + "s" : "0s")}\n" +
                $"Continue Campaign After Clear: {(continueCampaignProperty != null && continueCampaignProperty.boolValue ? "Yes" : "No")}";

            EditorGUILayout.HelpBox(message, MessageType.Info);

            if (mapProperty == null || mapProperty.objectReferenceValue == null ||
                enemyPrototypeProperty == null || enemyPrototypeProperty.objectReferenceValue == null ||
                enemyRootProperty == null || enemyRootProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "WaveSpawner 当前仍有关键场景引用缺项。由于这条链已经不再走运行时兜底创建，建议直接在场景 Inspector 里补齐。",
                    MessageType.Warning);
            }

            if (waveCatalogProperty == null || waveCatalogProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "WaveSpawner 当前没有接 WaveCatalogAsset。当前工作流已经默认按资产维护波次，建议先补上当前关卡自己的 WaveCatalog。",
                    MessageType.Warning);
            }
            else if (waveCatalogProperty.objectReferenceValue is WaveCatalogAsset waveCatalogAsset)
            {
                EditorGUILayout.HelpBox(
                    $"当前波次作者工作流已切到资产主链。\nAsset: {waveCatalogAsset.name}\nWave Count: {waveCatalogAsset.Waves.Length}",
                    MessageType.None);
            }

            if (enemyCatalogProperty == null || enemyCatalogProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "WaveSpawner 当前没有接 EnemyCatalogAsset，多怪物系统将无法正常工作。",
                    MessageType.Error);
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
