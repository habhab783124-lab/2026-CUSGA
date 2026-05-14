using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TruthRevealed : MonoBehaviour
{
    private static readonly List<DialogueLine> DefaultLines = new()
    {
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "末世事件发生后，可利用资源骤减至旧纪元时期的约百分之三。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "伊甸地区在经历数年资源冲突与势力整合后，在原址上建立起封闭式都市系统——“伊甸”。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "人们面临一个无法回避的问题：有限的资源，只能供养有限的人口。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "谁该活下来？" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "决策者选择了他们眼中的“最优解”——将资源优先分配给对社会最有贡献的人：工程师、医生、科学家、技术工人。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "这些人能够维持城市运转，能够研发防护技术，能够在未来重建文明。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "老弱病残，以及那些没有专业技能的人，被留在了墙外。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "这不是残忍。这是在极端环境下，为了整体存续不得不做出的取舍。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "资源总量有限，能承载的人口上限固定。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "如果平均分配，所有人都只能分到不足以抵御辐射的剂量，所有人都会死。但如果集中分配，至少有一部分人能活下来。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "墙外的人不理解这种计算。对他们来说，这只是简单的、赤裸裸的抛弃。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "所以他们攻击城墙。他们要活下去。这是人类最本能的求生反应。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "而城内的人，为了保护自己，为了保护“文明的种子”，开始杀戮。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "决策者给清理者装上认知滤镜，让他们以为自己在杀怪物。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "因为如果清理者知道自己杀的是同胞，战后创伤会摧毁他们。而伊甸需要他们像机器一样运转，心无杂念地杀敌。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "城内与城外。秩序与生存。理性与本能。" },
        new DialogueLine { speaker = DialogueSpeaker.NPC, text = "没有对错。只有选择。" }
    };

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

    [Header("过场")]
    [SerializeField] private SpriteRenderer background1;
    [SerializeField] private Image background1Image;
    [SerializeField] private float background1FadeDuration = 1f;

    private Canvas canvas;
    private RectTransform textBox;
    private Image background;
    private TextMeshProUGUI dialogueText;
    private Coroutine typingRoutine;
    private Coroutine backgroundFadeRoutine;
    private int currentLineIndex;
    private bool isTyping;
    private bool skipTyping;

    public bool IsTyping => isTyping;
    public bool HasReachedConversationEnd => lines != null && lines.Count > 0 && currentLineIndex >= lines.Count - 1;

    private void Awake()
    {
        ApplyDefaultLinesIfNeeded();
        EnsureVisuals();
        TryResolveSceneReferences();
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

        ShowLine(0);
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(mouseButton))
        {
            return;
        }

        if (isTyping)
        {
            skipTyping = true;
            return;
        }

        int nextLineIndex = currentLineIndex + 1;
        HandleLineTransition(currentLineIndex, nextLineIndex);
        ShowLine(nextLineIndex);
    }

    private void ApplyDefaultLinesIfNeeded()
    {
        if (!useScriptDefaultLines)
        {
            return;
        }

        if (lines.Count == DefaultLines.Count)
        {
            bool same = true;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null || lines[i].text != DefaultLines[i].text)
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                return;
            }
        }

        lines = new List<DialogueLine>(DefaultLines.Count);
        for (int i = 0; i < DefaultLines.Count; i++)
        {
            DialogueLine source = DefaultLines[i];
            lines.Add(new DialogueLine
            {
                speaker = source.speaker,
                text = source.text,
                emphasis = source.emphasis
            });
        }
    }

    private void TryResolveSceneReferences()
    {
        GameObject target = GameObject.Find("Background1");
        if (target == null)
        {
            return;
        }

        if (background1 == null)
        {
            background1 = target.GetComponent<SpriteRenderer>();
        }

        if (background1Image == null)
        {
            background1Image = target.GetComponent<Image>();
            if (background1Image == null)
            {
                background1Image = target.GetComponentInChildren<Image>(true);
            }
        }
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
            GameObject boxObject = new GameObject("DialogueBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            boxObject.transform.SetParent(canvas.transform, false);
            textBox = boxObject.GetComponent<RectTransform>();
            background = boxObject.GetComponent<Image>();

            GameObject textObject = new GameObject("DialogueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(boxObject.transform, false);
            dialogueText = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textBox = existingBox as RectTransform;
            background = existingBox.GetComponent<Image>();
            dialogueText = existingBox.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void ApplyStyle()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

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

            if (dialogueFontAsset != null)
            {
                dialogueText.font = dialogueFontAsset;
            }

            dialogueText.color = textColor;
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = minFontSize;
            dialogueText.fontSizeMax = maxFontSize;
            dialogueText.enableWordWrapping = true;
            dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            dialogueText.raycastTarget = false;
        }
    }

    private void HandleLineTransition(int fromLineIndex, int toLineIndex)
    {
        if (fromLineIndex == 1 && toLineIndex == 2)
        {
            StartBackground1Fade();
        }
    }

    private void StartBackground1Fade()
    {
        if (background1 == null && background1Image == null)
        {
            return;
        }

        if (backgroundFadeRoutine != null)
        {
            StopCoroutine(backgroundFadeRoutine);
        }

        backgroundFadeRoutine = StartCoroutine(FadeBackground1Routine());
    }

    private void ShowLine(int lineIndex)
    {
        if (lines == null || lines.Count == 0)
        {
            if (dialogueText != null)
            {
                dialogueText.text = string.Empty;
            }

            return;
        }

        if (lineIndex >= lines.Count)
        {
            return;
        }

        currentLineIndex = Mathf.Max(0, lineIndex);
        string fullText = lines[currentLineIndex]?.text ?? string.Empty;

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
        }

        typingRoutine = StartCoroutine(TypeLineRoutine(fullText));
    }

    private IEnumerator TypeLineRoutine(string fullText)
    {
        isTyping = true;
        skipTyping = false;

        if (dialogueText == null)
        {
            yield break;
        }

        dialogueText.text = string.Empty;
        for (int i = 0; i < fullText.Length; i++)
        {
            if (skipTyping)
            {
                dialogueText.text = fullText;
                break;
            }

            dialogueText.text += fullText[i];
            yield return new WaitForSeconds(secondsPerChar);
        }

        dialogueText.text = fullText;
        isTyping = false;
        skipTyping = false;
        typingRoutine = null;
    }

    private IEnumerator FadeBackground1Routine()
    {
        float safeDuration = Mathf.Max(0.01f, background1FadeDuration);
        float time = 0f;

        Color spriteStart = background1 != null ? background1.color : Color.white;
        Color imageStart = background1Image != null ? background1Image.color : Color.white;

        while (time < safeDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / safeDuration);

            if (background1 != null)
            {
                Color next = spriteStart;
                next.a = Mathf.Lerp(spriteStart.a, 0f, t);
                background1.color = next;
            }

            if (background1Image != null)
            {
                Color next = imageStart;
                next.a = Mathf.Lerp(imageStart.a, 0f, t);
                background1Image.color = next;
            }

            yield return null;
        }

        if (background1 != null)
        {
            Color final = background1.color;
            final.a = 0f;
            background1.color = final;
        }

        if (background1Image != null)
        {
            Color final = background1Image.color;
            final.a = 0f;
            background1Image.color = final;
        }

        backgroundFadeRoutine = null;
    }

    private void RefreshShownText()
    {
        if (dialogueText == null || lines == null || lines.Count == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(currentLineIndex, 0, lines.Count - 1);
        dialogueText.text = lines[clampedIndex]?.text ?? string.Empty;
    }

    private static TMP_FontAsset TryLoadDialogueFont()
    {
        string[] paths = { "DialogueFont", "Fonts/DialogueFont", "Fonts/SCfont SDF" };
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
