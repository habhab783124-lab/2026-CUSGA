using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Chapter10 : MonoBehaviour
{
    [Header("字体")]
    [SerializeField] private TMP_FontAsset dialogueFontAsset;

    [Header("台词")]
    [SerializeField] private bool useScriptDefaultLines = true;
    [SerializeField] private string dialogueId = "chapter10_intro";
    [SerializeField] private List<DialogueLine> lines = new();

    [Header("对话气泡")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform npcBubbleAnchor;
    [SerializeField] private Vector3 playerBubbleOffset = new(220f, -180f, 10f);
    [SerializeField] private Vector3 npcBubbleOffset = new(-220f, 180f, 10f);

    [Header("点击推进")]
    [SerializeField] private int mouseButton = 0;

    [Header("音乐")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private string musicResourcePath = "StoryAudio/BGM/TruthRevealedBGM";
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField] private bool loopMusic = true;

    [Header("结尾中央文本")]
    [SerializeField] private string finalMessage = "凌……希望以后我们都不用再淋雨了。";
    [SerializeField] [Min(0f)] private float finalMessageDelay = 1.5f;
    [SerializeField] [Min(0.01f)] private float finalMessageFadeDuration = 1.2f;
    [SerializeField] private float finalMessageFontSize = 54f;
    [SerializeField] private Color finalMessageColor = Color.white;

    private Canvas overlayCanvas;
    private TextMeshProUGUI finalMessageText;
    private CanvasGroup finalMessageCanvasGroup;
    private Coroutine finalMessageRoutine;
    private bool waitingForFinalClick;
    private AudioSource audioSource;

    private void Awake()
    {
        ApplyDefaultLinesIfNeeded();
        EnsureDialogueRunner();
        EnsureAnchors();
        EnsureFinalMessageUi();
        EnsureAudioSource();
        ApplyFont();
        ApplyFinalMessageStyle();
        UpdateBubbleAnchors();
        HideFinalMessageImmediate();
    }

    private void OnValidate()
    {
        ApplyDefaultLinesIfNeeded();
        if (!Application.isPlaying)
        {
            EnsureAnchors();
            EnsureFinalMessageUi();
        }
        ApplyFont();
        ApplyFinalMessageStyle();
        if (audioSource != null)
        {
            audioSource.volume = musicVolume;
            audioSource.loop = loopMusic;
        }
        if (!Application.isPlaying)
        {
            UpdateBubbleAnchors();
        }
    }

    private void Start()
    {
        if (dialogueFontAsset == null)
        {
            dialogueFontAsset = TryLoadDialogueFont();
            ApplyFont();
        }

        PlayMusic();
        StartDialogue();
    }

    private void Update()
    {
        UpdateBubbleAnchors();
        if (waitingForFinalClick && Input.GetMouseButtonDown(mouseButton))
        {
            waitingForFinalClick = false;
            if (dialogueRunner != null) dialogueRunner.HideDialogueBubble();
            StopFinalMessageRoutineIfNeeded();
            finalMessageRoutine = StartCoroutine(ShowFinalMessageAfterDelayRoutine());
        }
    }

    private void ApplyDefaultLinesIfNeeded()
    {
        if (!useScriptDefaultLines) return;
        IReadOnlyList<DialogueLine> source = Get(dialogueId);
        lines = source != null ? new List<DialogueLine>(source) : new List<DialogueLine>();
    }

    private void EnsureDialogueRunner()
    {
        dialogueRunner = dialogueRunner != null ? dialogueRunner : GetComponent<DialogueRunner>();
        if (dialogueRunner == null) dialogueRunner = gameObject.AddComponent<DialogueRunner>();
        dialogueRunner.ConfigureBottomLayout(false, Vector2.zero, Vector2.zero, 1f, false);
    }

    private void EnsureAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = loopMusic;
        audioSource.volume = musicVolume;
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

    private void EnsureAnchors()
    {
        if (playerBubbleAnchor == null) playerBubbleAnchor = CreateScreenAnchor("Chapter10PlayerBubbleAnchor");
        if (npcBubbleAnchor == null) npcBubbleAnchor = CreateScreenAnchor("Chapter10NpcBubbleAnchor");
    }

    private Transform CreateScreenAnchor(string anchorName)
    {
        Transform existing = transform.Find(anchorName);
        if (existing != null) return existing;
        GameObject anchor = new GameObject(anchorName);
        anchor.transform.SetParent(transform, false);
        return anchor.transform;
    }

    private void EnsureFinalMessageUi()
    {
        if (overlayCanvas == null)
        {
            Transform existingCanvas = transform.Find("Chapter10Canvas");
            if (existingCanvas != null) overlayCanvas = existingCanvas.GetComponent<Canvas>();
        }
        if (overlayCanvas == null)
        {
            GameObject canvasObject = new GameObject("Chapter10Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            overlayCanvas = canvasObject.GetComponent<Canvas>();
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Transform existingText = overlayCanvas.transform.Find("FinalMessageText");
        if (existingText != null)
        {
            finalMessageText = existingText.GetComponent<TextMeshProUGUI>();
            finalMessageCanvasGroup = existingText.GetComponent<CanvasGroup>();
        }
        if (finalMessageText == null)
        {
            GameObject textObject = new GameObject("FinalMessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup));
            textObject.transform.SetParent(overlayCanvas.transform, false);
            finalMessageText = textObject.GetComponent<TextMeshProUGUI>();
            finalMessageCanvasGroup = textObject.GetComponent<CanvasGroup>();
        }
        else if (finalMessageCanvasGroup == null)
        {
            finalMessageCanvasGroup = finalMessageText.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ApplyFont()
    {
        if (finalMessageText != null && dialogueFontAsset != null) finalMessageText.font = dialogueFontAsset;
    }

    private void ApplyFinalMessageStyle()
    {
        if (finalMessageText == null) return;
        RectTransform rect = finalMessageText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1400f, 220f);
        finalMessageText.text = finalMessage;
        finalMessageText.fontSize = finalMessageFontSize;
        finalMessageText.color = finalMessageColor;
        finalMessageText.alignment = TextAlignmentOptions.Center;
        finalMessageText.enableWordWrapping = true;
        finalMessageText.overflowMode = TextOverflowModes.Overflow;
        finalMessageText.raycastTarget = false;
        if (finalMessageCanvasGroup != null) finalMessageCanvasGroup.alpha = 0f;
    }

    private void StartDialogue()
    {
        waitingForFinalClick = false;
        StopFinalMessageRoutineIfNeeded();
        HideFinalMessageImmediate();
        UpdateBubbleAnchors();
        if (dialogueRunner == null || lines == null || lines.Count == 0)
        {
            finalMessageRoutine = StartCoroutine(ShowFinalMessageAfterDelayRoutine());
            return;
        }
        dialogueRunner.PlayConversation(null, playerBubbleAnchor, npcBubbleAnchor, lines, OnDialogueFinished);
    }

    private void UpdateBubbleAnchors()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (playerBubbleAnchor != null)
        {
            Vector3 p = new Vector3(Screen.width * 0.5f + playerBubbleOffset.x, Screen.height * 0.5f + playerBubbleOffset.y, Mathf.Max(0.01f, playerBubbleOffset.z));
            playerBubbleAnchor.position = cam.ScreenToWorldPoint(p);
        }
        if (npcBubbleAnchor != null)
        {
            Vector3 p = new Vector3(Screen.width * 0.5f + npcBubbleOffset.x, Screen.height * 0.5f + npcBubbleOffset.y, Mathf.Max(0.01f, npcBubbleOffset.z));
            npcBubbleAnchor.position = cam.ScreenToWorldPoint(p);
        }
    }

    private void OnDialogueFinished()
    {
        waitingForFinalClick = true;
    }

    private IEnumerator ShowFinalMessageAfterDelayRoutine()
    {
        EnsureFinalMessageUi();
        ApplyFont();
        ApplyFinalMessageStyle();
        finalMessageText.gameObject.SetActive(false);
        if (finalMessageDelay > 0f) yield return new WaitForSeconds(finalMessageDelay);
        finalMessageText.gameObject.SetActive(true);
        float duration = Mathf.Max(0.01f, finalMessageFadeDuration);
        float elapsed = 0f;
        if (finalMessageCanvasGroup != null) finalMessageCanvasGroup.alpha = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (finalMessageCanvasGroup != null) finalMessageCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        if (finalMessageCanvasGroup != null) finalMessageCanvasGroup.alpha = 1f;
        finalMessageRoutine = null;
    }

    private void HideFinalMessageImmediate()
    {
        if (finalMessageText != null) finalMessageText.gameObject.SetActive(false);
        if (finalMessageCanvasGroup != null) finalMessageCanvasGroup.alpha = 0f;
    }

    private void StopFinalMessageRoutineIfNeeded()
    {
        if (finalMessageRoutine == null) return;
        StopCoroutine(finalMessageRoutine);
        finalMessageRoutine = null;
    }

    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "chapter10_intro": return Intro();
            default: return null;
        }
    }

    private static IReadOnlyList<DialogueLine> Intro()
    {
        return new List<DialogueLine>
        {
            Npc("凌，你知道伞是什么意思吗？"),
            Player("什么意思？"),
            Npc("在我的家族，有一句流传很久的话。"),
            Npc("伞的意思是——“我在这里，你不用淋雨。”")
        };
    }

    private static DialogueLine Npc(string text, DialogueEmphasis? emphasis = null)
    {
        return new DialogueLine { speaker = DialogueSpeaker.NPC, text = text, emphasis = emphasis ?? Normal() };
    }

    private static DialogueLine Player(string text, DialogueEmphasis? emphasis = null)
    {
        return new DialogueLine { speaker = DialogueSpeaker.Player, text = text, emphasis = emphasis ?? Normal() };
    }

    private static DialogueEmphasis Normal()
    {
        return new DialogueEmphasis { enabled = false, scaleMultiplier = 1.25f, shakeMagnitude = 0.08f };
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
}
