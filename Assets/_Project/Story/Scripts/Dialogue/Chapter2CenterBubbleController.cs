using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Chapter2CenterBubbleController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private TMP_FontAsset dialogueFontAsset;

    [Header("文字")]
    [SerializeField] private Vector2 textScreenPosition = new(320f, 120f);
    [SerializeField] private Vector2 textSize = new(760f, 240f);
    [SerializeField] private float textFontSize = 46f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private FontStyles textFontStyle = FontStyles.Normal;
    [SerializeField] private TextAlignmentOptions textAlignment = TextAlignmentOptions.MidlineLeft;
    [SerializeField] private float secondsPerCharacter = 0.03f;

    [Header("展开/关闭")]
    [SerializeField] private float openDuration = 0.32f;
    [SerializeField] private float closeDuration = 0.24f;
    [SerializeField] private AnimationCurve widthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve closeWidthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve closeAlphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("投影感")]
    [SerializeField] private float overshootScaleX = 1.06f;
    [SerializeField] private float overshootDuration = 0.06f;
    [SerializeField] private float flickerAlphaDrop = 0.72f;
    [SerializeField] private int closeFlickerCount = 2;
    [SerializeField] private float closeFlickerInterval = 0.03f;

    private Canvas overlayCanvas;
    private TextMeshProUGUI dialogueText;
    private CanvasGroup dialogueTextCanvasGroup;
    private Transform bubbleVisualTransform;
    private Vector3 initialScale = Vector3.one;
    private Color initialColor = Color.white;
    private bool hasInitialVisualState;
    private Coroutine transitionRoutine;
    private Coroutine typingRoutine;
    private bool isOpen;
    private bool isTyping;
    private string currentFullText = string.Empty;
    private Action onTypingFinished;

    public bool IsOpen => isOpen;
    public bool IsTransitioning => transitionRoutine != null;
    public bool IsTyping => isTyping;

    private void Awake()
    {
        ResolveTargetRenderer();
        CacheInitialVisualStateIfNeeded();
        EnsureTextUi();
        ApplyFont();
        ApplyTextStyle();
    }

    private void OnEnable()
    {
        SetClosedImmediate();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ResolveTargetRenderer();
            CaptureInitialVisualState();

            EnsureTextUi();
        }

        ApplyFont();
        ApplyTextStyle();
    }

    public void ShowNpcLine(string content, Action onComplete = null)
    {
        ResolveTargetRenderer();
        CacheInitialVisualStateIfNeeded();
        EnsureTextUi();
        ApplyFont();
        ApplyTextStyle();
        onTypingFinished = onComplete;
        currentFullText = content ?? string.Empty;

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
            dialogueText.gameObject.SetActive(true);
        }

        if (dialogueTextCanvasGroup != null)
        {
            dialogueTextCanvasGroup.alpha = 1f;
        }

        if (isOpen)
        {
            StartTypingCurrentText();
            return;
        }

        PlayOpen(() => StartTypingCurrentText());
    }

    public void CompleteTyping()
    {
        StopTypingRoutine();
        if (dialogueText != null)
        {
            dialogueText.text = currentFullText;
        }

        isTyping = false;
        onTypingFinished?.Invoke();
        onTypingFinished = null;
    }

    public void HideNpcLine(Action onComplete = null)
    {
        StopTypingRoutine();
        currentFullText = string.Empty;
        onTypingFinished = null;

        if (!isOpen)
        {
            HideTextImmediate();
            onComplete?.Invoke();
            return;
        }

        PlayClose(() =>
        {
            HideTextImmediate();
            onComplete?.Invoke();
        });
    }

    public void SetClosedImmediate()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        StopTypingRoutine();
        ResolveTargetRenderer();
        CacheInitialVisualStateIfNeeded();
        ApplyVisual(0f, 0f);
        HideTextImmediate();
        isOpen = false;
    }

    public void PlayOpen(Action onComplete = null)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(PlayOpenRoutine(onComplete));
    }

    public void PlayClose(Action onComplete = null)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(PlayCloseRoutine(onComplete));
    }

    private void ResolveTargetRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (targetRenderer == null)
        {
            targetRenderer = FindCenterBubbleRenderer();
        }

        bubbleVisualTransform = targetRenderer != null ? targetRenderer.transform : transform;
    }

    private void CacheInitialVisualStateIfNeeded()
    {
        if (hasInitialVisualState)
        {
            return;
        }

        CaptureInitialVisualState();
    }

    private void CaptureInitialVisualState()
    {
        ResolveTargetRenderer();

        if (targetRenderer != null)
        {
            initialColor = targetRenderer.color;
        }

        Vector3 currentScale = bubbleVisualTransform != null ? bubbleVisualTransform.localScale : transform.localScale;
        if (Mathf.Abs(currentScale.x) > 0.0001f)
        {
            initialScale = currentScale;
        }
        else if (Mathf.Abs(initialScale.x) <= 0.0001f)
        {
            initialScale = new Vector3(1f, currentScale.y != 0f ? currentScale.y : 1f, currentScale.z != 0f ? currentScale.z : 1f);
        }

        hasInitialVisualState = true;
    }

    private SpriteRenderer FindCenterBubbleRenderer()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.gameObject.name == "CenterBubble")
            {
                return renderer;
            }
        }

        return null;
    }

    private void EnsureTextUi()
    {
        if (overlayCanvas == null)
        {
            Transform existingCanvas = transform.Find("Chapter2CenterBubbleCanvas");
            if (existingCanvas != null)
            {
                overlayCanvas = existingCanvas.GetComponent<Canvas>();
            }
        }

        if (overlayCanvas == null)
        {
            GameObject existingCanvasObject = GameObject.Find("Chapter2CenterBubbleCanvas");
            if (existingCanvasObject != null)
            {
                overlayCanvas = existingCanvasObject.GetComponent<Canvas>();
            }
        }

        if (overlayCanvas == null)
        {
            GameObject canvasObject = new GameObject("Chapter2CenterBubbleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayCanvas = canvasObject.GetComponent<Canvas>();
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        overlayCanvas.transform.SetParent(null, false);
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        Transform existingText = overlayCanvas.transform.Find("DialogueText");
        if (existingText != null)
        {
            dialogueText = existingText.GetComponent<TextMeshProUGUI>();
            dialogueTextCanvasGroup = existingText.GetComponent<CanvasGroup>();
        }

        if (dialogueText == null)
        {
            GameObject textObject = new GameObject("DialogueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup));
            textObject.transform.SetParent(overlayCanvas.transform, false);
            dialogueText = textObject.GetComponent<TextMeshProUGUI>();
            dialogueTextCanvasGroup = textObject.GetComponent<CanvasGroup>();
        }
        else if (dialogueTextCanvasGroup == null)
        {
            dialogueTextCanvasGroup = dialogueText.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ApplyFont()
    {
        if (dialogueFontAsset == null)
        {
            dialogueFontAsset = TryLoadDialogueFont();
        }

        if (dialogueText != null && dialogueFontAsset != null)
        {
            dialogueText.font = dialogueFontAsset;
        }
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

    private void ApplyTextStyle()
    {
        if (dialogueText == null)
        {
            return;
        }

        RectTransform rect = dialogueText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = textScreenPosition;
        rect.sizeDelta = textSize;

        dialogueText.fontSize = textFontSize;
        dialogueText.color = textColor;
        dialogueText.fontStyle = textFontStyle;
        dialogueText.alignment = textAlignment;
        dialogueText.enableWordWrapping = true;
        dialogueText.overflowMode = TextOverflowModes.Overflow;
        dialogueText.raycastTarget = false;

        if (dialogueTextCanvasGroup != null)
        {
            dialogueTextCanvasGroup.alpha = dialogueText.gameObject.activeSelf ? 1f : 0f;
        }
    }

    private void StartTypingCurrentText()
    {
        StopTypingRoutine();
        typingRoutine = StartCoroutine(TypeTextRoutine(currentFullText));
    }

    private IEnumerator TypeTextRoutine(string content)
    {
        isTyping = true;
        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (string.IsNullOrEmpty(content))
        {
            isTyping = false;
            typingRoutine = null;
            onTypingFinished?.Invoke();
            onTypingFinished = null;
            yield break;
        }

        float delay = Mathf.Max(0.001f, secondsPerCharacter);
        for (int i = 1; i <= content.Length; i++)
        {
            if (dialogueText != null)
            {
                dialogueText.text = content.Substring(0, i);
            }

            if (i < content.Length)
            {
                yield return new WaitForSeconds(delay);
            }
        }

        isTyping = false;
        typingRoutine = null;
        onTypingFinished?.Invoke();
        onTypingFinished = null;
    }

    private void StopTypingRoutine()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
    }

    private IEnumerator PlayOpenRoutine(Action onComplete)
    {
        yield return Animate(openDuration, widthCurve, alphaCurve);

        if (overshootDuration > 0f)
        {
            float elapsed = 0f;
            Vector3 from = new Vector3(initialScale.x, initialScale.y, initialScale.z);
            Vector3 to = new Vector3(initialScale.x * overshootScaleX, initialScale.y, initialScale.z);
            while (elapsed < overshootDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / overshootDuration);
                if (bubbleVisualTransform != null)
                {
                    bubbleVisualTransform.localScale = Vector3.LerpUnclamped(from, to, t);
                }

                yield return null;
            }

            elapsed = 0f;
            while (elapsed < overshootDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / overshootDuration);
                if (bubbleVisualTransform != null)
                {
                    bubbleVisualTransform.localScale = Vector3.LerpUnclamped(to, initialScale, t);
                }

                yield return null;
            }
        }

        ApplyVisual(1f, 1f);
        transitionRoutine = null;
        isOpen = true;
        onComplete?.Invoke();
    }

    private IEnumerator PlayCloseRoutine(Action onComplete)
    {
        for (int i = 0; i < closeFlickerCount; i++)
        {
            SetAlpha(flickerAlphaDrop);
            yield return new WaitForSeconds(closeFlickerInterval);
            SetAlpha(1f);
            yield return new WaitForSeconds(closeFlickerInterval);
        }

        yield return Animate(closeDuration, closeWidthCurve, closeAlphaCurve);
        ApplyVisual(0f, 0f);
        transitionRoutine = null;
        isOpen = false;
        onComplete?.Invoke();
    }

    private IEnumerator Animate(float duration, AnimationCurve scaleXCurve, AnimationCurve alphaAnimCurve)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float scaleX = scaleXCurve != null ? scaleXCurve.Evaluate(t) : t;
            float alpha = alphaAnimCurve != null ? alphaAnimCurve.Evaluate(t) : t;
            ApplyVisual(scaleX, alpha);
            yield return null;
        }
    }

    private void ApplyVisual(float normalizedScaleX, float normalizedAlpha)
    {
        float width = Mathf.Max(0f, normalizedScaleX);
        if (bubbleVisualTransform == null)
        {
            ResolveTargetRenderer();
        }

        if (bubbleVisualTransform != null)
        {
            bubbleVisualTransform.localScale = new Vector3(initialScale.x * width, initialScale.y, initialScale.z);
        }

        SetAlpha(normalizedAlpha);
    }

    private void SetAlpha(float normalizedAlpha)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (bubbleVisualTransform == null)
        {
            bubbleVisualTransform = targetRenderer.transform;
        }

        Color color = initialColor;
        color.a *= Mathf.Clamp01(normalizedAlpha);
        targetRenderer.color = color;
    }

    private void HideTextImmediate()
    {
        if (dialogueText != null)
        {
            dialogueText.gameObject.SetActive(false);
            dialogueText.text = string.Empty;
        }

        if (dialogueTextCanvasGroup != null)
        {
            dialogueTextCanvasGroup.alpha = 0f;
        }
    }
}
