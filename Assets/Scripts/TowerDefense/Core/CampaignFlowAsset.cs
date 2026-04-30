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
    [InspectorName("剧情过场")]
    StoryInterlude,
    [InspectorName("塔防关卡")]
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
        [Header("场景引用")]
        [SerializeField, InspectorName("场景资产")] private SceneAsset sceneAsset; // 中文：场景资产
#endif

        [SerializeField, InspectorName("流程段类型")] private CampaignSegmentType segmentType = CampaignSegmentType.StoryInterlude; // 中文：segment类型
        [SerializeField, InspectorName("场景名")] private string sceneName = "StoryInterludePlaceholder"; // 中文：场景名称
        [SerializeField, InspectorName("场景路径")] private string scenePath = "Assets/Scenes/StoryInterludePlaceholder.unity"; // 中文：场景路径
        [SerializeField, InspectorName("显示名称")] private string displayName = "剧情段"; // 中文：显示名称
        [SerializeField, TextArea(2, 5), InspectorName("设计备注")] private string designerNote = "说明这一段流程想传达什么内容。"; // 中文：designer备注
        [SerializeField, InspectorName("继续提示")] private string continuePrompt = "按 Enter / Space 继续。"; // 中文：继续提示

        public CampaignSegmentType SegmentType => segmentType; // 中文：Segment类型
        public string SceneName => sceneName; // 中文：场景名称
        public string ScenePath => scenePath; // 中文：场景路径
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? sceneName : displayName; // 中文：显示名称
        public string DesignerNote => designerNote ?? string.Empty; // 中文：Designer备注
        public string ContinuePrompt => string.IsNullOrWhiteSpace(continuePrompt) ? "按 Enter / Space 继续。" : continuePrompt; // 中文：继续提示

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

    [Header("标识")]
    [SerializeField, InspectorName("战役 ID")] private string campaignId = "MainCampaign"; // 中文：战役标识
    [SerializeField, InspectorName("完成后返回场景")] private string completionSceneName = "MainMenu"; // 中文：完成场景名称

    [Header("流程顺序")]
    [SerializeField, InspectorName("流程段列表")] private CampaignStep[] steps = Array.Empty<CampaignStep>(); // 中文：步骤列表

    public string CampaignId => string.IsNullOrWhiteSpace(campaignId) ? name : campaignId; // 中文：战役标识
    public string CompletionSceneName => completionSceneName; // 中文：完成场景名称
    public int StepCount => steps != null ? steps.Length : 0; // 中文：步骤数量
    public CampaignStep[] Steps => steps ?? Array.Empty<CampaignStep>(); // 中文：步骤列表

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
