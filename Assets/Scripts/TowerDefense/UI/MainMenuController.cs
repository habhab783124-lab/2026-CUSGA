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
/// MainMenuController 负责当前项目的主菜单入口。
///
/// 这次这一版进一步把主菜单收口成“Scene 主导、脚本只接行为”的模式：
/// 1. 场景里的 Canvas、按钮、文本和图片，默认由作者直接在 Scene / Inspector 中维护。
/// 2. 这个脚本主要只负责按钮行为、快捷键和最轻量的引用体检。
/// 3. 如果确实需要从空壳场景重新补一版默认骨架，则通过显式作者工具或显式菜单命令执行，
///    而不是在 `OnEnable / OnValidate` 中自动接管场景。
///
/// 这样处理后，你手动改好的 UI 不会再被脚本反复覆盖；
/// 同时项目仍然保留了一条“需要时可重新物化默认页面”的作者工作流。
/// </summary>
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Scene Flow")]

    /// <summary>
    /// 点击开始后要切换到的玩法场景名。
    ///
    /// 这里默认直接指向当前塔防主玩法场景 `SampleScene`。
    /// 如果你以后改了玩法场景名，要同步更新这里。
    /// </summary>
    [SerializeField] private string gameplaySceneName = "LevelSelect"; // 中文：gameplay场景名称
    [SerializeField] private bool useCampaignFlowOnStart = true; // 中文：使用战役流程On开始
    [SerializeField] private CampaignFlowAsset campaignFlowAsset; // 中文：战役流程资产

    [Header("Visual Theme")]

    /// <summary>
    /// 主页面大背景色。
    ///
    /// 这只是默认值；
    /// 一旦主菜单 UI 已经生成，你完全可以直接在场景里改具体对象颜色。
    /// </summary>
    [SerializeField] private Color backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f); // 中文：背景颜色

    /// <summary>
    /// 暖色强调色，主要服务于开始按钮和重点提示。
    /// </summary>
    [SerializeField] private Color primaryAccent = new Color(1f, 0.62f, 0.29f, 1f); // 中文：主Accent

    /// <summary>
    /// 冷色强调色，主要服务于边框、标签和终端感装饰。
    /// </summary>
    [SerializeField] private Color secondaryAccent = new Color(0.31f, 0.86f, 0.96f, 1f); // 中文：副Accent

    /// <summary>
    /// 外层框架底板色。
    /// </summary>
    [SerializeField] private Color frameCoreColor = new Color(0.04f, 0.06f, 0.09f, 0.92f); // 中文：边框核心颜色

    /// <summary>
    /// 内层信息底板色。
    /// </summary>
    [SerializeField] private Color frameInsetColor = new Color(0.03f, 0.05f, 0.08f, 0.96f); // 中文：边框Inset颜色

    /// <summary>
    /// 主标题颜色。
    /// </summary>
    [SerializeField] private Color titleColor = Color.white; // 中文：标题颜色

    /// <summary>
    /// 副标题颜色。
    /// </summary>
    [SerializeField] private Color subtitleColor = new Color(0.78f, 0.86f, 0.94f, 1f); // 中文：副标题颜色

    /// <summary>
    /// 正文说明颜色。
    /// </summary>
    [SerializeField] private Color descriptionColor = new Color(0.82f, 0.88f, 0.95f, 1f); // 中文：描述颜色

    /// <summary>
    /// 小提示颜色。
    /// </summary>
    [SerializeField] private Color hintColor = new Color(1f, 0.82f, 0.6f, 1f); // 中文：提示颜色

    /// <summary>
    /// 开始按钮主文字颜色。
    /// </summary>
    [SerializeField] private Color startButtonPrimaryTextColor = Color.white; // 中文：开始按钮主文本颜色

    /// <summary>
    /// 开始按钮副文字颜色。
    /// </summary>
    [SerializeField] private Color startButtonSecondaryTextColor = new Color(0.76f, 0.86f, 0.95f, 1f); // 中文：开始按钮副文本颜色

    /// <summary>
    /// 左下角页脚文字颜色。
    /// </summary>
    [SerializeField] private Color footerLeftTextColor = new Color(0.31f, 0.86f, 0.96f, 0.95f); // 中文：页脚剩余文本颜色

    /// <summary>
    /// 右下角页脚文字颜色。
    /// </summary>
    [SerializeField] private Color footerRightTextColor = new Color(1f, 0.62f, 0.29f, 0.95f); // 中文：页脚Right文本颜色

    /// <summary>
    /// 按钮不可用时的颜色。
    /// </summary>
    [SerializeField] private Color buttonDisabledColor = new Color(0.3f, 0.32f, 0.36f, 0.8f); // 中文：按钮Disabled颜色

    /// <summary>
    /// 可选的背景 Sprite。
    /// 如果你后续要用正式主菜单底图，可以直接从这里拖进来。
    /// </summary>
    [SerializeField] private Sprite backgroundSprite; // 中文：背景精灵

    /// <summary>
    /// 可选的外框 Sprite。
    /// </summary>
    [SerializeField] private Sprite frameCoreSprite; // 中文：边框核心精灵

    /// <summary>
    /// 可选的内框 Sprite。
    /// </summary>
    [SerializeField] private Sprite frameInsetSprite; // 中文：边框Inset精灵

    /// <summary>
    /// 可选的开始按钮 Sprite。
    /// </summary>
    [SerializeField] private Sprite startButtonSprite; // 中文：开始按钮精灵

    /// <summary>
    /// 主标题字体。
    /// 如果为空，会回退到项目默认 TMP 字体。
    /// </summary>
    [SerializeField] private TMP_FontAsset titleFontAsset; // 中文：标题字体资产

    /// <summary>
    /// 正文与按钮文字默认字体。
    /// </summary>
    [SerializeField] private TMP_FontAsset bodyFontAsset; // 中文：主体字体资产

    /// <summary>
    /// 强调型标签字体，例如副标题和页脚。
    /// </summary>
    [SerializeField] private TMP_FontAsset accentFontAsset; // 中文：accent字体资产

    [Header("Text Copy")]

    /// <summary>
    /// 主标题文案。
    /// 以后如果你想把首页名字换成自己的游戏名，直接改这里就可以。
    /// </summary>
    [SerializeField] private string titleCopy = "电网防线"; // 中文：标题文案

    /// <summary>
    /// 副标题文案。
    /// </summary>
    [SerializeField] private string subtitleCopy = "电网防线 / 原型入口终端"; // 中文：副标题文案

    /// <summary>
    /// 页面正文说明。
    /// </summary>
    [SerializeField]
    [TextArea(3, 8)]
    private string descriptionCopy = "从这里进入当前塔防原型的测试流程。\n\n利用继电器和战斗塔扩张你的部署网络，在有限废料与供电条件下守住防线。\n\n点击下方开始，进入当前的关卡选择页面。"; // 中文：描述文案

    /// <summary>
    /// 开始按钮上方提示。
    /// </summary>
    [SerializeField] private string hintCopy = "点击开始将进入关卡选择页面"; // 中文：提示文案

    /// <summary>
    /// 开始按钮主文字。
    /// </summary>
    [SerializeField] private string startPrimaryCopy = "开始"; // 中文：开始主文案

    /// <summary>
    /// 开始按钮副文字。
    /// </summary>
    [SerializeField] private string startSecondaryCopy = "进入关卡选择 / 任务终端"; // 中文：开始副文案

    /// <summary>
    /// 左侧页脚文案。
    /// </summary>
    [SerializeField] private string footerLeftCopy = "入口节点 / 主菜单"; // 中文：页脚剩余文案

    /// <summary>
    /// 右侧页脚文案。
    /// </summary>
    [SerializeField] private string footerRightCopy = "按 Enter / Space 或点击开始"; // 中文：页脚Right文案

    [Header("Scene UI Refs")]

    /// <summary>
    /// 主菜单相机。
    ///
    /// 如果场景里还没有相机，脚本会自动补一个；
    /// 补完后引用会记录到这里，方便你之后直接在 Inspector 里调。
    /// </summary>
    [SerializeField] private Camera sceneCamera; // 中文：场景相机

    /// <summary>
    /// 主菜单 Canvas 根。
    /// </summary>
    [SerializeField] private Canvas mainCanvas; // 中文：主画布

    /// <summary>
    /// Canvas 缩放器。
    /// </summary>
    [SerializeField] private CanvasScaler canvasScaler; // 中文：画布Scaler

    /// <summary>
    /// UI 射线器。
    /// </summary>
    [SerializeField] private GraphicRaycaster graphicRaycaster; // 中文：graphicRaycaster

    /// <summary>
    /// 主菜单 EventSystem。
    /// </summary>
    [SerializeField] private EventSystem eventSystem; // 中文：事件System

    /// <summary>
    /// 标准输入模块。
    /// </summary>
    [SerializeField] private StandaloneInputModule standaloneInputModule; // 中文：standalone输入模块

    /// <summary>
    /// 主菜单 UI 的总根节点。
    ///
    /// 所有真正可见的 UI 都挂在这里下面，
    /// 所以后面你如果要整体缩放、整体移动或重新分组，
    /// 这个根节点会是最好用的入口。
    /// </summary>
    [SerializeField] private RectTransform menuRoot; // 中文：菜单根节点

    /// <summary>
    /// 全屏背景面板。
    /// </summary>
    [SerializeField] private Image backgroundPanel; // 中文：背景面板

    /// <summary>
    /// 外层框架面板。
    /// </summary>
    [SerializeField] private Image frameCorePanel; // 中文：边框核心面板

    /// <summary>
    /// 内层信息底板。
    /// </summary>
    [SerializeField] private Image frameInsetPanel; // 中文：边框Inset面板

    /// <summary>
    /// 主标题文字。
    /// </summary>
    [SerializeField] private TextMeshProUGUI titleText; // 中文：标题文本

    /// <summary>
    /// 副标题文字。
    /// </summary>
    [SerializeField] private TextMeshProUGUI subtitleText; // 中文：副标题文本

    /// <summary>
    /// 页面说明正文。
    /// </summary>
    [SerializeField] private TextMeshProUGUI descriptionText; // 中文：描述文本

    /// <summary>
    /// 开始按钮上方的小提示。
    /// </summary>
    [SerializeField] private TextMeshProUGUI hintText; // 中文：提示文本

    /// <summary>
    /// 开始按钮本体。
    ///
    /// 这个引用是最关键的 Inspector 引用之一，
    /// 因为真正触发切场景的就是它。
    /// </summary>
    [SerializeField] private Button startButton; // 中文：开始按钮

    /// <summary>
    /// 开始按钮底图。
    /// </summary>
    [SerializeField] private Image startButtonImage; // 中文：开始按钮Image

    /// <summary>
    /// 开始按钮主文字。
    /// </summary>
    [SerializeField] private TextMeshProUGUI startButtonPrimaryText; // 中文：开始按钮主文本

    /// <summary>
    /// 开始按钮副文字。
    /// </summary>
    [SerializeField] private TextMeshProUGUI startButtonSecondaryText; // 中文：开始按钮副文本

    /// <summary>
    /// 底部左侧标签。
    /// </summary>
    [SerializeField] private TextMeshProUGUI footerLeftText; // 中文：页脚剩余文本

    /// <summary>
    /// 底部右侧标签。
    /// </summary>
    [SerializeField] private TextMeshProUGUI footerRightText; // 中文：页脚Right文本

    /// <summary>
    /// 记录默认主菜单骨架是否已经搭建过。
    ///
    /// 这个标记非常关键：
    /// - `false`：说明场景还是空的，脚本应该补齐一版默认 UI
    /// - `true`：说明主菜单已经成型，脚本以后就尽量只补缺引用，不再强推默认布局
    ///
    /// 这样我们就兼顾了“自动搭出来”和“后续可手改”两件事。
    /// </summary>
    [SerializeField] private bool hasBuiltSceneUi; // 中文：是否有Built场景界面

    private const string CanvasName = "MainMenuCanvas"; // 中文：画布名称
    private const string EventSystemName = "MainMenuEventSystem"; // 中文：事件System名称
    private const string RootName = "MainMenuRoot"; // 中文：根节点名称
    private const string BackgroundName = "BackgroundPanel"; // 中文：背景名称
    private const string FrameCoreName = "FrameCore"; // 中文：边框核心名称
    private const string FrameInsetName = "FrameInset"; // 中文：边框Inset名称
    private const string TitleName = "TitleText"; // 中文：标题名称
    private const string SubtitleName = "SubtitleText"; // 中文：副标题名称
    private const string DescriptionName = "DescriptionText"; // 中文：描述名称
    private const string HintName = "HintText"; // 中文：提示名称
    private const string StartButtonName = "StartGameButton"; // 中文：开始按钮名称
    private const string StartPrimaryName = "StartButtonPrimaryText"; // 中文：开始主名称
    private const string StartSecondaryName = "StartButtonSecondaryText"; // 中文：开始副名称
    private const string FooterLeftName = "FooterLeftText"; // 中文：页脚剩余名称
    private const string FooterRightName = "FooterRightText"; // 中文：页脚Right名称

    /// <summary>
    /// 运行时启用时，只负责绑定行为层监听。
    ///
    /// 主菜单的可视对象已经应该真实存在于场景里；
    /// 这里不再在启用瞬间自动补骨架或重刷整套视觉主题，
    /// 以免覆盖作者刚刚在 Scene 里做完的手动调整。
    /// </summary>
    private void OnEnable()
    {
        BindStartButton();
    }

    /// <summary>
    /// 关闭或销毁时解绑按钮，避免重复注册监听。
    /// </summary>
    private void OnDisable()
    {
        UnbindStartButton();
    }

    /// <summary>
    /// 编辑器里只做最轻量的引用回填，不再自动改场景布局和视觉。
    ///
    /// 这样你在 Scene 里手调颜色、文字和层级时，
    /// 不会因为检查器刷新而又被脚本改回“作者默认值”。
    /// </summary>
    private void OnValidate()
    {
        if (startButton != null)
        {
            startButtonImage = startButton.GetComponent<Image>();
        }
    }

    /// <summary>
    /// 额外支持 Enter / Space 作为开始键。
    ///
    /// 这里只在 Play 模式真正响应，
    /// 避免你在编辑器里调东西时误触发场景切换。
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            StartGame();
        }
    }

    /// <summary>
    /// 切换到当前玩法场景。
    ///
    /// 如果你在编辑模式下点了按钮，
    /// 这里不会真的切场景，防止改场景时误操作。
    /// </summary>
    public void StartGame()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (useCampaignFlowOnStart && campaignFlowAsset != null && CampaignFlowController.BeginCampaign(campaignFlowAsset))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogWarning("MainMenuController 没有配置要进入的玩法场景名。", this);
            return;
        }

        SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// Play 模式启动后，对当前场景做一次轻量体检。
    ///
    /// 这里的目标不是再偷偷补对象，
    /// 而是尽早把“哪个引用没接好”明确暴露出来。
    /// </summary>
    private void Start()
    {
        ValidateBoundReferences();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 显式把默认主菜单骨架物化到当前场景。
    ///
    /// 这个入口只在编辑器里手动调用，
    /// 用来替代过去那种“只要脚本启用就自动改场景”的工作流。
    /// </summary>
    public void EditorMaterializeDefaultSceneUi()
    {
        EnsureSceneObjects();
        ApplyThemeAndCopyToBoundSceneObjects();
        BindStartButton();
        MarkSceneDirty();
    }

    /// <summary>
    /// 把当前控制器里的作者默认主题同步到已经存在的场景 UI。
    ///
    /// 这同样是显式作者命令，不是常驻自动同步。
    /// </summary>
    public void EditorApplyAuthoringToScene()
    {
        ValidateBoundReferences();
        ApplyThemeAndCopyToBoundSceneObjects();
        BindStartButton();
        MarkSceneDirty();
    }
#endif

    /// <summary>
    /// 确保主菜单场景拥有一套可编辑、可运行的基础对象。
    ///
    /// 处理顺序是：
    /// 1. 补相机
    /// 2. 补 EventSystem
    /// 3. 补 Canvas
    /// 4. 如果主菜单骨架还没搭过，就生成默认 UI
    /// 5. 如果已经搭过，只做引用回填，不打断你自己的手动调整
    /// </summary>
    private void EnsureSceneObjects()
    {
        EnsureCameraExists();
        EnsureEventSystemExists();
        EnsureCanvasExists();

        if (!hasBuiltSceneUi)
        {
            BuildDefaultMenuLayout();
            hasBuiltSceneUi = true;
            MarkSceneDirty();
            return;
        }

        ValidateBoundReferences();
        ApplyThemeAndCopyToBoundSceneObjects();
    }

    /// <summary>
    /// 给开始按钮注册点击事件。
    ///
    /// 这里用脚本注册，而不是在场景 YAML 里硬写 onClick，
    /// 是为了让按钮逻辑更集中，也更方便你以后改目标场景或扩展逻辑。
    /// </summary>
    private void BindStartButton()
    {
        if (startButton == null)
        {
            return;
        }

        startButton.onClick.RemoveListener(StartGame);
        startButton.onClick.AddListener(StartGame);
    }

    /// <summary>
    /// 解绑按钮监听，避免重复添加。
    /// </summary>
    private void UnbindStartButton()
    {
        if (startButton == null)
        {
            return;
        }

        startButton.onClick.RemoveListener(StartGame);
    }

    /// <summary>
    /// 如果场景里还没有可用相机，就自动补一个。
    /// </summary>
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

    /// <summary>
    /// 确保 EventSystem 存在。
    /// </summary>
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

    /// <summary>
    /// 确保 Canvas 基础设施存在。
    /// </summary>
    private void EnsureCanvasExists()
    {
        if (mainCanvas == null)
        {
            mainCanvas = FindRootComponentByName<Canvas>(CanvasName);
        }

        if (mainCanvas == null)
        {
            GameObject canvasObject = new GameObject(CanvasName);
            mainCanvas = canvasObject.AddComponent<Canvas>();
        }

        RectTransform canvasRect = mainCanvas.transform as RectTransform;
        if (canvasRect != null)
        {
            canvasRect.localScale = Vector3.one;
        }

        // 主菜单是纯 UI 入口页，不依赖世界空间对象，
        // 所以这里直接使用 Screen Space Overlay。
        //
        // 这样比 Screen Space Camera 更稳：
        // - 不依赖相机参数是否被场景手动改坏
        // - 不会因为 Plane Distance / Camera 绑定异常导致整个界面空白
        // - 对当前“打开场景就应该直接看见并编辑 UI”的工作流更友好
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
    /// 搭一版默认主菜单骨架。
    ///
    /// 重点是：
    /// - 把 UI 对象真正创建到场景层级里
    /// - 创建完后记录引用
    /// - 以后你就可以直接在 Scene / Inspector 里继续调
    /// </summary>
    private void BuildDefaultMenuLayout()
    {
        RectTransform canvasRect = mainCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        menuRoot = EnsureRectTransform(canvasRect, RootName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 760f));
        backgroundPanel = EnsureImage(canvasRect, BackgroundName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), backgroundColor);
        backgroundPanel.raycastTarget = false;
        backgroundPanel.rectTransform.SetAsFirstSibling();
        menuRoot.SetAsLastSibling();

        frameCorePanel = EnsureImage(menuRoot, FrameCoreName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 760f), frameCoreColor);
        frameInsetPanel = EnsureImage(menuRoot, FrameInsetName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1256f, 696f), frameInsetColor);

        titleText = EnsureText(menuRoot, TitleName, new Vector2(-304f, 168f), new Vector2(720f, 180f), 84f, FontStyles.Bold, titleColor, titleCopy, TextAlignmentOptions.Left, titleFontAsset);
        titleText.lineSpacing = -18f;

        subtitleText = EnsureText(menuRoot, SubtitleName, new Vector2(-300f, 76f), new Vector2(760f, 92f), 28f, FontStyles.Bold, subtitleColor, subtitleCopy, TextAlignmentOptions.Left, accentFontAsset);
        subtitleText.characterSpacing = 2f;

        descriptionText = EnsureText(menuRoot, DescriptionName, new Vector2(-288f, -40f), new Vector2(760f, 180f), 26f, FontStyles.Normal, descriptionColor, descriptionCopy, TextAlignmentOptions.Left, bodyFontAsset);
        descriptionText.lineSpacing = 6f;

        hintText = EnsureText(menuRoot, HintName, new Vector2(-284f, -214f), new Vector2(760f, 36f), 20f, FontStyles.Bold, hintColor, hintCopy, TextAlignmentOptions.Left, accentFontAsset);

        startButton = EnsureButton(menuRoot, StartButtonName, new Vector2(344f, -28f), new Vector2(360f, 116f), primaryAccent);
        startButtonImage = startButton.GetComponent<Image>();
        startButtonPrimaryText = EnsureText(startButton.transform as RectTransform, StartPrimaryName, new Vector2(24f, 12f), new Vector2(260f, 40f), 36f, FontStyles.Bold, startButtonPrimaryTextColor, startPrimaryCopy, TextAlignmentOptions.Left, titleFontAsset);
        startButtonSecondaryText = EnsureText(startButton.transform as RectTransform, StartSecondaryName, new Vector2(24f, -22f), new Vector2(260f, 28f), 18f, FontStyles.Bold, startButtonSecondaryTextColor, startSecondaryCopy, TextAlignmentOptions.Left, accentFontAsset);

        footerLeftText = EnsureText(menuRoot, FooterLeftName, new Vector2(-334f, -322f), new Vector2(420f, 34f), 18f, FontStyles.Bold, footerLeftTextColor, footerLeftCopy, TextAlignmentOptions.Left, accentFontAsset);
        footerRightText = EnsureText(menuRoot, FooterRightName, new Vector2(310f, -322f), new Vector2(560f, 34f), 18f, FontStyles.Bold, footerRightTextColor, footerRightCopy, TextAlignmentOptions.Left, accentFontAsset);
    }

    /// <summary>
    /// 当主菜单骨架已经搭好后，我们改成“显式引用优先”的维护方式。
    ///
    /// 这里不再像旧版本那样，悄悄按对象名把一整套 UI 子节点再找回来；
    /// 原因是主菜单场景现在已经把这些引用序列化保存好了，
    /// 再去按名字回填，反而会让对象名重新承担装配职责，后续改名也更不安心。
    ///
    /// 因此这一步只做两件事：
    /// 1. 补齐那些可以从已绑定组件直接推导出的轻量引用，例如按钮底图。
    /// 2. 对真正缺失的关键引用输出明确告警，提醒维护者去 Inspector 里补。
    /// </summary>
    private void ValidateBoundReferences()
    {
        if (startButton != null)
        {
            startButtonImage = startButton.GetComponent<Image>();
        }

        WarnIfMissing(sceneCamera, nameof(sceneCamera));
        WarnIfMissing(mainCanvas, nameof(mainCanvas));
        WarnIfMissing(canvasScaler, nameof(canvasScaler));
        WarnIfMissing(graphicRaycaster, nameof(graphicRaycaster));
        WarnIfMissing(eventSystem, nameof(eventSystem));
        WarnIfMissing(standaloneInputModule, nameof(standaloneInputModule));
        WarnIfMissing(menuRoot, nameof(menuRoot));
        WarnIfMissing(backgroundPanel, nameof(backgroundPanel));
        WarnIfMissing(frameCorePanel, nameof(frameCorePanel));
        WarnIfMissing(frameInsetPanel, nameof(frameInsetPanel));
        WarnIfMissing(titleText, nameof(titleText));
        WarnIfMissing(subtitleText, nameof(subtitleText));
        WarnIfMissing(descriptionText, nameof(descriptionText));
        WarnIfMissing(hintText, nameof(hintText));
        WarnIfMissing(startButton, nameof(startButton));
        WarnIfMissing(startButtonImage, nameof(startButtonImage));
        WarnIfMissing(startButtonPrimaryText, nameof(startButtonPrimaryText));
        WarnIfMissing(startButtonSecondaryText, nameof(startButtonSecondaryText));
        WarnIfMissing(footerLeftText, nameof(footerLeftText));
        WarnIfMissing(footerRightText, nameof(footerRightText));
    }

    /// <summary>
    /// 把当前 Inspector 里的主题和文案正式应用到已经存在的场景对象。
    ///
    /// 这里的边界很刻意：
    /// - 会同步颜色、Sprite、字体和文案
    /// - 不会重排你已经在 Scene 里调好的位置和尺寸
    ///
    /// 这样主菜单就更接近我们希望的目标：
    /// “样式入口在 Inspector，布局入口在 Scene”。
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

        if (menuRoot != null)
        {
            menuRoot.gameObject.SetActive(true);
            menuRoot.SetAsLastSibling();
        }

        SetActiveIfPresent(backgroundPanel);
        SetActiveIfPresent(frameCorePanel);
        SetActiveIfPresent(frameInsetPanel);
        SetActiveIfPresent(titleText);
        SetActiveIfPresent(subtitleText);
        SetActiveIfPresent(descriptionText);
        SetActiveIfPresent(hintText);
        SetActiveIfPresent(startButton);
        SetActiveIfPresent(startButtonPrimaryText);
        SetActiveIfPresent(startButtonSecondaryText);
        SetActiveIfPresent(footerLeftText);
        SetActiveIfPresent(footerRightText);

        ApplyImageTheme(backgroundPanel, backgroundColor, backgroundSprite, preserveAspect: false);
        ApplyImageTheme(frameCorePanel, frameCoreColor, frameCoreSprite, preserveAspect: false);
        ApplyImageTheme(frameInsetPanel, frameInsetColor, frameInsetSprite, preserveAspect: false);
        ApplyImageTheme(startButtonImage, primaryAccent, startButtonSprite, preserveAspect: false);

        if (backgroundPanel != null)
        {
            backgroundPanel.rectTransform.SetAsFirstSibling();
        }

        if (menuRoot != null)
        {
            menuRoot.SetAsLastSibling();
        }

        ApplyTextTheme(titleText, titleCopy, titleColor, titleFontAsset);
        ApplyTextTheme(subtitleText, subtitleCopy, subtitleColor, accentFontAsset);
        ApplyTextTheme(descriptionText, descriptionCopy, descriptionColor, bodyFontAsset);
        ApplyTextTheme(hintText, hintCopy, hintColor, accentFontAsset);
        ApplyTextTheme(startButtonPrimaryText, startPrimaryCopy, startButtonPrimaryTextColor, titleFontAsset);
        ApplyTextTheme(startButtonSecondaryText, startSecondaryCopy, startButtonSecondaryTextColor, accentFontAsset);
        ApplyTextTheme(footerLeftText, footerLeftCopy, footerLeftTextColor, accentFontAsset);
        ApplyTextTheme(footerRightText, footerRightCopy, footerRightTextColor, accentFontAsset);

        if (startButton != null)
        {
            startButton.targetGraphic = startButtonImage;

            ColorBlock colors = startButton.colors;
            colors.normalColor = primaryAccent;
            colors.highlightedColor = Color.Lerp(primaryAccent, Color.white, 0.22f);
            colors.pressedColor = Color.Lerp(primaryAccent, Color.black, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = buttonDisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            startButton.colors = colors;
        }
    }

    /// <summary>
    /// 主菜单现在走“显式场景装配”后，缺引用应该尽早暴露出来，
    /// 而不是继续靠隐式查找把问题藏住。
    /// </summary>
    private void WarnIfMissing(Object reference, string fieldName)
    {
        if (reference != null)
        {
            return;
        }

        Debug.LogWarning($"MainMenuController 缺少场景引用：{fieldName}。请在 MainMenu 场景的 Inspector 中补齐。", this);
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

    /// <summary>
    /// 创建或复用一个 RectTransform。
    ///
    /// 如果对象已存在，就直接复用；
    /// 只有首次创建时才写默认位置和尺寸，避免覆盖你后续手调。
    /// </summary>
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

    /// <summary>
    /// 创建或复用一个 Image。
    /// </summary>
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

    /// <summary>
    /// 创建或复用一个 TMP 文本。
    /// </summary>
    private TextMeshProUGUI EnsureText(RectTransform parent, string objectName, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, FontStyles fontStyle, Color color, string text, TextAlignmentOptions alignment, TMP_FontAsset preferredFontAsset)
    {
        RectTransform rectTransform = EnsureRectTransform(parent, objectName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);
        TextMeshProUGUI label = rectTransform.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            label = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        }

        TMP_FontAsset resolvedFontAsset = ResolveFontAsset(preferredFontAsset);
        if (resolvedFontAsset != null)
        {
            label.font = resolvedFontAsset;
        }
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
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

    /// <summary>
    /// 创建或复用一个按钮。
    /// </summary>
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

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.22f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.3f, 0.32f, 0.36f, 0.8f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        return button;
    }

    /// <summary>
    /// 查找某个同名子 RectTransform。
    /// </summary>
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

    /// <summary>
    /// 查找某个同名子 Image。
    /// </summary>
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
