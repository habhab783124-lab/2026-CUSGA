using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Chapter1CenterBubbleScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Transition")]
    [SerializeField] private float openDuration = 0.32f;
    [SerializeField] private float closeDuration = 0.24f;
    [SerializeField] private AnimationCurve widthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve closeWidthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve closeAlphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Accent")]
    [SerializeField] private float overshootScaleX = 1.06f;
    [SerializeField] private float overshootDuration = 0.06f;
    [SerializeField] private float flickerAlphaDrop = 0.72f;
    [SerializeField] private int closeFlickerCount = 2;
    [SerializeField] private float closeFlickerInterval = 0.03f;

    private CenterBubbleTransitionDriver transitionDriver;

    public bool IsOpen => transitionDriver != null && transitionDriver.IsOpen;
    public bool IsTransitioning => transitionDriver != null && transitionDriver.IsTransitioning;

    private void Awake()
    {
        transitionDriver = new CenterBubbleTransitionDriver(this);
        ResolveTargetRenderer();
        ConfigureDriver();
    }

    private void OnEnable()
    {
        SetClosedImmediate();
    }

    public void SetClosedImmediate()
    {
        ConfigureDriver();
        transitionDriver.SetClosedImmediate();
    }

    public Coroutine PlayOpen(MonoBehaviour owner, System.Action onComplete = null)
    {
        ConfigureDriver();
        return transitionDriver.PlayOpen(onComplete);
    }

    public Coroutine PlayClose(MonoBehaviour owner, System.Action onComplete = null)
    {
        ConfigureDriver();
        return transitionDriver.PlayClose(onComplete);
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
            SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null && renderer.gameObject.name == "CenterBubble")
                {
                    targetRenderer = renderer;
                    break;
                }
            }
        }
    }

    private void ConfigureDriver()
    {
        if (transitionDriver == null)
        {
            transitionDriver = new CenterBubbleTransitionDriver(this);
        }

        transitionDriver.Configure(
            targetRenderer,
            targetRenderer != null ? targetRenderer.transform : transform,
            new CenterBubbleTransitionDriver.Settings
            {
                openDuration = openDuration,
                closeDuration = closeDuration,
                widthCurve = widthCurve,
                alphaCurve = alphaCurve,
                closeWidthCurve = closeWidthCurve,
                closeAlphaCurve = closeAlphaCurve,
                overshootScaleX = overshootScaleX,
                overshootDuration = overshootDuration,
                flickerAlphaDrop = flickerAlphaDrop,
                closeFlickerCount = closeFlickerCount,
                closeFlickerInterval = closeFlickerInterval
            });
    }
}
