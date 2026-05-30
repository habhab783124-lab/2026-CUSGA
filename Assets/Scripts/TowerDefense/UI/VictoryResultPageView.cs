using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// `VictoryResultPageView` 负责驱动正式结果页 prefab 的运行时显示。
///
/// 它和 `VictoryResultPreviewController` 的关系是：
/// - 预览控制器负责在独立 scene 里验证布局、气质和节奏
/// - 本类负责在正式塔防流程里“拿到现成 prefab 后，把文案、主题与显隐跑起来”
///
/// 这样正式流程不需要依赖预览 scene，
/// 但仍然可以复用同一套 prefab 结构。
/// </summary>
public sealed class VictoryResultPageView : MonoBehaviour
{
    [System.Serializable]
    private sealed class ResultPageThemePalette
    {
        [Header("Background")]
        public Color dimBackgroundColor = new Color(0.03f, 0.07f, 0.11f, 0.72f);
        public Color vignetteOverlayColor = new Color(0.01f, 0.04f, 0.07f, 0.58f);
        public Color holoNoiseOverlayColor = new Color(0.2f, 0.92f, 1f, 0.06f);
        public Color scanBandColor = new Color(0.46f, 0.98f, 1f, 0.12f);

        [Header("Panels")]
        public Color mainBriefPanelColor = new Color(0.05f, 0.11f, 0.16f, 0.86f);
        public Color mainPanelInnerColor = new Color(0.02f, 0.08f, 0.12f, 0.64f);
        public Color eventBlockColor = new Color(0.08f, 0.2f, 0.28f, 0.22f);
        public Color projectionShellColor = new Color(0.07f, 0.19f, 0.26f, 0.24f);
        public Color projectionInnerColor = new Color(0.05f, 0.11f, 0.16f, 0.72f);
        public Color dialogueBubbleColor = new Color(0.08f, 0.18f, 0.26f, 0.42f);
        public Color chipPanelColor = new Color(0.06f, 0.14f, 0.18f, 0.72f);

        [Header("Lines")]
        public Color lineColor = new Color(0.31f, 0.95f, 1f, 0.9f);
        public Color accentColor = new Color(0.45f, 0.98f, 1f, 0.9f);
        public Color portraitGlowColor = new Color(0.29f, 0.9f, 1f, 0.18f);
        public Color portraitTintColor = new Color(0.76f, 0.96f, 1f, 0.96f);

        [Header("Text")]
        public Color titleTextColor = new Color(0.94f, 0.98f, 1f, 1f);
        public Color bodyTextColor = new Color(0.86f, 0.95f, 1f, 1f);
        public Color secondaryTextColor = new Color(0.54f, 0.76f, 0.86f, 1f);
        public Color accentTextColor = new Color(0.47f, 0.97f, 1f, 1f);
        public Color buttonTextColor = new Color(0.96f, 0.99f, 1f, 1f);

        [Header("Button")]
        public Color continueButtonNormalColor = new Color(0.17f, 0.72f, 0.92f, 0.92f);
        public Color continueButtonHighlightedColor = new Color(0.29f, 0.82f, 1f, 1f);
        public Color continueButtonPressedColor = new Color(0.09f, 0.49f, 0.69f, 1f);
        public Color continueButtonDisabledColor = new Color(0.12f, 0.24f, 0.32f, 0.5f);
    }

    public enum ResultPageTone
    {
        Victory,
        Failure
    }

    [System.Serializable]
    private sealed class FailureLayoutTuning
    {
        [Header("Main Brief")]
        public Vector2 mainBriefAnchoredPosition = new Vector2(164f, 36f);
        public Vector2 mainBriefSizeDelta = new Vector2(812f, 470f);

        [Header("Commander Projection")]
        public Vector2 projectionAnchoredPosition = new Vector2(-96f, 6f);
        public Vector2 projectionSizeDelta = new Vector2(470f, 596f);

        [Header("Dialogue Bubble")]
        public Vector2 dialogueBubbleAnchoredPosition = new Vector2(20f, 14f);
        public Vector2 dialogueBubbleSizeDelta = new Vector2(362f, 196f);
        public Vector2 dialogueTextAnchoredPosition = new Vector2(22f, -58f);
        public Vector2 dialogueTextSizeDelta = new Vector2(316f, 124f);

        [Header("Event Block")]
        public Vector2 eventBlockAnchoredPosition = new Vector2(42f, -338f);
        public Vector2 eventBlockSizeDelta = new Vector2(626f, 102f);

        [Header("Signal Sweep")]
        public Vector2 scanBandAnchoredPosition = new Vector2(0f, 148f);
        public Vector2 scanBandSizeDelta = new Vector2(1920f, 196f);
        public Vector2 portraitGlowAnchoredPosition = new Vector2(0f, -12f);
        public Vector2 portraitGlowSizeDelta = new Vector2(398f, 538f);
        public Vector2 portraitScanAnchoredPosition = new Vector2(0f, 106f);
        public Vector2 portraitScanSizeDelta = new Vector2(344f, 112f);

        [Header("Continue")]
        public Vector2 continueButtonAnchoredPosition = new Vector2(0f, 54f);
        public Vector2 continueButtonSizeDelta = new Vector2(272f, 60f);
        public Vector2 continueHintAnchoredPosition = new Vector2(0f, 18f);
        public Vector2 continueHintSizeDelta = new Vector2(420f, 28f);
    }

