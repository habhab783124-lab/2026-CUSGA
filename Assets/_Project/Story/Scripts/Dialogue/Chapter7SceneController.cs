using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueRunner))]
public sealed class Chapter7SceneController : StoryCutsceneControllerBase
{
    [Header("Dialogue")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string dialogueId = "chapter7_intro";
    [SerializeField] private bool playOnStart = true;

    [Header("Dialogue Bubble")]
    [SerializeField] private DialogueBubbleView dialogueBubblePrefab;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform npcBubbleAnchor;

    [Header("Bubble Offsets")]
    [SerializeField] private Vector3 playerBubbleAnchorOffset = new Vector3(-2f, 1.8f, 0f);
    [SerializeField] private Vector3 npcBubbleAnchorOffset = new Vector3(2f, 1.8f, 0f);

    [Header("Actors")]
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private Transform player;
    [SerializeField] private Transform npc;

    [Header("Opening")]
    [SerializeField] private float dialogueDelayOnStart = 0.1f;

    private bool hasPlayed;
    private Transform runtimePlayerBubbleAnchor;
    private Transform runtimeNpcBubbleAnchor;

    protected override void Reset()
    {
        base.Reset();
        dialogueRunner = GetComponent<DialogueRunner>();
        playerInteractor = ResolvePlayerInteractor(playerInteractor);
    }

    protected override void Awake()
    {
        base.Awake();
        dialogueRunner = ResolveDialogueRunner(dialogueRunner);
        ResolveReferences();
        EnsureRuntimeAnchors();
        UpdateRuntimeAnchors();
    }

    private void LateUpdate()
    {
        UpdateRuntimeAnchors();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (hasPlayed)
        {
            return;
        }

        dialogueRunner = ResolveDialogueRunner(dialogueRunner);
        ResolveReferences();
        EnsureRuntimeAnchors();
        UpdateRuntimeAnchors();

        if (!ValidateSetup())
        {
            return;
        }

        hasPlayed = true;
        StartCoroutine(PlayRoutine());
    }

    private void ResolveReferences()
    {
        playerInteractor = ResolvePlayerInteractor(playerInteractor);
        player = ResolvePlayerTransform(player);
        npc = ResolveActor(npc, "shen", "Shen");
    }

    private void EnsureRuntimeAnchors()
    {
        EnsureRuntimeAnchor(ref runtimePlayerBubbleAnchor, "Chapter7PlayerBubbleAnchorRuntime");
        EnsureRuntimeAnchor(ref runtimeNpcBubbleAnchor, "Chapter7NpcBubbleAnchorRuntime");
    }

    private void UpdateRuntimeAnchors()
    {
        UpdateRuntimeAnchor(
            runtimePlayerBubbleAnchor,
            playerBubbleAnchor,
            player,
            playerBubbleAnchorOffset,
            "player",
            "Player");
        UpdateRuntimeAnchor(
            runtimeNpcBubbleAnchor,
            npcBubbleAnchor,
            npc,
            npcBubbleAnchorOffset,
            "shen",
            "Shen");
    }

    private bool ValidateSetup()
    {
        if (!TryConfigureDialogueRunner(ref dialogueRunner, dialogueBubblePrefab, nameof(Chapter7SceneController)))
        {
            return false;
        }

        if (runtimePlayerBubbleAnchor == null || runtimeNpcBubbleAnchor == null)
        {
            Debug.LogError("Chapter7SceneController: Failed to create runtime dialogue anchors.", this);
            return false;
        }

        return true;
    }

    private IEnumerator PlayRoutine()
    {
        if (dialogueDelayOnStart > 0f)
        {
            yield return new WaitForSeconds(dialogueDelayOnStart);
        }

        IReadOnlyList<DialogueLine> lines = Chapter7.Get(dialogueId);
        if (lines == null || lines.Count == 0)
        {
            yield break;
        }

        IList<DialogueLine> resolvedLines = lines as IList<DialogueLine> ?? new List<DialogueLine>(lines);
        dialogueRunner.PlayConversation(
            playerInteractor,
            runtimePlayerBubbleAnchor,
            runtimeNpcBubbleAnchor,
            resolvedLines);
    }
}
