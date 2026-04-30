using UnityEngine;

/// <summary>
/// `StoryInterludePlaceholderController` 是当前阶段用于占位剧情横板段的极简控制器。
///
/// 这份脚本的目标非常朴素：
/// - 先用一个空场景占住“剧情段”这个结构位置
/// - 让流程链已经能在“剧情段 -> 塔防段 -> 剧情段”之间真实切换
/// - 等以后 2D 横板内容合并进来时，直接替换场景内容或继续复用这条流程接口
///
/// 当前它主要做两件事：
/// 1. 用最轻量的方式给运行时显示一层提示文案
/// 2. 在玩家按下继续键后，调用 `CampaignFlowController` 进入下一段
/// </summary>
public sealed class StoryInterludePlaceholderController : MonoBehaviour
{
    [Header("Placeholder Copy")]
    [SerializeField] private string fallbackTitle = "剧情过场占位"; // 中文：fallback标题
    [SerializeField] [TextArea(3, 6)] private string fallbackBody = "未来的 2D 横板剧情内容会放在这里。当前这个场景只负责先占住战役流程里“剧情段”的位置，让塔防关卡和剧情段之间能够正常切换。"; // 中文：fallback主体
    [SerializeField] private string fallbackContinuePrompt = "按 Enter / Space 继续进入下一段战斗。"; // 中文：fallback继续提示

    [Header("Input")]
    [SerializeField] private KeyCode primaryContinueKey = KeyCode.Return; // 中文：主继续Key
    [SerializeField] private KeyCode secondaryContinueKey = KeyCode.Space; // 中文：副继续Key
    [SerializeField] private bool allowLeftMouseClickContinue = true; // 中文：允许剩余MouseClick继续

    [Header("Scene Look")]
    [SerializeField] private Camera sceneCameraReference; // 中文：场景相机引用
    [SerializeField] private Color backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f); // 中文：背景颜色
    [SerializeField] private bool drawRuntimeOverlay = true; // 中文：draw运行时覆盖层

    private void OnEnable()
    {
        if (sceneCameraReference == null)
        {
            sceneCameraReference = Camera.main;
        }

        if (sceneCameraReference != null)
        {
            sceneCameraReference.clearFlags = CameraClearFlags.SolidColor;
            sceneCameraReference.backgroundColor = backgroundColor;
            sceneCameraReference.orthographic = true;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        bool pressedContinue = Input.GetKeyDown(primaryContinueKey) || Input.GetKeyDown(secondaryContinueKey);
        bool clickedToContinue = allowLeftMouseClickContinue && Input.GetMouseButtonDown(0);
        if (!pressedContinue && !clickedToContinue)
        {
            return;
        }

        if (!CampaignFlowController.AdvanceToNextStep())
        {
            Debug.LogWarning("StoryInterludePlaceholderController 没有检测到有效的活动流程，无法切到下一段。", this);
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !drawRuntimeOverlay)
        {
            return;
        }

        string title = CampaignFlowController.GetCurrentStepDisplayName();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = fallbackTitle;
        }

        string continuePrompt = CampaignFlowController.GetCurrentContinuePrompt();
        if (string.IsNullOrWhiteSpace(continuePrompt))
        {
            continuePrompt = fallbackContinuePrompt;
        }

        Rect panelRect = new Rect(40f, 40f, 860f, 240f);
        GUI.Box(panelRect, string.Empty);
        GUILayout.BeginArea(new Rect(panelRect.x + 24f, panelRect.y + 24f, panelRect.width - 48f, panelRect.height - 48f));
        GUILayout.Label(title);
        GUILayout.Space(12f);
        GUILayout.Label(fallbackBody);
        GUILayout.FlexibleSpace();
        GUILayout.Label(continuePrompt);
        GUILayout.EndArea();
    }
}