    [System.Serializable]
    private sealed class FailureDamageEffectTuning
    {
        [Header("Noise / Scan")]
        public float noiseAlphaPulseAmplitude = 0.24f;
        public float noiseAlphaPulseSpeed = 7.4f;
        public float scanBandDriftAmplitude = 34f;
        public float scanBandDriftSpeed = 0.92f;
        public float scanBandAlphaPulseAmplitude = 0.26f;
        public float scanBandAlphaPulseSpeed = 2.8f;

        [Header("Projection Distortion")]
        public float projectionJitterXAmplitude = 5f;
        public float projectionJitterYAmplitude = 2.6f;
        public float projectionJitterSpeed = 11.5f;
        public float dialogueJitterXAmplitude = 1.8f;
        public float dialogueJitterYAmplitude = 1.1f;
        public float dialogueJitterSpeed = 13.1f;

        [Header("Alert Pulse")]
        public float portraitGlowScalePulseAmplitude = 0.08f;
        public float portraitGlowScalePulseSpeed = 2.9f;
        public float portraitGlowAlphaPulseAmplitude = 0.22f;
        public float portraitGlowAlphaPulseSpeed = 3.4f;
        public float continueButtonPulseAmplitude = 0.07f;
        public float continueButtonPulseSpeed = 4.2f;
        public float continueHintBlinkAmplitude = 0.32f;
        public float continueHintBlinkSpeed = 2.3f;
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private ResultPageThemePalette victoryTheme = new ResultPageThemePalette();
    [SerializeField] private ResultPageThemePalette failureTheme = new ResultPageThemePalette
    {
        dimBackgroundColor = new Color(0.09f, 0.02f, 0.03f, 0.82f),
        vignetteOverlayColor = new Color(0.12f, 0.02f, 0.04f, 0.66f),
        holoNoiseOverlayColor = new Color(1f, 0.22f, 0.26f, 0.13f),
        scanBandColor = new Color(1f, 0.28f, 0.3f, 0.2f),
        mainBriefPanelColor = new Color(0.18f, 0.04f, 0.06f, 0.9f),
        mainPanelInnerColor = new Color(0.12f, 0.02f, 0.03f, 0.8f),
        eventBlockColor = new Color(0.25f, 0.06f, 0.08f, 0.3f),
        projectionShellColor = new Color(0.17f, 0.03f, 0.05f, 0.34f),
        projectionInnerColor = new Color(0.19f, 0.03f, 0.05f, 0.82f),
        dialogueBubbleColor = new Color(0.16f, 0.03f, 0.05f, 0.56f),
        chipPanelColor = new Color(0.24f, 0.05f, 0.07f, 0.78f),
        lineColor = new Color(1f, 0.24f, 0.28f, 0.96f),
        accentColor = new Color(1f, 0.36f, 0.4f, 1f),
        portraitGlowColor = new Color(1f, 0.15f, 0.19f, 0.34f),
        portraitTintColor = new Color(1f, 0.74f, 0.74f, 0.96f),
        titleTextColor = new Color(1f, 0.95f, 0.95f, 1f),
        bodyTextColor = new Color(1f, 0.88f, 0.88f, 1f),
        secondaryTextColor = new Color(0.98f, 0.5f, 0.54f, 1f),
        accentTextColor = new Color(1f, 0.34f, 0.38f, 1f),
        buttonTextColor = new Color(1f, 0.97f, 0.97f, 1f),
        continueButtonNormalColor = new Color(0.86f, 0.11f, 0.15f, 0.96f),
        continueButtonHighlightedColor = new Color(1f, 0.18f, 0.22f, 1f),
        continueButtonPressedColor = new Color(0.62f, 0.06f, 0.09f, 1f),
        continueButtonDisabledColor = new Color(0.32f, 0.06f, 0.08f, 0.56f)
    };

    [Header("Top Signal")]
    [SerializeField] private TMP_Text signalTitleText;
    [SerializeField] private TMP_Text signalStatusText;
    [SerializeField] private TMP_Text signalChannelText;
    [SerializeField] private CanvasGroup topSignalBarGroup;

