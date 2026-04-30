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
        controller.LoadCurrentStep();
        return true;
    }

    public static bool AdvanceToNextStep()
    {
        if (!HasActiveCampaign)
        {
            return false;
        }

        s_instance.currentStepIndex++;
        if (s_instance.activeCampaignAsset == null || s_instance.currentStepIndex >= s_instance.activeCampaignAsset.StepCount)
        {
            s_instance.CompleteCampaign();
            return true;
        }

        s_instance.LoadCurrentStep();
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

    private void LoadCurrentStep()
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
        SceneManager.LoadScene(step.SceneName, LoadSceneMode.Single);
    }

    private void CompleteCampaign()
    {
        string completionSceneName = activeCampaignAsset != null ? activeCampaignAsset.CompletionSceneName : string.Empty;
        activeCampaignAsset = null;
        currentStepIndex = -1;

        Time.timeScale = 1f;
        if (!string.IsNullOrWhiteSpace(completionSceneName))
        {
            SceneManager.LoadScene(completionSceneName, LoadSceneMode.Single);
        }
    }
}
