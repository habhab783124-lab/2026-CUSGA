using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// `LevelSelectController` 负责整个关卡选择页的装配、显示和场景跳转。
///
/// 这里延续当前项目已经在主菜单里使用的工作流：
/// 1. 场景第一次还是空壳时，自动补出一套默认 UI 骨架。
/// 2. 后续尽量只同步文案、主题和引用，不去覆盖你在 Scene 里手调过的布局。
///
/// 这样能同时满足两件事：
/// - 我们先把页面搭出来，省掉从零摆 UI 的重复工作。
/// - 页面对象依旧真实存在于场景层级里，方便你之后继续手调。
/// </summary>
[ExecuteAlways]
public sealed class LevelSelectController : MonoBehaviour
{
    [Serializable]
    public sealed class LevelDefinition
    {
#if UNITY_EDITOR
        [Header("Scene Ref")]
        [SerializeField] private SceneAsset sceneAsset;
#endif

        [SerializeField] private string sceneName = "SampleScene";
        [SerializeField] private string scenePath = "Assets/Scenes/SampleScene.unity";

        [Header("Display Copy")]
        [SerializeField] private string displayName = "LEVEL 01";
        [SerializeField] private string subtitle = "CURRENT TEST ROUTE";
        [SerializeField]
        [TextArea(2, 5)]
        private string description = "The current playable sample mission.";
        [SerializeField] private string statusLabel = "OPEN";

        [Header("Card Style")]
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Color accentColor = new Color(1f, 0.68f, 0.36f, 1f);
        [SerializeField] private bool interactable = true;

        public string SceneName => sceneName;
        public string ScenePath => scenePath;
        public string DisplayName => displayName;
        public string Subtitle => subtitle;
        public string Description => description;
        public string StatusLabel => statusLabel;
        public Sprite IconSprite => iconSprite;
        public Color AccentColor => accentColor;
        public bool Interactable => interactable;

        /// <summary>
        /// 通过构造函数写入默认关卡数据，
        /// 可以继续保持字段是私有序列化的，同时避免外层类直接去改内部字段。
        /// </summary>
        public LevelDefinition(
            string sceneName,
            string scenePath,
            string displayName,
            string subtitle,
            string description,
            string statusLabel,
            Color accentColor,
            bool interactable = true)
        {
            this.sceneName = sceneName;
            this.scenePath = scenePath;
            this.displayName = displayName;
            this.subtitle = subtitle;
            this.description = description;
            this.statusLabel = statusLabel;
            this.accentColor = accentColor;
            this.interactable = interactable;
        }

        /// <summary>
        /// Unity 序列化系统需要无参构造入口，所以这里保留一个空构造。
        /// </summary>
        public LevelDefinition()
        {
        }

#if UNITY_EDITOR
        /// <summary>
        /// 允许你直接在 Inspector 里拖场景资产。
        /// 真正运行时只吃字符串，避免打包后再依赖 `SceneAsset` 这种编辑器类型。
        /// </summary>
        public bool SyncSceneReference()
        {
            if (sceneAsset == null)
            {
                if (!string.IsNullOrWhiteSpace(scenePath) && string.IsNullOrWhiteSpace(sceneName))
                {
                    sceneName = Path.GetFileNameWithoutExtension(scenePath);
                    return true;
                }

                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(sceneAsset);
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            bool changed = assetPath != scenePath || assetName != sceneName;
            scenePath = assetPath;
            sceneName = assetName;
            return changed;
        }
#endif
    }

