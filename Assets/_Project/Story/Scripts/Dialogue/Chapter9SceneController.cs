using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public sealed class Chapter9SceneController : MonoBehaviour
{
    [Header("流程引用")]
    [SerializeField] private Chapter9 chapter9;

    [Header("开场黑屏")]
    [SerializeField] [Min(0f)] private float initialBlackScreenSeconds = 2f;
    [SerializeField] [Min(0f)] private float fadeInFromBlackSeconds = 1.25f;

    [Header("结尾转场")]
    [SerializeField] [Min(0f)] private float fadeOutToBlackSeconds = 1.25f;
    [SerializeField] [Min(0f)] private float truthRevealedFadeInSeconds = 1.25f;
    [SerializeField] private string nextSceneName = "truth_revealed";

    [Header("开场音效")]
    [SerializeField] private AudioClip introAudioClip;
    [SerializeField] private string introAudioResourcePath = "StoryAudio/BGM/water_step";
    [SerializeField] [Range(0f, 1f)] private float introAudioVolume = 1f;

    private AudioSource audioSource;
    private CanvasGroup fadeCanvasGroup;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = introAudioVolume;

        if (chapter9 == null)
        {
            chapter9 = GetComponent<Chapter9>();
        }

        BuildFadeOverlay();
    }

    private void Start()
    {
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        SetFadeAlpha(1f);
        PlayIntroAudio();

        if (initialBlackScreenSeconds > 0f)
        {
            yield return new WaitForSeconds(initialBlackScreenSeconds);
        }

        StopIntroAudio();
        yield return FadeRoutine(1f, 0f, fadeInFromBlackSeconds);

        bool sequenceCompleted = false;
        if (chapter9 != null)
        {
            chapter9.BeginSequence(() => sequenceCompleted = true);
        }
        else
        {
            sequenceCompleted = true;
        }

        while (!sequenceCompleted)
        {
            yield return null;
        }

        yield return FadeRoutine(0f, 1f, fadeOutToBlackSeconds);
        ScreenFadeTransition.Play(nextSceneName, 0f, truthRevealedFadeInSeconds, startOpaque: true);
    }

    private void PlayIntroAudio()
    {
        AudioClip clip = introAudioClip;
        if (clip == null && !string.IsNullOrWhiteSpace(introAudioResourcePath))
        {
            clip = Resources.Load<AudioClip>(introAudioResourcePath);
        }

        if (clip == null)
        {
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.volume = introAudioVolume;
        audioSource.Play();
    }

    private void StopIntroAudio()
    {
        if (!audioSource.isPlaying)
        {
            audioSource.clip = null;
            return;
        }

        audioSource.Stop();
        audioSource.clip = null;
    }

    private void BuildFadeOverlay()
    {
        var canvasGo = new GameObject("Chapter9FadeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        fadeCanvasGroup = canvasGo.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = true;
        fadeCanvasGroup.interactable = false;

        var imageGo = new GameObject("Chapter9FadeImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageGo.transform.SetParent(canvasGo.transform, false);
        var image = imageGo.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            fadeCanvasGroup.alpha = to;
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(time / duration));
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
    }
}
