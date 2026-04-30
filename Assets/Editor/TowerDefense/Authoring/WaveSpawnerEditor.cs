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
                $"地图：{DescribeObject(mapProperty)}\n" +
                $"波次目录：{DescribeObject(waveCatalogProperty)}\n" +
                $"敌人目录：{DescribeObject(enemyCatalogProperty)}\n" +
                $"敌人后备 Prefab：{DescribeObject(enemyPrototypeProperty)}\n" +
                $"敌人根节点：{DescribeObject(enemyRootProperty)}\n" +
                $"路线预告提前：{(routePreviewProperty != null ? routePreviewProperty.floatValue.ToString("0.00") + " 秒" : "0 秒")}\n" +
                $"通关后继续战役：{(continueCampaignProperty != null && continueCampaignProperty.boolValue ? "是" : "否")}";

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
                    $"当前波次作者工作流已切到资产主链。\n资产：{waveCatalogAsset.name}\n波次数量：{waveCatalogAsset.Waves.Length}",
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
                return "未设置";
            }

            return property.objectReferenceValue.name;
        }
    }
}