    [Header("Scene Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private LevelDefinition[] levels = Array.Empty<LevelDefinition>();

    [Header("Visual Theme")]
    [SerializeField] private Color backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
    [SerializeField] private Color frameCoreColor = new Color(0.05f, 0.08f, 0.11f, 0.95f);
    [SerializeField] private Color frameInsetColor = new Color(0.04f, 0.06f, 0.09f, 0.98f);
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color subtitleColor = new Color(0.78f, 0.86f, 0.94f, 1f);
    [SerializeField] private Color descriptionColor = new Color(0.82f, 0.88f, 0.95f, 1f);
    [SerializeField] private Color backButtonColor = new Color(0.18f, 0.72f, 0.94f, 1f);
    [SerializeField] private Color backButtonPrimaryTextColor = Color.white;
    [SerializeField] private Color backButtonSecondaryTextColor = new Color(0.85f, 0.95f, 1f, 1f);

    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite frameCoreSprite;
    [SerializeField] private Sprite frameInsetSprite;
    [SerializeField] private Sprite backButtonSprite;

    [SerializeField] private TMP_FontAsset titleFontAsset;
    [SerializeField] private TMP_FontAsset bodyFontAsset;
    [SerializeField] private TMP_FontAsset accentFontAsset;

    [Header("Page Copy")]
    [SerializeField] private string titleCopy = "MISSION SELECT";
    [SerializeField] private string subtitleCopy = "Choose a battlefield scene to edit or play";
    [SerializeField]
    [TextArea(2, 5)]
    private string descriptionCopy = "The first mission is the current sample scene. The extra four scenes are prepared so you can continue building later maps directly in the Unity editor.";
    [SerializeField] private string backPrimaryCopy = "Back";
    [SerializeField] private string backSecondaryCopy = "RETURN TO MAIN MENU";

    [Header("Scene UI Refs")]
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Canvas mainCanvas;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private StandaloneInputModule standaloneInputModule;

    [SerializeField] private RectTransform pageRoot;
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private Image frameCorePanel;
    [SerializeField] private Image frameInsetPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button backButton;
    [SerializeField] private Image backButtonImage;
    [SerializeField] private TextMeshProUGUI backButtonPrimaryText;
    [SerializeField] private TextMeshProUGUI backButtonSecondaryText;
    [SerializeField] private RectTransform cardsRoot;
    [SerializeField] private LevelSelectCard[] levelCards = Array.Empty<LevelSelectCard>();
    [SerializeField] private bool hasBuiltSceneUi;

    private const string CanvasName = "LevelSelectCanvas";
    private const string EventSystemName = "LevelSelectEventSystem";
    private const string RootName = "LevelSelectRoot";
    private const string BackgroundName = "BackgroundPanel";
    private const string FrameCoreName = "FrameCore";
    private const string FrameInsetName = "FrameInset";
    private const string TitleName = "TitleText";
    private const string SubtitleName = "SubtitleText";
    private const string DescriptionName = "DescriptionText";
    private const string BackButtonName = "BackButton";
    private const string BackPrimaryName = "BackButtonPrimaryText";
    private const string BackSecondaryName = "BackButtonSecondaryText";
    private const string CardsRootName = "LevelCardsRoot";

    private void OnEnable()
    {
        EnsureDefaultLevelDefinitions();
        EnsureEditorSceneReferences();
        EnsureSceneObjects();
        ApplyThemeAndCopyToBoundSceneObjects();
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void OnValidate()
    {
        EnsureDefaultLevelDefinitions();
        EnsureEditorSceneReferences();
        EnsureSceneObjects();
        ApplyThemeAndCopyToBoundSceneObjects();
    }

    public void EditorMaterializeSceneUi()
    {
        EnsureDefaultLevelDefinitions();
        EnsureEditorSceneReferences();
        EnsureSceneObjects();
    }

    public void EditorApplyAuthoringToScene()
    {
        EnsureDefaultLevelDefinitions();
        EnsureEditorSceneReferences();
        EnsureSceneObjects();
        ApplyThemeAndCopyToBoundSceneObjects();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToMainMenu();
        }
    }

    /// <summary>
    /// 返回主菜单。这里只在 Play 模式真正切场景，避免你在编辑器里误触。
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning("LevelSelectController 没有配置主菜单场景名。", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// 进入某个关卡场景。
    /// </summary>
    public void LoadLevel(string sceneName)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("LevelSelectController 收到了空的关卡场景名。", this);
            return;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// 如果场景里还没有配置关卡列表，就先补一套默认 5 关数据。
    /// 这样你第一次打开 Inspector 时就已经有可改的入口。
    /// </summary>
    private void EnsureDefaultLevelDefinitions()
    {
        if (levels != null && levels.Length > 0)
        {
            return;
        }

        levels = new[]
        {
            CreateDefaultLevel("SampleScene", "Assets/Scenes/SampleScene.unity", "LEVEL 01", "CURRENT TEST ROUTE", "The current playable sample battlefield. Use this as the base mission while the later scenes are still being authored.", "OPEN", new Color(1f, 0.68f, 0.36f, 1f)),
            CreateDefaultLevel("Level02", "Assets/Scenes/Level02.unity", "LEVEL 02", "SECOND FRONT", "A new mission scene prepared for your next map. You can open it directly in the editor and start rebuilding terrain, routes, and decoration.", "EDIT READY", new Color(0.34f, 0.88f, 0.96f, 1f)),
            CreateDefaultLevel("Level03", "Assets/Scenes/Level03.unity", "LEVEL 03", "OUTER GRID", "Reserved for the third mission layout. The gameplay skeleton is copied over so you can focus on map authoring first.", "EDIT READY", new Color(0.47f, 0.96f, 0.74f, 1f)),
            CreateDefaultLevel("Level04", "Assets/Scenes/Level04.unity", "LEVEL 04", "RELAY BREAK", "Reserved for the fourth mission layout. Treat this as another fully editable gameplay scene slot.", "EDIT READY", new Color(1f, 0.78f, 0.43f, 1f)),
            CreateDefaultLevel("Level05", "Assets/Scenes/Level05.unity", "LEVEL 05", "FINAL CIRCUIT", "Reserved for the fifth mission layout. Keep this one for your later, more advanced battlefield ideas.", "EDIT READY", new Color(1f, 0.52f, 0.41f, 1f))
        };

        MarkSceneDirty();
    }

    /// <summary>
    /// 如果你在 Inspector 里拖了场景资产，这里会把场景名和路径同步回运行时字段。
    /// </summary>
    private void EnsureEditorSceneReferences()
    {
#if UNITY_EDITOR
        if (levels == null)
        {
            return;
        }

        bool changed = false;
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null && levels[i].SyncSceneReference())
            {
                changed = true;
            }
        }

        if (changed)
        {
            MarkSceneDirty();
        }
#endif
    }

    /// <summary>
    /// 先确保基础设施存在，再决定是搭建默认页面，还是只维护已有引用。
    /// </summary>
    private void EnsureSceneObjects()
    {
        EnsureCameraExists();
        EnsureEventSystemExists();
        EnsureCanvasExists();

        if (!hasBuiltSceneUi)
        {
            BuildDefaultLayout();
            hasBuiltSceneUi = true;
            MarkSceneDirty();
            return;
        }

        ValidateBoundReferences();
        EnsureLevelCardCount();
    }

    private void EnsureCameraExists()
    {
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (sceneCamera != null)
        {
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = backgroundColor;
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        sceneCamera = cameraObject.AddComponent<Camera>();
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = backgroundColor;
        sceneCamera.orthographic = true;
        sceneCamera.nearClipPlane = -10f;
        sceneCamera.farClipPlane = 10f;
        cameraObject.tag = "MainCamera";
    }

    private void EnsureEventSystemExists()
    {
        if (eventSystem == null)
        {
            eventSystem = FindObjectOfType<EventSystem>();
        }

        if (eventSystem != null)
        {
            standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInputModule == null)
            {
                standaloneInputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            return;
        }

        GameObject eventSystemObject = new GameObject(EventSystemName);
        eventSystem = eventSystemObject.AddComponent<EventSystem>();
        standaloneInputModule = eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void EnsureCanvasExists()
    {
        if (mainCanvas == null)
        {
            mainCanvas = FindObjectOfType<Canvas>();
        }

        if (mainCanvas == null)
        {
            GameObject canvasObject = new GameObject(
                CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            mainCanvas = canvasObject.GetComponent<Canvas>();
            canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            graphicRaycaster = canvasObject.GetComponent<GraphicRaycaster>();
        }

        RectTransform canvasRect = mainCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            // 理论上 Canvas 应该总是挂在 RectTransform 上，
            // 这里做一次保守兜底，避免空场景首次生成时因为根节点类型不对而导致后续整页 UI 不被创建。
            GameObject replacementCanvasObject = new GameObject(
                CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas replacementCanvas = replacementCanvasObject.GetComponent<Canvas>();
            replacementCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            replacementCanvas.worldCamera = null;
            replacementCanvas.planeDistance = 100f;

            mainCanvas = replacementCanvas;
            canvasScaler = replacementCanvasObject.GetComponent<CanvasScaler>();
            graphicRaycaster = replacementCanvasObject.GetComponent<GraphicRaycaster>();
            canvasRect = replacementCanvas.transform as RectTransform;
        }

        if (canvasRect != null)
        {
            canvasRect.localScale = Vector3.one;
        }

        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.worldCamera = null;
        mainCanvas.planeDistance = 100f;

        canvasScaler = mainCanvas.GetComponent<CanvasScaler>();
        if (canvasScaler == null)
        {
            canvasScaler = mainCanvas.gameObject.AddComponent<CanvasScaler>();
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        graphicRaycaster = mainCanvas.GetComponent<GraphicRaycaster>();
        if (graphicRaycaster == null)
        {
            graphicRaycaster = mainCanvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    /// <summary>
    /// 搭一版默认关卡选择页骨架。
    /// 重点是把对象真的创建到场景层级里，而不是只做运行时临时 UI。
    /// </summary>
    private void BuildDefaultLayout()
    {
        RectTransform canvasRect = mainCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        pageRoot = EnsureRectTransform(canvasRect, RootName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 900f));
        backgroundPanel = EnsureImage(canvasRect, BackgroundName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), backgroundColor);
        backgroundPanel.raycastTarget = false;
        backgroundPanel.rectTransform.SetAsFirstSibling();
        pageRoot.SetAsLastSibling();

        frameCorePanel = EnsureImage(pageRoot, FrameCoreName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 900f), frameCoreColor);
        frameInsetPanel = EnsureImage(pageRoot, FrameInsetName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1496f, 836f), frameInsetColor);

        titleText = EnsureText(pageRoot, TitleName, new Vector2(-548f, 318f), new Vector2(720f, 116f), 72f, FontStyles.Bold, titleColor, titleCopy, TextAlignmentOptions.Left, titleFontAsset);
        subtitleText = EnsureText(pageRoot, SubtitleName, new Vector2(-544f, 244f), new Vector2(760f, 44f), 26f, FontStyles.Bold, subtitleColor, subtitleCopy, TextAlignmentOptions.Left, accentFontAsset);
        subtitleText.characterSpacing = 2f;

        descriptionText = EnsureText(pageRoot, DescriptionName, new Vector2(-430f, 170f), new Vector2(980f, 88f), 23f, FontStyles.Normal, descriptionColor, descriptionCopy, TextAlignmentOptions.Left, bodyFontAsset);
        descriptionText.lineSpacing = 8f;

        backButton = EnsureButton(pageRoot, BackButtonName, new Vector2(-584f, -344f), new Vector2(280f, 92f), backButtonColor);
        backButtonImage = backButton.GetComponent<Image>();
        backButtonPrimaryText = EnsureText(backButton.transform as RectTransform, BackPrimaryName, new Vector2(20f, 10f), new Vector2(180f, 36f), 32f, FontStyles.Bold, backButtonPrimaryTextColor, backPrimaryCopy, TextAlignmentOptions.Left, titleFontAsset);
        backButtonSecondaryText = EnsureText(backButton.transform as RectTransform, BackSecondaryName, new Vector2(20f, -18f), new Vector2(220f, 24f), 16f, FontStyles.Bold, backButtonSecondaryTextColor, backSecondaryCopy, TextAlignmentOptions.Left, accentFontAsset);

        cardsRoot = EnsureRectTransform(pageRoot, CardsRootName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, -56f), new Vector2(1240f, 520f));
        EnsureLevelCardCount();
    }

    /// <summary>
    /// 让场景里的卡片数量始终和关卡配置数量对齐。
    /// </summary>
    private void EnsureLevelCardCount()
    {
        if (cardsRoot == null)
        {
            return;
        }

        RefreshLevelCardCache();

        int desiredCount = Mathf.Max(1, levels != null ? levels.Length : 0);
        while (levelCards.Length < desiredCount)
        {
            int newIndex = levelCards.Length;
            CreateCardAtIndex(newIndex);
            RefreshLevelCardCache();
        }

        for (int i = 0; i < levelCards.Length; i++)
        {
            if (levelCards[i] != null)
            {
                levelCards[i].gameObject.SetActive(i < desiredCount);
            }
        }
    }

    private void RefreshLevelCardCache()
    {
        if (cardsRoot == null)
        {
            levelCards = Array.Empty<LevelSelectCard>();
            return;
        }

        LevelSelectCard[] cards = cardsRoot.GetComponentsInChildren<LevelSelectCard>(true);
        Array.Sort(cards, (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
        levelCards = cards;
    }

    /// <summary>
    /// 创建单张关卡卡片。
    /// 这里把卡片拆成真实子物体，方便你后面直接在 Hierarchy 里找得到、改得动。
    /// </summary>
    private void CreateCardAtIndex(int index)
    {
        Vector2 size = new Vector2(360f, 188f);
        Vector2 anchoredPosition = GetDefaultCardPosition(index);

        string cardName = $"LevelCard_{index + 1:00}";
        RectTransform cardRect = EnsureRectTransform(cardsRoot, cardName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);

        Image backgroundImage = cardRect.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = cardRect.gameObject.AddComponent<Image>();
        }

        backgroundImage.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);

        Button button = cardRect.GetComponent<Button>();
        if (button == null)
        {
            button = cardRect.gameObject.AddComponent<Button>();
        }

        button.targetGraphic = backgroundImage;
        button.transition = Selectable.Transition.ColorTint;

        LevelSelectCard card = cardRect.GetComponent<LevelSelectCard>();
        if (card == null)
        {
            card = cardRect.gameObject.AddComponent<LevelSelectCard>();
        }

        Image accentStrip = EnsureImage(cardRect, "AccentStrip", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(12f, 188f), Color.white);
        Image iconImage = EnsureImage(cardRect, "IconImage", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(44f, 44f), Color.white);
        iconImage.raycastTarget = false;

        TextMeshProUGUI title = EnsureText(cardRect, "TitleText", new Vector2(-138f, 56f), new Vector2(248f, 42f), 32f, FontStyles.Bold, Color.white, $"LEVEL {index + 1:00}", TextAlignmentOptions.Left, titleFontAsset);
        TextMeshProUGUI subtitle = EnsureText(cardRect, "SubtitleText", new Vector2(-132f, 22f), new Vector2(248f, 26f), 17f, FontStyles.Bold, subtitleColor, "MISSION SLOT", TextAlignmentOptions.Left, accentFontAsset);
        subtitle.characterSpacing = 1.2f;
        TextMeshProUGUI description = EnsureText(cardRect, "DescriptionText", new Vector2(-120f, -30f), new Vector2(270f, 78f), 18f, FontStyles.Normal, descriptionColor, "This card will route to a gameplay scene.", TextAlignmentOptions.Left, bodyFontAsset);
        description.lineSpacing = 4f;
        TextMeshProUGUI status = EnsureText(cardRect, "StatusText", new Vector2(-124f, -72f), new Vector2(160f, 24f), 16f, FontStyles.Bold, Color.white, "OPEN", TextAlignmentOptions.Left, accentFontAsset);
        status.characterSpacing = 1.2f;

        card.CaptureGeneratedReferences(button, backgroundImage, accentStrip, iconImage, title, subtitle, description, status);
    }

    /// <summary>
    /// 默认卡片布局先给出一个可编辑的网格起点。
    /// 你后面如果觉得位置不合适，直接在 Scene 里拖每张卡片即可。
    /// </summary>
    private Vector2 GetDefaultCardPosition(int index)
    {
        int columns = 3;
        float horizontalSpacing = 410f;

        int row = index / columns;
        int column = index % columns;

        float offsetX = (column - 1) * horizontalSpacing;
        float offsetY = row == 0 ? 118f : -104f;

        if (index == 3)
        {
            offsetX = -205f;
        }
        else if (index == 4)
        {
            offsetX = 205f;
        }

        return new Vector2(offsetX, offsetY);
    }

    private void BindButtons()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ReturnToMainMenu);
            backButton.onClick.AddListener(ReturnToMainMenu);
        }

