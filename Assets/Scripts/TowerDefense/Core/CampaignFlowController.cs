using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// `CampaignFlowController` 是剧情段与塔防段之间的常驻流程管理器。
///
/// 它负责的事情非常聚焦：
/// 1. 持有当前激活的 `CampaignFlowAsset`
/// 2. 记录当前已经走到第几段
/// 3. 在不同场景之间加载“下一段内容”
///
/// 这里故意不做复杂存档、章节解锁或分支树，
/// 是因为当前需求只是先把“剧情横板插入塔防关卡”的主链接通。
/// 架构上先把这条最小主链稳定住，比一开始就做重系统更重要。
/// </summary>
public sealed class CampaignFlowController : MonoBehaviour
{
    private static CampaignFlowController s_instance; // 中文：实例

    [SerializeField] private CampaignFlowAsset activeCampaignAsset; // 中文：激活战役资产
    [SerializeField] private int currentStepIndex = -1; // 中文：当前步骤Index

    public static bool HasActiveCampaign => s_instance != null && s_instance.activeCampaignAsset != null && s_instance.currentStepIndex >= 0; // 中文：是否有激活战役

    public static CampaignFlowAsset ActiveCampaignAsset => s_instance != null ? s_instance.activeCampaignAsset : null; // 中文：激活战役资产

    public static int CurrentStepIndex => s_instance != null ? s_instance.currentStepIndex : -1; // 中文：当前步骤Index

    public static bool TryGetCurrentStep(out CampaignFlowAsset.CampaignStep step)
    {
        if (s_instance != null &&
            s_instance.activeCampaignAsset != null &&
            s_instance.activeCampaignAsset.TryGetStep(s_instance.currentStepIndex, out step))
        {
            return true;
        }

        step = null;
        return false;
    }

    public static string GetCurrentStepDisplayName()
    {
        return TryGetCurrentStep(out CampaignFlowAsset.CampaignStep step)
            ? step.DisplayName
            : string.Empty;
    }

    public static string GetCurrentContinuePrompt()
    {
        return TryGetCurrentStep(out CampaignFlowAsset.CampaignStep step)
            ? step.ContinuePrompt
            : "按 Enter / Space 继续。";
    }

    public static CampaignSegmentType GetCurrentStepType()
    {
        return TryGetCurrentStep(out CampaignFlowAsset.CampaignStep step)
            ? step.SegmentType
            : CampaignSegmentType.StoryInterlude;
    }

    public static bool BeginCampaign(CampaignFlowAsset campaignAsset)
    {
        if (campaignAsset == null || campaignAsset.StepCount == 0)
        {
            Debug.LogWarning("CampaignFlowController 无法开始流程：CampaignFlowAsset 为空或没有配置任何步骤。");
            return false;
        }

        CampaignFlowController controller = EnsureInstance();
        controller.activeCampaignAsset = campaignAsset;
        controller.currentStepIndex = 0;
        controller.LoadCurrentStep(
            campaignAsset.DefaultFadeOutToBlackDuration,
            campaignAsset.DefaultFadeInFromBlackDuration,
            campaignAsset.DefaultStartOpaqueOnLoad);
        return true;
    }

    public static bool AdvanceToNextStep()
    {
        if (!HasActiveCampaign)
        {
            return false;
        }

        float fadeOutSeconds = s_instance.activeCampaignAsset != null
            ? s_instance.activeCampaignAsset.DefaultFadeOutToBlackDuration
            : 0.75f;
        float fadeInSeconds = s_instance.activeCampaignAsset != null
            ? s_instance.activeCampaignAsset.DefaultFadeInFromBlackDuration
            : 0.75f;
        bool startOpaque = s_instance.activeCampaignAsset != null && s_instance.activeCampaignAsset.DefaultStartOpaqueOnLoad;
        return AdvanceToNextStep(fadeOutSeconds, fadeInSeconds, startOpaque);
    }

