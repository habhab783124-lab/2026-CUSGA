using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 第二章过场：NPC 使用中心气泡展开/关闭并打字显示，Player 使用气泡对话；
/// 仅鼠标左键推进；结束后执行全屏淡入淡出切换下一场景。
/// </summary>
public sealed class Chapter2CutsceneController : MonoBehaviour
{
    [Header("字体")]
    [SerializeField] private TMP_FontAsset dialogueFontAsset;

    [Header("对白")]
    [SerializeField] private bool useScriptDefaultLines = true;
    [SerializeField] private string dialogueId = "chapter2_intro";
    [SerializeField] private List<DialogueLine> lines = new();

    [Header("玩家气泡")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private DialogueBubbleView playerBubblePrefab;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Vector3 playerBubbleScreenOffset = new(240f, -220f, 10f);

    [Header("NPC 中心气泡")]
    [SerializeField] private Chapter2CenterBubbleController centerBubbleController;

    [Header("交互")]
    [SerializeField] private int mouseButton = 0;
    [SerializeField] private PlayerInteractor2D playerInteractor;

    [Header("切换至下一章（全屏淡入淡出）")]
    [SerializeField] private string nextSceneName = "chapter3";
    [SerializeField] private float fadeOutToBlackDuration = 0.75f;
    [SerializeField] private float fadeInFromBlackDuration = 0.75f;

    private bool waitingForAdvanceClick;
    private int currentLineIndex = -1;
    private bool isConversationActive;
    private bool transitionQueued;

    private void Awake()
    {
        ApplyDefaultLinesIfNeeded();
        EnsureDialogueRunner();
        EnsurePlayerAnchor();
        ResolveCenterBubbleController();
        UpdatePlayerBubbleAnchor();
        HideNpcTextImmediate();
        ResolvePlayerInteractor();
        PrepareSceneActors();
    }

    private void OnValidate()
    {
        ApplyDefaultLinesIfNeeded();
        if (!Application.isPlaying)
        {
            EnsurePlayerAnchor();
            ResolveCenterBubbleController();
            UpdatePlayerBubbleAnchor();
        }
    }

    private void Start()
    {
        if (playerInteractor != null)
        {
            playerInteractor.SetInteractionInputEnabled(false);
            playerInteractor.SetInteractionPromptVisible(false);
            if (playerInteractor.Motor != null)
            {
                playerInteractor.Motor.SetMovementLocked(true);
            }
        }

        StartDialogue();
    }

    private void Update()
    {
        UpdatePlayerBubbleAnchor();

        if (!isConversationActive || transitionQueued || !Input.GetMouseButtonDown(mouseButton))
        {
            return;
        }

        if (centerBubbleController != null && centerBubbleController.IsTyping)
        {
            centerBubbleController.CompleteTyping();
            return;
        }

        if (!waitingForAdvanceClick)
        {
            return;
        }

        waitingForAdvanceClick = false;
        AdvanceDialogue();
    }

    private void ApplyDefaultLinesIfNeeded()
    {
        if (!useScriptDefaultLines)
        {
            return;
        }

        IReadOnlyList<DialogueLine> source = DialogueScripts.Get(dialogueId);
        lines = source != null ? new List<DialogueLine>(source) : new List<DialogueLine>();
    }

    private void EnsureDialogueRunner()
    {
        dialogueRunner = dialogueRunner != null ? dialogueRunner : GetComponent<DialogueRunner>();
        if (dialogueRunner == null)
        {
            dialogueRunner = gameObject.AddComponent<DialogueRunner>();
        }

        if (playerBubblePrefab == null)
        {
            playerBubblePrefab = Resources.Load<DialogueBubbleView>("DialogueBubble");
        }

        dialogueRunner.SetBubblePrefab(playerBubblePrefab);
        dialogueRunner.ConfigureBottomLayout(false, Vector2.zero, Vector2.zero, 1f, false);
    }

    private void EnsurePlayerAnchor()
    {
        if (playerBubbleAnchor == null)
        {
            playerBubbleAnchor = CreateChildAnchor("Chapter2PlayerBubbleAnchor");
        }
    }

    private void ResolveCenterBubbleController()
    {
        if (centerBubbleController != null)
        {
            return;
        }

        centerBubbleController = FindObjectOfType<Chapter2CenterBubbleController>(true);
    }

    private Transform CreateChildAnchor(string anchorName)
    {
        Transform existing = transform.Find(anchorName);
        if (existing != null)
        {
            return existing;
        }

        GameObject anchor = new GameObject(anchorName);
        anchor.transform.SetParent(transform, false);
        return anchor.transform;
    }

    private void ResolvePlayerInteractor()
    {
        if (playerInteractor == null)
        {
            playerInteractor = FindObjectOfType<PlayerInteractor2D>();
        }
    }

    private void PrepareSceneActors()
    {
        GameObject center = GameObject.Find("Center");
        if (center != null)
        {
            NpcDialogue npcDialogue = center.GetComponent<NpcDialogue>();
            if (npcDialogue != null)
            {
                npcDialogue.enabled = false;
            }
        }
    }

    private void StartDialogue()
    {
        currentLineIndex = -1;
        isConversationActive = lines != null && lines.Count > 0;
        waitingForAdvanceClick = false;
        transitionQueued = false;
        HideNpcTextImmediate();

        if (dialogueRunner != null)
        {
            dialogueRunner.HideDialogueBubble();
        }

        if (!isConversationActive)
        {
            return;
        }

        AdvanceDialogue();
    }

    private void AdvanceDialogue()
    {
        currentLineIndex++;
        if (lines == null || currentLineIndex >= lines.Count)
        {
            EndDialogue();
            return;
        }

        ShowLine(lines[currentLineIndex]);
    }

    private void ShowLine(DialogueLine line)
    {
        waitingForAdvanceClick = false;

        if (line.speaker == DialogueSpeaker.NPC)
        {
            if (dialogueRunner != null)
            {
                dialogueRunner.HideDialogueBubble();
            }

            if (centerBubbleController != null)
            {
                centerBubbleController.ShowNpcLine(line.text, OnNpcLineFinished);
            }
            else
            {
                waitingForAdvanceClick = true;
            }

            return;
        }

        HideNpcTextImmediate();
        if (dialogueRunner != null)
        {
            dialogueRunner.PlayConversation(null, playerBubbleAnchor, null, new List<DialogueLine> { line }, OnPlayerLineFinished);
        }
        else
        {
            waitingForAdvanceClick = true;
        }
    }

    private void OnNpcLineFinished()
    {
        waitingForAdvanceClick = true;
    }

    private void OnPlayerLineFinished()
    {
        waitingForAdvanceClick = true;
    }

    private void EndDialogue()
    {
        isConversationActive = false;
        waitingForAdvanceClick = false;
        currentLineIndex = -1;
        HideNpcTextImmediate();

        if (dialogueRunner != null)
        {
            dialogueRunner.HideDialogueBubble();
        }

        if (!transitionQueued)
        {
            transitionQueued = true;
            StartCoroutine(TransitionToNextSceneRoutine());
        }
    }

    private IEnumerator TransitionToNextSceneRoutine()
    {
        yield return null;
        ScreenFadeTransition.Play(nextSceneName, fadeOutToBlackDuration, fadeInFromBlackDuration, startOpaque: false);
    }

    private void HideNpcTextImmediate()
    {
        if (centerBubbleController != null)
        {
            centerBubbleController.SetClosedImmediate();
        }
    }

    private void UpdatePlayerBubbleAnchor()
    {
        Camera cam = Camera.main;
        if (cam == null || playerBubbleAnchor == null)
        {
            return;
        }

        Vector3 screenPoint = new Vector3(
            Screen.width * 0.5f + playerBubbleScreenOffset.x,
            Screen.height * 0.5f + playerBubbleScreenOffset.y,
            Mathf.Max(0.01f, playerBubbleScreenOffset.z));

        playerBubbleAnchor.position = cam.ScreenToWorldPoint(screenPoint);
    }
}