    [Header("Brief")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text reportHeaderText;
    [SerializeField] private TMP_Text integrityRowText;
    [SerializeField] private TMP_Text scrapRowText;
    [SerializeField] private TMP_Text eventRowText;
    [SerializeField] private TMP_Text footerHintText;
    [SerializeField] private CanvasGroup mainBriefPanelGroup;

    [Header("Commander")]
    [SerializeField] private TMP_Text commanderNameText;
    [SerializeField] private TMP_Text commanderCodenameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private CanvasGroup commanderProjectionGroup;

    [Header("Continue")]
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonText;
    [SerializeField] private TMP_Text continueHintText;
    [SerializeField] private CanvasGroup continueButtonGroup;

    [Header("Failure Layout")]
    [SerializeField] private FailureLayoutTuning failureLayout = new FailureLayoutTuning();

    [Header("Failure Damage FX")]
    [SerializeField] private FailureDamageEffectTuning failureDamageEffects = new FailureDamageEffectTuning();

    [Header("Scene Authoring")]
    [SerializeField] private bool preserveSceneVisuals = false;

    [Header("Reveal")]
    [SerializeField] private bool autoReveal = true;
    [SerializeField] private float topBarRevealDelay = 0.05f;
    [SerializeField] private float mainPanelRevealDelay = 0.18f;
    [SerializeField] private float projectionRevealDelay = 0.34f;
    [SerializeField] private float continueRevealDelay = 0.72f;
    [SerializeField] private float continueHintRevealDelay = 0.88f;
    [SerializeField] private float typewriterCharactersPerSecond = 36f;

    private float _revealStartedAt;
    private bool _isShowing;
    private string _cachedTitle = string.Empty;
    private string _cachedSubtitle = string.Empty;
    private string _cachedEvent = string.Empty;
    private string _cachedDialogue = string.Empty;
    private string _cachedContinueHint = string.Empty;
    private ResultPageTone _currentTone = ResultPageTone.Victory;
    private bool _defaultLayoutCaptured;
    private RectTransform _mainBriefRect;
    private RectTransform _projectionRect;
    private RectTransform _dialogueBubbleRect;
    private RectTransform _dialogueTextRect;
    private RectTransform _eventBlockRect;
    private RectTransform _scanBandRect;
    private RectTransform _portraitGlowRect;
    private RectTransform _portraitScanRect;
    private RectTransform _continueButtonRect;
    private RectTransform _continueHintRect;
    private Graphic _holoNoiseGraphic;
    private Graphic _scanBandGraphic;
    private Graphic _portraitGlowGraphic;
    private Graphic _continueButtonGraphic;
    private LayoutSnapshot _defaultMainBriefLayout;
    private LayoutSnapshot _defaultProjectionLayout;
    private LayoutSnapshot _defaultDialogueBubbleLayout;
    private LayoutSnapshot _defaultDialogueTextLayout;
    private LayoutSnapshot _defaultEventBlockLayout;
    private LayoutSnapshot _defaultScanBandLayout;
    private LayoutSnapshot _defaultPortraitGlowLayout;
    private LayoutSnapshot _defaultPortraitScanLayout;
    private LayoutSnapshot _defaultContinueButtonLayout;
    private LayoutSnapshot _defaultContinueHintLayout;

    private readonly struct LayoutSnapshot
    {
        public LayoutSnapshot(RectTransform rectTransform)
        {
            AnchoredPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
            SizeDelta = rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;
        }

        public Vector2 AnchoredPosition { get; }
        public Vector2 SizeDelta { get; }
    }

    private void Awake()
    {
        ResolveReferences();
        SetVisible(false);
    }

    private void Update()
    {
        if (!_isShowing || !autoReveal)
        {
            return;
        }

        float elapsed = Time.unscaledTime - _revealStartedAt;

        ApplyGroupAlpha(topSignalBarGroup, EaseReveal(elapsed - topBarRevealDelay, 0.28f));
        ApplyGroupAlpha(mainBriefPanelGroup, EaseReveal(elapsed - mainPanelRevealDelay, 0.36f));
        ApplyGroupAlpha(commanderProjectionGroup, EaseReveal(elapsed - projectionRevealDelay, 0.42f));
        ApplyGroupAlpha(continueButtonGroup, EaseReveal(elapsed - continueRevealDelay, 0.24f));

        if (!preserveSceneVisuals)
        {
            ApplyVisibleCharacters(titleText, _cachedTitle, elapsed - mainPanelRevealDelay + 0.02f);
            ApplyVisibleCharacters(subtitleText, _cachedSubtitle, elapsed - mainPanelRevealDelay + 0.14f);
            ApplyVisibleCharacters(eventRowText, _cachedEvent, elapsed - mainPanelRevealDelay + 0.26f);
            ApplyVisibleCharacters(dialogueText, _cachedDialogue, elapsed - projectionRevealDelay + 0.12f);
            ApplyVisibleCharacters(continueHintText, _cachedContinueHint, elapsed - continueHintRevealDelay);
        }

        if (_currentTone == ResultPageTone.Failure)
        {
            ApplyFailureDamageEffects(elapsed);
        }
    }

    public void BindContinueAction(UnityEngine.Events.UnityAction onClick)
    {
        ResolveReferences();
        if (continueButton == null)
        {
            return;
        }

        continueButton.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            continueButton.onClick.AddListener(onClick);
        }
    }

    public void Show(VictoryResultPageContent content)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        _currentTone = content.Tone;
        ResolveReferences();

        // When preserveSceneVisuals is on, the Scene-authored prefab instance already holds the
        // correct look (colors, sizes, positions). Skip ApplyTheme so C# palette values don't
        // overwrite the author's manual adjustments.
        if (!preserveSceneVisuals)
        {
            ApplyTheme(content.Tone);
        }

        SetText(signalTitleText, content.SignalTitle);
        SetText(signalStatusText, content.SignalStatus);
        SetText(signalChannelText, content.SignalChannel);
        SetText(titleText, content.Title);
        SetText(subtitleText, content.Subtitle);
        SetText(reportHeaderText, content.ReportHeader);
        SetText(integrityRowText, content.IntegrityRow);
        SetText(scrapRowText, content.ScrapRow);
        SetText(eventRowText, content.EventRow);
        SetText(footerHintText, content.FooterHint);
        SetText(commanderNameText, content.CommanderName);
        SetText(commanderCodenameText, content.CommanderCodename);
        SetText(dialogueText, content.DialogueText);
        SetText(continueButtonText, content.ContinueButtonText);
        SetText(continueHintText, content.ContinueHintText);

        _cachedTitle = content.Title ?? string.Empty;
        _cachedSubtitle = content.Subtitle ?? string.Empty;
        _cachedEvent = content.EventRow ?? string.Empty;
        _cachedDialogue = content.DialogueText ?? string.Empty;
        _cachedContinueHint = content.ContinueHintText ?? string.Empty;

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }

        // The formal tower-defense clear flow intentionally pauses gameplay with `Time.timeScale = 0`
        // before showing the result page. In that paused state we do not want the page to stay stuck
        // at alpha 0 waiting for an animation tick; the player should immediately see the full page.
        bool useImmediateReveal = preserveSceneVisuals || !autoReveal || !Application.isPlaying || Time.timeScale <= 0f;

        if (!useImmediateReveal)
        {
            _revealStartedAt = Time.unscaledTime;
            ApplyGroupAlpha(topSignalBarGroup, 0f);
            ApplyGroupAlpha(mainBriefPanelGroup, 0f);
            ApplyGroupAlpha(commanderProjectionGroup, 0f);
            ApplyGroupAlpha(continueButtonGroup, 0f);

            if (!preserveSceneVisuals)
            {
                ResetVisibleCharacters(titleText);
                ResetVisibleCharacters(subtitleText);
                ResetVisibleCharacters(eventRowText);
                ResetVisibleCharacters(dialogueText);
                ResetVisibleCharacters(continueHintText);
            }
        }

        ApplyGroupAlpha(topSignalBarGroup, useImmediateReveal ? 1f : 0f);
        ApplyGroupAlpha(mainBriefPanelGroup, useImmediateReveal ? 1f : 0f);
        ApplyGroupAlpha(commanderProjectionGroup, useImmediateReveal ? 1f : 0f);
        ApplyGroupAlpha(continueButtonGroup, useImmediateReveal ? 1f : 0f);

        if (useImmediateReveal && !preserveSceneVisuals)
        {
            ShowAllCharacters(titleText);
            ShowAllCharacters(subtitleText);
            ShowAllCharacters(eventRowText);
            ShowAllCharacters(dialogueText);
            ShowAllCharacters(continueHintText);
        }

