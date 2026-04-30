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
/// `LevelSelectController` 负责整个关卡选择页的行为层装配和场景跳转。
///
/// 这一版进一步把关卡选择页收口成“Scene 主导、脚本只接行为”的模式：
/// 1. 场景中的卡片、标题、返回按钮和背景，默认都由作者直接在 Scene / Inspector 中维护。
/// 2. 控制器主要只负责：
///    - 返回主菜单
///    - 给每张卡绑定点击行为
///    - 进行最轻量的引用体检
/// 3. 如果需要从空壳场景重建默认骨架，改成显式作者命令执行，
///    不再在 `OnEnable / OnValidate` 中自动接管整个场景。
///
/// 这样做以后，Scene 里的卡片布局、文本和配色都可以稳定手调，
/// 不会再因为脚本启用或字段变化而被整页回写。
/// </summary>
public sealed class LevelSelectController : MonoBehaviour
{
    [Serializable]
    public sealed class LevelDefinition
    {
#if UNITY_EDITOR
        [Header("Scene Ref")]
        [SerializeField] private SceneAsset sceneAsset; // 中文：场景资产
#endif

        [SerializeField] private string sceneName = "SampleScene"; // 中文：场景名称
        [SerializeField] private string scenePath = "Assets/Scenes/SampleScene.unity"; // 中文：场景路径

        [Header("Display Copy")]
        [SerializeField] private string displayName = "第一关"; // 中文：显示名称
        [SerializeField] private string subtitle = "当前测试路线"; // 中文：副标题
        [SerializeField]
        [TextArea(2, 5)]
        private string description = "当前可游玩的样例关卡。"; // 中文：描述
        [SerializeField] private string statusLabel = "可进入"; // 中文：状态标签

        [Header("Card Style")]
        [SerializeField] private Sprite iconSprite; // 中文：图标精灵
        [SerializeField] private Color accentColor = new Color(1f, 0.68f, 0.36f, 1f); // 中文：accent颜色
        [SerializeField] private bool interactable = true; // 中文：可交互

        public string SceneName => sceneName; // 中文：场景名称
        public string ScenePath => scenePath; // 中文：场景路径
        public string DisplayName => displayName; // 中文：显示名称
        public string Subtitle => subtitle; // 中文：副标题
        public string Description => description; // 中文：描述
        public string StatusLabel => statusLabel; // 中文：状态标签
        public Sprite IconSprite => iconSprite; // 中文：图标精灵
        public Color AccentColor => accentColor; // 中文：Accent颜色
        public bool Interactable => interactable; // 中文：可交互

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
    [SerializeField] private string mainMenuSceneName = "MainMenu"; // 中文：主菜单场景名称
    [Tooltip("推荐使用的关卡目录资产。关卡卡片数据应优先维护在这份独立资产中，而不是继续塞在控制器里。")]
    [SerializeField] private LevelSelectCatalogAsset levelCatalogAsset; // 中文：等级目录资产
    [SerializeField] private LevelDefinition[] levels = Array.Empty<LevelDefinition>(); // 中文：等级列表

    [Header("Visual Theme")]
    [SerializeField] private Color backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f); // 中文：背景颜色
    [SerializeField] private Color frameCoreColor = new Color(0.05f, 0.08f, 0.11f, 0.95f); // 中文：边框核心颜色
    [SerializeField] private Color frameInsetColor = new Color(0.04f, 0.06f, 0.09f, 0.98f); // 中文：边框Inset颜色
    [SerializeField] private Color titleColor = Color.white; // 中文：标题颜色
    [SerializeField] private Color subtitleColor = new Color(0.78f, 0.86f, 0.94f, 1f); // 中文：副标题颜色
    [SerializeField] private Color descriptionColor = new Color(0.82f, 0.88f, 0.95f, 1f); // 中文：描述颜色
    [SerializeField] private Color backButtonColor = new Color(0.18f, 0.72f, 0.94f, 1f); // 中文：back按钮颜色
    [SerializeField] private Color backButtonPrimaryTextColor = Color.white; // 中文：back按钮主文本颜色
    [SerializeField] private Color backButtonSecondaryTextColor = new Color(0.85f, 0.95f, 1f, 1f); // 中文：back按钮副文本颜色

