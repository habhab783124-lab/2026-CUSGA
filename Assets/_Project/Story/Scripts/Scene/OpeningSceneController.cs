using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏启动开幕：显示单张全屏背景，点击任意位置后淡入下一场景。
/// </summary>
[DisallowMultipleComponent]
public sealed class OpeningSceneController : MonoBehaviour
{
    [Header("Opening Visual")]
    [SerializeField] private Sprite openingSprite;
    [SerializeField] private Color backgroundColor = Color.black;

    [Header("Scene Flow")]
    [SerializeField] private string nextSceneName = "chapter1";
    [SerializeField] [Min(0f)] private float fadeOutToBlackDuration = 0.75f;
    [SerializeField] [Min(0f)] private float fadeInFromBlackDuration = 0.75f;

    private Canvas openingCanvas;
    private CanvasScaler openingCanvasScaler;
    private Image backdropImage;
    private Image openingImage;
    private AspectRatioFitter openingAspectFitter;
    private bool transitionQueued;

    private const string CanvasName = "OpeningCanvas";
    private const string BackdropName = "OpeningBackdrop";
    private const string ImageName = "OpeningImage";

    private void Awake()
    {
        EnsureVisuals();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        EnsureVisuals();
        ApplyVisualState();
    }

    private void Update()
    {
        if (!Application.isPlaying || transitionQueued)
        {
            return;
        }

        if (!ShouldAdvance())
        {
            return;
        }

        transitionQueued = true;
        ScreenFadeTransition.Play(nextSceneName, fadeOutToBlackDuration, fadeInFromBlackDuration, startOpaque: false);
    }

    private bool ShouldAdvance()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                return true;
            }
        }

        return Input.GetMouseButtonDown(0);
    }

    private void EnsureVisuals()
    {
        if (openingCanvas == null)
        {
            Transform existingCanvas = transform.Find(CanvasName);
            GameObject canvasObject = existingCanvas != null
                ? existingCanvas.gameObject
                : new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

            if (existingCanvas == null)
            {
                canvasObject.transform.SetParent(transform, false);
            }

            openingCanvas = canvasObject.GetComponent<Canvas>();
            openingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            openingCanvas.sortingOrder = 1000;

            openingCanvasScaler = canvasObject.GetComponent<CanvasScaler>();
            openingCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            openingCanvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            openingCanvasScaler.matchWidthOrHeight = 0.5f;
        }

        if (backdropImage == null)
        {
            Transform existingBackdrop = openingCanvas.transform.Find(BackdropName);
            GameObject backdropObject = existingBackdrop != null
                ? existingBackdrop.gameObject
                : new GameObject(BackdropName, typeof(RectTransform), typeof(Image));

            if (existingBackdrop == null)
            {
                backdropObject.transform.SetParent(openingCanvas.transform, false);
            }

            backdropImage = backdropObject.GetComponent<Image>();
            ConfigureStretchRect(backdropImage.rectTransform);
            backdropImage.raycastTarget = false;
        }

        if (openingImage == null)
        {
            Transform existingImage = openingCanvas.transform.Find(ImageName);
            GameObject imageObject = existingImage != null
                ? existingImage.gameObject
                : new GameObject(ImageName, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));

            if (existingImage == null)
            {
                imageObject.transform.SetParent(openingCanvas.transform, false);
            }

            openingImage = imageObject.GetComponent<Image>();
            openingAspectFitter = imageObject.GetComponent<AspectRatioFitter>();

            ConfigureStretchRect(openingImage.rectTransform);
            openingImage.raycastTarget = false;
            openingAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }
    }

    private void ApplyVisualState()
    {
        if (backdropImage != null)
        {
            backdropImage.color = backgroundColor;
            backdropImage.sprite = null;
        }

        if (openingImage == null)
        {
            return;
        }

        openingImage.sprite = openingSprite;
        openingImage.color = openingSprite != null ? Color.white : backgroundColor;
        openingImage.preserveAspect = false;

        if (openingAspectFitter == null)
        {
            return;
        }

        if (openingSprite == null || Mathf.Approximately(openingSprite.rect.height, 0f))
        {
            openingAspectFitter.aspectMode = AspectRatioFitter.AspectMode.None;
            return;
        }

        openingAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        openingAspectFitter.aspectRatio = openingSprite.rect.width / openingSprite.rect.height;
    }

    private static void ConfigureStretchRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }
}
