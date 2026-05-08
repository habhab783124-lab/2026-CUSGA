using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 场景切换全屏黑场：先淡出至黑，异步加载场景，再淡入显示新场景。使用 DontDestroyOnLoad，加载完成后自毁。
/// </summary>
public sealed class ScreenFadeTransition : MonoBehaviour
{
    private static ScreenFadeTransition _active;

    private CanvasGroup _canvasGroup;

    /// <summary>淡出 → 加载场景 → 淡入，使用 unscaled 时间。</summary>
    /// <param name="startOpaque">为 true 时跳过淡出（假定屏幕已为全黑），直接加载后再淡入。</param>
    public static void Play(string sceneName, float fadeOutSeconds, float fadeInSeconds, bool startOpaque = false)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (_active != null)
        {
            Debug.LogWarning("ScreenFadeTransition: 已有过渡在进行，忽略重复请求。", _active);
            return;
        }

        var go = new GameObject(nameof(ScreenFadeTransition));
        var t = go.AddComponent<ScreenFadeTransition>();
        _active = t;
        DontDestroyOnLoad(go);
        t.StartCoroutine(t.RunRoutine(sceneName, fadeOutSeconds, fadeInSeconds, startOpaque));
    }

    private void Awake()
    {
        BuildOverlay();
    }

    private void OnDestroy()
    {
        if (_active == this)
        {
            _active = null;
        }
    }

    private void BuildOverlay()
    {
        var canvasGo = new GameObject("FadeCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();

        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = false;

        var imgGo = new GameObject("FadeImage");
        imgGo.transform.SetParent(canvasGo.transform, false);
        var img = imgGo.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private IEnumerator RunRoutine(string sceneName, float fadeOutSeconds, float fadeInSeconds, bool startOpaque)
    {
        if (startOpaque)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            yield return null;
        }
        else
        {
            yield return FadeRoutine(0f, 1f, fadeOutSeconds);
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError($"ScreenFadeTransition: 无法加载场景「{sceneName}」。请确认已加入 Build Settings。", this);
            Destroy(gameObject);
            yield break;
        }

        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            yield return null;
        }

        op.allowSceneActivation = true;
        yield return null;
        yield return null;

        yield return FadeRoutine(1f, 0f, fadeInSeconds);

        Destroy(gameObject);
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (_canvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            _canvasGroup.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }

        _canvasGroup.alpha = to;
    }
}
