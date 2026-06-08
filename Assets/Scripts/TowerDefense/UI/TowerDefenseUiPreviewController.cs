using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-only controller that simulates HUD states in the UI preview scene so the
/// author can tune layout, colors, fonts and sizes without entering Play mode.
///
/// Use the <see cref="previewState"/> dropdown to cycle through common HUD states:
/// idle, shop-card selected, placed tower selected, upgrade available, max level.
///
/// When you are satisfied with the result, click "Apply to All Levels" to sync the
/// HUDCanvas from the template scene into every final tower-defense level.
/// </summary>
[ExecuteAlways]
public sealed class TowerDefenseUiPreviewController : MonoBehaviour
{
    public enum UiPreviewState
    {
        Idle,
        ShopCardSelected,
        PlacedTowerSelected,
        PlacedTowerWithUpgrade,
        PlacedTowerMaxLevel,
        DragPreviewActive,
    }

    // ────────────────────────────
    //  Mock Game State
    // ────────────────────────────

    [Header("Mock Game State")]
    [SerializeField] private int mockScrap = 146;
    [SerializeField] private int mockBaseHealth = 78;
    [SerializeField] private int mockCurrentWave = 3;
    [SerializeField] private int mockTotalWaves = 8;

    [Header("Preview State")]
    [SerializeField] private UiPreviewState previewState = UiPreviewState.Idle;

    // ────────────────────────────
    //  Mock Tower Data
    // ────────────────────────────

    [Header("Mock Tower Data")]
    [SerializeField] private string mockTowerName = "Generator";
    [SerializeField] private int mockTowerNumber = 1;
    [SerializeField] private int mockTowerLevel = 2;
    [SerializeField] private int mockTowerMaxLevel = 4;
    [SerializeField] private int mockUpgradeCost = 45;
    [SerializeField] private bool mockIsPowered = true;

    [Header("Mock Tower Stats")]
    [SerializeField] private int mockDamage = 5;
    [SerializeField] private float mockAttackInterval = 0.55f;
    [SerializeField] private float mockAttackRange = 3.2f;
    [SerializeField] private int mockPowerRequired = 3;

    [Header("Mock Mechanical Upgrade")]
    [SerializeField] private string mockMechanicalDescription = "Chain lightning: attacks bounce to 2 additional targets.";

    [Header("Mock Shop Card")]
    [SerializeField] private TowerType mockSelectedCardType = TowerType.SingleTarget;

    [Header("Preview Behavior")]
    [SerializeField] private bool refreshOnValidate = true;

    // ────────────────────────────
    //  Cached references
    // ────────────────────────────

    private Canvas _hudCanvas;
    private TMP_Text _scrapText;
    private TMP_Text _baseHealthText;
    private TMP_Text _waveText;
    private TMP_Text _selectionText;
    private TMP_Text _structureStatusText;
    private Button _relayTowerButton;
    private Button _defenseTowerButton;
    private Button _slowFieldTowerButton;
    private Button _bombardTowerButton;
    private Button _upgradeButton;
    private Button _deleteButton;
    private TowerInfoPopup _infoPopup;

    private void OnEnable()
    {
        RefreshPreview();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
#endif
    }

#if UNITY_EDITOR
    private void OnUndoRedoPerformed()
    {
        RefreshPreview();
    }
#endif

    private void OnValidate()
    {
        if (refreshOnValidate)
        {
            // Defer to avoid OnValidate modifying scene state during asset import
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        RefreshPreview();
                    }
                };
                return;
            }
