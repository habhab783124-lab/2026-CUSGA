using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueRunner))]
public sealed class Chapter7SceneController : StoryCutsceneControllerBase
{
    [Header("Dialogue")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string dialogueId = "chapter7_intro";
    [SerializeField] private bool playOnStart = true;

    [Header("Dialogue Bubble")]
    [SerializeField] private DialogueBubbleView dialogueBubblePrefab;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform npcBubbleAnchor;

    [Header("Bubble Offsets")]
    [SerializeField] private Vector3 playerBubbleAnchorOffset = new Vector3(-2f, 1.8f, 0f);
    [SerializeField] private Vector3 npcBubbleAnchorOffset = new Vector3(2f, 1.8f, 0f);

    [Header("Actors")]
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private Transform player;
    [SerializeField] private Transform npc;

    [Header("Opening")]
    [SerializeField] private float dialogueDelayOnStart = 0.1f;

    [Header("Background")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private Sprite nothBackground;
    [SerializeField] private Sprite earthBackground;
    [SerializeField] private Sprite statBackground;
    [SerializeField] private Sprite starBackground;

    [Header("Audio")]
    [SerializeField] private string impactSfxResourcePath = "StoryAudio/SFX/嘭咚";
    [SerializeField] private string electromagneticNoiseResourcePath = "StoryAudio/BGM/纯电磁噪音";
    [SerializeField] private string pulseBgmResourcePath = "StoryAudio/BGM/低频哔声加大间隔";
    [SerializeField] private string starBgmResourcePath = string.Empty;
    [SerializeField] [Range(0f, 1f)] private float impactSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float electromagneticNoiseVolume = 0.85f;
    [SerializeField] [Range(0f, 1f)] private float pulseBgmVolume = 0.9f;
    [SerializeField] [Range(0f, 1f)] private float starBgmVolume = 1f;

    [Header("Background Flicker")]
    [SerializeField] private float backgroundFlickerInterval = 0.18f;
    [SerializeField] private int backgroundFlickerSwitchCount = 10;
    [SerializeField] private bool endFlickerOnStat = true;

    [Header("Background Zoom")]
    [SerializeField] private float backgroundZoomDuration = 2.5f;
    [SerializeField] private float backgroundZoomScaleMultiplier = 1.08f;
    [SerializeField] private Vector2 backgroundZoomAnchorNormalized = new Vector2(0f, 0.22f);

    [Header("Ending Zoom")]
    [SerializeField] private float endingZoomDuration = 2.2f;
    [SerializeField] private float endingZoomScaleMultiplier = 1.12f;
    [SerializeField] private Vector2 endingZoomAnchorNormalized = new Vector2(0f, -0.16f);

    [Header("Transition")]
    [SerializeField] private string nextSceneName = "chapter8";
    [SerializeField] private float fadeOutToBlackDuration = 1f;
    [SerializeField] private float fadeInFromBlackDuration = 1f;

    private const int BrightLineIndex = 14;
    private const int CanTryLineIndex = 17;
    private const int EllipsisLineIndex = 30;
    private const int WatchingEllipsisLineIndex = 43;
    private const int WatchingLineIndex = 44;

    private bool hasPlayed;
    private Transform runtimePlayerBubbleAnchor;
    private Transform runtimeNpcBubbleAnchor;
    private AudioSource impactSfxAudioSource;
    private AudioSource ambientNoiseAudioSource;
    private AudioSource pulseBgmAudioSource;
    private AudioSource starBgmAudioSource;
    private Coroutine backgroundFlickerRoutine;
    private Coroutine backgroundZoomRoutine;
    private bool useBackgroundRelativeBubbleAnchors;
    private Vector3 playerBubbleAnchorLocalToBackground;
    private Vector3 npcBubbleAnchorLocalToBackground;
    private Vector3 originalBackgroundLocalPosition;
    private Vector3 originalBackgroundLocalScale;
    private bool hasCachedOriginalBackgroundTransform;
    private bool transitionQueued;

    protected override void Reset()
    {
        base.Reset();
        dialogueRunner = GetComponent<DialogueRunner>();
        playerInteractor = ResolvePlayerInteractor(playerInteractor);
        backgroundRenderer = backgroundRenderer != null ? backgroundRenderer : FindBackgroundRenderer();
    }

    protected override void Awake()
    {
        base.Awake();
        dialogueRunner = ResolveDialogueRunner(dialogueRunner);
        ResolveReferences();
        EnsureRuntimeAnchors();
        EnsureAudioSources();
        CacheOriginalBackgroundTransform();
        UpdateRuntimeAnchors();
    }

    private void LateUpdate()
    {
        UpdateRuntimeAnchors();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (hasPlayed)
        {
            return;
        }

        dialogueRunner = ResolveDialogueRunner(dialogueRunner);
        ResolveReferences();
        EnsureRuntimeAnchors();
        EnsureAudioSources();
        CacheOriginalBackgroundTransform();
        UpdateRuntimeAnchors();

        if (!ValidateSetup())
        {
            return;
        }

        hasPlayed = true;
        StartCoroutine(PlayRoutine());
    }

    private void ResolveReferences()
    {
        playerInteractor = ResolvePlayerInteractor(playerInteractor);
        player = ResolvePlayerTransform(player);
        npc = ResolveActor(npc, "shen", "Shen");
        backgroundRenderer = backgroundRenderer != null ? backgroundRenderer : FindBackgroundRenderer();
        if (backgroundRenderer != null && !hasCachedOriginalBackgroundTransform)
        {
            CacheOriginalBackgroundTransform();
        }
    }

    private void EnsureRuntimeAnchors()
    {
        EnsureRuntimeAnchor(ref runtimePlayerBubbleAnchor, "Chapter7PlayerBubbleAnchorRuntime");
        EnsureRuntimeAnchor(ref runtimeNpcBubbleAnchor, "Chapter7NpcBubbleAnchorRuntime");
    }

    private void EnsureAudioSources()
    {
        impactSfxAudioSource = EnsureAudioSource(ref impactSfxAudioSource, "Chapter7ImpactSfxAudio");
        ambientNoiseAudioSource = EnsureAudioSource(ref ambientNoiseAudioSource, "Chapter7AmbientNoiseAudio");
        pulseBgmAudioSource = EnsureAudioSource(ref pulseBgmAudioSource, "Chapter7PulseBgmAudio");
        starBgmAudioSource = EnsureAudioSource(ref starBgmAudioSource, "Chapter7StarBgmAudio");
    }

    private AudioSource EnsureAudioSource(ref AudioSource audioSource, string childName)
    {
        if (audioSource != null)
        {
            ConfigureAudioSource(audioSource);
            return audioSource;
        }

        Transform existing = transform.Find(childName);
        if (existing != null)
        {
            audioSource = existing.GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            GameObject go = new GameObject(childName, typeof(AudioSource));
            go.transform.SetParent(transform, false);
            audioSource = go.GetComponent<AudioSource>();
        }

        ConfigureAudioSource(audioSource);
        return audioSource;
    }

    private static void ConfigureAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = false;
    }

    private void UpdateRuntimeAnchors()
    {
        if (UpdateBackgroundRelativeBubbleAnchors())
        {
            return;
        }

        UpdateRuntimeAnchor(
            runtimePlayerBubbleAnchor,
            playerBubbleAnchor,
            player,
            playerBubbleAnchorOffset,
            "player",
            "Player");
        UpdateRuntimeAnchor(
            runtimeNpcBubbleAnchor,
            npcBubbleAnchor,
            npc,
            npcBubbleAnchorOffset,
            "shen",
            "Shen");
    }

    private bool UpdateBackgroundRelativeBubbleAnchors()
    {
        if (!useBackgroundRelativeBubbleAnchors || backgroundRenderer == null)
        {
            return false;
        }

        Transform backgroundTransform = backgroundRenderer.transform;
        if (runtimePlayerBubbleAnchor != null)
        {
            runtimePlayerBubbleAnchor.position = backgroundTransform.TransformPoint(playerBubbleAnchorLocalToBackground);
        }

        if (runtimeNpcBubbleAnchor != null)
        {
            runtimeNpcBubbleAnchor.position = backgroundTransform.TransformPoint(npcBubbleAnchorLocalToBackground);
        }

        return true;
    }

    private bool ValidateSetup()
    {
        if (!TryConfigureDialogueRunner(ref dialogueRunner, dialogueBubblePrefab, nameof(Chapter7SceneController)))
        {
            return false;
        }

        if (runtimePlayerBubbleAnchor == null || runtimeNpcBubbleAnchor == null)
        {
            Debug.LogError("Chapter7SceneController: Failed to create runtime dialogue anchors.", this);
            return false;
        }

        return true;
    }

    private IEnumerator PlayRoutine()
    {
        if (dialogueDelayOnStart > 0f)
        {
            yield return new WaitForSeconds(dialogueDelayOnStart);
        }

        IReadOnlyList<DialogueLine> lines = Chapter7.Get(dialogueId);
        if (lines == null || lines.Count == 0)
        {
            yield break;
        }

        IList<DialogueLine> resolvedLines = lines as IList<DialogueLine> ?? new List<DialogueLine>(lines);
        ApplyBackground(nothBackground);
        SetPlayerCutsceneLock(playerInteractor, true);

        yield return PlayDialogueRange(resolvedLines, 0, BrightLineIndex - 1);

        PlayImpactSfx();
        ApplyBackground(earthBackground);
        PlayLoopingAudio(ambientNoiseAudioSource, electromagneticNoiseResourcePath, electromagneticNoiseVolume);

        yield return PlayDialogueRange(resolvedLines, BrightLineIndex, CanTryLineIndex);

        PlayImpactSfx();
        StartBackgroundFlicker();

        yield return PlayDialogueRange(resolvedLines, CanTryLineIndex + 1, EllipsisLineIndex - 1);

        PlayPulseBgm();
        yield return PlayDialogueRange(resolvedLines, EllipsisLineIndex, EllipsisLineIndex);

        StopBackgroundFlicker();
        ApplyBackground(starBackground);
        PlayImpactSfx();
        PlayStarBgmIfConfigured();

        if (EllipsisLineIndex + 1 <= WatchingEllipsisLineIndex - 1)
        {
            yield return PlayDialogueRange(resolvedLines, EllipsisLineIndex + 1, WatchingEllipsisLineIndex - 1);
        }

        StartBackgroundZoom();
        yield return PlayDialogueRange(resolvedLines, WatchingEllipsisLineIndex, WatchingLineIndex);

        if (WatchingLineIndex + 1 < resolvedLines.Count)
        {
            yield return PlayDialogueRange(resolvedLines, WatchingLineIndex + 1, resolvedLines.Count - 1);
        }

        yield return PlayEndingZoom();
        CleanupSceneState(stopAudio: false);
        QueueSceneTransition();
    }

    private IEnumerator PlayDialogueRange(IList<DialogueLine> source, int startIndexInclusive, int endIndexInclusive)
    {
        if (source == null || source.Count == 0)
        {
            yield break;
        }

        int start = Mathf.Clamp(startIndexInclusive, 0, source.Count - 1);
        int end = Mathf.Clamp(endIndexInclusive, start, source.Count - 1);
        List<DialogueLine> segment = new List<DialogueLine>(end - start + 1);
        for (int i = start; i <= end; i++)
        {
            segment.Add(source[i]);
        }

        bool finished = false;
        dialogueRunner.PlayConversation(
            playerInteractor,
            runtimePlayerBubbleAnchor,
            runtimeNpcBubbleAnchor,
            segment,
            () =>
            {
                SetPlayerCutsceneLock(playerInteractor, true);
                finished = true;
            });

        while (!finished)
        {
            yield return null;
        }
    }

    private void PlayImpactSfx()
    {
        PlayOneShot(impactSfxAudioSource, impactSfxResourcePath, impactSfxVolume);
    }

    private void PlayPulseBgm()
    {
        PlayLoopingAudio(pulseBgmAudioSource, pulseBgmResourcePath, pulseBgmVolume);
    }

    private void PlayStarBgmIfConfigured()
    {
        if (string.IsNullOrWhiteSpace(starBgmResourcePath))
        {
            return;
        }

        StopAudioSource(impactSfxAudioSource);
        StopAudioSource(ambientNoiseAudioSource);
        StopAudioSource(pulseBgmAudioSource);
        PlayLoopingAudio(starBgmAudioSource, starBgmResourcePath, starBgmVolume);
    }

    private void PlayOneShot(AudioSource source, string resourcePath, float volume)
    {
        if (source == null || string.IsNullOrWhiteSpace(resourcePath))
        {
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"Chapter7SceneController: Missing audio clip at Resources/{resourcePath}", this);
            return;
        }

        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void PlayLoopingAudio(AudioSource source, string resourcePath, float volume)
    {
        if (source == null || string.IsNullOrWhiteSpace(resourcePath))
        {
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"Chapter7SceneController: Missing audio clip at Resources/{resourcePath}", this);
            return;
        }

        if (source.clip == clip && source.isPlaying)
        {
            source.volume = Mathf.Clamp01(volume);
            source.loop = true;
            return;
        }

        source.clip = clip;
        source.loop = true;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
    }

