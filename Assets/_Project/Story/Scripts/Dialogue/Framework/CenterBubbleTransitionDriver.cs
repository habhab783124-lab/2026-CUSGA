using System;
using System.Collections;
using UnityEngine;

internal sealed class CenterBubbleTransitionDriver
{
    internal struct Settings
    {
        public float openDuration;
        public float closeDuration;
        public AnimationCurve widthCurve;
        public AnimationCurve alphaCurve;
        public AnimationCurve closeWidthCurve;
        public AnimationCurve closeAlphaCurve;
        public float overshootScaleX;
        public float overshootDuration;
        public float flickerAlphaDrop;
        public int closeFlickerCount;
        public float closeFlickerInterval;
    }

    private readonly MonoBehaviour owner;
    private SpriteRenderer targetRenderer;
    private Transform visualTransform;
    private Settings settings;
    private Coroutine transitionRoutine;
    private Vector3 initialScale = Vector3.one;
    private Color initialColor = Color.white;
    private bool hasInitialVisualState;
    private bool isOpen;

    internal CenterBubbleTransitionDriver(MonoBehaviour owner)
    {
        this.owner = owner;
    }

    internal bool IsOpen => isOpen;
    internal bool IsTransitioning => transitionRoutine != null;

    internal void Configure(SpriteRenderer renderer, Transform transform, Settings newSettings)
    {
        targetRenderer = renderer;
        visualTransform = transform != null ? transform : (renderer != null ? renderer.transform : owner.transform);
        settings = newSettings;
        CaptureInitialVisualState();
    }

    internal void ResetInitialVisualState()
    {
        hasInitialVisualState = false;
        CaptureInitialVisualState();
    }

    internal void SetClosedImmediate()
    {
        StopTransitionRoutine();
        ApplyVisual(0f, 0f);
        isOpen = false;
    }

    internal Coroutine PlayOpen(Action onComplete = null)
    {
        if (owner == null)
        {
            return null;
        }

        StopTransitionRoutine();
        transitionRoutine = owner.StartCoroutine(PlayOpenRoutine(onComplete));
        return transitionRoutine;
    }

    internal Coroutine PlayClose(Action onComplete = null)
    {
        if (owner == null)
        {
            return null;
        }

        StopTransitionRoutine();
        transitionRoutine = owner.StartCoroutine(PlayCloseRoutine(onComplete));
        return transitionRoutine;
    }

    private void StopTransitionRoutine()
    {
        if (transitionRoutine != null && owner != null)
        {
            owner.StopCoroutine(transitionRoutine);
        }

        transitionRoutine = null;
    }

    private void CaptureInitialVisualState()
    {
        if (hasInitialVisualState)
        {
            return;
        }

        if (targetRenderer != null)
        {
            initialColor = targetRenderer.color;
        }

        Transform t = visualTransform != null ? visualTransform : owner.transform;
        if (t != null)
        {
            Vector3 currentScale = t.localScale;
            if (Mathf.Abs(currentScale.x) > 0.0001f)
            {
                initialScale = currentScale;
            }
            else
            {
                initialScale = new Vector3(1f, currentScale.y != 0f ? currentScale.y : 1f, currentScale.z != 0f ? currentScale.z : 1f);
            }
        }

        hasInitialVisualState = true;
    }

    private IEnumerator PlayOpenRoutine(Action onComplete)
    {
        yield return Animate(settings.openDuration, settings.widthCurve, settings.alphaCurve);

        if (settings.overshootDuration > 0f)
        {
            float elapsed = 0f;
            Vector3 from = new Vector3(initialScale.x, initialScale.y, initialScale.z);
            Vector3 to = new Vector3(initialScale.x * settings.overshootScaleX, initialScale.y, initialScale.z);
            while (elapsed < settings.overshootDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settings.overshootDuration);
                if (visualTransform != null)
                {
                    visualTransform.localScale = Vector3.LerpUnclamped(from, to, t);
                }

                yield return null;
            }

            elapsed = 0f;
            while (elapsed < settings.overshootDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settings.overshootDuration);
                if (visualTransform != null)
                {
                    visualTransform.localScale = Vector3.LerpUnclamped(to, initialScale, t);
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
        for (int i = 0; i < settings.closeFlickerCount; i++)
        {
            SetAlpha(settings.flickerAlphaDrop);
            yield return new WaitForSeconds(settings.closeFlickerInterval);
            SetAlpha(1f);
            yield return new WaitForSeconds(settings.closeFlickerInterval);
        }

        yield return Animate(settings.closeDuration, settings.closeWidthCurve, settings.closeAlphaCurve);
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
        if (visualTransform == null)
        {
            visualTransform = targetRenderer != null ? targetRenderer.transform : owner.transform;
        }

        if (visualTransform != null)
        {
            visualTransform.localScale = new Vector3(initialScale.x * width, initialScale.y, initialScale.z);
        }

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