#endif
            RefreshPreview();
        }
    }

    private void FindCachedReferences()
    {
        _hudCanvas = FindObjectOfType<Canvas>();
        if (_hudCanvas == null) return;

        _scrapText = FindTmpTextByName("ScrapText");
        _baseHealthText = FindTmpTextByName("BaseHealthText");
        _waveText = FindTmpTextByName("WaveText");
        _selectionText = FindTmpTextByName("SelectionText");
        _structureStatusText = FindTmpTextByName("StructureStatusText");

        _relayTowerButton = FindButtonByName("RelayTowerButton");
        _defenseTowerButton = FindButtonByName("DefenseTowerButton");
        _slowFieldTowerButton = FindButtonByName("SlowFieldTowerButton");
        _bombardTowerButton = FindButtonByName("BombardTowerButton");
        _upgradeButton = FindButtonByName("UpgradeSelectedStructureButton");
        _deleteButton = FindButtonByName("DeleteSelectedStructureButton");

        if (_infoPopup == null)
        {
            _infoPopup = FindFirstObjectByType<TowerInfoPopup>(FindObjectsInactive.Include);
        }
    }

    private TMP_Text FindTmpTextByName(string name)
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == name)
            {
                return texts[i];
            }
        }

        return null;
    }

    private Button FindButtonByName(string name)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == name)
            {
                return buttons[i];
            }
        }

        return null;
    }

    // ────────────────────────────
    //  Refresh
    // ────────────────────────────

    public void RefreshPreview()
    {
        FindCachedReferences();

        SetTextIfBound(_scrapText, FormatMetric("Scrap", mockScrap.ToString()));
        SetTextIfBound(_baseHealthText, FormatMetric("Base HP", mockBaseHealth.ToString()));
        string waveText = mockTotalWaves > 0 ? $"{mockCurrentWave}/{mockTotalWaves}" : "0/0";
        SetTextIfBound(_waveText, FormatMetric("Wave", waveText));

        // Clear old text fields (popup handles tower info now)
        SetTextIfBound(_selectionText, string.Empty);
        SetTextIfBound(_structureStatusText, string.Empty);

        // Reset all card button highlights
        SetCardHighlight(_relayTowerButton, false, new Color(1f, 1f, 1f, 1f));
        SetCardHighlight(_defenseTowerButton, false, new Color(1f, 1f, 1f, 1f));
        SetCardHighlight(_slowFieldTowerButton, false, new Color(1f, 1f, 1f, 1f));
        SetCardHighlight(_bombardTowerButton, false, new Color(1f, 1f, 1f, 1f));

        // Hide upgrade/delete by default
        SetButtonVisible(_upgradeButton, false);
        SetButtonVisible(_deleteButton, false);

        // Hide popup by default
        if (_infoPopup != null) _infoPopup.Hide();

        switch (previewState)
        {
            case UiPreviewState.ShopCardSelected:
                ApplyShopCardSelected();
                break;

            case UiPreviewState.PlacedTowerSelected:
                ApplyPlacedTowerSelected(false, false);
                break;

            case UiPreviewState.PlacedTowerWithUpgrade:
                ApplyPlacedTowerSelected(true, false);
                break;

            case UiPreviewState.PlacedTowerMaxLevel:
                ApplyPlacedTowerSelected(false, true);
                break;

            case UiPreviewState.DragPreviewActive:
                ApplyDragPreviewActive();
                break;

            default:
                // Idle — nothing extra
                break;
        }
    }

    private void ApplyShopCardSelected()
    {
        Button cardButton = GetCardButton(mockSelectedCardType);
        SetCardHighlight(cardButton, true, new Color(1f, 0.92f, 0.55f, 1f));

        ShowShopCardPopup(mockSelectedCardType, cardButton);
    }

    private void ApplyPlacedTowerSelected(bool showUpgrade, bool isMaxLevel)
    {
        int displayLevel = isMaxLevel ? mockTowerMaxLevel : mockTowerLevel;
        bool hasMech = isMaxLevel || (displayLevel >= mockTowerMaxLevel);

        string title = $"{mockTowerName} #{mockTowerNumber}  LV {displayLevel}/{mockTowerMaxLevel}";
        string powerState = mockIsPowered ? "ONLINE" : "OFFLINE";
        string stats = $"Power: {mockPowerRequired} req  |  {powerState}\n"
                     + $"Damage: {mockDamage}\n"
                     + $"Interval: {mockAttackInterval:0.00}s\n"
                     + $"Range: {mockAttackRange:0.0}";

        string extra = hasMech && !string.IsNullOrWhiteSpace(mockMechanicalDescription)
            ? $"[M] {mockMechanicalDescription}"
            : (!isMaxLevel && !hasMech && !string.IsNullOrWhiteSpace(mockMechanicalDescription)
                ? $"LV {mockTowerMaxLevel} unlocks: {mockMechanicalDescription}"
                : null);

        // Position the popup at a mock world position (center-right of screen)
        if (_infoPopup != null)
        {
            if (_hudCanvas != null && Camera.main != null)
            {
                // Place popup at a simulated world position: center of the screen
                Vector3 screenCenter = new Vector3(Screen.width * 0.6f, Screen.height * 0.55f, 0f);
                _infoPopup.ShowAtWorldPosition(
                    Camera.main.ScreenToWorldPoint(screenCenter),
                    title, stats, extra);
            }
        }

        // Show delete button
        SetButtonVisible(_deleteButton, true);

        // Show upgrade button if applicable
        if (showUpgrade)
        {
            SetButtonVisible(_upgradeButton, true);
            if (_upgradeButton != null)
            {
                TMP_Text label = _upgradeButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    bool isFinalUpgrade = displayLevel + 1 >= mockTowerMaxLevel;
                    label.text = isFinalUpgrade
                        ? $"Upgrade LV{displayLevel + 1} [{mockUpgradeCost}] M"
                        : $"Upgrade LV{displayLevel + 1} [{mockUpgradeCost}]";
                }
            }
        }
    }

    private void ApplyDragPreviewActive()
    {
        // Show a selected card highlight
        Button cardButton = GetCardButton(mockSelectedCardType);
        SetCardHighlight(cardButton, true, new Color(1f, 0.92f, 0.55f, 1f));

        ShowShopCardPopup(mockSelectedCardType, cardButton);
    }

    private void ShowShopCardPopup(TowerType towerType, Button cardButton)
    {
        if (_infoPopup == null || cardButton == null) return;

        string title = mockSelectedCardType.ToString();
        string stats = $"Cost: {mockUpgradeCost} SCRAP\n"
                     + $"Power Required: {mockPowerRequired}\n"
                     + $"Damage: {mockDamage}\n"
                     + $"Interval: {mockAttackInterval:0.00}s\n"
                     + $"Range: {mockAttackRange:0.0}";
        string extra = "Drag to preview legal placement areas.";

        RectTransform cardRect = cardButton.GetComponent<RectTransform>();
        _infoPopup.ShowAboveRect(cardRect, title, stats, extra);
    }

    private Button GetCardButton(TowerType towerType)
    {
        switch (towerType)
        {
            case TowerType.Relay: return _relayTowerButton;
            case TowerType.SlowField: return _slowFieldTowerButton;
            case TowerType.Bombard: return _bombardTowerButton;
            default: return _defenseTowerButton;
        }
    }

    // ────────────────────────────
    //  Helpers
    // ────────────────────────────

    private static void SetTextIfBound(TMP_Text text, string value)
    {
        if (text != null) text.text = value ?? string.Empty;
    }

    private static void SetCardHighlight(Button button, bool highlighted, Color highlightColor)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image == null) return;
        image.color = highlighted ? highlightColor : new Color(1f, 1f, 1f, 1f);
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }

    private static string FormatMetric(string label, string value)
    {
        return $"{label}: {value}";
    }

}
