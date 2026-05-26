using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// `VictoryResultPageView` 负责驱动正式胜利页 prefab 的运行时显示。
///
/// 它和 `VictoryResultPreviewController` 的关系是：
/// - 预览控制器负责在独立 scene 里验证布局、气质和节奏
/// - 本类负责在正式塔防流程里“拿到现成 prefab 后，把文案与显隐跑起来”
///
/// 这样正式流程不需要依赖预览 scene，
/// 但仍然可以复用同一套 prefab 结构。
/// </summary>
public sealed class VictoryResultPageView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup rootCanvasGroup;

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

        ApplyVisibleCharacters(titleText, _cachedTitle, elapsed - mainPanelRevealDelay + 0.02f);
        ApplyVisibleCharacters(subtitleText, _cachedSubtitle, elapsed - mainPanelRevealDelay + 0.14f);
        ApplyVisibleCharacters(eventRowText, _cachedEvent, elapsed - mainPanelRevealDelay + 0.26f);
        ApplyVisibleCharacters(dialogueText, _cachedDialogue, elapsed - projectionRevealDelay + 0.12f);
        ApplyVisibleCharacters(continueHintText, _cachedContinueHint, elapsed - continueHintRevealDelay);
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

        ResolveReferences();

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
        bool useImmediateReveal = !autoReveal || !Application.isPlaying || Time.timeScale <= 0f;

        if (!useImmediateReveal)
        {
            _revealStartedAt = Time.unscaledTime;
            ApplyGroupAlpha(topSignalBarGroup, 0f);
            ApplyGroupAlpha(mainBriefPanelGroup, 0f);
            ApplyGroupAlpha(commanderProjectionGroup, 0f);
            ApplyGroupAlpha(continueButtonGroup, 0f);

            ResetVisibleCharacters(titleText);
            ResetVisibleCharacters(subtitleText);
            ResetVisibleCharacters(eventRowText);
            ResetVisibleCharacters(dialogueText);
            ResetVisibleCharacters(continueHintText);
        }

        ApplyGroupAlpha(topSignalBarGroup, useImmediateReveal ? 1f : 0f);
        ApplyGroupAlpha(mainBriefPanelGroup, useImmediateReveal ? 1f : 0f);
        ApplyGroupAlpha(commanderProjectionGroup, useImmediateReveal ? 1f : 0f);
        ApplyGroupAlpha(continueButtonGroup, useImmediateReveal ? 1f : 0f);

        if (useImmediateReveal)
        {
            ShowAllCharacters(titleText);
            ShowAllCharacters(subtitleText);
            ShowAllCharacters(eventRowText);
            ShowAllCharacters(dialogueText);
            ShowAllCharacters(continueHintText);
        }

        _isShowing = true;
    }

    public void Hide()
    {
        _isShowing = false;
        SetVisible(false);
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
}

/// <summary>
/// 正式胜利页运行时所需的最小文案数据。
/// 这里先保持轻量结构，方便后续从塔防结算结果组装。
/// </summary>
public readonly struct VictoryResultPageContent
{
    public VictoryResultPageContent(
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