    [SerializeField] private Sprite backgroundSprite; // 中文：背景精灵
    [SerializeField] private Sprite frameCoreSprite; // 中文：边框核心精灵
    [SerializeField] private Sprite frameInsetSprite; // 中文：边框Inset精灵
    [SerializeField] private Sprite backButtonSprite; // 中文：back按钮精灵

    [SerializeField] private TMP_FontAsset titleFontAsset; // 中文：标题字体资产
    [SerializeField] private TMP_FontAsset bodyFontAsset; // 中文：主体字体资产
    [SerializeField] private TMP_FontAsset accentFontAsset; // 中文：accent字体资产

    [Header("Page Copy")]
    [SerializeField] private string titleCopy = "关卡选择"; // 中文：标题文案
    [SerializeField] private string subtitleCopy = "选择要编辑或游玩的战场场景"; // 中文：副标题文案
    [SerializeField]
    [TextArea(2, 5)]
    private string descriptionCopy = "第一关是当前主测试场景，后面的四个场景已经预留好，方便你直接在 Unity 里继续制作后续地图。"; // 中文：描述文案
    [SerializeField] private string backPrimaryCopy = "返回"; // 中文：back主文案
    [SerializeField] private string backSecondaryCopy = "回到主菜单"; // 中文：back副文案

    [Header("Scene UI Refs")]
    [SerializeField] private Camera sceneCamera; // 中文：场景相机
    [SerializeField] private Canvas mainCanvas; // 中文：主画布
    [SerializeField] private CanvasScaler canvasScaler; // 中文：画布Scaler
    [SerializeField] private GraphicRaycaster graphicRaycaster; // 中文：graphicRaycaster
    [SerializeField] private EventSystem eventSystem; // 中文：事件System
    [SerializeField] private StandaloneInputModule standaloneInputModule; // 中文：standalone输入模块

    [SerializeField] private RectTransform pageRoot; // 中文：page根节点
    [SerializeField] private Image backgroundPanel; // 中文：背景面板
    [SerializeField] private Image frameCorePanel; // 中文：边框核心面板
    [SerializeField] private Image frameInsetPanel; // 中文：边框Inset面板
    [SerializeField] private TextMeshProUGUI titleText; // 中文：标题文本
    [SerializeField] private TextMeshProUGUI subtitleText; // 中文：副标题文本
    [SerializeField] private TextMeshProUGUI descriptionText; // 中文：描述文本
    [SerializeField] private Button backButton; // 中文：back按钮
    [SerializeField] private Image backButtonImage; // 中文：back按钮Image
    [SerializeField] private TextMeshProUGUI backButtonPrimaryText; // 中文：back按钮主文本
    [SerializeField] private TextMeshProUGUI backButtonSecondaryText; // 中文：back按钮副文本
    [SerializeField] private RectTransform cardsRoot; // 中文：卡片列表根节点
    [SerializeField] private LevelSelectCard[] levelCards = Array.Empty<LevelSelectCard>(); // 中文：等级卡片列表
    [SerializeField] private bool hasBuiltSceneUi; // 中文：是否有Built场景界面