    private void StartBackgroundFlicker()
    {
        StopBackgroundFlicker();
        backgroundFlickerRoutine = StartCoroutine(BackgroundFlickerRoutine());
    }

    private void StopBackgroundFlicker()
    {
        if (backgroundFlickerRoutine != null)
        {
            StopCoroutine(backgroundFlickerRoutine);
            backgroundFlickerRoutine = null;
        }
    }

    private IEnumerator BackgroundFlickerRoutine()
    {
        float interval = Mathf.Max(0.01f, backgroundFlickerInterval);
        int switchCount = Mathf.Max(1, backgroundFlickerSwitchCount);
        bool showEarth = false;

        for (int i = 0; i < switchCount; i++)
        {
            showEarth = !showEarth;
            ApplyBackground(showEarth ? earthBackground : statBackground);
            yield return new WaitForSeconds(interval);
        }

        ApplyBackground(endFlickerOnStat ? statBackground : earthBackground);
        backgroundFlickerRoutine = null;
    }

    private void StartBackgroundZoom()
    {
        if (backgroundRenderer == null)
        {
            return;
        }

        CaptureBubbleAnchorsRelativeToBackground();
        StopBackgroundZoom();
        backgroundZoomRoutine = StartCoroutine(BackgroundZoomRoutine());
    }

