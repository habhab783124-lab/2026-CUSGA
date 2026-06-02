using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialDragHintOverlay : MonoBehaviour
{
    [Header("Indicator Visual")]
    [SerializeField] private Sprite handSprite;
    [SerializeField] private Vector2 handSize = new Vector2(48f, 48f);
    [SerializeField] private float handAlpha = 0.72f;
    [SerializeField] private Color handColor = Color.white;

    [Header("Ghost Tower")]
    [SerializeField] private Sprite ghostTowerSprite;
    [SerializeField] private Vector2 ghostTowerSize = new Vector2(44f, 44f);
    [SerializeField] private float ghostTowerAlpha = 0.45f;

    [Header("Animation")]
    [SerializeField] private float cycleDuration = 2.2f;
    [SerializeField] private float pauseBetweenCycles = 0.6f;
    [SerializeField] private float pressScale = 0.7f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Positions")]
    [SerializeField] private Vector2 startAnchorPosition = new Vector2(200f, 120f);
    [SerializeField] private Vector3 targetWorldPosition = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector2 curveControlOffset = new Vector2(0f, 180f);

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _handRect;
    private RectTransform _ghostRect;
    private Image _handImage;
    private Image _ghostImage;
    private Coroutine _loopRoutine;
    private bool _dismissed;
    private bool _showing;

    public bool IsShowing => _showing;

    public void Show()
    {
        if (_dismissed || _showing) return;
        _showing = true;
        EnsureVisuals();
        _canvasGroup.alpha = 1f;
        _loopRoutine = StartCoroutine(AnimationLoop());
    }

    public void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        _showing = false;

        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }

        if (_canvasGroup != null)
        {
            StartCoroutine(FadeOutAndDestroy());
        }
    }

    private void OnDestroy()
    {
        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }
    }

    private void EnsureVisuals()
    {
        if (_canvas != null) return;

        GameObject canvasGo = new GameObject("DragHintCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 210;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        CreateHandIndicator();
        CreateGhostTower();
    }

    private void CreateHandIndicator()
    {
        GameObject go = new GameObject("HandIndicator");
        go.transform.SetParent(_canvas.transform, false);

        _handRect = go.AddComponent<RectTransform>();
        _handRect.anchorMin = Vector2.zero;
        _handRect.anchorMax = Vector2.zero;
        _handRect.pivot = new Vector2(0.5f, 0.5f);
        _handRect.sizeDelta = handSize;
        _handRect.anchoredPosition = startAnchorPosition;

        _handImage = go.AddComponent<Image>();

        Sprite resolvedSprite = handSprite;
        if (resolvedSprite == null)
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];
            Vector2 center = new Vector2(15.5f, 15.5f);
            for (int i = 0; i < pixels.Length; i++)
            {
                int x = i % 32;
                int y = i / 32;
                float dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[i] = dist <= 14f ? Color.white : Color.clear;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            resolvedSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        }
        _handImage.sprite = resolvedSprite;
        _handImage.color = new Color(handColor.r, handColor.g, handColor.b, 0f);
        _handImage.raycastTarget = false;
    }

    private void CreateGhostTower()
    {
        if (ghostTowerSprite == null) return;

        GameObject go = new GameObject("GhostTower");
        go.transform.SetParent(_canvas.transform, false);

        _ghostRect = go.AddComponent<RectTransform>();
        _ghostRect.anchorMin = Vector2.zero;
        _ghostRect.anchorMax = Vector2.zero;
        _ghostRect.pivot = new Vector2(0.5f, 0.5f);
        _ghostRect.sizeDelta = ghostTowerSize;
        _ghostRect.anchoredPosition = startAnchorPosition;

        _ghostImage = go.AddComponent<Image>();
        _ghostImage.sprite = ghostTowerSprite;
        _ghostImage.preserveAspect = true;
        _ghostImage.color = new Color(1f, 1f, 1f, 0f);
        _ghostImage.raycastTarget = false;
    }

    private IEnumerator AnimationLoop()
    {
        while (!_dismissed)
        {
            yield return StartCoroutine(PlayOneCycle());
            float waited = 0f;
            while (waited < pauseBetweenCycles && !_dismissed)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator PlayOneCycle()
    {
        Vector2 endAnchor = WorldToCanvasAnchor(targetWorldPosition);
        Vector2 controlPoint = (startAnchorPosition + endAnchor) * 0.5f + curveControlOffset;

        float fadeInDuration = 0.18f;
        float pressDuration = 0.15f;
        float moveDuration = Mathf.Max(0.3f, cycleDuration - fadeInDuration - pressDuration - 0.4f);
        float releaseAndFadeDuration = 0.4f;

        // Phase 1: Fade in at start position
        SetPositions(startAnchorPosition);
        yield return StartCoroutine(FadeHand(0f, handAlpha, fadeInDuration));

        // Phase 2: Press down (scale shrink + ghost appears)
        yield return StartCoroutine(PressDown(pressDuration));

        // Phase 3: Move along bezier curve
        yield return StartCoroutine(MoveAlongCurve(startAnchorPosition, controlPoint, endAnchor, moveDuration));

        // Phase 4: Release and fade out
        yield return StartCoroutine(ReleaseAndFadeOut(releaseAndFadeDuration));
    }

    private IEnumerator FadeHand(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !_dismissed)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            SetHandAlpha(alpha);
            _handRect.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one, t);
            yield return null;
        }
        SetHandAlpha(toAlpha);
        _handRect.localScale = Vector3.one;
    }

    private IEnumerator PressDown(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !_dismissed)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(1f, pressScale, t);
            _handRect.localScale = Vector3.one * scale;
            SetGhostAlpha(Mathf.Lerp(0f, ghostTowerAlpha, t));
            yield return null;
        }
        _handRect.localScale = Vector3.one * pressScale;
        SetGhostAlpha(ghostTowerAlpha);
    }

    private IEnumerator MoveAlongCurve(Vector2 start, Vector2 control, Vector2 end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !_dismissed)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = moveCurve != null ? moveCurve.Evaluate(t) : t;
            Vector2 pos = QuadraticBezier(start, control, end, curveT);
            SetPositions(pos);
            yield return null;
        }
        SetPositions(end);
    }

    private IEnumerator ReleaseAndFadeOut(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !_dismissed)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(pressScale, 1f, Mathf.Clamp01(t * 3f));
            _handRect.localScale = Vector3.one * scale;
            float fadeT = Mathf.Clamp01((t - 0.3f) / 0.7f);
            SetHandAlpha(Mathf.Lerp(handAlpha, 0f, fadeT));
            SetGhostAlpha(Mathf.Lerp(ghostTowerAlpha, 0f, fadeT));
            yield return null;
        }
        SetHandAlpha(0f);
        SetGhostAlpha(0f);
        _handRect.localScale = Vector3.one;
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            }
            yield return null;
        }

        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
        }
    }

    private void SetPositions(Vector2 anchoredPos)
    {
        if (_handRect != null) _handRect.anchoredPosition = anchoredPos;
        if (_ghostRect != null) _ghostRect.anchoredPosition = anchoredPos + new Vector2(0f, -handSize.y * 0.4f);
    }

    private void SetHandAlpha(float alpha)
    {
        if (_handImage != null)
        {
            Color c = _handImage.color;
            c.a = alpha;
            _handImage.color = c;
        }
    }

    private void SetGhostAlpha(float alpha)
    {
        if (_ghostImage != null)
        {
            Color c = _ghostImage.color;
            c.a = alpha;
            _ghostImage.color = c;
        }
    }

    private Vector2 WorldToCanvasAnchor(Vector3 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return startAnchorPosition;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
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

        return new Vector2(screenPos.x / canvasScale, screenPos.y / canvasScale);
    }

    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }
}