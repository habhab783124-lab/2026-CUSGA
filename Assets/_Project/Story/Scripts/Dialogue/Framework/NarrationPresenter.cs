using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NarrationPresenter : MonoBehaviour
{
    [SerializeField] private string canvasObjectName = "NarrationCanvas";
    [SerializeField] private string textBoxObjectName = "DialogueBox";
    [SerializeField] private string textObjectName = "DialogueText";
    [SerializeField] private string typingSfxChildName = "NarrationTypingSfx";

    [SerializeField] private TMP_FontAsset dialogueFontAsset;
    [SerializeField] private Vector2 boxSize = new Vector2(1280f, 220f);
    [SerializeField] private Vector2 bottomOffset = new Vector2(0f, 120f);
    [SerializeField] private bool useCustomBoxLayout;
    [SerializeField] private Vector2 boxAnchorMin = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 boxAnchorMax = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 boxPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 boxAnchoredPosition = Vector2.zero;
    [SerializeField] private float backgroundAlpha = 0.6f;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float minFontSize = 24f;
    [SerializeField] private float maxFontSize = 42f;
    [SerializeField] private Vector2 textPadding = new Vector2(40f, 25f);
    [SerializeField] private float secondsPerChar = 0.03f;
    [SerializeField] private DialogueBubbleView typingSfxReferenceBubble;

    private Canvas canvas;
    private RectTransform textBox;
    private Image background;
    private TextMeshProUGUI dialogueText;
    private CanvasGroup canvasGroup;
    private AudioSource typingAudioSource;
    private DialogueBubbleView.ExternalTypingSfxPlayer typingSfxPlayer;
    private Coroutine typingRoutine;
    private IReadOnlyList<DialogueLine> lines;
    private DialogueInlineEffects.ParsedLine currentParsedLine;
    private string currentRawText = string.Empty;
    private int currentLineIndex = -1;
    private bool isTyping;
    private bool isPaused;
    private bool skipTyping;
    private Action currentOnComplete;

    public bool IsTyping => isTyping;
    public bool IsPaused => isPaused;
    public int CurrentLineIndex => currentLineIndex;
    public bool HasReachedEnd => lines != null && lines.Count > 0 && currentLineIndex >= lines.Count - 1;

    public void SetUiNames(string canvasName, string boxName, string textName)
    {
        if (!string.IsNullOrWhiteSpace(canvasName))
        {
            canvasObjectName = canvasName;
        }

        if (!string.IsNullOrWhiteSpace(boxName))
        {
            textBoxObjectName = boxName;
        }

        if (!string.IsNullOrWhiteSpace(textName))
        {
            textObjectName = textName;
        }
    }

    public void ConfigureAppearance(
        TMP_FontAsset fontAsset,
        Vector2 newBoxSize,
        Vector2 newBottomOffset,
        float newBackgroundAlpha,
        Color newBackgroundColor,
        Color newTextColor,
        float newMinFontSize,
        float newMaxFontSize,
        Vector2 newTextPadding)
    {
        dialogueFontAsset = fontAsset;
        boxSize = newBoxSize;
        bottomOffset = newBottomOffset;
        backgroundAlpha = Mathf.Clamp01(newBackgroundAlpha);
        backgroundColor = newBackgroundColor;
        textColor = newTextColor;
        minFontSize = newMinFontSize;
        maxFontSize = newMaxFontSize;
        textPadding = newTextPadding;
        ApplyStyle();
    }

    public void ConfigureLayout(Vector2 newAnchorMin, Vector2 newAnchorMax, Vector2 newPivot, Vector2 newAnchoredPosition)
    {
        useCustomBoxLayout = true;
        boxAnchorMin = newAnchorMin;
        boxAnchorMax = newAnchorMax;
        boxPivot = newPivot;
        boxAnchoredPosition = newAnchoredPosition;
        ApplyStyle();
    }

    public void UseBottomLayout()
    {
        useCustomBoxLayout = false;
        ApplyStyle();
    }

    public void ConfigureTyping(float newSecondsPerChar, DialogueBubbleView sfxReferenceBubble, string sfxChildName = null)
    {
        secondsPerChar = Mathf.Max(0f, newSecondsPerChar);
        typingSfxReferenceBubble = sfxReferenceBubble;
        if (!string.IsNullOrWhiteSpace(sfxChildName))
        {
            typingSfxChildName = sfxChildName;
        }

        EnsureTypingSfxSupport();
    }

    public void SetLines(IReadOnlyList<DialogueLine> source)
    {
        lines = source;
        if ((lines == null || lines.Count == 0) && dialogueText != null)
        {
            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = 0;
        }
    }

    public void PlayText(string content, Action onComplete = null)
    {
        lines = null;
        currentLineIndex = 0;
        currentRawText = content ?? string.Empty;
        currentOnComplete = onComplete;
        StartTyping(currentRawText);
    }

    public void CompleteTyping()
    {
        if (!isTyping)
        {
            return;
        }

        if (isPaused)
        {
            isPaused = false;
        }

        if (dialogueText != null)
        {
            dialogueText.text = currentParsedLine != null ? currentParsedLine.DisplayText : string.Empty;
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }

        FinishTyping();
    }

    public void Show()
    {
        EnsureVisuals();
        ApplyStyle();
        if (textBox != null)
        {
            textBox.gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void Hide()
    {
        StopTyping();
        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = 0;
        }

        if (textBox != null)
        {
            textBox.gameObject.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    public bool TryHandleAdvanceInput()
    {
        if (!isTyping)
        {
            return false;
        }

        if (isPaused)
        {
            isPaused = false;
            return true;
        }

        skipTyping = true;
        return true;
    }

    public void ShowLine(int lineIndex)
    {
        EnsureVisuals();
        ApplyStyle();

        if (lines == null || lines.Count == 0)
        {
            if (dialogueText != null)
            {
                dialogueText.text = string.Empty;
            }

            return;
        }

        if (lineIndex < 0 || lineIndex >= lines.Count)
        {
            return;
        }

        Show();
        currentLineIndex = lineIndex;
        string fullText = lines[currentLineIndex] != null ? lines[currentLineIndex].text ?? string.Empty : string.Empty;

        currentRawText = fullText;
        currentOnComplete = null;
        StartTyping(fullText);
    }

    public void RefreshShownText()
    {
        EnsureVisuals();
        ApplyStyle();

        if (dialogueText == null || lines == null || lines.Count == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(currentLineIndex, 0, lines.Count - 1);
        string raw = lines[clampedIndex] != null ? lines[clampedIndex].text ?? string.Empty : string.Empty;
        DialogueInlineEffects.ParsedLine parsedLine = DialogueInlineEffects.Parse(
            raw,
            secondsPerChar,
            dialogueText.fontSize > 0f ? dialogueText.fontSize : maxFontSize);
        dialogueText.richText = true;
        dialogueText.text = parsedLine.DisplayText;
        dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    private void StartTyping(string fullText)
    {
        EnsureVisuals();
        ApplyStyle();
        Show();
        StopTyping();
        currentParsedLine = DialogueInlineEffects.Parse(
            fullText ?? string.Empty,
            secondsPerChar,
            dialogueText != null && dialogueText.fontSize > 0f ? dialogueText.fontSize : maxFontSize);
        typingSfxPlayer?.Reset();
        typingRoutine = StartCoroutine(TypeLineRoutine());
    }

    private void EnsureVisuals()
    {
        if (canvas == null)
        {
            Transform existingCanvas = transform.Find(canvasObjectName);
            if (existingCanvas != null)
            {
                canvas = existingCanvas.GetComponent<Canvas>();
            }
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                canvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        Transform existingBox = canvas.transform.Find(textBoxObjectName);
        if (existingBox == null)
        {
            GameObject boxObject = new GameObject(
                textBoxObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            boxObject.transform.SetParent(canvas.transform, false);
            textBox = boxObject.GetComponent<RectTransform>();
            background = boxObject.GetComponent<Image>();
            canvasGroup = boxObject.GetComponent<CanvasGroup>();

            GameObject textObject = new GameObject(
                textObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(boxObject.transform, false);
            dialogueText = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textBox = existingBox as RectTransform;
            background = existingBox.GetComponent<Image>();
            canvasGroup = existingBox.GetComponent<CanvasGroup>() ?? existingBox.gameObject.AddComponent<CanvasGroup>();
            dialogueText = existingBox.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        EnsureTypingSfxSupport();
    }

    private void ApplyStyle()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        if (textBox != null)
        {
            if (useCustomBoxLayout)
            {
                textBox.anchorMin = boxAnchorMin;
                textBox.anchorMax = boxAnchorMax;
                textBox.pivot = boxPivot;
                textBox.anchoredPosition = boxAnchoredPosition;
            }
            else
            {
                textBox.anchorMin = new Vector2(0.5f, 0f);
                textBox.anchorMax = new Vector2(0.5f, 0f);
                textBox.pivot = new Vector2(0.5f, 0.5f);
                textBox.anchoredPosition = bottomOffset;
            }

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
            dialogueText.richText = true;
            dialogueText.enableWordWrapping = true;
            dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            dialogueText.overflowMode = TextOverflowModes.Overflow;
            dialogueText.raycastTarget = false;
        }
    }

    private IEnumerator TypeLineRoutine()
    {
        isTyping = true;
        isPaused = false;
        skipTyping = false;

        if (dialogueText == null)
        {
            FinishTyping();
            yield break;
        }

        dialogueText.richText = true;
        dialogueText.text = currentParsedLine != null ? currentParsedLine.DisplayText : string.Empty;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        List<DialogueInlineEffects.VisibleCharacter> visibleCharacters =
            currentParsedLine != null ? currentParsedLine.VisibleCharacters : null;
        if (visibleCharacters == null || visibleCharacters.Count == 0)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            FinishTyping();
            yield break;
        }

        int visible = 0;
        int lastPauseIndex = -1;
        while (visible < visibleCharacters.Count)
        {
            if (skipTyping)
            {
                dialogueText.maxVisibleCharacters = int.MaxValue;
                break;
            }

            if (isPaused)
            {
                yield return null;
                continue;
            }

            DialogueInlineEffects.VisibleCharacter visibleCharacter = visibleCharacters[visible];
            if (visibleCharacter.PauseBefore && lastPauseIndex != visible)
            {
                isPaused = true;
                lastPauseIndex = visible;
                continue;
            }

            visible++;
            dialogueText.maxVisibleCharacters = visible;
            typingSfxPlayer?.ApplyPool(visibleCharacter.TypingSfxPoolId);
            typingSfxPlayer?.Play(dialogueText, visible - 1, visibleCharacter.FontSizeScale);

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

        dialogueText.text = currentParsedLine != null ? currentParsedLine.DisplayText : string.Empty;
        dialogueText.maxVisibleCharacters = int.MaxValue;
        FinishTyping();
    }

    private void StopTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
        isPaused = false;
        skipTyping = false;
    }

    private void FinishTyping()
    {
        isTyping = false;
        isPaused = false;
        skipTyping = false;
        typingRoutine = null;

        Action callback = currentOnComplete;
        currentOnComplete = null;
        callback?.Invoke();
    }

    private void EnsureTypingSfxSupport()
    {
        typingSfxReferenceBubble = DialogueBubbleView.ResolveTypingSfxReference(typingSfxReferenceBubble);
        typingAudioSource = DialogueBubbleView.EnsureExternalTypingAudioSource(this, typingSfxChildName);
        typingSfxPlayer = DialogueBubbleView.CreateExternalTypingSfxPlayer(typingSfxReferenceBubble, typingAudioSource);
    }
}
