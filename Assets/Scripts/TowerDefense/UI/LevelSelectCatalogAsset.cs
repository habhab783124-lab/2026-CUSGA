using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// `LevelSelectCatalogAsset` 把关卡选择页的关卡数据从控制器里拆成独立资产。
///
/// 这样以后：
/// - 新增关卡
/// - 改卡片文案
/// - 改开放状态
/// - 调整图标和强调色
/// 都不需要再去碰 `LevelSelectController` 本体。
/// </summary>
[CreateAssetMenu(
    fileName = "LevelSelectCatalog",
    menuName = "Tower Defense/UI/Level Select Catalog")]
public sealed class LevelSelectCatalogAsset : ScriptableObject
{
    [Serializable]
    public sealed class LevelEntry
    {
#if UNITY_EDITOR
        [Header("场景引用")]
        [SerializeField, InspectorName("场景资产")] private SceneAsset sceneAsset; // 中文：场景资产
#endif

        [SerializeField, InspectorName("场景名")] private string sceneName = "SampleScene"; // 中文：场景名称
        [SerializeField, InspectorName("场景路径")] private string scenePath = "Assets/Scenes/SampleScene.unity"; // 中文：场景路径

        [Header("显示文案")]
        [SerializeField, InspectorName("显示名称")] private string displayName = "第一关"; // 中文：显示名称
        [SerializeField, InspectorName("副标题")] private string subtitle = "当前测试路线"; // 中文：副标题
        [SerializeField, TextArea(2, 5), InspectorName("描述")] private string description = "当前可游玩的样例关卡。"; // 中文：描述
        [SerializeField, InspectorName("状态标签")] private string statusLabel = "可进入"; // 中文：状态标签

        [Header("卡片样式")]
        [SerializeField, InspectorName("图标")] private Sprite iconSprite; // 中文：图标精灵
        [SerializeField, InspectorName("强调色")] private Color accentColor = new Color(1f, 0.68f, 0.36f, 1f); // 中文：accent颜色
        [SerializeField, InspectorName("可交互")] private bool interactable = true; // 中文：可交互

        public string SceneName => sceneName; // 中文：场景名称
        public string ScenePath => scenePath; // 中文：场景路径
        public string DisplayName => displayName; // 中文：显示名称
        public string Subtitle => subtitle; // 中文：副标题
        public string Description => description; // 中文：描述
        public string StatusLabel => statusLabel; // 中文：状态标签
        public Sprite IconSprite => iconSprite; // 中文：图标精灵
        public Color AccentColor => accentColor; // 中文：Accent颜色
        public bool Interactable => interactable; // 中文：可交互

#if UNITY_EDITOR
        public bool SyncSceneReference()
        {
            if (sceneAsset == null)
            {
                if (!string.IsNullOrWhiteSpace(scenePath) && string.IsNullOrWhiteSpace(sceneName))
                {
                    sceneName = Path.GetFileNameWithoutExtension(scenePath);
                    return true;
                }

                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(sceneAsset);
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            bool changed = assetPath != scenePath || assetName != sceneName;
            scenePath = assetPath;
            sceneName = assetName;
            return changed;
        }
#endif
    }

    [SerializeField, InspectorName("关卡列表")] private LevelEntry[] levels = Array.Empty<LevelEntry>(); // 中文：等级列表

    public LevelEntry[] Levels => levels ?? Array.Empty<LevelEntry>(); // 中文：等级列表

#if UNITY_EDITOR
    public bool SyncSceneReferences()
    {
        if (levels == null)
        {
            return false;
        }

        bool changed = false;
        for (int index = 0; index < levels.Length; index++)
        {
            if (levels[index] != null && levels[index].SyncSceneReference())
            {
                changed = true;
            }
        }

        return changed;
    }
#endif
}