    private const string CanvasName = "LevelSelectCanvas"; // 中文：画布名称
    private const string EventSystemName = "LevelSelectEventSystem"; // 中文：事件System名称
    private const string RootName = "LevelSelectRoot"; // 中文：根节点名称
    private const string BackgroundName = "BackgroundPanel"; // 中文：背景名称
    private const string FrameCoreName = "FrameCore"; // 中文：边框核心名称
    private const string FrameInsetName = "FrameInset"; // 中文：边框Inset名称
    private const string TitleName = "TitleText"; // 中文：标题名称
    private const string SubtitleName = "SubtitleText"; // 中文：副标题名称
    private const string DescriptionName = "DescriptionText"; // 中文：描述名称
    private const string BackButtonName = "BackButton"; // 中文：Back按钮名称
    private const string BackPrimaryName = "BackButtonPrimaryText"; // 中文：Back主名称
    private const string BackSecondaryName = "BackButtonSecondaryText"; // 中文：Back副名称
    private const string CardsRootName = "LevelCardsRoot"; // 中文：卡片列表根节点名称

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void OnValidate()
    {
        if (backButton != null)
        {
            backButtonImage = backButton.GetComponent<Image>();
        }

        RefreshLevelCardCache();
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
    /// Play 模式启动后，对关卡选择页做一次轻量引用体检。
    /// </summary>
    private void Start()
    {
        ValidateBoundReferences();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 显式把默认关卡选择页骨架物化到当前场景。
    /// </summary>
    public void EditorMaterializeSceneUi()
    {
        EnsureDefaultLevelDefinitions();
        EnsureEditorSceneReferences();
        EnsureSceneObjects();
        ApplyThemeAndCopyToBoundSceneObjects();
        BindButtons();
        MarkSceneDirty();
    }

    /// <summary>
    /// 显式把当前作者默认数据刷到现有卡片和页面对象上。
    /// </summary>
    public void EditorApplyAuthoringToScene()
    {
        EnsureDefaultLevelDefinitions();
        EnsureEditorSceneReferences();
        ValidateBoundReferences();
        ApplyThemeAndCopyToBoundSceneObjects();
        BindButtons();
        MarkSceneDirty();
    }
#endif

    /// <summary>
    /// 如果场景里还没有配置关卡列表，就先补一套默认 5 关数据。
    /// 这样你第一次打开 Inspector 时就已经有可改的入口。
    /// </summary>
    private void EnsureDefaultLevelDefinitions()
    {
        if (ResolveLevelCatalogAsset() != null)
        {
            return;
        }

        if (levels != null && levels.Length > 0)
        {
            return;
        }

        levels = new[]
        {
            CreateDefaultLevel("SampleScene", "Assets/Scenes/SampleScene.unity", "第一关", "当前测试路线", "当前可游玩的样例战场。后续关卡还在制作中时，可以先把这里当作主测试关卡。", "可进入", new Color(1f, 0.68f, 0.36f, 1f)),
            CreateDefaultLevel("Level02", "Assets/Scenes/Level02.unity", "第二关", "第二战线", "为下一张地图预留的新任务场景。你可以直接在编辑器里打开它，开始重做地形、路线和装饰。", "可编辑", new Color(0.34f, 0.88f, 0.96f, 1f)),
            CreateDefaultLevel("Level03", "Assets/Scenes/Level03.unity", "第三关", "外围电网", "为第三张任务地图预留的场景。玩法骨架已经复制好，你可以优先专注于地图制作。", "可编辑", new Color(0.47f, 0.96f, 0.74f, 1f)),
            CreateDefaultLevel("Level04", "Assets/Scenes/Level04.unity", "第四关", "继电断层", "为第四张任务地图预留的场景。可以把它当作另一个完整可编辑的玩法场景槽位。", "可编辑", new Color(1f, 0.78f, 0.43f, 1f)),
            CreateDefaultLevel("Level05", "Assets/Scenes/Level05.unity", "第五关", "最终回路", "为第五张任务地图预留的场景。适合留给后续更复杂、更进阶的战场构想。", "可编辑", new Color(1f, 0.52f, 0.41f, 1f))
        };

        MarkSceneDirty();
    }

    /// <summary>
    /// 如果你在 Inspector 里拖了场景资产，这里会把场景名和路径同步回运行时字段。
    /// </summary>
    private void EnsureEditorSceneReferences()
    {
#if UNITY_EDITOR
        LevelSelectCatalogAsset resolvedCatalogAsset = ResolveLevelCatalogAsset();
        if (resolvedCatalogAsset != null)
        {
            if (resolvedCatalogAsset.SyncSceneReferences())
            {
                EditorUtility.SetDirty(resolvedCatalogAsset);
            }

            return;
        }

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
            sceneCamera = FindRootComponentByName<Camera>("Main Camera");
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
            eventSystem = FindRootComponentByName<EventSystem>(EventSystemName);
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
            mainCanvas = FindRootComponentByName<Canvas>(CanvasName);
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

        int desiredCount = Mathf.Max(1, GetConfiguredLevels().Length);
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

        BindLevelCardActionsOnly();
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
        LevelSelectCatalogAsset.LevelEntry[] configuredLevels = GetConfiguredLevels();
        if (configuredLevels.Length == 0 || levelCards == null)
        {
            return;
        }

        int visibleCount = Mathf.Min(configuredLevels.Length, levelCards.Length);
        for (int i = 0; i < levelCards.Length; i++)
        {
            if (levelCards[i] == null)
            {
                continue;
            }

            if (i >= visibleCount || configuredLevels[i] == null)
            {
                levelCards[i].gameObject.SetActive(false);
                levelCards[i].SetClickAction(null);
                continue;
            }

            LevelSelectCatalogAsset.LevelEntry level = configuredLevels[i];
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

    /// <summary>
    /// 运行时只绑定卡片点击行为，不主动覆写卡片视觉。
    ///
    /// 这样关卡卡片的标题、颜色、图标和排版可以继续直接在 Scene 中手调；
    /// 脚本只负责把“点这张卡后该进哪一关”接起来。
    /// </summary>
    private void BindLevelCardActionsOnly()
    {
        LevelSelectCatalogAsset.LevelEntry[] configuredLevels = GetConfiguredLevels();
        if (configuredLevels.Length == 0 || levelCards == null)
        {
            return;
        }

        int visibleCount = Mathf.Min(configuredLevels.Length, levelCards.Length);
        for (int i = 0; i < levelCards.Length; i++)
        {
            if (levelCards[i] == null)
            {
                continue;
            }

            if (i >= visibleCount || configuredLevels[i] == null)
            {
                levelCards[i].SetClickAction(null);
                continue;
            }

            LevelSelectCatalogAsset.LevelEntry level = configuredLevels[i];
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

    private LevelSelectCatalogAsset ResolveLevelCatalogAsset()
    {
        return levelCatalogAsset;
    }

    private LevelSelectCatalogAsset.LevelEntry[] GetConfiguredLevels()
    {
        LevelSelectCatalogAsset resolvedCatalogAsset = ResolveLevelCatalogAsset();
        if (resolvedCatalogAsset != null && resolvedCatalogAsset.Levels.Length > 0)
        {
            return resolvedCatalogAsset.Levels;
        }

        if (levels == null || levels.Length == 0)
        {
            return Array.Empty<LevelSelectCatalogAsset.LevelEntry>();
        }

        LevelSelectCatalogAsset.LevelEntry[] migratedLevels = new LevelSelectCatalogAsset.LevelEntry[levels.Length];
        for (int index = 0; index < levels.Length; index++)
        {
            LevelDefinition level = levels[index];
            if (level == null)
            {
                continue;
            }

            migratedLevels[index] = BuildFallbackLevelEntry(level);
        }

        return migratedLevels;
    }

    private static LevelSelectCatalogAsset.LevelEntry BuildFallbackLevelEntry(LevelDefinition level)
    {
        string json = JsonUtility.ToJson(level);
        LevelSelectCatalogAsset.LevelEntry migratedLevel = new LevelSelectCatalogAsset.LevelEntry();
        JsonUtility.FromJsonOverwrite(json, migratedLevel);
        return migratedLevel;
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

        return null;
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

    private T FindRootComponentByName<T>(string objectName) where T : Component
    {
        if (!gameObject.scene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
        for (int index = 0; index < rootObjects.Length; index++)
        {
            GameObject rootObject = rootObjects[index];
            if (rootObject == null || rootObject.name != objectName)
            {
                continue;
            }

            return rootObject.GetComponent<T>();
        }

        return null;
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