        ApplyLevelCards();
    }

    private void UnbindButtons()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ReturnToMainMenu);
        }

        if (levelCards == null)
        {
            return;
        }

        for (int i = 0; i < levelCards.Length; i++)
        {
            if (levelCards[i] != null)
            {
                levelCards[i].SetClickAction(null);
            }
        }
    }

    /// <summary>
    /// 页面已经成型后，只做显式引用体检，不再靠对象名回查整套 UI。
    /// </summary>
    private void ValidateBoundReferences()
    {
        if (backButton != null)
        {
            backButtonImage = backButton.GetComponent<Image>();
        }

        RefreshLevelCardCache();

        WarnIfMissing(sceneCamera, nameof(sceneCamera));
        WarnIfMissing(mainCanvas, nameof(mainCanvas));
        WarnIfMissing(canvasScaler, nameof(canvasScaler));
        WarnIfMissing(graphicRaycaster, nameof(graphicRaycaster));
        WarnIfMissing(eventSystem, nameof(eventSystem));
        WarnIfMissing(standaloneInputModule, nameof(standaloneInputModule));
        WarnIfMissing(pageRoot, nameof(pageRoot));
        WarnIfMissing(backgroundPanel, nameof(backgroundPanel));
        WarnIfMissing(frameCorePanel, nameof(frameCorePanel));
        WarnIfMissing(frameInsetPanel, nameof(frameInsetPanel));
        WarnIfMissing(titleText, nameof(titleText));
        WarnIfMissing(subtitleText, nameof(subtitleText));
        WarnIfMissing(descriptionText, nameof(descriptionText));
        WarnIfMissing(backButton, nameof(backButton));
        WarnIfMissing(backButtonImage, nameof(backButtonImage));
        WarnIfMissing(backButtonPrimaryText, nameof(backButtonPrimaryText));
        WarnIfMissing(backButtonSecondaryText, nameof(backButtonSecondaryText));
        WarnIfMissing(cardsRoot, nameof(cardsRoot));
    }

    /// <summary>
    /// 把 Inspector 里的主题和文案应用到当前已存在的场景对象上。
    /// 这里故意不碰布局参数，避免覆盖你手调过的页面。
    /// </summary>
    private void ApplyThemeAndCopyToBoundSceneObjects()
    {
        if (sceneCamera != null)
        {
            sceneCamera.backgroundColor = backgroundColor;
        }

        if (mainCanvas != null)
        {
            mainCanvas.gameObject.SetActive(true);
        }

        if (pageRoot != null)
        {
            pageRoot.gameObject.SetActive(true);
            pageRoot.SetAsLastSibling();
        }

        SetActiveIfPresent(backgroundPanel);
        SetActiveIfPresent(frameCorePanel);
        SetActiveIfPresent(frameInsetPanel);
        SetActiveIfPresent(titleText);
        SetActiveIfPresent(subtitleText);
        SetActiveIfPresent(descriptionText);
        SetActiveIfPresent(backButton);
        SetActiveIfPresent(backButtonPrimaryText);
        SetActiveIfPresent(backButtonSecondaryText);
        SetActiveIfPresent(cardsRoot);

        ApplyImageTheme(backgroundPanel, backgroundColor, backgroundSprite, false);
        ApplyImageTheme(frameCorePanel, frameCoreColor, frameCoreSprite, false);
        ApplyImageTheme(frameInsetPanel, frameInsetColor, frameInsetSprite, false);
        ApplyImageTheme(backButtonImage, backButtonColor, backButtonSprite, false);

        if (backgroundPanel != null)
        {
            backgroundPanel.rectTransform.SetAsFirstSibling();
        }

        if (pageRoot != null)
        {
            pageRoot.SetAsLastSibling();
        }

        ApplyTextTheme(titleText, titleCopy, titleColor, titleFontAsset);
        ApplyTextTheme(subtitleText, subtitleCopy, subtitleColor, accentFontAsset);
        ApplyTextTheme(descriptionText, descriptionCopy, descriptionColor, bodyFontAsset);
        ApplyTextTheme(backButtonPrimaryText, backPrimaryCopy, backButtonPrimaryTextColor, titleFontAsset);
        ApplyTextTheme(backButtonSecondaryText, backSecondaryCopy, backButtonSecondaryTextColor, accentFontAsset);

        if (backButton != null)
        {
            backButton.targetGraphic = backButtonImage;

            ColorBlock colors = backButton.colors;
            colors.normalColor = backButtonColor;
            colors.highlightedColor = Color.Lerp(backButtonColor, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(backButtonColor, Color.black, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.3f, 0.32f, 0.36f, 0.8f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            backButton.colors = colors;
        }

        ApplyLevelCards();
    }

    /// <summary>
    /// 用配置数据刷新每张关卡卡片的显示和点击行为。
    /// </summary>
    private void ApplyLevelCards()
    {
        if (levels == null || levelCards == null)
        {
            return;
        }

        int visibleCount = Mathf.Min(levels.Length, levelCards.Length);
        for (int i = 0; i < levelCards.Length; i++)
        {
            if (levelCards[i] == null)
            {
                continue;
            }

            if (i >= visibleCount || levels[i] == null)
            {
                levelCards[i].gameObject.SetActive(false);
                levelCards[i].SetClickAction(null);
                continue;
            }

            LevelDefinition level = levels[i];
            levelCards[i].gameObject.SetActive(true);
            levelCards[i].ApplyPresentation(
                level.DisplayName,
                level.Subtitle,
                level.Description,
                level.StatusLabel,
                level.AccentColor,
                level.IconSprite,
                titleFontAsset,
                bodyFontAsset,
                accentFontAsset,
                level.Interactable);

            string targetSceneName = level.SceneName;
            if (level.Interactable && !string.IsNullOrWhiteSpace(targetSceneName))
            {
                levelCards[i].SetClickAction(() => LoadLevel(targetSceneName));
            }
            else
            {
                levelCards[i].SetClickAction(null);
            }
        }
    }

    private static LevelDefinition CreateDefaultLevel(
        string sceneName,
        string scenePath,
        string displayName,
        string subtitle,
        string description,
        string statusLabel,
        Color accentColor)
    {
        return new LevelDefinition(sceneName, scenePath, displayName, subtitle, description, statusLabel, accentColor, true);
    }

    private static RectTransform EnsureRectTransform(RectTransform parent, string objectName, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        RectTransform existing = FindRect(parent, objectName);
        if (existing != null)
        {
            return existing;
        }

        GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.localScale = Vector3.one;
        return rectTransform;
    }

    private static Image EnsureImage(RectTransform parent, string objectName, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        RectTransform rectTransform = EnsureRectTransform(parent, objectName, anchor, pivot, anchoredPosition, sizeDelta);
        Image image = rectTransform.GetComponent<Image>();
        if (image == null)
        {
            image = rectTransform.gameObject.AddComponent<Image>();
        }

        image.color = color;
        return image;
    }

    private TextMeshProUGUI EnsureText(RectTransform parent, string objectName, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, FontStyles fontStyle, Color color, string text, TextAlignmentOptions alignment, TMP_FontAsset preferredFontAsset)
    {
        RectTransform rectTransform = EnsureRectTransform(parent, objectName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);
        TextMeshProUGUI label = rectTransform.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            label = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        }

        label.font = ResolveFontAsset(preferredFontAsset);
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private Button EnsureButton(RectTransform parent, string objectName, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        RectTransform rectTransform = EnsureRectTransform(parent, objectName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);

        Image image = rectTransform.GetComponent<Image>();
        if (image == null)
        {
            image = rectTransform.gameObject.AddComponent<Image>();
        }

        image.color = color;

        Button button = rectTransform.GetComponent<Button>();
        if (button == null)
        {
            button = rectTransform.gameObject.AddComponent<Button>();
        }

        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        return button;
    }

    private void ApplyTextTheme(TextMeshProUGUI label, string text, Color color, TMP_FontAsset preferredFontAsset)
    {
        if (label == null)
        {
            return;
        }

        label.text = text;
        label.color = color;
        TMP_FontAsset resolvedFontAsset = ResolveFontAsset(preferredFontAsset);
        if (resolvedFontAsset != null)
        {
            label.font = resolvedFontAsset;
        }
    }

    private static void ApplyImageTheme(Image image, Color color, Sprite sprite, bool preserveAspect)
    {
        if (image == null)
        {
            return;
        }

        image.color = color;
        if (sprite != null)
        {
            image.sprite = sprite;
        }

        image.preserveAspect = preserveAspect && image.sprite != null;
    }

    private static void SetActiveIfPresent(Component component)
    {
        if (component != null)
        {
            component.gameObject.SetActive(true);
        }
    }

    private TMP_FontAsset ResolveFontAsset(TMP_FontAsset preferredFontAsset)
    {
        if (preferredFontAsset != null)
        {
            return preferredFontAsset;
        }

        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    private static RectTransform FindRect(RectTransform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(objectName);
        return child as RectTransform;
    }

    private void WarnIfMissing(UnityEngine.Object reference, string fieldName)
    {
        if (reference != null)
        {
            return;
        }

        Debug.LogWarning($"LevelSelectController 缺少场景引用：{fieldName}。请在 LevelSelect 场景的 Inspector 中补齐。", this);
    }

    private void MarkSceneDirty()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return;
        }

        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
