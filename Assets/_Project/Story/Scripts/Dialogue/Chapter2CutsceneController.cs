using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第二章过场：开场仅 NPC 对话框闪烁后出现并自动第一句；仅鼠标左键推进；无 NPC 贴图、气泡无 tail；
/// 结束后对话框「关机」式闪烁渐隐，再加载下一场景（全屏淡入淡出仍用 ScreenFadeTransition）。
/// </summary>
public sealed class Chapter2CutsceneController : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string dialogueId = "chapter2_intro";
    [SerializeField] private Transform npcAnchor;
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private string nextSceneName = "chapter3";
    [Header("切换至下一章（全屏淡入淡出）")]
    [SerializeField] private float fadeOutToBlackDuration = 0.75f;
    [SerializeField] private float fadeInFromBlackDuration = 0.75f;
    [Header("开场：对话框闪烁")]
    [SerializeField] private float introBubbleFlashDuration = 0.12f;
    [SerializeField] private float introBubbleFlashPeakAlpha = 0.92f;
    [Header("结束：对话框关机感")]
    [SerializeField] private int shutdownBubbleWhiteFlickerCount = 2;
    [SerializeField] private float shutdownBubbleFlickerSegmentDuration = 0.05f;
    [SerializeField] private float shutdownBubbleFadeOutDuration = 0.22f;

    private void Reset()
    {
        dialogueRunner = GetComponent<DialogueRunner>();
    }

    private void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    private IEnumerator BootstrapRoutine()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (npcAnchor == null)
        {
            var go = GameObject.Find("Center");
            if (go != null)
            {
                npcAnchor = go.transform;
            }
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindObjectOfType<PlayerInteractor2D>();
        }

        if (dialogueRunner == null || playerInteractor == null || npcAnchor == null)
        {
            Debug.LogError("Chapter2CutsceneController: 缺少 DialogueRunner / PlayerInteractor2D / Center。", this);
            yield break;
        }

        var npcDialogue = npcAnchor.GetComponent<NpcDialogue>();
        if (npcDialogue != null)
        {
            npcDialogue.enabled = false;
        }

        foreach (var r in npcAnchor.GetComponentsInChildren<SpriteRenderer>(true))
        {
            r.enabled = false;
        }

        playerInteractor.SetInteractionInputEnabled(false);
        playerInteractor.SetInteractionPromptVisible(false);

        IReadOnlyList<DialogueLine> read = DialogueScripts.Get(dialogueId);
        if (read == null || read.Count == 0)
        {
            Debug.LogError($"Chapter2CutsceneController: 对白 id「{dialogueId}」无内容。", this);
            yield break;
        }

        IList<DialogueLine> lines = read as IList<DialogueLine> ?? new List<DialogueLine>(read);

        dialogueRunner.PlayConversation(
            playerInteractor,
            playerInteractor.transform,
            npcAnchor,
            lines,
            OnConversationComplete,
            b => b.SetShowTail(false),
            deferFirstLineUntilExternal: true,
            suppressBubbleHideOnConversationEnd: true);

        DialogueBubbleView bubble = dialogueRunner.BubbleView;
        if (bubble != null)
        {
            yield return bubble.StartCoroutine(bubble.FlashAppearChromeRoutine(introBubbleFlashDuration, introBubbleFlashPeakAlpha));
        }

        dialogueRunner.PlayDeferredFirstLine();
    }

    private void OnConversationComplete()
    {
        if (playerInteractor != null && playerInteractor.Motor != null)
        {
            playerInteractor.Motor.SetMovementLocked(true);
        }

        CutsceneCoroutineUtility.StartPreferredOrFallback(this, playerInteractor, OutroShutdownRoutine());
    }

    private IEnumerator OutroShutdownRoutine()
    {
        DialogueBubbleView bubble = dialogueRunner != null ? dialogueRunner.BubbleView : null;
        if (bubble != null)
        {
            yield return bubble.StartCoroutine(
                bubble.ShutdownChromeRoutine(
                    shutdownBubbleWhiteFlickerCount,
                    shutdownBubbleFlickerSegmentDuration,
                    shutdownBubbleFadeOutDuration));
        }

        if (dialogueRunner != null)
        {
            dialogueRunner.HideDialogueBubble();
        }

        ScreenFadeTransition.Play(nextSceneName, fadeOutToBlackDuration, fadeInFromBlackDuration, startOpaque: true);
        yield return null;
    }
}
