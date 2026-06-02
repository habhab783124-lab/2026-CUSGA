using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialCommanderBubble : MonoBehaviour
{
    [Header("Bubble Visual")]
    [SerializeField] private SpriteRenderer bubbleRenderer;
    [SerializeField] private float openDuration = 0.22f;
    [SerializeField] private float closeDuration = 0.16f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Text")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Vector2 textBoxSize = new Vector2(620f, 140f);
    [SerializeField] private float fontSize = 32f;
    [SerializeField] private Color textColor = new Color(0.96f, 0.98f, 1f, 1f);
    [SerializeField] private Vector2 textPadding = new Vector2(24f, 16f);

    [Header("Portrait")]
    [SerializeField] private Sprite portraitSprite;
    [SerializeField] private Vector2 portraitSize = new Vector2(96f, 96f);

    [Header("Layout")]
    [SerializeField] private bool autoAlignToRenderer = true;
    [SerializeField] private float portraitLeftMargin = 20f;
    [SerializeField] private float textBoxRightPadding = 20f;
    [SerializeField] private Vector2 boxAnchorPosition = new Vector2(380f, 140f);
    [SerializeField] private Vector2 portraitAnchorPosition = new Vector2(70f, 140f);

    [Header("Continue Prompt")]
    [SerializeField] private string continuePromptText = "<size=80%><color=#88AABB>[ 点击继续 ]</color></size>";

    private Canvas _canvas;
    private RectTransform _textBox;
    private TextMeshProUGUI _dialogueText;
    private Image _backgroundImage;
    private Image _portraitImage;
    private CanvasGroup _canvasGroup;
    private Coroutine _transitionRoutine;
    private bool _isOpen;
    private bool _visualsCreated;

    public bool IsOpen => _isOpen;

    public void ShowLine(string text, Action onComplete = null)
    {
        EnsureVisuals();

        string displayText = string.IsNullOrWhiteSpace(continuePromptText)
            ? text
            : text + "\n" + continuePromptText;

        if (autoAlignToRenderer)
        {
            AlignUiToRenderer();
        }

        if (_isOpen)
        {
            SetText(displayText);
            onComplete?.Invoke();
            return;
        }

        PlayOpen(() =>
        {
            SetText(displayText);
            onComplete?.Invoke();
        });
    }

    public void Hide(Action onComplete = null)
    {
        if (!_isOpen)
        {
            onComplete?.Invoke();
            return;
        }

        PlayClose(() =>
        {
            ClearText();
            onComplete?.Invoke();
        });
    }

    public void HideImmediate()
    {
        StopTransition();
        ApplyVisualState(0f);
        _isOpen = false;
        ClearText();
    }

    private void Awake()
    {
        if (bubbleRenderer == null)
        {
            bubbleRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        EnsureVisuals();
        ApplyVisualState(0f);
        _isOpen = false;
    }

    private void LateUpdate()
    {
        if (autoAlignToRenderer && _isOpen && bubbleRenderer != null)
        {
            AlignUiToRenderer();
        }
    }

    private void OnDestroy()
    {
        StopTransition();
    }

    private void SetText(string text)
    {
        if (_dialogueText != null)
        {
            _dialogueText.text = text ?? string.Empty;
            _dialogueText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private void ClearText()
    {
        if (_dialogueText != null)
        {
            _dialogueText.text = string.Empty;
        }
    }

    private void PlayOpen(Action onComplete)
    {
        StopTransition();
        _transitionRoutine = StartCoroutine(TransitionRoutine(0f, 1f, openDuration, openCurve, () =>
        {
            _isOpen = true;
            onComplete?.Invoke();
        }));
    }

    private void PlayClose(Action onComplete)
    {
        StopTransition();
        _transitionRoutine = StartCoroutine(TransitionRoutine(1f, 0f, closeDuration, closeCurve, () =>
        {
            _isOpen = false;
            onComplete?.Invoke();
        }));
    }

    private IEnumerator TransitionRoutine(float from, float to, float duration, AnimationCurve curve, Action onComplete)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float evaluated = curve != null ? curve.Evaluate(t) : t;
            float value = Mathf.LerpUnclamped(from, to, evaluated);
            ApplyVisualState(value);
            yield return null;
        }

        ApplyVisualState(to);
        _transitionRoutine = null;
        onComplete?.Invoke();
    }

    private void ApplyVisualState(float normalizedValue)
    {
        if (bubbleRenderer != null)
        {
            Color c = bubbleRenderer.color;
            c.a = normalizedValue;
            bubbleRenderer.color = c;

            Vector3 scale = bubbleRenderer.transform.localScale;
            scale.x = Mathf.Abs(scale.y) * normalizedValue;
            bubbleRenderer.transform.localScale = scale;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = normalizedValue;
        }
    }

    private void StopTransition()
    {
        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }
    }

    private void EnsureVisuals()
    {
        if (_visualsCreated)
        {
            return;
        }

        _visualsCreated = true;
        CreateOverlayCanvas();
        CreatePortrait();
        CreateTextBox();
    }

    private void CreateOverlayCanvas()
    {
        GameObject canvasGo = new GameObject("TutorialBubbleCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void CreatePortrait()
    {
        if (portraitSprite == null || _canvas == null)
        {
            return;
        }

        GameObject portraitGo = new GameObject("Portrait");
        portraitGo.transform.SetParent(_canvas.transform, false);

        RectTransform rect = portraitGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = portraitAnchorPosition;
        rect.sizeDelta = portraitSize;

        _portraitImage = portraitGo.AddComponent<Image>();
        _portraitImage.sprite = portraitSprite;
        _portraitImage.preserveAspect = true;
        _portraitImage.raycastTarget = false;
    }

    private void CreateTextBox()
    {
        if (_canvas == null)
        {
            return;
        }

        GameObject boxGo = new GameObject("TextBox");
        boxGo.transform.SetParent(_canvas.transform, false);

        _textBox = boxGo.AddComponent<RectTransform>();
        _textBox.anchorMin = new Vector2(0f, 0f);
        _textBox.anchorMax = new Vector2(0f, 0f);
        _textBox.pivot = new Vector2(0.5f, 0.5f);
        _textBox.anchoredPosition = boxAnchorPosition;
        _textBox.sizeDelta = textBoxSize;

        _backgroundImage = boxGo.AddComponent<Image>();
        _backgroundImage.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
        _backgroundImage.raycastTarget = false;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(boxGo.transform, false);

        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(textPadding.x, textPadding.y);
        textRect.offsetMax = new Vector2(-textPadding.x, -textPadding.y);

        _dialogueText = textGo.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            _dialogueText.font = fontAsset;
        }

        _dialogueText.color = textColor;
        _dialogueText.fontSize = fontSize;
        _dialogueText.enableAutoSizing = false;
        _dialogueText.richText = true;
        _dialogueText.enableWordWrapping = true;
        _dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
        _dialogueText.overflowMode = TextOverflowModes.Overflow;
        _dialogueText.raycastTarget = false;
        _dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    private void AlignUiToRenderer()
    {
        Camera cam = Camera.main;
        if (cam == null || bubbleRenderer == null || bubbleRenderer.sprite == null)
        {
            return;
        }

        Bounds bounds = bubbleRenderer.bounds;
        Vector3 screenMin = cam.WorldToScreenPoint(bounds.min);
        Vector3 screenMax = cam.WorldToScreenPoint(bounds.max);

        float canvasScale = 1f;
        if (_canvas != null)
        {
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                float logWidth = Mathf.Log(Screen.width / scaler.referenceResolution.x, 2);
                float logHeight = Mathf.Log(Screen.height / scaler.referenceResolution.y, 2);
                float logInterp = Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight);
                canvasScale = Mathf.Pow(2, logInterp);
            }
        }

        float left = screenMin.x / canvasScale;
        float right = screenMax.x / canvasScale;
        float bottom = screenMin.y / canvasScale;
        float top = screenMax.y / canvasScale;
        float centerX = (left + right) * 0.5f;
        float centerY = (bottom + top) * 0.5f;
        float bubbleWidth = right - left;
        float bubbleHeight = top - bottom;

        float portraitX = left + portraitLeftMargin + portraitSize.x * 0.5f;
        float portraitY = centerY;

        float textLeft;
        if (_portraitImage != null)
        {
            textLeft = portraitX + portraitSize.x * 0.5f + portraitLeftMargin;
        }
        else
        {
            textLeft = left + textBoxRightPadding;
        }
        float textRight = right - textBoxRightPadding;
        float textWidth = Mathf.Max(100f, textRight - textLeft);
        float textHeight = Mathf.Max(60f, bubbleHeight - textPadding.y * 2f);
        float textCenterX = (textLeft + textRight) * 0.5f;

        if (_portraitImage != null)
        {
            RectTransform pr = _portraitImage.rectTransform;
            pr.anchoredPosition = new Vector2(portraitX, portraitY);
        }

        if (_textBox != null)
        {
            _textBox.anchoredPosition = new Vector2(textCenterX, centerY);
            _textBox.sizeDelta = new Vector2(textWidth, textHeight);
        }
    }
}
