using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EndSceneController : MonoBehaviour
{
    [Header("Font")]
    [SerializeField] private TMP_FontAsset dialogueFontAsset;

    [Header("Camera")]
    [SerializeField] private Camera sceneCamera;

    [Header("Text")]
    [SerializeField] private string endTitle = "END";
    [SerializeField] private string reflectionText = "谢谢你一路陪我们走到这里。\n故事暂时告一段落，但愿你也能记住这段旅程。";
    [SerializeField] private string creditsText = "制作人员\nrei\n占位\n占位\n占位\n占位\n占位\n占位\n特别感谢：你";

    [Header("Timing")]
    [SerializeField] private float initialDelay = 0.4f;
    [SerializeField] private float titleFadeDuration = 0.8f;
    [SerializeField] private float titleHoldDuration = 0.8f;
    [SerializeField] private float reflectionSecondsPerChar = 0.03f;
    [SerializeField] private float reflectionHoldDuration = 1.2f;
    [SerializeField] private float creditsFadeDuration = 1.4f;

    [Header("Style")]
    [SerializeField] private float titleFontSize = 120f;
    [SerializeField] private float reflectionFontSize = 42f;
    [SerializeField] private float creditsFontSize = 36f;
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color reflectionColor = Color.white;
    [SerializeField] private Color creditsColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private DialogueBubbleView typingSfxReferenceBubble;

    private Canvas canvas;
    private Image backgroundImage;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI reflectionTextUi;
    private TextMeshProUGUI creditsTextUi;
    private CanvasGroup titleCanvasGroup;
    private CanvasGroup reflectionCanvasGroup;
    private CanvasGroup creditsCanvasGroup;
    private Coroutine sequenceRoutine;
    private AudioSource typingAudioSource;
    private DialogueBubbleView.ExternalTypingSfxPlayer typingSfxPlayer;

    private void Awake()
    {
        EnsureSceneCamera();
        EnsureCanvas();
        EnsureTextObjects();
        EnsureTypingSfxSupport();
        ApplyFont();
        ApplyStyle();
        SetInitialState();
    }

    private void Start()
    {
        if (dialogueFontAsset == null)
        {
            dialogueFontAsset = TryLoadDialogueFont();
            ApplyFont();
        }

        sequenceRoutine = StartCoroutine(RunSequence());
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureSceneCamera();
            EnsureCanvas();
            EnsureTextObjects();
            SyncSerializedTextFromScene();
        }

        ApplyFont();
        ApplyStyle();
    }

    private void EnsureSceneCamera()
    {
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
        }

        sceneCamera.orthographic = true;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = Color.black;
        sceneCamera.orthographicSize = 5f;
    }

    private void EnsureCanvas()
    {
        if (canvas == null)
        {
            Transform existingCanvas = transform.Find("EndSceneCanvas");
            if (existingCanvas != null)
            {
                canvas = existingCanvas.GetComponent<Canvas>();
            }
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("EndSceneCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        Transform background = canvas.transform.Find("EndBackground");
        if (background != null)
        {
            backgroundImage = background.GetComponent<Image>();
        }

        if (backgroundImage == null)
        {
            GameObject backgroundObject = new GameObject("EndBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(canvas.transform, false);
            backgroundImage = backgroundObject.GetComponent<Image>();
        }

        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundImage.color = Color.black;
        backgroundImage.raycastTarget = false;
        backgroundImage.transform.SetAsFirstSibling();
    }

    private void EnsureTextObjects()
    {
        titleText = EnsureText("EndTitleText", new Vector2(0f, 260f), new Vector2(1400f, 180f), titleFontSize, titleColor, ref titleCanvasGroup);
        reflectionTextUi = EnsureText("EndReflectionText", new Vector2(0f, -10f), new Vector2(1500f, 260f), reflectionFontSize, reflectionColor, ref reflectionCanvasGroup);
        creditsTextUi = EnsureText("EndCreditsText", new Vector2(0f, -320f), new Vector2(1300f, 300f), creditsFontSize, creditsColor, ref creditsCanvasGroup);
    }

    private TextMeshProUGUI EnsureText(string objectName, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, Color color, ref CanvasGroup group)
    {
        if (canvas == null)
        {
            return null;
        }

        Transform existing = canvas.transform.Find(objectName);
        TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        bool createdNewTextObject = false;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup));
            textObject.transform.SetParent(canvas.transform, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
            group = textObject.GetComponent<CanvasGroup>();
            createdNewTextObject = true;
        }
        else if (group == null)
        {
            group = text.gameObject.GetComponent<CanvasGroup>() ?? text.gameObject.AddComponent<CanvasGroup>();
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        // 方案 A：Scene 里手改的字号应该成为权威来源。
        //
        // 所以这里只在“第一次创建文本对象”时写入默认字号，
        // 对已经存在的文本对象，不再在编辑态强行把字号改回序列化字段。
        // 这样你在 Scene 里直接调 `TMP_Text.fontSize`，
        // OnValidate 不会立刻又把它打回旧值。
        if (createdNewTextObject)
        {
            text.fontSize = fontSize;
        }

        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    private void ApplyFont()
    {
        if (dialogueFontAsset == null)
        {
            dialogueFontAsset = TryLoadDialogueFont();
        }

        if (dialogueFontAsset == null)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.font = dialogueFontAsset;
        }

        if (reflectionTextUi != null)
        {
            reflectionTextUi.font = dialogueFontAsset;
        }

        if (creditsTextUi != null)
        {
            creditsTextUi.font = dialogueFontAsset;
        }
    }

    private void ApplyStyle()
    {
        if (titleText != null)
        {
            titleText.text = endTitle;
            titleText.color = titleColor;
        }

        if (reflectionTextUi != null)
        {
            reflectionTextUi.text = reflectionText;
            reflectionTextUi.color = reflectionColor;
        }

        if (creditsTextUi != null)
        {
            creditsTextUi.text = creditsText;
            creditsTextUi.color = creditsColor;
        }
    }

    private void SyncSerializedTextFromScene()
    {
        if (titleText != null && !string.IsNullOrWhiteSpace(titleText.text) && titleText.text != endTitle)
        {
            endTitle = titleText.text;
        }

        if (titleText != null && titleText.fontSize > 0f && !Mathf.Approximately(titleText.fontSize, titleFontSize))
        {
            titleFontSize = titleText.fontSize;
        }

        if (reflectionTextUi != null && !string.IsNullOrWhiteSpace(reflectionTextUi.text) && reflectionTextUi.text != reflectionText)
        {
            reflectionText = reflectionTextUi.text;
        }

        if (reflectionTextUi != null && reflectionTextUi.fontSize > 0f && !Mathf.Approximately(reflectionTextUi.fontSize, reflectionFontSize))
        {
            reflectionFontSize = reflectionTextUi.fontSize;
        }

        if (creditsTextUi != null && !string.IsNullOrWhiteSpace(creditsTextUi.text) && creditsTextUi.text != creditsText)
        {
            creditsText = creditsTextUi.text;
        }

        if (creditsTextUi != null && creditsTextUi.fontSize > 0f && !Mathf.Approximately(creditsTextUi.fontSize, creditsFontSize))
        {
            creditsFontSize = creditsTextUi.fontSize;
        }
    }

    private void SetInitialState()
    {
        SetGroupAlpha(titleCanvasGroup, 0f);
        SetGroupAlpha(reflectionCanvasGroup, 0f);
        SetGroupAlpha(creditsCanvasGroup, 0f);
        if (reflectionTextUi != null)
        {
            reflectionTextUi.text = string.Empty;
            reflectionTextUi.maxVisibleCharacters = 0;
        }
    }

    private IEnumerator RunSequence()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        yield return FadeGroup(titleCanvasGroup, 0f, 1f, titleFadeDuration);

        if (titleHoldDuration > 0f)
        {
            yield return new WaitForSeconds(titleHoldDuration);
        }

        yield return TypeReflectionText();

        if (reflectionHoldDuration > 0f)
        {
            yield return new WaitForSeconds(reflectionHoldDuration);
        }

        yield return FadeGroup(creditsCanvasGroup, 0f, 1f, creditsFadeDuration);
    }

    private IEnumerator TypeReflectionText()
    {
        if (reflectionTextUi == null)
        {
            yield break;
        }

        reflectionTextUi.text = reflectionText;
        reflectionTextUi.maxVisibleCharacters = 0;
        reflectionTextUi.ForceMeshUpdate();
        SetGroupAlpha(reflectionCanvasGroup, 1f);

        DialogueInlineEffects.ParsedLine parsedLine = DialogueInlineEffects.Parse(
            reflectionText ?? string.Empty,
            reflectionSecondsPerChar,
            reflectionTextUi.fontSize > 0f ? reflectionTextUi.fontSize : reflectionFontSize);

        if (typingSfxPlayer != null)
        {
            typingSfxPlayer.Reset();
        }

        int visible = 0;
        while (parsedLine != null && visible < parsedLine.VisibleCharacters.Count)
        {
            DialogueInlineEffects.VisibleCharacter visibleCharacter = parsedLine.VisibleCharacters[visible];
            visible++;
            reflectionTextUi.maxVisibleCharacters = visible;
            typingSfxPlayer?.ApplyPool(visibleCharacter.TypingSfxPoolId);
            typingSfxPlayer?.Play(reflectionTextUi, visible - 1, visibleCharacter.FontSizeScale);

            float delay = Mathf.Max(0f, visibleCharacter.SecondsPerCharacter);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            else
            {
                yield return null;
            }
        }

        reflectionTextUi.maxVisibleCharacters = int.MaxValue;
    }

    private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }

    private static void SetGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
        {
            group.alpha = alpha;
        }
    }

    private void EnsureTypingSfxSupport()
    {
        typingSfxReferenceBubble = DialogueBubbleView.ResolveTypingSfxReference(typingSfxReferenceBubble);
        typingAudioSource = DialogueBubbleView.EnsureExternalTypingAudioSource(this, "EndSceneTypingSfx");
        typingSfxPlayer = DialogueBubbleView.CreateExternalTypingSfxPlayer(typingSfxReferenceBubble, typingAudioSource);
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
