using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class Chapter5 : MonoBehaviour
{
    [Header("Font")]
    [SerializeField] private TMP_FontAsset dialogueFontAsset;

    [Header("Narration Box")]
    [SerializeField] private Vector2 boxSize = new Vector2(1280f, 220f);
    [SerializeField] private Vector2 bottomOffset = new Vector2(0f, 120f);
    [SerializeField] [Range(0f, 1f)] private float backgroundAlpha = 0.6f;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float minFontSize = 24f;
    [SerializeField] private float maxFontSize = 42f;
    [SerializeField] private Vector2 textPadding = new Vector2(40f, 25f);

    [Header("Typing")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float secondsPerChar = 0.03f;
    [SerializeField] private int mouseButton = 0;

    [Header("Shen Entrance")]
    [SerializeField] private Transform shen;
    [SerializeField] private Transform shenStopPoint;
    [SerializeField] private float shenMoveSpeed = 3f;
    [SerializeField] private float shenExtraLeftBeyondCamera = 1.2f;
    [SerializeField] private float shenArriveEpsilon = 0.02f;

    [Header("Character Dialogue Bubble")]
    [SerializeField] private DialogueBubbleView dialogueBubblePrefab;
    [SerializeField] private Transform playerCharacter;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform shenBubbleAnchor;
    [SerializeField] private Vector3 playerBubbleOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private Vector3 shenBubbleOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private Vector3 bubbleWorldOffset = Vector3.zero;
    [SerializeField] private Vector2 bubbleContentPadding = new Vector2(0.55f, 0.35f);
    [SerializeField] private float bubbleMaxWidth = 7.2f;
    [SerializeField] private float bubbleMinWidth = 2f;
    [SerializeField] private float bubbleMinHeight = 1.2f;
    [SerializeField] private float bubbleFontSize = 36f;

    private NarrationPresenter narrationPresenter;
    private Coroutine shenEntranceRoutine;
    private DialogueBubbleView playerBubble;
    private DialogueBubbleView shenBubble;
    private readonly List<DialogueLine> characterLines = new List<DialogueLine>();
    private bool hasPlayed;
    private bool introAwaitingAdvance;
    private bool shenEntranceStarted;
    private bool characterDialogueActive;
    private Vector3 shenTargetPosition;
    private bool hasCachedShenTargetPosition;
    private int characterDialogueIndex;

    private void Awake()
    {
        ResolveShenReferences();
        ResolvePlayerReference();
        CacheShenTargetPosition(true);
        PlaceShenOffscreenLeft();

        EnsureDialogueBubblePrefab();
        EnsureNarrationPresenter();
        EnsureBubbleAnchors();
        EnsureBubbleInstances();

        ConfigureNarrationPresenter();
        ApplyBubbleFont();

        narrationPresenter?.Hide();
        HideAllCharacterBubbles();
        BuildCharacterDialogueLines();
    }

    private void Start()
    {
        if (dialogueFontAsset == null)
        {
            dialogueFontAsset = TryLoadDialogueFont();
            ConfigureNarrationPresenter();
            ApplyBubbleFont();
        }

        if (playOnStart)
        {
            PlayIntro();
        }
    }

    private void Update()
    {
        UpdateBubbleAnchors();
        UpdateBubbleFollowTargets();

        if (!Input.GetMouseButtonDown(mouseButton))
        {
            return;
        }

        if (characterDialogueActive)
        {
            DialogueBubbleView activeBubble = GetActiveCharacterBubble();
            if (activeBubble != null && activeBubble.IsTyping)
            {
                activeBubble.SkipTyping();
                return;
            }

            AdvanceCharacterDialogue();
            return;
        }

        if (introAwaitingAdvance)
        {
            if (narrationPresenter != null && narrationPresenter.TryHandleAdvanceInput())
            {
                return;
            }

            introAwaitingAdvance = false;
            narrationPresenter?.Hide();
            StartShenEntrance();
        }
    }

    public void PlayIntro()
    {
        if (hasPlayed)
        {
            return;
        }

        hasPlayed = true;
        introAwaitingAdvance = true;

        IReadOnlyList<DialogueLine> introLines = Get("chapter5_intro");
        narrationPresenter?.SetLines(introLines);
        narrationPresenter?.ShowLine(0);
    }

    private void ResolveShenReferences()
    {
        if (shen == null)
        {
            GameObject shenObject = GameObject.Find("Shen");
            if (shenObject != null)
            {
                shen = shenObject.transform;
            }
        }
    }

    private void ResolvePlayerReference()
    {
        if (playerCharacter == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
            {
                playerCharacter = playerObject.transform;
            }
        }
    }

    private void CacheShenTargetPosition(bool force)
    {
        if (shenStopPoint != null)
        {
            shenTargetPosition = shenStopPoint.position;
            hasCachedShenTargetPosition = true;
            return;
        }

        if (shen != null && (force || !hasCachedShenTargetPosition))
        {
            shenTargetPosition = shen.position;
            hasCachedShenTargetPosition = true;
        }
    }

    private void PlaceShenOffscreenLeft()
    {
        Camera cam = Camera.main;
        if (cam == null || shen == null || !hasCachedShenTargetPosition)
        {
            return;
        }

        float halfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : 5f;
        float startX = cam.transform.position.x - halfWidth - shenExtraLeftBeyondCamera;
        shen.position = new Vector3(startX, shenTargetPosition.y, shenTargetPosition.z);
    }

    private void StartShenEntrance()
    {
        if (shenEntranceStarted)
        {
            return;
        }

        ResolveShenReferences();
        CacheShenTargetPosition(false);

        if (shen == null)
        {
            Debug.LogError("Chapter5: Missing Shen reference in scene.", this);
            return;
        }

        if (!hasCachedShenTargetPosition)
        {
            Debug.LogError("Chapter5: Missing Shen stop point.", this);
            return;
        }

        shenEntranceStarted = true;

        if (shenEntranceRoutine != null)
        {
            StopCoroutine(shenEntranceRoutine);
        }

        shenEntranceRoutine = StartCoroutine(ShenEntranceRoutine());
    }

    private IEnumerator ShenEntranceRoutine()
    {
        while (shen != null && Vector3.Distance(shen.position, shenTargetPosition) > shenArriveEpsilon)
        {
            shen.position = Vector3.MoveTowards(shen.position, shenTargetPosition, shenMoveSpeed * Time.deltaTime);
            yield return null;
        }

        if (shen != null)
        {
            shen.position = shenTargetPosition;
        }

        shenEntranceRoutine = null;
        StartCharacterDialogue();
    }

    private void EnsureDialogueBubblePrefab()
    {
        if (dialogueBubblePrefab == null)
        {
            dialogueBubblePrefab = Resources.Load<DialogueBubbleView>("DialogueBubble");
        }
    }

    private void EnsureNarrationPresenter()
    {
        if (narrationPresenter == null)
        {
            narrationPresenter = GetComponent<NarrationPresenter>();
        }

        if (narrationPresenter == null)
        {
            narrationPresenter = gameObject.AddComponent<NarrationPresenter>();
        }
    }

    private void ConfigureNarrationPresenter()
    {
        if (narrationPresenter == null)
        {
            return;
        }

        narrationPresenter.SetUiNames("Chapter5Canvas", "DialogueBox", "DialogueText");
        narrationPresenter.ConfigureAppearance(
            dialogueFontAsset,
            boxSize,
            bottomOffset,
            backgroundAlpha,
            backgroundColor,
            textColor,
            minFontSize,
            maxFontSize,
            textPadding);
        narrationPresenter.ConfigureTyping(secondsPerChar, dialogueBubblePrefab, "Chapter5IntroTypingSfx");
    }

    private void EnsureBubbleAnchors()
    {
        if (playerBubbleAnchor == null)
        {
            GameObject anchor = new GameObject("Chapter5PlayerBubbleAnchor");
            anchor.transform.SetParent(transform, false);
            playerBubbleAnchor = anchor.transform;
        }

        if (shenBubbleAnchor == null)
        {
            GameObject anchor = new GameObject("Chapter5ShenBubbleAnchor");
            anchor.transform.SetParent(transform, false);
            shenBubbleAnchor = anchor.transform;
        }
    }

    private void EnsureBubbleInstances()
    {
        if (dialogueBubblePrefab == null)
        {
            return;
        }

        if (playerBubble == null)
        {
            playerBubble = Instantiate(dialogueBubblePrefab, transform);
            playerBubble.name = "Chapter5PlayerBubble";
            PrepareCharacterBubble(playerBubble, playerBubbleAnchor);
            playerBubble.Show(false);
        }

        if (shenBubble == null)
        {
            shenBubble = Instantiate(dialogueBubblePrefab, transform);
            shenBubble.name = "Chapter5ShenBubble";
            PrepareCharacterBubble(shenBubble, shenBubbleAnchor);
            shenBubble.Show(false);
        }
    }

    private void PrepareCharacterBubble(DialogueBubbleView bubble, Transform anchor)
    {
        if (bubble == null)
        {
            return;
        }

        bubble.SetFollow(anchor);
        bubble.SetShowTail(true);
        bubble.SetLayout(
            bubbleWorldOffset,
            bubbleContentPadding,
            bubbleMaxWidth,
            bubbleMinWidth,
            bubbleMinHeight);
        bubble.SetFontSizeOverride(bubbleFontSize);

        if (dialogueFontAsset != null)
        {
            bubble.SetFont(dialogueFontAsset);
        }
    }

    private void ApplyBubbleFont()
    {
        if (playerBubble != null && dialogueFontAsset != null)
        {
            playerBubble.SetFont(dialogueFontAsset);
        }

        if (shenBubble != null && dialogueFontAsset != null)
        {
            shenBubble.SetFont(dialogueFontAsset);
        }
    }

    private void UpdateBubbleAnchors()
    {
        if (playerBubbleAnchor != null && playerCharacter != null)
        {
            playerBubbleAnchor.position = playerCharacter.position + playerBubbleOffset;
        }

        if (shenBubbleAnchor != null && shen != null)
        {
            shenBubbleAnchor.position = shen.position + shenBubbleOffset;
        }
    }

    private void UpdateBubbleFollowTargets()
    {
        if (playerBubble != null)
        {
            playerBubble.SetFollow(playerBubbleAnchor);
        }

        if (shenBubble != null)
        {
            shenBubble.SetFollow(shenBubbleAnchor);
        }
    }

    private void StartCharacterDialogue()
    {
        if (characterLines.Count == 0)
        {
            return;
        }

        characterDialogueIndex = 0;
        characterDialogueActive = true;
        ShowCurrentCharacterLine();
    }

    private void AdvanceCharacterDialogue()
    {
        characterDialogueIndex++;
        if (characterDialogueIndex >= characterLines.Count)
        {
            characterDialogueActive = false;
            HideAllCharacterBubbles();
            return;
        }

        ShowCurrentCharacterLine();
    }

    private void ShowCurrentCharacterLine()
    {
        if (characterDialogueIndex < 0 || characterDialogueIndex >= characterLines.Count)
        {
            characterDialogueActive = false;
            HideAllCharacterBubbles();
            return;
        }

        DialogueLine line = characterLines[characterDialogueIndex];
        DialogueBubbleView activeBubble = line.speaker == DialogueSpeaker.Player ? playerBubble : shenBubble;
        DialogueBubbleView inactiveBubble = line.speaker == DialogueSpeaker.Player ? shenBubble : playerBubble;

        if (inactiveBubble != null)
        {
            inactiveBubble.Show(false);
        }

        if (activeBubble == null)
        {
            return;
        }

        activeBubble.Show(false);
        activeBubble.ClearText();
        activeBubble.Show(true);
        activeBubble.TypeLine(line.text, secondsPerChar, line.emphasis);
    }

    private DialogueBubbleView GetActiveCharacterBubble()
    {
        if (characterDialogueIndex < 0 || characterDialogueIndex >= characterLines.Count)
        {
            return null;
        }

        return characterLines[characterDialogueIndex].speaker == DialogueSpeaker.Player ? playerBubble : shenBubble;
    }

    private void HideAllCharacterBubbles()
    {
        if (playerBubble != null)
        {
            playerBubble.Show(false);
        }

        if (shenBubble != null)
        {
            shenBubble.Show(false);
        }
    }

    private void BuildCharacterDialogueLines()
    {
        characterLines.Clear();
        characterLines.Add(ShenLine("你在看我手里的东西。"));
        characterLines.Add(PlayerLine("没有。"));
        characterLines.Add(ShenLine("你从刚才开始一直在看。"));
        characterLines.Add(ShenLine("走到那头之后，又回头看了两次。"));
        characterLines.Add(PlayerLine("……"));
        characterLines.Add(ShenLine("这个，你想知道这是什么吗？"));
        characterLines.Add(PlayerLine("我该知道吗。"));
        characterLines.Add(ShenLine("……也没什么。一把伞。"));
        characterLines.Add(PlayerLine("伞？"));
        characterLines.Add(ShenLine("嗯。伞。"));
        characterLines.Add(PlayerLine("伞是什么。"));
        characterLines.Add(ShenLine("就是……一种防雨的工具。"));
        characterLines.Add(ShenLine("撑开之后，雨就不会落在身上。"));
        characterLines.Add(PlayerLine("那不就是防雨力场吗？"));
        characterLines.Add(ShenLine("嗯，但这是相当古老的东西呢，难怪你不知道。"));
        characterLines.Add(PlayerLine("你为什么要带着这个？"));
        characterLines.Add(ShenLine("我是一个怀旧的人呢。"));
        characterLines.Add(PlayerLine("你也是清理者？"));
        characterLines.Add(ShenLine("嗯……算是吧。刚从别的区域调过来。"));
        characterLines.Add(PlayerLine("我没见过你。"));
        characterLines.Add(ShenLine("世界很大。"));
        characterLines.Add(PlayerLine("……世界，不就是伊甸吗？"));
        characterLines.Add(ShenLine("你一直这么认为？"));
        characterLines.Add(PlayerLine("这是事实。"));
        characterLines.Add(ShenLine("车来了哦～"));
    }

    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "chapter5_intro":
                return Intro();
            default:
                return null;
        }
    }

    private static IReadOnlyList<DialogueLine> Intro()
    {
        return new List<DialogueLine>
        {
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "战斗结束已是午夜。今天是伊甸人工降雨日，人们通过降雨冲刷掉含有辐射的尘埃。",
                emphasis = Normal()
            }
        };
    }

    private static DialogueLine PlayerLine(string text)
    {
        return new DialogueLine
        {
            speaker = DialogueSpeaker.Player,
            text = text,
            emphasis = Normal()
        };
    }

    private static DialogueLine ShenLine(string text)
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
        return DialogueBubbleView.CreateNormalEmphasis();
    }

    private static TMP_FontAsset TryLoadDialogueFont()
    {
        string[] paths = { "DialogueFont", "Fonts/DialogueFont", "Fonts/SCfont SDF", "Fonts/zpix SDF" };
        foreach (string path in paths)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(path);
            if (font != null)
            {
                return font;
            }
        }

        return null;
    }
}
