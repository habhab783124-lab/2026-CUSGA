using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// `StorySceneStepController` 是 2D 横板剧情段和塔防关卡之间的最小桥接器。
///
/// 这里故意把职责压得很薄，只做三件事：
/// 1. 观察当前剧情场景里“是否已经完成了必要对话”。
/// 2. 在条件满足后，向玩家显示“可以继续”的提示。
/// 3. 最终统一调用 `CampaignFlowController.AdvanceToNextStep()`，进入下一段流程。
///
/// 这样做的原因很简单：
/// - 2D 场景负责“交互和叙事”
/// - 塔防场景负责“战斗和资源循环”
/// - 场景切换权尽量只放在流程控制器里
///
/// 当前版本先做一个最小可跑链路：
/// - 只要场景里所有要求完成的 `NpcDialogue` 都已经结束，
///   就允许玩家按 Enter / Space / 左键继续到下一段。
/// </summary>
public sealed class StorySceneStepController : MonoBehaviour
{
    [Header("Required Dialogue")]
    [Tooltip("需要完成的对话组件列表。全部完成后，才允许进入下一段。若为空，会在运行时自动收集场景中的所有 NpcDialogue。")]
    [SerializeField] private NpcDialogue[] requiredDialogues = new NpcDialogue[0];
    [Tooltip("是否在 Awake 时自动从场景里收集所有 NpcDialogue。当前用于尽量减少第一次接场景时的手工接线成本。")]
    [SerializeField] private bool autoCollectDialogues = true;

    [Header("Advance Input")]
    [SerializeField] private KeyCode primaryAdvanceKey = KeyCode.Return;
    [SerializeField] private KeyCode secondaryAdvanceKey = KeyCode.Space;
    [SerializeField] private bool allowLeftMouseClickAdvance = true;

    [Header("Fallback Next Scene")]
    [Tooltip("如果当前场景不是通过 CampaignFlowController 进入的，就回退到这个场景名。")]
    [SerializeField] private string fallbackNextSceneName = "SampleScene";

    [Header("Overlay")]
    [SerializeField] private bool showOverlayPrompt = true;
    [SerializeField] private string waitingPrompt = "先完成当前剧情对话。";
    [SerializeField] private string continuePrompt = "按 Enter / Space 继续进入下一关塔防。";

    private bool _advanceUnlocked;
    private bool _hasAnyDialogueRequirement;

    private void Awake()
    {
        if (autoCollectDialogues && (requiredDialogues == null || requiredDialogues.Length == 0))
        {
            requiredDialogues = FindObjectsOfType<NpcDialogue>(includeInactive: true);
        }

        _hasAnyDialogueRequirement = requiredDialogues != null && requiredDialogues.Length > 0;
    }

    private void Update()
    {
        RefreshAdvanceState();

        if (!_advanceUnlocked)
        {
            return;
        }

        bool pressedAdvance = Input.GetKeyDown(primaryAdvanceKey) || Input.GetKeyDown(secondaryAdvanceKey);
        bool clickedAdvance = allowLeftMouseClickAdvance && Input.GetMouseButtonDown(0);
        if (!pressedAdvance && !clickedAdvance)
        {
            return;
        }

        AdvanceToNextStep();
    }

    private void OnGUI()
    {
        if (!showOverlayPrompt)
        {
            return;
        }

        string prompt = _advanceUnlocked ? continuePrompt : waitingPrompt;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        GUI.Box(new Rect(24f, 24f, 520f, 56f), string.Empty);
        GUI.Label(new Rect(40f, 40f, 488f, 24f), prompt);
    }

    /// <summary>
    /// 每帧都重新检查一次推进条件。
    ///
    /// 这里不缓存“某个 NPC 曾经完成过”之类的中间态，
    /// 因为当前 `NpcDialogue` 的完成标志已经体现在 `CanInteract` 上：
    /// - 还能交互：说明这段对话还没完成
    /// - 不能交互：说明这段对话已经结束
    /// </summary>
    private void RefreshAdvanceState()
    {
        if (!_hasAnyDialogueRequirement)
        {
            _advanceUnlocked = true;
            return;
        }

        for (int index = 0; index < requiredDialogues.Length; index++)
        {
            NpcDialogue dialogue = requiredDialogues[index];
            if (dialogue == null)
            {
                continue;
            }

            if (dialogue.CanInteract)
            {
                _advanceUnlocked = false;
                return;
            }
        }

        _advanceUnlocked = true;
    }

    private void AdvanceToNextStep()
    {
        if (CampaignFlowController.HasActiveCampaign)
        {
            if (!CampaignFlowController.AdvanceToNextStep())
            {
                Debug.LogWarning("StorySceneStepController 无法推进战役流程，当前 CampaignFlowController 没有成功切到下一步。", this);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(fallbackNextSceneName))
        {
            Debug.LogWarning("StorySceneStepController 没有活动战役流程，且 fallbackNextSceneName 为空，无法继续。", this);
            return;
        }

        SceneManager.LoadScene(fallbackNextSceneName, LoadSceneMode.Single);
    }
}