        _isShowing = true;
    }

    public void SetPreserveSceneVisuals(bool preserve)
    {
        preserveSceneVisuals = preserve;
    }

    public void Hide()
    {
        _isShowing = false;
        SetVisible(false);
    }

    private void ApplyTheme(ResultPageTone tone)
    {
        bool isFailure = tone == ResultPageTone.Failure;

        // Victory: the prefab's own child-component values ARE the victory look.
        // Never overwrite them with C# defaults.
        if (!isFailure)
        {
            ApplyToneLayout(tone);
            return;
        }

        ResultPageThemePalette palette = failureTheme;
        if (palette == null)
        {
            return;
        }

        ApplyGraphicColor("DimBackground", palette.dimBackgroundColor);
        ApplyGraphicColor("VignetteOverlay", palette.vignetteOverlayColor);
        ApplyGraphicColor("HoloNoiseOverlay", palette.holoNoiseOverlayColor);
        ApplyGraphicColor("ScanBand", palette.scanBandColor);
        ApplyGraphicColor("MainBriefPanel", palette.mainBriefPanelColor);
        ApplyGraphicColor("MainPanelInner", palette.mainPanelInnerColor);
        ApplyGraphicColor("EventBlock", palette.eventBlockColor);
        ApplyGraphicColor("CommanderProjection", palette.projectionShellColor);
        ApplyGraphicColor("ProjectionInner", palette.projectionInnerColor);
        ApplyGraphicColor("DialogueBubble", palette.dialogueBubbleColor);
        ApplyGraphicColor("IntegrityChip", palette.chipPanelColor);
        ApplyGraphicColor("ScrapChip", palette.chipPanelColor);
        ApplyGraphicColor("PortraitGlow", palette.portraitGlowColor);
        ApplyGraphicColor("PortraitScan", palette.scanBandColor);
        ApplyGraphicColor("CommanderPortrait", palette.portraitTintColor);

        ApplyGraphicColor("TopSignalBar", palette.lineColor);
        ApplyGraphicColor("TopSignalLeftCap", palette.accentColor);
        ApplyGraphicColor("TopSignalRightCap", palette.accentColor);
        ApplyGraphicColor("IntegrityChip_Accent", palette.accentColor);
        ApplyGraphicColor("ScrapChip_Accent", palette.accentColor);
        ApplyGraphicColor("EventAccent", palette.accentColor);
        ApplyGraphicColor("DialogueAccent", palette.accentColor);
        ApplyGraphicColor("TitleDivider", palette.lineColor);

        ApplyLineColor(palette.lineColor);

        ApplyTextColor(signalTitleText, palette.secondaryTextColor);
        ApplyTextColor(signalStatusText, palette.accentTextColor);
        ApplyTextColor(signalChannelText, palette.secondaryTextColor);
        ApplyTextColor(titleText, palette.titleTextColor);
        ApplyTextColor(subtitleText, palette.secondaryTextColor);
        ApplyTextColor(reportHeaderText, palette.accentTextColor);
        ApplyTextColor(integrityRowText, palette.bodyTextColor);
        ApplyTextColor(scrapRowText, palette.bodyTextColor);
        ApplyTextColor(eventRowText, palette.bodyTextColor);
        ApplyTextColor(footerHintText, palette.secondaryTextColor);
        ApplyTextColor(commanderNameText, palette.accentTextColor);
        ApplyTextColor(commanderCodenameText, palette.secondaryTextColor);
        ApplyTextColor(dialogueText, palette.bodyTextColor);
        ApplyTextColor(continueButtonText, palette.buttonTextColor);
        ApplyTextColor(continueHintText, palette.secondaryTextColor);
        ApplyContinueButtonTheme(palette);
        ApplyToneLayout(tone);
    }

    private void ResolveReferences()
    {
        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Re-resolve every time instead of relying on a one-time cached lookup.
        // The formal victory page is instantiated from a prefab and then hidden / shown while the
        // scene is paused. If any serialized link is missing or becomes stale, keeping the lookup
        // idempotent makes the runtime page self-healing and keeps it aligned with the preview scene.
        signalTitleText = FindText("SignalTitle");
        signalStatusText = FindText("SignalStatus");
        signalChannelText = FindText("SignalChannel");
        titleText = FindText("TitleText");
        subtitleText = FindText("SubtitleText");
        reportHeaderText = FindText("ReportHeader");
        integrityRowText = FindText("IntegrityChip_Text");
        scrapRowText = FindText("ScrapChip_Text");
        eventRowText = FindText("EventRow");
        footerHintText = FindText("FooterHint");
        commanderNameText = FindText("DialogueName");
        commanderCodenameText = FindText("CommanderCodename");
        dialogueText = FindText("DialogueText");
        continueHintText = FindText("ContinueHint");
        continueButton = FindButton("ContinueButton");
        if (continueButton != null)
        {
            continueButtonText = continueButton.GetComponentInChildren<TMP_Text>(true);
        }
        else
        {
            continueButtonText = null;
        }

        topSignalBarGroup = EnsureCanvasGroup(FindChild("TopSignalBar"));
        mainBriefPanelGroup = EnsureCanvasGroup(FindChild("MainBriefPanel"));
        commanderProjectionGroup = EnsureCanvasGroup(FindChild("CommanderProjection"));
        continueButtonGroup = EnsureCanvasGroup(FindChild("ContinueButton"));

        _mainBriefRect = FindRect("MainBriefPanel");
        _projectionRect = FindRect("CommanderProjection");
        _dialogueBubbleRect = FindRect("DialogueBubble");
        _dialogueTextRect = FindRect("DialogueText");
        _eventBlockRect = FindRect("EventBlock");
        _scanBandRect = FindRect("ScanBand");
        _portraitGlowRect = FindRect("PortraitGlow");
        _portraitScanRect = FindRect("PortraitScan");
        _continueButtonRect = FindRect("ContinueButton");
        _continueHintRect = FindRect("ContinueHint");
        _holoNoiseGraphic = FindGraphic("HoloNoiseOverlay");
        _scanBandGraphic = FindGraphic("ScanBand");
        _portraitGlowGraphic = FindGraphic("PortraitGlow");
        _continueButtonGraphic = continueButton != null ? continueButton.targetGraphic : null;

        CaptureDefaultLayoutsIfNeeded();
    }

    private void SetVisible(bool visible)
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = visible ? 1f : 0f;
            rootCanvasGroup.interactable = visible;
            rootCanvasGroup.blocksRaycasts = visible;
        }

        gameObject.SetActive(visible);
    }

    private static void ApplyGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = Mathf.Clamp01(alpha);
    }

    private void ApplyVisibleCharacters(TMP_Text target, string fullText, float elapsed)
    {
        if (target == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(fullText) || elapsed <= 0f)
        {
            target.maxVisibleCharacters = 0;
            return;
        }

        int visibleCount = Mathf.FloorToInt(elapsed * typewriterCharactersPerSecond);
        target.maxVisibleCharacters = Mathf.Clamp(visibleCount, 0, fullText.Length);
    }

    private static void ResetVisibleCharacters(TMP_Text target)
    {
        if (target != null)
        {
            target.maxVisibleCharacters = 0;
        }
    }

    private static void ShowAllCharacters(TMP_Text target)
    {
        if (target != null)
        {
            target.maxVisibleCharacters = int.MaxValue;
        }
    }

    private static float EaseReveal(float elapsed, float duration)
    {
        if (elapsed <= 0f)
        {
            return 0f;
        }

        if (duration <= 0.001f)
        {
            return 1f;
        }

        float t = Mathf.Clamp01(elapsed / duration);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }

    private TMP_Text FindText(string objectName)
    {
        Transform target = FindChild(objectName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private Button FindButton(string objectName)
    {
        Transform target = FindChild(objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private RectTransform FindRect(string objectName)
    {
        Transform target = FindChild(objectName);
        return target as RectTransform;
    }

    private Graphic FindGraphic(string objectName)
    {
        Transform target = FindChild(objectName);
        return target != null ? target.GetComponent<Graphic>() : null;
    }

    private Transform FindChild(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            if (transforms[index] != null && transforms[index].name == objectName)
            {
                return transforms[index];
            }
        }

        return null;
    }

    private static CanvasGroup EnsureCanvasGroup(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private void CaptureDefaultLayoutsIfNeeded()
    {
        if (_defaultLayoutCaptured)
        {
            return;
        }

        _defaultMainBriefLayout = new LayoutSnapshot(_mainBriefRect);
        _defaultProjectionLayout = new LayoutSnapshot(_projectionRect);
        _defaultDialogueBubbleLayout = new LayoutSnapshot(_dialogueBubbleRect);
        _defaultDialogueTextLayout = new LayoutSnapshot(_dialogueTextRect);
        _defaultEventBlockLayout = new LayoutSnapshot(_eventBlockRect);
        _defaultScanBandLayout = new LayoutSnapshot(_scanBandRect);
        _defaultPortraitGlowLayout = new LayoutSnapshot(_portraitGlowRect);
        _defaultPortraitScanLayout = new LayoutSnapshot(_portraitScanRect);
        _defaultContinueButtonLayout = new LayoutSnapshot(_continueButtonRect);
        _defaultContinueHintLayout = new LayoutSnapshot(_continueHintRect);
        _defaultLayoutCaptured = true;
    }

    private void ApplyToneLayout(ResultPageTone tone)
    {
        if (!_defaultLayoutCaptured)
        {
            return;
        }

        bool isFailure = tone == ResultPageTone.Failure;
        ApplyLayout(_mainBriefRect, isFailure ? failureLayout.mainBriefAnchoredPosition : _defaultMainBriefLayout.AnchoredPosition, isFailure ? failureLayout.mainBriefSizeDelta : _defaultMainBriefLayout.SizeDelta);
        ApplyLayout(_projectionRect, isFailure ? failureLayout.projectionAnchoredPosition : _defaultProjectionLayout.AnchoredPosition, isFailure ? failureLayout.projectionSizeDelta : _defaultProjectionLayout.SizeDelta);
        ApplyLayout(_dialogueBubbleRect, isFailure ? failureLayout.dialogueBubbleAnchoredPosition : _defaultDialogueBubbleLayout.AnchoredPosition, isFailure ? failureLayout.dialogueBubbleSizeDelta : _defaultDialogueBubbleLayout.SizeDelta);
        ApplyLayout(_dialogueTextRect, isFailure ? failureLayout.dialogueTextAnchoredPosition : _defaultDialogueTextLayout.AnchoredPosition, isFailure ? failureLayout.dialogueTextSizeDelta : _defaultDialogueTextLayout.SizeDelta);
        ApplyLayout(_eventBlockRect, isFailure ? failureLayout.eventBlockAnchoredPosition : _defaultEventBlockLayout.AnchoredPosition, isFailure ? failureLayout.eventBlockSizeDelta : _defaultEventBlockLayout.SizeDelta);
        ApplyLayout(_scanBandRect, isFailure ? failureLayout.scanBandAnchoredPosition : _defaultScanBandLayout.AnchoredPosition, isFailure ? failureLayout.scanBandSizeDelta : _defaultScanBandLayout.SizeDelta);
        ApplyLayout(_portraitGlowRect, isFailure ? failureLayout.portraitGlowAnchoredPosition : _defaultPortraitGlowLayout.AnchoredPosition, isFailure ? failureLayout.portraitGlowSizeDelta : _defaultPortraitGlowLayout.SizeDelta);
        ApplyLayout(_portraitScanRect, isFailure ? failureLayout.portraitScanAnchoredPosition : _defaultPortraitScanLayout.AnchoredPosition, isFailure ? failureLayout.portraitScanSizeDelta : _defaultPortraitScanLayout.SizeDelta);
        ApplyLayout(_continueButtonRect, isFailure ? failureLayout.continueButtonAnchoredPosition : _defaultContinueButtonLayout.AnchoredPosition, isFailure ? failureLayout.continueButtonSizeDelta : _defaultContinueButtonLayout.SizeDelta);
        ApplyLayout(_continueHintRect, isFailure ? failureLayout.continueHintAnchoredPosition : _defaultContinueHintLayout.AnchoredPosition, isFailure ? failureLayout.continueHintSizeDelta : _defaultContinueHintLayout.SizeDelta);
    }

    private static void ApplyLayout(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.localScale = Vector3.one;
    }

    private void ApplyFailureDamageEffects(float elapsed)
    {
        if (preserveSceneVisuals)
        {
            ApplyFailureDamageEffectsPreserved(elapsed);
        }
        else
        {
            ApplyFailureDamageEffectsThemed(elapsed);
        }
    }

    private void ApplyFailureDamageEffectsThemed(float elapsed)
    {
        float noisePulse = 1f + Mathf.Sin(elapsed * failureDamageEffects.noiseAlphaPulseSpeed) * failureDamageEffects.noiseAlphaPulseAmplitude;
        SetGraphicColorAlpha(_holoNoiseGraphic, failureTheme.holoNoiseOverlayColor, noisePulse);

        float scanPulse = 0.88f + Mathf.Sin(elapsed * failureDamageEffects.scanBandAlphaPulseSpeed) * failureDamageEffects.scanBandAlphaPulseAmplitude;
        if (_scanBandRect != null)
        {
            _scanBandRect.anchoredPosition = new Vector2(
                failureLayout.scanBandAnchoredPosition.x,
                failureLayout.scanBandAnchoredPosition.y + Mathf.Sin(elapsed * failureDamageEffects.scanBandDriftSpeed) * failureDamageEffects.scanBandDriftAmplitude);
        }

        SetGraphicColorAlpha(_scanBandGraphic, failureTheme.scanBandColor, scanPulse);

        if (_projectionRect != null)
        {
            _projectionRect.anchoredPosition = failureLayout.projectionAnchoredPosition + new Vector2(
                Mathf.Sin(elapsed * failureDamageEffects.projectionJitterSpeed) * failureDamageEffects.projectionJitterXAmplitude,
                Mathf.Cos(elapsed * (failureDamageEffects.projectionJitterSpeed * 1.27f)) * failureDamageEffects.projectionJitterYAmplitude);
        }

        if (_dialogueBubbleRect != null)
        {
            _dialogueBubbleRect.anchoredPosition = failureLayout.dialogueBubbleAnchoredPosition + new Vector2(
                Mathf.Sin(elapsed * failureDamageEffects.dialogueJitterSpeed) * failureDamageEffects.dialogueJitterXAmplitude,
                Mathf.Cos(elapsed * (failureDamageEffects.dialogueJitterSpeed * 1.19f)) * failureDamageEffects.dialogueJitterYAmplitude);
        }

        float glowScale = 1f + Mathf.Sin(elapsed * failureDamageEffects.portraitGlowScalePulseSpeed) * failureDamageEffects.portraitGlowScalePulseAmplitude;
        if (_portraitGlowRect != null)
        {
            _portraitGlowRect.localScale = new Vector3(glowScale, glowScale, 1f);
        }

        float glowPulse = 0.82f + Mathf.Sin(elapsed * failureDamageEffects.portraitGlowAlphaPulseSpeed) * failureDamageEffects.portraitGlowAlphaPulseAmplitude;
        SetGraphicColorAlpha(_portraitGlowGraphic, failureTheme.portraitGlowColor, glowPulse);

        float buttonPulse = 0.5f + (Mathf.Sin(elapsed * failureDamageEffects.continueButtonPulseSpeed) * 0.5f);
        if (_continueButtonRect != null)
        {
            float scale = 1f + (buttonPulse * failureDamageEffects.continueButtonPulseAmplitude);
            _continueButtonRect.localScale = new Vector3(scale, scale, 1f);
        }

        if (_continueButtonGraphic != null)
        {
            _continueButtonGraphic.color = Color.Lerp(
                failureTheme.continueButtonNormalColor,
                failureTheme.continueButtonHighlightedColor,
                buttonPulse);
        }

        if (continueHintText != null)
        {
            float hintPulse = 1f - failureDamageEffects.continueHintBlinkAmplitude
                + (Mathf.PingPong(elapsed * failureDamageEffects.continueHintBlinkSpeed, failureDamageEffects.continueHintBlinkAmplitude) * 2f);
            Color hintColor = failureTheme.secondaryTextColor;
            hintColor.a *= Mathf.Clamp01(hintPulse);
            continueHintText.color = hintColor;
        }
    }

    private void ApplyFailureDamageEffectsPreserved(float elapsed)
    {
        if (_holoNoiseGraphic != null)
        {
            float noisePulse = 1f + Mathf.Sin(elapsed * failureDamageEffects.noiseAlphaPulseSpeed) * failureDamageEffects.noiseAlphaPulseAmplitude;
            Color baseColor = _holoNoiseGraphic.color;
            baseColor.a = Mathf.Clamp01(baseColor.a * noisePulse);
            _holoNoiseGraphic.color = baseColor;
        }

        if (_scanBandRect != null)
        {
            _scanBandRect.anchoredPosition = new Vector2(
                _defaultScanBandLayout.AnchoredPosition.x,
                _defaultScanBandLayout.AnchoredPosition.y + Mathf.Sin(elapsed * failureDamageEffects.scanBandDriftSpeed) * failureDamageEffects.scanBandDriftAmplitude);
        }

        if (_scanBandGraphic != null)
        {
            float scanPulse = 0.88f + Mathf.Sin(elapsed * failureDamageEffects.scanBandAlphaPulseSpeed) * failureDamageEffects.scanBandAlphaPulseAmplitude;
            Color baseColor = _scanBandGraphic.color;
            baseColor.a = Mathf.Clamp01(baseColor.a * scanPulse);
            _scanBandGraphic.color = baseColor;
        }

        if (_projectionRect != null)
        {
            _projectionRect.anchoredPosition = _defaultProjectionLayout.AnchoredPosition + new Vector2(
                Mathf.Sin(elapsed * failureDamageEffects.projectionJitterSpeed) * failureDamageEffects.projectionJitterXAmplitude,
                Mathf.Cos(elapsed * (failureDamageEffects.projectionJitterSpeed * 1.27f)) * failureDamageEffects.projectionJitterYAmplitude);
        }

        if (_dialogueBubbleRect != null)
        {
            _dialogueBubbleRect.anchoredPosition = _defaultDialogueBubbleLayout.AnchoredPosition + new Vector2(
                Mathf.Sin(elapsed * failureDamageEffects.dialogueJitterSpeed) * failureDamageEffects.dialogueJitterXAmplitude,
                Mathf.Cos(elapsed * (failureDamageEffects.dialogueJitterSpeed * 1.19f)) * failureDamageEffects.dialogueJitterYAmplitude);
        }

        float glowScale = 1f + Mathf.Sin(elapsed * failureDamageEffects.portraitGlowScalePulseSpeed) * failureDamageEffects.portraitGlowScalePulseAmplitude;
        if (_portraitGlowRect != null)
        {
            _portraitGlowRect.localScale = new Vector3(glowScale, glowScale, 1f);
        }

        if (_portraitGlowGraphic != null)
        {
            float glowPulse = 0.82f + Mathf.Sin(elapsed * failureDamageEffects.portraitGlowAlphaPulseSpeed) * failureDamageEffects.portraitGlowAlphaPulseAmplitude;
            Color baseColor = _portraitGlowGraphic.color;
            baseColor.a = Mathf.Clamp01(baseColor.a * glowPulse);
            _portraitGlowGraphic.color = baseColor;
        }

        float buttonPulse = 0.5f + (Mathf.Sin(elapsed * failureDamageEffects.continueButtonPulseSpeed) * 0.5f);
        if (_continueButtonRect != null)
        {
            float scale = 1f + (buttonPulse * failureDamageEffects.continueButtonPulseAmplitude);
            _continueButtonRect.localScale = new Vector3(scale, scale, 1f);
        }

        if (_continueButtonGraphic != null)
        {
            Color baseColor = _continueButtonGraphic.color;
            Color buttonColor = baseColor;
            buttonColor.a = Mathf.Lerp(baseColor.a * 0.85f, baseColor.a, buttonPulse);
            _continueButtonGraphic.color = buttonColor;
        }

        if (continueHintText != null)
        {
            float hintPulse = 1f - failureDamageEffects.continueHintBlinkAmplitude
                + (Mathf.PingPong(elapsed * failureDamageEffects.continueHintBlinkSpeed, failureDamageEffects.continueHintBlinkAmplitude) * 2f);
            Color hintColor = continueHintText.color;
            hintColor.a = Mathf.Clamp01(hintColor.a * hintPulse);
            continueHintText.color = hintColor;
        }
    }

    private void ApplyContinueButtonTheme(ResultPageThemePalette palette)
    {
        if (continueButton == null || palette == null)
        {
            return;
        }

        Graphic buttonGraphic = continueButton.GetComponent<Graphic>();
        if (buttonGraphic != null)
        {
            buttonGraphic.color = palette.continueButtonNormalColor;
        }

        ColorBlock colors = continueButton.colors;
        colors.normalColor = palette.continueButtonNormalColor;
        colors.highlightedColor = palette.continueButtonHighlightedColor;
        colors.pressedColor = palette.continueButtonPressedColor;
        colors.selectedColor = palette.continueButtonHighlightedColor;
        colors.disabledColor = palette.continueButtonDisabledColor;
        continueButton.colors = colors;
    }

    private void ApplyLineColor(Color color)
    {
        ApplyGraphicColor("TopLeftBracket_Horizontal", color);
        ApplyGraphicColor("TopLeftBracket_Vertical", color);
        ApplyGraphicColor("TopRightBracket_Horizontal", color);
        ApplyGraphicColor("TopRightBracket_Vertical", color);
        ApplyGraphicColor("BottomLeftBracket_Horizontal", color);
        ApplyGraphicColor("BottomLeftBracket_Vertical", color);
        ApplyGraphicColor("BottomRightBracket_Horizontal", color);
        ApplyGraphicColor("BottomRightBracket_Vertical", color);
        ApplyGraphicColor("ProjectionTopLeftBracket_Horizontal", color);
        ApplyGraphicColor("ProjectionTopLeftBracket_Vertical", color);
        ApplyGraphicColor("ProjectionBottomRightBracket_Horizontal", color);
        ApplyGraphicColor("ProjectionBottomRightBracket_Vertical", color);
    }

    private void ApplyGraphicColor(string objectName, Color color)
    {
        Transform target = FindChild(objectName);
        if (target == null)
        {
            return;
        }

        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = color;
        }
    }

    private static void SetGraphicColorAlpha(Graphic graphic, Color baseColor, float alphaMultiplier)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = baseColor;
        color.a = Mathf.Clamp01(baseColor.a * alphaMultiplier);
        graphic.color = color;
    }

    private static void ApplyTextColor(TMP_Text target, Color color)
    {
        if (target != null)
        {
            target.color = color;
        }
    }
}

/// <summary>
/// 正式结果页运行时所需的最小文案数据。
/// 这里保持轻量结构，方便总控根据胜利或失败快速组装。
/// </summary>
public readonly struct VictoryResultPageContent
{
    public VictoryResultPageContent(
        VictoryResultPageView.ResultPageTone tone,
        string signalTitle,
        string signalStatus,
        string signalChannel,
        string title,
        string subtitle,
        string reportHeader,
        string integrityRow,
        string scrapRow,
        string eventRow,
        string footerHint,
        string commanderName,
        string commanderCodename,
        string dialogueText,
        string continueButtonText,
        string continueHintText)
    {
        Tone = tone;
        SignalTitle = signalTitle ?? string.Empty;
        SignalStatus = signalStatus ?? string.Empty;
        SignalChannel = signalChannel ?? string.Empty;
        Title = title ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        ReportHeader = reportHeader ?? string.Empty;
        IntegrityRow = integrityRow ?? string.Empty;
        ScrapRow = scrapRow ?? string.Empty;
        EventRow = eventRow ?? string.Empty;
        FooterHint = footerHint ?? string.Empty;
        CommanderName = commanderName ?? string.Empty;
        CommanderCodename = commanderCodename ?? string.Empty;
        DialogueText = dialogueText ?? string.Empty;
        ContinueButtonText = continueButtonText ?? string.Empty;
        ContinueHintText = continueHintText ?? string.Empty;
    }

    public VictoryResultPageView.ResultPageTone Tone { get; }
    public string SignalTitle { get; }
    public string SignalStatus { get; }
    public string SignalChannel { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string ReportHeader { get; }
    public string IntegrityRow { get; }
    public string ScrapRow { get; }
    public string EventRow { get; }
    public string FooterHint { get; }
    public string CommanderName { get; }
    public string CommanderCodename { get; }
    public string DialogueText { get; }
    public string ContinueButtonText { get; }
    public string ContinueHintText { get; }
}
