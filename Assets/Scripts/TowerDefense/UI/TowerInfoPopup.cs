using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A lightweight info popup that displays tower stats.
///
/// It can be positioned either next to a placed tower (world-space → screen-space)
/// or above a shop card (screen-space). The presenter calls Show* / Hide as needed.
/// </summary>
public sealed class TowerInfoPopup : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Image panelBackground;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text extraText;

    [Header("Layout")]
    [SerializeField] private Vector2 screenOffset = new Vector2(24f, 48f);
    [SerializeField] private Vector2 cardPopupOffset = new Vector2(0f, 16f);
    [SerializeField] private float paddingX = 18f;
    [SerializeField] private float paddingY = 14f;

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Camera _worldCamera;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvasRect = _canvas.GetComponent<RectTransform>();
        _worldCamera = Camera.main;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Position the popup at a world position (for placed towers), offset to the upper-right.
    /// </summary>
    public void ShowAtWorldPosition(
        Vector3 worldPosition,
        string title,
        string stats,
        string extra = null)
    {
        if (_canvas == null || _worldCamera == null) return;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldPosition);
        PositionAtScreenPoint(screenPoint, screenOffset, title, stats, extra);
    }

    /// <summary>
    /// Position the popup relative to a screen-space RectTransform (for shop cards).
    /// </summary>
    public void ShowAboveRect(RectTransform targetRect, string title, string stats, string extra = null)
    {
        if (targetRect == null) return;

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        // Top-center of the target rect
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;

        PositionAtScreenPoint(topCenter, cardPopupOffset, title, stats, extra);
    }

    private void PositionAtScreenPoint(Vector3 screenPoint, Vector2 offset, string title, string stats, string extra)
    {
        if (_canvasRect == null || panelRect == null || _canvas == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPoint, _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _worldCamera,
            out localPoint);

        // Apply pivot-relative offset so the popup appears to the upper-right
        panelRect.anchoredPosition = localPoint + offset;

        SetContent(title, stats, extra);
        ClampToCanvas();

        gameObject.SetActive(true);
        IsVisible = true;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        IsVisible = false;
    }

    private void SetContent(string title, string stats, string extra)
    {
        if (titleText != null) titleText.text = title ?? string.Empty;
        if (statsText != null) statsText.text = stats ?? string.Empty;
        if (extraText != null)
        {
            bool hasExtra = !string.IsNullOrWhiteSpace(extra);
            extraText.gameObject.SetActive(hasExtra);
            extraText.text = hasExtra ? extra : string.Empty;
        }
    }

    private void ClampToCanvas()
    {
        if (_canvasRect == null || panelRect == null) return;

        Vector2 canvasSize = _canvasRect.rect.size;
        Vector2 halfPanel = panelRect.rect.size * 0.5f;
        Vector2 anchoredPos = panelRect.anchoredPosition;

        float minX = halfPanel.x;
        float maxX = canvasSize.x - halfPanel.x;
        float minY = halfPanel.y;
        float maxY = canvasSize.y - halfPanel.y;

        anchoredPos.x = Mathf.Clamp(anchoredPos.x, minX, maxX);
        anchoredPos.y = Mathf.Clamp(anchoredPos.y, minY, maxY);

        panelRect.anchoredPosition = anchoredPos;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
    }
#endif
}
