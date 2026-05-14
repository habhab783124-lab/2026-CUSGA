using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Chapter9 : MonoBehaviour
{
    private enum PlaybackState { Intro, Dialogue }

    [Header("字体")]
    [SerializeField] private TMP_FontAsset dialogueFontAsset;
    [Header("台词")]
    [SerializeField] private bool useScriptDefaultLines = true;
    [SerializeField] private List<DialogueLine> lines = new();
    [Header("底部文本框")]
    [SerializeField] private Vector2 boxSize = new(1280f, 220f);
    [SerializeField] private Vector2 bottomOffset = new(0f, 120f);
    [SerializeField] [Range(0f, 1f)] private float backgroundAlpha = 0.6f;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float minFontSize = 24f;
    [SerializeField] private float maxFontSize = 42f;
    [SerializeField] private Vector2 textPadding = new(40f, 25f);
    [Header("打字机")]
    [SerializeField] private float secondsPerChar = 0.03f;
    [SerializeField] private int mouseButton = 0;
    [Header("音乐")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private string musicResourcePath = "StoryAudio/BGM/chapter9BGM";
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField] private bool loopMusic = true;
    [Header("启动控制")]
    [SerializeField] private bool playOnStart = true;
    [Header("气泡对白")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform npcBubbleAnchor;
    [SerializeField] private int bubbleDialogueStartIndex = 0;
    [SerializeField] private Vector3 playerBubbleOffset = new(3f, -1f, 0f);
    [SerializeField] private Vector3 npcBubbleOffset = new(-3f, 1f, 0f);

    private Canvas canvas;
    private RectTransform textBox;
    private Image background;
    private TextMeshProUGUI dialogueText;
    private CanvasGroup introCanvasGroup;
    private Coroutine typingRoutine;
    private int currentLineIndex;
    private bool isTyping;
    private bool skipTyping;
    private PlaybackState state;
    private AudioSource audioSource;
    private bool hasStartedSequence;
    private Action onSequenceComplete;

    private void Awake()
    {
        ApplyDefaultLinesIfNeeded();
        EnsureVisuals();
        EnsureAudioSource();
        EnsureDialogueRunner();
        ApplyStyle();
    }

    private void OnValidate()
    {
        ApplyDefaultLinesIfNeeded();
        ApplyStyle();
        RefreshShownText();
    }

    private void Start()
    {
        if (dialogueFontAsset == null)
        {
            dialogueFontAsset = TryLoadDialogueFont();
            ApplyStyle();
        }

        if (playOnStart)
        {
            BeginSequence();
        }
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(mouseButton) || state == PlaybackState.Dialogue)
        {
            return;
        }

        if (isTyping)
        {
            skipTyping = true;
            return;
        }

        if (state != PlaybackState.Intro)
        {
            return;
        }

        int nextLineIndex = currentLineIndex + 1;
        if (nextLineIndex >= lines.Count)
        {
            if (introCanvasGroup != null)
            {
                introCanvasGroup.alpha = 0f;
                introCanvasGroup.gameObject.SetActive(false);
            }

            StartBubbleDialogue();
            return;
        }

        ShowLine(nextLineIndex);
    }

    public void BeginSequence(Action onCompleted = null)
    {
        if (hasStartedSequence)
        {
            return;
        }

        hasStartedSequence = true;
        onSequenceComplete = onCompleted;
        PlayMusic();
        state = PlaybackState.Intro;
        ShowLine(0);
    }

    private void ApplyDefaultLinesIfNeeded()
    {
        if (!useScriptDefaultLines) return;

        lines = new List<DialogueLine>
        {
            new DialogueLine { speaker = DialogueSpeaker.NPC, text = "战场上到处都是尸体。" },
            new DialogueLine { speaker = DialogueSpeaker.NPC, text = "不是变异体的尸体。是人的尸体。" },
            new DialogueLine { speaker = DialogueSpeaker.NPC, text = "穿着简陋防护服的城外人。老人，年轻人，孩子。" },
            new DialogueLine { speaker = DialogueSpeaker.NPC, text = "他们手里拿着简陋的武器——钢管焊成的矛，木板钉成的盾，几把锈蚀的枪。" },
            new DialogueLine { speaker = DialogueSpeaker.NPC, text = "他们从来都不是怪物。" },
            new DialogueLine { speaker = DialogueSpeaker.NPC, text = "凌的刀从手中滑落。" },
            new DialogueLine { speaker = DialogueSpeaker.NPC, text = "她跪下来。跪在积水中。" },
            new DialogueLine { speaker = DialogueSpeaker.NPC, text = "躺在她面前的，是奄奄一息的沈湮。" }
        };
    }

    private void EnsureVisuals()
    {
        canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("TruthRevealedCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        Transform existingBox = canvas.transform.Find("DialogueBox");
        if (existingBox == null)
        {
            GameObject boxObject = new GameObject("DialogueBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            boxObject.transform.SetParent(canvas.transform, false);
            textBox = boxObject.GetComponent<RectTransform>();
            background = boxObject.GetComponent<Image>();
            introCanvasGroup = boxObject.GetComponent<CanvasGroup>();
            GameObject textObject = new GameObject("DialogueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(boxObject.transform, false);
            dialogueText = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textBox = existingBox as RectTransform;
            background = existingBox.GetComponent<Image>();
            introCanvasGroup = existingBox.GetComponent<CanvasGroup>() ?? existingBox.gameObject.AddComponent<CanvasGroup>();
            dialogueText = existingBox.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void EnsureAudioSource()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void EnsureDialogueRunner()
    {
        dialogueRunner = dialogueRunner != null ? dialogueRunner : GetComponent<DialogueRunner>();
        if (dialogueRunner == null) dialogueRunner = gameObject.AddComponent<DialogueRunner>();
        if (playerBubbleAnchor == null) playerBubbleAnchor = CreateCenterAnchor("Chapter9PlayerBubbleAnchor");
        if (npcBubbleAnchor == null) npcBubbleAnchor = CreateCenterAnchor("Chapter9NpcBubbleAnchor");
    }

    private Transform CreateCenterAnchor(string anchorName)
    {
        GameObject anchor = new GameObject(anchorName);
        anchor.transform.SetParent(transform, false);
        anchor.transform.position = GetScreenCenterWorldPosition();
        return anchor.transform;
    }

    private Vector3 GetScreenCenterWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        float depth = Mathf.Abs(cam.transform.position.z);
        return cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
    }

    private void ApplyStyle()
    {
        if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (introCanvasGroup != null) introCanvasGroup.alpha = 1f;
        if (textBox != null)
        {
            textBox.anchorMin = new Vector2(0.5f, 0f);
            textBox.anchorMax = new Vector2(0.5f, 0f);
            textBox.pivot = new Vector2(0.5f, 0.5f);
            textBox.anchoredPosition = bottomOffset;
            textBox.sizeDelta = boxSize;
        }
        if (background != null)
        {
            Color color = backgroundColor;
            color.a = backgroundAlpha;
            background.color = color;
            background.raycastTarget = false;
        }
        if (dialogueText != null)
        {
            RectTransform textRect = dialogueText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(textPadding.x, textPadding.y);
            textRect.offsetMax = new Vector2(-textPadding.x, -textPadding.y);
            if (dialogueFontAsset != null) dialogueText.font = dialogueFontAsset;
            dialogueText.color = textColor;
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = minFontSize;
            dialogueText.fontSizeMax = maxFontSize;
            dialogueText.enableWordWrapping = true;
            dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            dialogueText.raycastTarget = false;
        }
    }

    private void PlayMusic()
    {
        if (audioSource == null) return;
        AudioClip clip = backgroundMusic;
        if (clip == null && !string.IsNullOrWhiteSpace(musicResourcePath)) clip = Resources.Load<AudioClip>(musicResourcePath);
        if (clip == null) return;
        audioSource.clip = clip;
        audioSource.loop = loopMusic;
        audioSource.volume = musicVolume;
        audioSource.Play();
    }

    private void ShowLine(int lineIndex)
    {
        if (lines == null || lines.Count == 0)
        {
            if (dialogueText != null) dialogueText.text = string.Empty;
            return;
        }
        if (lineIndex >= lines.Count) return;
        currentLineIndex = Mathf.Max(0, lineIndex);
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeLineRoutine(lines[currentLineIndex]?.text ?? string.Empty));
    }

    private IEnumerator TypeLineRoutine(string fullText)
    {
        isTyping = true;
        skipTyping = false;
        if (dialogueText == null) yield break;
        dialogueText.text = string.Empty;
        for (int i = 0; i < fullText.Length; i++)
        {
            if (skipTyping) { dialogueText.text = fullText; break; }
            dialogueText.text += fullText[i];
            yield return new WaitForSeconds(secondsPerChar);
        }
        dialogueText.text = fullText;
        isTyping = false;
        skipTyping = false;
        typingRoutine = null;
    }

    private void StartBubbleDialogue()
    {
        state = PlaybackState.Dialogue;
        UpdateBubbleAnchors();
        dialogueRunner.ConfigureBottomLayout(false, Vector2.zero, Vector2.zero, 1f, false);

        IReadOnlyList<DialogueLine> source = Chapter9.Get("chapter9_intro");
        List<DialogueLine> dialogue = null;
        if (source != null)
        {
            dialogue = new List<DialogueLine>();
            int startIndex = Mathf.Clamp(bubbleDialogueStartIndex, 0, source.Count);
            for (int i = startIndex; i < source.Count; i++)
            {
                dialogue.Add(source[i]);
            }
        }

        dialogueRunner.PlayConversation(null, playerBubbleAnchor, npcBubbleAnchor, dialogue, OnBubbleDialogueCompleted);
    }

    private void OnBubbleDialogueCompleted()
    {
        StopMusic();
        Action callback = onSequenceComplete;
        onSequenceComplete = null;
        callback?.Invoke();
    }

    private void StopMusic()
    {
        if (audioSource == null)
        {
            return;
        }

        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        audioSource.clip = null;
    }

    private void UpdateBubbleAnchors()
    {
        Vector3 center = GetScreenCenterWorldPosition();
        if (playerBubbleAnchor != null) playerBubbleAnchor.position = center + playerBubbleOffset;
        if (npcBubbleAnchor != null) npcBubbleAnchor.position = center + npcBubbleOffset;
    }

    private void RefreshShownText()
    {
        if (dialogueText == null || lines == null || lines.Count == 0) return;
        int clampedIndex = Mathf.Clamp(currentLineIndex, 0, lines.Count - 1);
        dialogueText.text = lines[clampedIndex]?.text ?? string.Empty;
    }

    private static TMP_FontAsset TryLoadDialogueFont()
    {
        string[] paths = { "DialogueFont", "Fonts/DialogueFont", "Fonts/SCfont SDF" };
        foreach (string path in paths)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(path);
            if (font != null) return font;
        }
        return null;
    }
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "chapter9_intro":
                return Chen();

            default:
                return null;
        }
    }

      private static IReadOnlyList<DialogueLine> Chen()
    {
        return new List<DialogueLine>
        {
            Npc("你终于……看见我了。"),
            Player("不对……不对……不是你。",Strong()),
            Player("那些防御塔，是为怪物准备的。"),
            Player("……"),
            Player("……",Strong()),
            Player("是我开的枪……是我……",Strong()),
            Player("我刚才……是在对着你开枪。",Strong()),
            Npc("我知道你也有那种东西，认知滤镜。每个清理者都有。"),
            Npc("它让你们以为墙外的是怪物，这样你们就能毫无负担地杀人，保护伊甸和城内的人。"),
            Player("!!!",Strong()),
            Npc("你看到的，从一开始就不是我。那不是你的错……"),
            Player("我能带你回去。医疗舱、修复……他们能救你。",Strong()),
            Player("我可以说你是样本，是俘虏……",Strong()),
            Npc("凌。如果我留下来，他们会发现你包庇城外人。你会失去一切。"),
            Player("我不在乎!",Strong()),
            Npc("你不要命了吗!",Strong()),
            Npc("我们城外人苦苦追求生存，而你生来就有活下去的机会。"),
            Npc("如果你违反规定，就会被逐出城。"),
            Npc("城外什么都没有。没有药，没有干净的水。你去了，也会像我一样……被辐射吃掉。"),
            Player("可是……可是我杀了那么多人。",Strong()),
            Player("我以为我杀的是怪物。我以为我是在保卫家园。"),
            Player("但我的家园把活生生的人关在墙外。"),
            Npc("凌……",Strong()),
            Npc("你现在是城内为数不多知道真相的人。我希望你能活下去。"),
            Npc("为了你自己，也为了我们所有人。"),
            Npc("凌。我来到你身边，一开始是因为任务。"),
            Npc("城外的人做了一种叫\"伞\"的病毒，能改变认知滤镜，让人们发动反叛，庇护我们。"),
            Player("你接近我，是为了给我植入病毒？",Strong()),
            Npc("是。"),
            Player("那你为什么没做？"),
            Npc("组织说我是为了正义。"),
            Npc("但他们让我做的事，用病毒控制一个人的心智，这跟城里人用滤镜有什么区别？"),
            Npc("组织说，杀了伊甸的决策者，重新分配资源，就能救所有人。"),
            Npc("但杀了他们之后呢？谁来分配资源？谁来维持秩序？谁来——",Strong()),
            Npc("——谁来决定谁该活、谁该死？",Strong()),
            Player("……",Strong()),
            Npc("我们的伞……你还留着吗？"),
            Player("留着。"),
            Npc("那就够了。"),
            Npc("我把它给你那天……不是任务的一部分。"),
            Npc("没有任何人让我这么做。只是因为我想。"),
            Player("你不恨我们吗？"),
            Npc("恨过。",Strong()),
            Npc("恨这座城市，恨那些做决定的人，恨所有在里面吃饱穿暖的人。"),
            Npc("但现在不一样了……"),
            Player("现在恨谁？"),
            Npc("不知道。可能谁都不恨，可能谁都得恨。"),
            Npc("如果有一天我们进了城，我们也会变成他们。因为我们也要做同样的决定。"),
            Npc("谁分得多，谁分得少，谁在外面等死。"),
            Player("沈湮……"),
            Npc("现在……我只想撑起一把小小的伞。"),
            Npc("至少伞下面，我能决定谁不被淋湿。"),
            Npc("墙外的世界，其实也看不到那么多的星星，一直都是漫天的风沙。"),
            Npc("那个天文馆，我早就知道有这个地方了，但因为没有供电，所以一直没法启动。"),
            Npc("是你让我看到了那样的美景。"),
            Npc("谢谢你。")


        };
    }


    private static DialogueLine Npc(
    string text,
    DialogueEmphasis? emphasis = null)
{
    return new DialogueLine
    {
        speaker = DialogueSpeaker.NPC,
        text = text,
        emphasis = emphasis ?? Normal()
    };
}

   private static DialogueLine Player(
    string text,
    DialogueEmphasis? emphasis = null)
{
    return new DialogueLine
    {
        speaker = DialogueSpeaker.Player,
        text = text,
        emphasis = emphasis ?? Normal()
    };
}

    private static DialogueEmphasis Normal()
    {
        return new DialogueEmphasis { enabled = false, scaleMultiplier = 1.25f, shakeMagnitude = 0.08f };
    }

    private static DialogueEmphasis Strong(float scaleMultiplier = 2.0f, float shakeMagnitude = 0.2f)
    {
        return new DialogueEmphasis { enabled = true, scaleMultiplier = scaleMultiplier, shakeMagnitude = shakeMagnitude };
    }

    private static DialogueEmphasis Pulse(float scaleMultiplier = 0.92f, float shakeMagnitude = 0.1f)
    {
        return new DialogueEmphasis
        {
            enabled = true,
            scaleMultiplier = scaleMultiplier,
            shakeMagnitude = shakeMagnitude
        };
    }
}