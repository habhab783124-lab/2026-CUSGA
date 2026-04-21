using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// `CampaignSegmentType` 描述一段流程在结构上属于哪一类内容。
///
/// 当前先收两种：
/// - `StoryInterlude`：用于剧情横板或剧情过场
/// - `TowerDefenseEncounter`：用于真正的塔防战斗关卡
///
/// 这样做的意义不只是“记一下类型”，
/// 更是为了以后别的系统能根据类型决定：
/// - 要不要显示某种提示
/// - 这一段是否允许直接继续
/// - 后续统计里这段属于剧情还是战斗
/// </summary>
public enum CampaignSegmentType
{
    StoryInterlude,
    TowerDefenseEncounter
}

/// <summary>
/// `CampaignFlowAsset` 把“剧情段和塔防段怎样交错排列”收成一份可编辑资产。
///
/// 当前项目的目标不是马上做复杂章节系统，
/// 而是先把最核心的流程主链搭好：
/// - 剧情场景
/// - 塔防关卡
/// - 再回剧情场景
/// - 再进下一个塔防关卡
///
/// 所以后续无论 2D 横板团队怎么实现剧情，
/// 只要他们最后也落成场景，并接到这份流程资产里，
/// 主链就能继续复用。
/// </summary>
[CreateAssetMenu(
    fileName = "StoryTowerDefenseCampaign",
    menuName = "Tower Defense/Campaign/Campaign Flow")]
public sealed class CampaignFlowAsset : ScriptableObject
{
    [Serializable]
    public sealed class CampaignStep
    {
#if UNITY_EDITOR
        [Header("Scene Ref")]
        [SerializeField] private SceneAsset sceneAsset;
#endif

        [SerializeField] private CampaignSegmentType segmentType = CampaignSegmentType.StoryInterlude;
        [SerializeField] private string sceneName = "StoryInterludePlaceholder";
        [SerializeField] private string scenePath = "Assets/Scenes/StoryInterludePlaceholder.unity";
        [SerializeField] private string displayName = "Story Segment";
        [SerializeField] [TextArea(2, 5)] private string designerNote = "Describe what this segment is meant to communicate.";
        [SerializeField] private string continuePrompt = "Press Enter / Space to continue.";

        public CampaignSegmentType SegmentType => segmentType;
        public string SceneName => sceneName;
        public string ScenePath => scenePath;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? sceneName : displayName;
        public string DesignerNote => designerNote ?? string.Empty;
        public string ContinuePrompt => string.IsNullOrWhiteSpace(continuePrompt) ? "Press Enter / Space to continue." : continuePrompt;

#if UNITY_EDITOR
        /// <summary>
        /// 允许作者直接拖场景资产。
        /// 运行时只使用稳定的字符串字段，避免真正打包后依赖编辑器类型。
        /// </summary>
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

    [Header("Identity")]
    [SerializeField] private string campaignId = "MainCampaign";
    [SerializeField] private string completionSceneName = "MainMenu";

    [Header("Segment Order")]
    [SerializeField] private CampaignStep[] steps = Array.Empty<CampaignStep>();

    public string CampaignId => string.IsNullOrWhiteSpace(campaignId) ? name : campaignId;
    public string CompletionSceneName => completionSceneName;
    public int StepCount => steps != null ? steps.Length : 0;
    public CampaignStep[] Steps => steps ?? Array.Empty<CampaignStep>();

    public bool TryGetStep(int stepIndex, out CampaignStep step)
    {
        if (steps != null && stepIndex >= 0 && stepIndex < steps.Length && steps[stepIndex] != null)
        {
            step = steps[stepIndex];
            return true;
        }

        step = null;
        return false;
    }

#if UNITY_EDITOR
    public bool SyncSceneReferences()
    {
        if (steps == null)
        {
            return false;
        }

        bool changed = false;
        for (int index = 0; index < steps.Length; index++)
        {
            if (steps[index] != null && steps[index].SyncSceneReference())
            {
                changed = true;
            }
        }

        return changed;
    }
#endif
}
