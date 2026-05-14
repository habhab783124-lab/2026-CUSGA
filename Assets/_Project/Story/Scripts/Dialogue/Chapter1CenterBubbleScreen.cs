using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Chapter1CenterBubbleScreen : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private SpriteRenderer targetRenderer;

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

    private Vector3 initialScale = Vector3.one;
    private Color initialColor = Color.white;
    private Coroutine transitionRoutine;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public bool IsTransitioning => transitionRoutine != null;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (targetRenderer != null)
        {
            initialColor = targetRenderer.color;
        }

        initialScale = transform.localScale;
    }

    private void OnEnable()
    {
        SetClosedImmediate();
    }

    public void SetClosedImmediate()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        ApplyVisual(0f, 0f);
        isOpen = false;
    }

    public Coroutine PlayOpen(MonoBehaviour owner, System.Action onComplete = null)
    {
        if (owner == null)
        {
            return null;
        }

        if (transitionRoutine != null)
        {
            owner.StopCoroutine(transitionRoutine);
        }

        transitionRoutine = owner.StartCoroutine(PlayOpenRoutine(onComplete));
        return transitionRoutine;
    }

    public Coroutine PlayClose(MonoBehaviour owner, System.Action onComplete = null)
    {
        if (owner == null)
        {
            return null;
        }

        if (transitionRoutine != null)
        {
            owner.StopCoroutine(transitionRoutine);
        }

        transitionRoutine = owner.StartCoroutine(PlayCloseRoutine(onComplete));
        return transitionRoutine;
    }

    private IEnumerator PlayOpenRoutine(System.Action onComplete)
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
                transform.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < overshootDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / overshootDuration);
                transform.localScale = Vector3.LerpUnclamped(to, initialScale, t);
                yield return null;
            }
        }

        ApplyVisual(1f, 1f);
        transitionRoutine = null;
        isOpen = true;
        onComplete?.Invoke();
    }

    private IEnumerator PlayCloseRoutine(System.Action onComplete)
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
        transform.localScale = new Vector3(initialScale.x * width, initialScale.y, initialScale.z);
        SetAlpha(normalizedAlpha);
    }

    private void SetAlpha(float normalizedAlpha)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Color color = initialColor;
        color.a *= Mathf.Clamp01(normalizedAlpha);
        targetRenderer.color = color;
    }
}