    /// <summary>
    /// 允许调用方显式指定这一次推进的淡出/淡入参数。
    ///
    /// 这一步的意义是把塔防关卡、剧情桥接器和占位剧情段统一到同一套切场体验上：
    /// - 场景推进仍然由 `CampaignFlowController` 负责
    /// - 但视觉过渡手感可以像 2D 横板剧情一样，统一走 `ScreenFadeTransition`
    /// </summary>
    public static bool AdvanceToNextStep(float fadeOutSeconds, float fadeInSeconds, bool startOpaque = false)
    {
        if (!HasActiveCampaign)
        {
            return false;
        }

        s_instance.currentStepIndex++;
        if (s_instance.activeCampaignAsset == null || s_instance.currentStepIndex >= s_instance.activeCampaignAsset.StepCount)
        {
            s_instance.CompleteCampaign(fadeOutSeconds, fadeInSeconds, startOpaque);
            return true;
        }

        s_instance.LoadCurrentStep(fadeOutSeconds, fadeInSeconds, startOpaque);
        return true;
    }

    /// <summary>
    /// 统一处理“推进下一段流程，或者退回到显式场景名”这条桥接逻辑。
    ///
    /// 这是这次收口场景切换主链的关键入口：
    /// - 如果当前已经有激活的 `CampaignFlowAsset`，就推进到下一步
    /// - 如果没有活动流程，就回退到调用方显式给出的下一个场景名
    ///
    /// 这样塔防关卡和 2D 横板剧情都能走同一条判断逻辑，
    /// 而不是各自再手写一份“有流程/没流程怎么办”的分支。
    /// </summary>
    public static bool AdvanceToNextStepOrLoadFallback(
        string fallbackSceneName,
        float fadeOutSeconds,
        float fadeInSeconds,
        bool startOpaque = false)
    {
        if (HasActiveCampaign)
        {
            return AdvanceToNextStep(fadeOutSeconds, fadeInSeconds, startOpaque);
        }

        if (string.IsNullOrWhiteSpace(fallbackSceneName))
        {
            return false;
        }

        Time.timeScale = 1f;
        PlaySceneTransition(fallbackSceneName, fadeOutSeconds, fadeInSeconds, startOpaque);
        return true;
    }

    public static void AbortCampaign()
    {
        if (s_instance == null)
        {
            return;
        }

        s_instance.activeCampaignAsset = null;
        s_instance.currentStepIndex = -1;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private static CampaignFlowController EnsureInstance()
    {
        if (s_instance != null)
        {
            return s_instance;
        }

        GameObject controllerObject = new GameObject("CampaignFlowController");
        s_instance = controllerObject.AddComponent<CampaignFlowController>();
        return s_instance;
    }

    private void LoadCurrentStep(float fadeOutSeconds, float fadeInSeconds, bool startOpaque)
    {
        if (activeCampaignAsset == null || !activeCampaignAsset.TryGetStep(currentStepIndex, out CampaignFlowAsset.CampaignStep step))
        {
            Debug.LogWarning("CampaignFlowController 无法加载当前步骤：当前索引无效。");
            return;
        }

        if (string.IsNullOrWhiteSpace(step.SceneName))
        {
            Debug.LogWarning("CampaignFlowController 当前步骤没有配置场景名。");
            return;
        }

        Time.timeScale = 1f;
        PlaySceneTransition(step.SceneName, fadeOutSeconds, fadeInSeconds, startOpaque);
    }

    private void CompleteCampaign(float fadeOutSeconds, float fadeInSeconds, bool startOpaque)
    {
        string completionSceneName = activeCampaignAsset != null ? activeCampaignAsset.CompletionSceneName : string.Empty;
        activeCampaignAsset = null;
        currentStepIndex = -1;

        Time.timeScale = 1f;
        if (!string.IsNullOrWhiteSpace(completionSceneName))
        {
            PlaySceneTransition(completionSceneName, fadeOutSeconds, fadeInSeconds, startOpaque);
        }
    }

    private static void PlaySceneTransition(string sceneName, float fadeOutSeconds, float fadeInSeconds, bool startOpaque)
    {
        ScreenFadeTransition.Play(
            sceneName,
            Mathf.Max(0f, fadeOutSeconds),
            Mathf.Max(0f, fadeInSeconds),
            startOpaque);
    }
}
