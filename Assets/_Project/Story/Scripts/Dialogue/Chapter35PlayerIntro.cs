using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Chapter35PlayerIntro : MonoBehaviour
{
    [Header("Move In")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float moveSpeed = 2.8f;
    [SerializeField] private float extraLeftBeyondCamera = 1.2f;
    [SerializeField] private float arriveEpsilon = 0.02f;

    [Header("Target")]
    [SerializeField] private Transform targetPoint;

    [Header("Dialogue Bubble")]
    [SerializeField] private DialogueBubbleView dialogueBubblePrefab;
    [SerializeField] private float dialogueDelayAfterIntro = 1f;
    [SerializeField] private int advanceMouseButton = 0;
    [SerializeField] private float chapter35SecondsPerChar = 0.03f;
    [SerializeField] private Vector3 speakerABubbleScreenOffset = new(480f, -250f, 10f);
    [SerializeField] private Vector3 speakerBBubbleScreenOffset = new(480f, -120f, 10f);
    [SerializeField] private Vector3 chapter35BubbleWorldOffset = new(0f, 0f, 0f);
    [SerializeField] private Vector2 chapter35BubbleContentPadding = new(0.72f, 0.48f);
    [SerializeField] private float chapter35BubbleMaxWidth = 8.4f;
    [SerializeField] private float chapter35BubbleMinWidth = 3.2f;
    [SerializeField] private float chapter35BubbleMinHeight = 1.55f;
    [SerializeField] private float chapter35BubbleFontSize = 42f;

    [Header("Exit")]
    [SerializeField] private float playerExitMoveSpeed = 3.5f;
    [SerializeField] private float playerOffscreenMarginWorld = 1.5f;

    private Vector3 targetPosition;
    private Coroutine introRoutine;
    private Coroutine exitRoutine;
    private Transform speakerAAnchor;
    private Transform speakerBAnchor;
    private DialogueBubbleView speakerABubble;
    private DialogueBubbleView speakerBBubble;
    private readonly List<DialogueLine> queuedLines = new();
    private int dialogueIndex;
    private bool dialogueSequenceActive;
    private bool waitingForExitClick;

    private void Awake()
    {
        targetPosition = targetPoint != null ? targetPoint.position : transform.position;
        EnsureDialogueBubblePrefab();
        EnsureDialogueAnchors();
        BuildDialogueLines();
    }

    private void Start()
    {
        if (!playOnStart)
        {
            return;
        }

        introRoutine = StartCoroutine(PlayIntroRoutine());
    }

    private void Update()
    {
        UpdateDialogueAnchors();
        UpdateBubbleFollowTargets();

        if (!Input.GetMouseButtonDown(advanceMouseButton))
        {
            return;
        }

        if (dialogueSequenceActive)
        {
            DialogueBubbleView activeBubble = GetActiveBubble();
            if (activeBubble != null && activeBubble.IsTyping)
            {
                activeBubble.SkipTyping();
                return;
            }

            AdvanceDialogue();
            return;
        }

        if (!waitingForExitClick)
        {
            return;
        }

        waitingForExitClick = false;
        HideAllBubbles();

        if (exitRoutine != null)
        {
            StopCoroutine(exitRoutine);
        }

        exitRoutine = StartCoroutine(PlayerExitRoutine());
    }

    public void PlayIntro()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
        }

        introRoutine = StartCoroutine(PlayIntroRoutine());
    }

    private IEnumerator PlayIntroRoutine()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            yield break;
        }

        targetPosition = targetPoint != null ? targetPoint.position : targetPosition;

        float halfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : 5f;
        float startX = cam.transform.position.x - halfWidth - extraLeftBeyondCamera;
        transform.position = new Vector3(startX, targetPosition.y, targetPosition.z);

        while (Vector3.Distance(transform.position, targetPosition) > arriveEpsilon)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition;
        introRoutine = null;

        if (dialogueDelayAfterIntro > 0f)
        {
            yield return new WaitForSeconds(dialogueDelayAfterIntro);
        }

        StartDialogueSequence();
    }

    private void StartDialogueSequence()
    {
        if (dialogueBubblePrefab == null || queuedLines.Count == 0)
        {
            return;
        }

        EnsureDialogueAnchors();
        EnsureBubbleInstances();
        UpdateDialogueAnchors();
        UpdateBubbleFollowTargets();

        dialogueIndex = 0;
        dialogueSequenceActive = true;
        waitingForExitClick = false;
        ShowCurrentDialogueLine();
    }

    private void AdvanceDialogue()
    {
        dialogueIndex++;
        if (dialogueIndex >= queuedLines.Count)
        {
            EndDialogueSequence();
            return;
        }

        ShowCurrentDialogueLine();
    }

    private void ShowCurrentDialogueLine()
    {
        if (dialogueIndex < 0 || dialogueIndex >= queuedLines.Count)
        {
            EndDialogueSequence();
            return;
        }

        DialogueLine line = queuedLines[dialogueIndex];
        DialogueBubbleView activeBubble = line.speaker == DialogueSpeaker.Player ? speakerABubble : speakerBBubble;
        DialogueBubbleView inactiveBubble = line.speaker == DialogueSpeaker.Player ? speakerBBubble : speakerABubble;

        if (activeBubble == null)
        {
            EndDialogueSequence();
            return;
        }

        if (inactiveBubble != null)
        {
            inactiveBubble.Show(false);
        }

        activeBubble.Show(false);
        activeBubble.ClearText();
        activeBubble.Show(true);
        activeBubble.TypeLine(line.text, chapter35SecondsPerChar, line.emphasis);
    }

    private void EndDialogueSequence()
    {
        dialogueSequenceActive = false;
        waitingForExitClick = true;
    }

    private IEnumerator PlayerExitRoutine()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            yield break;
        }

        float halfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : 5f;
        float exitX = cam.transform.position.x + halfWidth + playerOffscreenMarginWorld;

        while (transform.position.x < exitX)
        {
            transform.position += Vector3.right * (playerExitMoveSpeed * Time.deltaTime);
            yield return null;
        }

        exitRoutine = null;
    }

    private void HideAllBubbles()
    {
        if (speakerABubble != null)
        {
            speakerABubble.Show(false);
        }

        if (speakerBBubble != null)
        {
            speakerBBubble.Show(false);
        }
    }

    private void EnsureDialogueBubblePrefab()
    {
        if (dialogueBubblePrefab == null)
        {
            dialogueBubblePrefab = Resources.Load<DialogueBubbleView>("DialogueBubble");
        }
    }

    private void EnsureDialogueAnchors()
    {
        if (speakerAAnchor == null)
        {
            speakerAAnchor = CreateChildAnchor("Chapter35SpeakerAAnchor");
        }

        if (speakerBAnchor == null)
        {
            speakerBAnchor = CreateChildAnchor("Chapter35SpeakerBAnchor");
        }
    }

    private void EnsureBubbleInstances()
    {
        if (speakerABubble == null)
        {
            speakerABubble = Instantiate(dialogueBubblePrefab, transform);
            speakerABubble.name = "Chapter35SpeakerABubble";
            PrepareChapter35Bubble(speakerABubble, speakerAAnchor);
            speakerABubble.Show(false);
        }

        if (speakerBBubble == null)
        {
            speakerBBubble = Instantiate(dialogueBubblePrefab, transform);
            speakerBBubble.name = "Chapter35SpeakerBBubble";
            PrepareChapter35Bubble(speakerBBubble, speakerBAnchor);
            speakerBBubble.Show(false);
        }
    }

    private void PrepareChapter35Bubble(DialogueBubbleView bubble, Transform anchor)
    {
        if (bubble == null)
        {
            return;
        }

        bubble.SetFollow(anchor);
        bubble.SetShowTail(true);
        bubble.SetLayout(
            chapter35BubbleWorldOffset,
            chapter35BubbleContentPadding,
            chapter35BubbleMaxWidth,
            chapter35BubbleMinWidth,
            chapter35BubbleMinHeight);
        bubble.SetFontSizeOverride(chapter35BubbleFontSize);
    }

    private void UpdateBubbleFollowTargets()
    {
        if (speakerABubble != null)
        {
            speakerABubble.SetFollow(speakerAAnchor);
        }

        if (speakerBBubble != null)
        {
            speakerBBubble.SetFollow(speakerBAnchor);
        }
    }

    private DialogueBubbleView GetActiveBubble()
    {
        if (dialogueIndex < 0 || dialogueIndex >= queuedLines.Count)
        {
            return null;
        }

        DialogueLine line = queuedLines[dialogueIndex];
        return line.speaker == DialogueSpeaker.Player ? speakerABubble : speakerBBubble;
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

    private void UpdateDialogueAnchors()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (speakerAAnchor != null)
        {
            speakerAAnchor.position = ScreenOffsetToWorld(cam, speakerABubbleScreenOffset);
        }

        if (speakerBAnchor != null)
        {
            speakerBAnchor.position = ScreenOffsetToWorld(cam, speakerBBubbleScreenOffset);
        }
    }

    private static Vector3 ScreenOffsetToWorld(Camera cam, Vector3 screenOffset)
    {
        Vector3 screenPoint = new Vector3(
            Screen.width * 0.5f + screenOffset.x,
            Screen.height * 0.5f + screenOffset.y,
            Mathf.Max(0.01f, screenOffset.z));

        return cam.ScreenToWorldPoint(screenPoint);
    }

    private void BuildDialogueLines()
    {
        queuedLines.Clear();
        queuedLines.Add(SpeakerA("听说西区又减配额了。这周每人每天三百毫升水。"));
        queuedLines.Add(SpeakerB("三百？上个月还有五百。"));
        queuedLines.Add(SpeakerA("有什么办法。废料厂受到攻击停产，净水材料跟不上了。"));
        queuedLines.Add(SpeakerB("那帮城外的变异体……要不是他们天天进攻，废料厂也不会停。"));
    }

    private static DialogueLine SpeakerA(string text)
    {
        return new DialogueLine
        {
            speaker = DialogueSpeaker.Player,
            text = text,
            emphasis = Normal()
        };
    }

    private static DialogueLine SpeakerB(string text)
    {
        return new DialogueLine
        {
            speaker = DialogueSpeaker.NPC,
            text = text,
            emphasis = Normal()
        };
    }

    private static DialogueEmphasis Normal()
    {
        return new DialogueEmphasis { enabled = false, scaleMultiplier = 1.25f, shakeMagnitude = 0.08f };
    }
}
