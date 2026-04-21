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
        [Header("Scene Ref")]
        [SerializeField] private SceneAsset sceneAsset;
#endif

        [SerializeField] private string sceneName = "SampleScene";
        [SerializeField] private string scenePath = "Assets/Scenes/SampleScene.unity";

        [Header("Display Copy")]
        [SerializeField] private string displayName = "LEVEL 01";
        [SerializeField] private string subtitle = "CURRENT TEST ROUTE";
        [SerializeField] [TextArea(2, 5)] private string description = "The current playable sample mission.";
        [SerializeField] private string statusLabel = "OPEN";

        [Header("Card Style")]
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Color accentColor = new Color(1f, 0.68f, 0.36f, 1f);
        [SerializeField] private bool interactable = true;

        public string SceneName => sceneName;
        public string ScenePath => scenePath;
        public string DisplayName => displayName;
        public string Subtitle => subtitle;
        public string Description => description;
        public string StatusLabel => statusLabel;
        public Sprite IconSprite => iconSprite;
        public Color AccentColor => accentColor;
        public bool Interactable => interactable;

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

    [SerializeField] private LevelEntry[] levels = Array.Empty<LevelEntry>();

    public LevelEntry[] Levels => levels ?? Array.Empty<LevelEntry>();

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