    private void CaptureBubbleAnchorsRelativeToBackground()
    {
        if (backgroundRenderer == null)
        {
            return;
        }

        Transform backgroundTransform = backgroundRenderer.transform;
        if (runtimePlayerBubbleAnchor != null)
        {
            playerBubbleAnchorLocalToBackground =
                backgroundTransform.InverseTransformPoint(runtimePlayerBubbleAnchor.position);
        }

        if (runtimeNpcBubbleAnchor != null)
        {
            npcBubbleAnchorLocalToBackground =
                backgroundTransform.InverseTransformPoint(runtimeNpcBubbleAnchor.position);
        }

        useBackgroundRelativeBubbleAnchors = true;
    }

    private void StopBackgroundZoom()
    {
        if (backgroundZoomRoutine != null)
        {
            StopCoroutine(backgroundZoomRoutine);
            backgroundZoomRoutine = null;
        }
    }

    private IEnumerator BackgroundZoomRoutine()
    {
        if (backgroundRenderer == null)
        {
            yield break;
        }

        Transform backgroundTransform = backgroundRenderer.transform;
        Vector3 startScale = backgroundTransform.localScale;
        Vector3 targetScale = startScale * Mathf.Max(1f, backgroundZoomScaleMultiplier);
        Vector3 anchorOffset = ResolveBackgroundZoomAnchorOffset(startScale);
        Vector3 anchorPosition = backgroundTransform.localPosition + anchorOffset;
        float duration = Mathf.Max(0.01f, backgroundZoomDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector3 nextScale = Vector3.Lerp(startScale, targetScale, eased);
            backgroundTransform.localScale = nextScale;
            backgroundTransform.localPosition = anchorPosition - ResolveBackgroundZoomAnchorOffset(nextScale);
            yield return null;
        }

        backgroundTransform.localScale = targetScale;
        backgroundTransform.localPosition = anchorPosition - ResolveBackgroundZoomAnchorOffset(targetScale);
        backgroundZoomRoutine = null;
    }

    private IEnumerator PlayEndingZoom()
    {
        if (backgroundRenderer == null)
        {
            yield break;
        }

        useBackgroundRelativeBubbleAnchors = false;
        CacheOriginalBackgroundTransform();

        Transform backgroundTransform = backgroundRenderer.transform;
        Vector3 startScale = backgroundTransform.localScale;
        Vector3 targetScale = originalBackgroundLocalScale * Mathf.Max(1f, endingZoomScaleMultiplier);
        Vector3 anchorPosition = originalBackgroundLocalPosition
            + ResolveZoomAnchorOffset(endingZoomAnchorNormalized, originalBackgroundLocalScale);
        float duration = Mathf.Max(0.01f, endingZoomDuration);
        float elapsed = 0f;
        Vector3 startPosition = backgroundTransform.localPosition;
        Vector3 targetPosition = anchorPosition - ResolveZoomAnchorOffset(endingZoomAnchorNormalized, targetScale);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector3 nextScale = Vector3.Lerp(startScale, targetScale, eased);
            Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, eased);
            backgroundTransform.localScale = nextScale;
            backgroundTransform.localPosition = nextPosition;
            yield return null;
        }

        backgroundTransform.localScale = targetScale;
        backgroundTransform.localPosition = targetPosition;
    }

    private Vector3 ResolveBackgroundZoomAnchorOffset(Vector3 currentScale)
    {
        return ResolveZoomAnchorOffset(backgroundZoomAnchorNormalized, currentScale);
    }

    private Vector3 ResolveZoomAnchorOffset(Vector2 normalizedAnchor, Vector3 currentScale)
    {
        if (backgroundRenderer == null || backgroundRenderer.sprite == null)
        {
            return Vector3.zero;
        }

        Bounds bounds = backgroundRenderer.sprite.bounds;
        Vector3 localAnchorOffset = new Vector3(
            bounds.size.x * normalizedAnchor.x,
            bounds.size.y * normalizedAnchor.y,
            0f);

        if (backgroundRenderer.flipX)
        {
            localAnchorOffset.x = -localAnchorOffset.x;
        }

        if (backgroundRenderer.flipY)
        {
            localAnchorOffset.y = -localAnchorOffset.y;
        }

        return Vector3.Scale(localAnchorOffset, currentScale);
    }

    private void ApplyBackground(Sprite sprite)
    {
        if (backgroundRenderer == null || sprite == null)
        {
            return;
        }

        backgroundRenderer.sprite = sprite;
    }

    private void CacheOriginalBackgroundTransform()
    {
        if (backgroundRenderer == null || hasCachedOriginalBackgroundTransform)
        {
            return;
        }

        Transform backgroundTransform = backgroundRenderer.transform;
        originalBackgroundLocalPosition = backgroundTransform.localPosition;
        originalBackgroundLocalScale = backgroundTransform.localScale;
        hasCachedOriginalBackgroundTransform = true;
    }

    private SpriteRenderer FindBackgroundRenderer()
    {
        GameObject namedBackground = GameObject.Find("Background");
        if (namedBackground != null)
        {
            SpriteRenderer renderer = namedBackground.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                return renderer;
            }
        }

        return FindFirstObjectByType<SpriteRenderer>(FindObjectsInactive.Include);
    }

    private void OnDisable()
    {
        StopBackgroundFlicker();
        StopBackgroundZoom();
        CleanupSceneState(stopAudio: true);
    }

    private void CleanupSceneState(bool stopAudio)
    {
        SetPlayerCutsceneLock(playerInteractor, false);
        useBackgroundRelativeBubbleAnchors = false;

        if (!stopAudio)
        {
            return;
        }

        StopAudioSource(impactSfxAudioSource);
        StopAudioSource(ambientNoiseAudioSource);
        StopAudioSource(pulseBgmAudioSource);
        StopAudioSource(starBgmAudioSource);
    }

    private void QueueSceneTransition()
    {
        if (transitionQueued || string.IsNullOrWhiteSpace(nextSceneName))
        {
            return;
        }

        transitionQueued = true;
        ScreenFadeTransition.Play(nextSceneName, fadeOutToBlackDuration, fadeInFromBlackDuration, startOpaque: false);
    }

    private static void StopAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        if (source.isPlaying)
        {
            source.Stop();
        }

        source.clip = null;
    }
}
