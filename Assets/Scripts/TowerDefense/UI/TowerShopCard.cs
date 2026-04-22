using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// `TowerShopCard` 负责把右侧商店中的一张部署卡，变成真正可点击、可拖拽的建造入口。
///
/// 它本身不负责决定“这个位置能不能放塔”，也不负责真正实例化塔。
/// 这张卡更像是 UI 输入层，主要承担三类职责：
/// 1. 处理点击选中某种塔型。
/// 2. 处理从卡片开始的拖拽放置手势。
/// 3. 在悬停和拖拽时提供轻量视觉反馈。
///
/// 这样拆分的好处是：
/// - 卡片只关心“玩家在 UI 上做了什么输入”。
/// - 规则判断继续交给放置规则链。
/// - 真正的放塔流程继续交给总控和交互控制器协调。
/// </summary>
public class TowerShopCard : MonoBehaviour,
    IPointerClickHandler,
    IPointerDownHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IInitializePotentialDragHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Card Identity")]

    /// <summary>
    /// 这张部署卡对应的塔类型。
    ///
    /// 现在这里必须通过 Inspector 显式配置，
    /// 不再根据对象名去猜是发电机还是炮塔。
    /// 这样后续就算你改卡片对象名，也不会把功能改坏。
    /// </summary>
    [SerializeField] private TowerType towerType = TowerType.None;

    /// <summary>
    /// 鼠标悬停在卡片上时显示的提示语。
    ///
    /// 这类文案属于“交互引导”，主要帮助玩家理解：
    /// - 当前卡片代表什么建筑。
    /// - 拖拽后会看到什么反馈。
    /// </summary>
    [SerializeField] private string hoverHint = "Drag the card to preview exact legal areas. Your first structure starts in the starter zone.";

    [Header("Card Visual Refs")]

    /// <summary>
    /// 卡片底板。
    ///
    /// 这里优先支持显式 Inspector 引用；
    /// 如果暂时没拖，脚本会先回退到按钮本体上的 `Image`。
    /// </summary>
    [SerializeField] private Image backgroundImageReference;

    /// <summary>
    /// 卡片主图标。
    ///
    /// 这让“卡片用什么图标”从场景零散子节点，变成一个更明确的替换入口。
    /// </summary>
    [SerializeField] private Image iconImageReference;

    /// <summary>
    /// 卡片主文本引用。
    /// 这样卡片自己的文案入口就能继续留在卡片组件上，
    /// 方便直接在 Inspector 中确认和替换，而不是让别的系统跨层级去猜。
    /// </summary>
    [SerializeField] private TMP_Text labelTextReference;

    /// <summary>
    /// 卡片上的强调图形。
    ///
    /// 常见情况包括：
    /// - 左右细条
    /// - 小徽记
    /// - 分隔装饰
    ///
    /// 这些元素通常不需要逐个写逻辑，
    /// 但它们的颜色最好能跟着塔定义统一切换。
    /// </summary>
    [SerializeField] private Graphic[] accentGraphicReferences = new Graphic[0];

    [Header("Drag Feedback")]

    /// <summary>
    /// 拖拽进行时，卡片本体降低到多少透明度。
    ///
    /// 这样做是为了让玩家看见“手里抓着卡”，同时又不会完全挡住地图。
    /// </summary>
    [SerializeField] private float draggingAlpha = 0.82f;

    /// <summary>
    /// 拖拽进行时，卡片本体缩放倍率。
    ///
    /// 略微缩小一点可以让“卡片已经被抓起”这件事更明显。
    /// </summary>
    [SerializeField] private float draggingScaleMultiplier = 0.98f;

    [Header("Idle Motion")]

    /// <summary>
    /// 鼠标悬停在卡片上时的放大倍率。
    ///
    /// 这类轻微缩放主要用于告诉玩家：
    /// “这张卡当前正处于可交互状态”。
    /// </summary>
    [SerializeField] private float hoverScaleMultiplier = 1.035f;

    /// <summary>
    /// 悬停脉冲的速度。
    ///
    /// 数值越大，卡片在悬停时的呼吸感越明显。
    /// </summary>
    [SerializeField] private float hoverPulseSpeed = 4.2f;

    /// <summary>
    /// 悬停脉冲的幅度。
    ///
    /// 这里保持很小，是为了让卡片更灵动，而不是变成夸张跳动的 UI。
    /// </summary>
    [SerializeField] private float hoverPulseAmplitude = 0.02f;

    /// <summary>
    /// 控制卡片透明度和射线拦截状态的 `CanvasGroup` 缓存。
    /// </summary>
    private CanvasGroup _canvasGroup;

    /// <summary>
    /// 卡片初始缩放，用于在悬停和拖拽结束后恢复原状。
    /// </summary>
    private Vector3 _originalScale;

    /// <summary>
    /// 当前这张卡是否已经进入“正式拖拽放置”状态。
    /// </summary>
    private bool _isDragging;

    /// <summary>
    /// 记录“Unity 已经进入拖拽手势，但我们还没真正启动放置拖拽链”的过渡状态。
    ///
    /// 这次修复的核心就是把“Unity 开始认定是拖拽”和“我们正式创建放置预览塔”
    /// 拆成两个阶段：
    /// 1. `OnBeginDrag` 只记录候选状态。
    /// 2. 第一次 `OnDrag` 到来时，再用最新鼠标位置正式启动放置拖拽。
    ///
    /// 这样可以避免第一次只是轻微点按或抖动时，就在地图中央提前实例化一个不跟手的预览塔。
    /// </summary>
    private bool _isAwaitingPlacementDragStart;

    /// <summary>
    /// 当前鼠标是否悬停在卡片上。
    ///
    /// `Update()` 会根据这个状态驱动轻量缩放和呼吸动画。
    /// </summary>
    private bool _isPointerOver;

    public TMP_Text LabelText => labelTextReference;

    /// <summary>
    /// 缓存并补齐运行时需要的 UI 组件引用。
    /// </summary>
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _originalScale = transform.localScale;
        CacheVisualReferences();
    }

    /// <summary>
    /// 编辑器里改层级或补引用后，也尽量把视觉引用自动整理一下。
    /// </summary>
    private void OnValidate()
    {
        CacheVisualReferences();
    }

    /// <summary>
    /// 运行轻量卡片悬停动画。
    ///
    /// 这里不用 `Animator`，是为了让原型期的参数更直观可控，
    /// 也避免为了一个非常小的呼吸效果再多维护一套动画状态机。
    /// </summary>
    private void Update()
    {
        if (_isDragging)
        {
            return;
        }

        if (!_isPointerOver)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _originalScale, Time.unscaledDeltaTime * 14f);
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * hoverPulseSpeed) * hoverPulseAmplitude;
        Vector3 targetScale = _originalScale * hoverScaleMultiplier * pulse;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 12f);
    }

    /// <summary>
    /// 让 Unity 保留正常拖拽阈值。
    ///
    /// 这样点击选中和真正拖拽放置才会分成两种清晰手势。
    /// </summary>
    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        // 这里明确保留 Unity 自带的拖拽阈值。
        //
        // 之前把阈值强行关掉后，鼠标一次很轻的点击抖动就可能被判成“开始拖拽”，
        // 于是玩家第一次只是想点一下部署卡，也会提前生成预览塔，
        // 看起来就像“卡片一点击，地图中间先冒出一个不能动的放置示意画面”。
        //
        // 保留阈值后的交互边界会更符合直觉：
        // - 轻点：走 OnPointerClick，做“选中该塔型”
        // - 真正拖动：跨过阈值后才进入 OnBeginDrag，开始跟手拖拽
        //
        // 这样可以把“点击选择”和“拖拽放置”重新分开，避免第一次点击就误触发拖拽链。
        eventData.useDragThreshold = true;
    }

    /// <summary>
    /// 点击卡片时，切换当前选中的塔类型。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging || TowerDefenseGame.Instance == null || !HasConfiguredTowerType())
        {
            return;
        }

        switch (towerType)
        {
            case TowerType.Relay:
                TowerDefenseGame.Instance.SelectRelayTower();
                break;
            case TowerType.SingleTarget:
                TowerDefenseGame.Instance.SelectDefenseTower();
                break;
            case TowerType.SlowField:
                TowerDefenseGame.Instance.SelectSlowFieldTower();
                break;
            case TowerType.Bombard:
                TowerDefenseGame.Instance.SelectBombardTower();
                break;
        }
    }

    /// <summary>
    /// 在鼠标按下部署卡的那一刻，就尽量把拖拽需要的资源提前热起来。
    ///
    /// 这里不直接启动放置拖拽链，只做轻量前置准备：
    /// - 预热当前塔型的合法区覆盖层缓存。
    /// - 预热当前塔型的预览塔实例。
    ///
    /// 这样真正跨过拖拽阈值进入 `OnDrag` 时，主线程更可能只做状态切换和位置更新，
    /// 而不是同时第一次建对象、第一次跑覆盖层采样。
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || TowerDefenseGame.Instance == null || !HasConfiguredTowerType())
        {
            return;
        }

        TowerDefenseGame.Instance.PrewarmPlacementAreaOverlay(towerType);
    }

    /// <summary>
    /// 鼠标进入卡片时，记录悬停状态并预热拖拽所需资源。
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;

        if (_isDragging || TowerDefenseGame.Instance == null || !HasConfiguredTowerType())
        {
            return;
        }

        // 悬停通常会早于真正开始拖拽，
        // 所以这里顺手把拖拽首帧最重的资源提前热起来。
        TowerDefenseGame.Instance.PrewarmPlacementAreaOverlay(towerType);
        TowerDefenseGame.Instance.SetStatusMessage(hoverHint);
    }

    /// <summary>
    /// 鼠标离开卡片时，清掉悬停状态。
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
    }

    /// <summary>
    /// 当 Unity 认定这次输入已经跨过拖拽阈值时，记录拖拽候选状态。
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || TowerDefenseGame.Instance == null || !HasConfiguredTowerType())
        {
            return;
        }

        // 不在这里立刻启动真正的放置拖拽。
        //
        // `OnBeginDrag` 只说明 Unity 认定这次输入已经跨过拖拽阈值，
        // 但这时鼠标位置、UI 射线状态和玩家真实意图都还处在一个“刚开始切换”的边缘帧。
        // 之前在这里马上调用 `BeginPlacementDrag()`，就会出现：
        // - 第一次点卡片时预览塔先被创建
        // - 如果后续没有稳定进入持续拖拽，预览塔就停在默认位置不动
        //
        // 所以这里先只记一个“等待真正开始放置拖拽”的标志，
        // 把正式启动延后到第一次 `OnDrag`。
        _isAwaitingPlacementDragStart = true;
    }

    /// <summary>
    /// 拖拽过程中，持续把当前鼠标位置同步给总控。
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (TowerDefenseGame.Instance == null)
        {
            return;
        }

        if (!_isDragging)
        {
            if (!_isAwaitingPlacementDragStart || eventData.button != PointerEventData.InputButton.Left || !HasConfiguredTowerType())
            {
                return;
            }

            // 直到真正收到第一帧 Drag，我们才正式创建预览塔并切换卡片视觉。
            //
            // 这样第一次真实拖卡时仍然能立即跟手，
            // 但第一次只是点击或轻微误触时，不会再在地图中央留下一个“冻结”的预览塔。
            if (!TowerDefenseGame.Instance.BeginPlacementDrag(towerType, eventData.position))
            {
                _isAwaitingPlacementDragStart = false;
                return;
            }

            _isDragging = true;
            _isAwaitingPlacementDragStart = false;
            _canvasGroup.alpha = draggingAlpha;
            _canvasGroup.blocksRaycasts = false;
            transform.localScale = _originalScale * draggingScaleMultiplier;
        }

        TowerDefenseGame.Instance.UpdatePlacementDrag(eventData.position);
    }

    /// <summary>
    /// 当玩家松开鼠标时，结束本次部署拖拽。
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        _isAwaitingPlacementDragStart = false;

        if (!_isDragging)
        {
            RestoreVisualState();
            return;
        }

        _isDragging = false;
        RestoreVisualState();

        if (TowerDefenseGame.Instance == null)
        {
            return;
        }

        bool releasedOverUserInterface = IsReleasedOverUserInterface(eventData);
        TowerDefenseGame.Instance.EndPlacementDrag(eventData.position, releasedOverUserInterface);
    }

    /// <summary>
    /// 判断本次拖拽释放时，鼠标下方是否仍然压着真正应该阻止地图放塔的 UI。
    ///
    /// 这里不信任旧的事件缓存，而是根据当前鼠标位置重新做一次 UI 射线，
    /// 避免“从卡片拖到地图再松手”时还被历史 UI 命中误判为取消。
    /// </summary>
    private static bool IsReleasedOverUserInterface(PointerEventData eventData)
    {
        if (eventData == null || EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = eventData.position
        };

        System.Collections.Generic.List<RaycastResult> raycastResults = new System.Collections.Generic.List<RaycastResult>(8);
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);

        GameObject ignoredDragObject = eventData.pointerDrag != null ? eventData.pointerDrag.gameObject : null;
        for (int i = 0; i < raycastResults.Count; i++)
        {
            GameObject currentRaycastObject = raycastResults[i].gameObject;
            if (IsGameplayBlockingUserInterface(currentRaycastObject, ignoredDragObject))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断当前命中的 UI 对象，是否属于“应该阻止地图放塔”的交互控件。
    ///
    /// 这里目前只认两类：
    /// 1. 部署卡自己。
    /// 2. `Selectable` 体系下真正可交互的按钮类控件。
    ///
    /// 地图上的装饰标签、提示面板和纯文本不会被当成阻挡。
    /// </summary>
    private static bool IsGameplayBlockingUserInterface(GameObject currentRaycastObject, GameObject ignoredDragObject)
    {
        if (currentRaycastObject == null)
        {
            return false;
        }

        if (ignoredDragObject != null && currentRaycastObject.transform.IsChildOf(ignoredDragObject.transform))
        {
            return false;
        }

        if (currentRaycastObject.GetComponentInParent<TowerShopCard>() != null)
        {
            return true;
        }

        return currentRaycastObject.GetComponentInParent<Selectable>() != null;
    }

    /// <summary>
    /// 当对象被禁用时，安全收尾当前卡片的交互状态。
    /// </summary>
    private void OnDisable()
    {
        _isDragging = false;
        _isAwaitingPlacementDragStart = false;
        _isPointerOver = false;
        RestoreVisualState();
    }

    /// <summary>
    /// 检查这张部署卡是否已经正确配置了塔类型。
    ///
    /// 现在项目已经不再允许靠对象名推断卡片身份，
    /// 所以这里如果没在 Inspector 里显式指定 `towerType`，就会给出明确告警。
    /// </summary>
    private bool HasConfiguredTowerType()
    {
        if (towerType != TowerType.None)
        {
            return true;
        }

        Debug.LogWarning("TowerShopCard is missing an explicit towerType assignment. Configure the card in the Inspector.", this);
        return false;
    }

    /// <summary>
    /// 根据塔定义应用卡片样式。
    ///
    /// 这一层的目标不是“强行重排 UI 布局”，
    /// 而是把图标、底色和强调色从硬编码散点，
    /// 收口成一个统一的入口。
    /// </summary>
    public void ApplyDefinitionVisuals(TowerDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        CacheVisualReferences();

        if (backgroundImageReference != null)
        {
            backgroundImageReference.color = definition.CardBackgroundTint;
        }

        if (iconImageReference != null)
        {
            if (definition.CardIconSprite != null)
            {
                iconImageReference.sprite = definition.CardIconSprite;
            }

            iconImageReference.color = definition.CardIconTint;
            iconImageReference.preserveAspect = true;
        }

        if (accentGraphicReferences != null)
        {
            for (int i = 0; i < accentGraphicReferences.Length; i++)
            {
                if (accentGraphicReferences[i] != null)
                {
                    accentGraphicReferences[i].color = definition.CardAccentTint;
                }
            }
        }

        Button button = GetComponent<Button>();
        if (button != null)
        {
            ColorBlock colors = button.colors;
            Color baseColor = definition.CardBackgroundTint;
            Color accentColor = definition.CardAccentTint;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, accentColor, 0.18f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(baseColor.r * 0.45f, baseColor.g * 0.45f, baseColor.b * 0.45f, 0.82f);
            button.colors = colors;

            if (backgroundImageReference != null)
            {
                button.targetGraphic = backgroundImageReference;
            }
        }
    }

    /// <summary>
    /// 恢复拖拽前的卡片视觉状态。
    /// </summary>
    private void RestoreVisualState()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
        }

        transform.localScale = _originalScale;
    }

    /// <summary>
    /// 把卡片视觉引用尽量收口成稳定入口。
    ///
    /// 显式 Inspector 引用仍然是首选；
    /// 这里只在缺项时做一层轻量回填，避免老场景立刻全部失效。
    /// </summary>
    private void CacheVisualReferences()
    {
        if (backgroundImageReference == null)
        {
            backgroundImageReference = GetComponent<Image>();
        }

        if (labelTextReference == null)
        {
            labelTextReference = GetComponentInChildren<TMP_Text>(true);
        }

        Image[] childImages = GetComponentsInChildren<Image>(true);
        if ((iconImageReference == null || accentGraphicReferences == null || accentGraphicReferences.Length == 0) && childImages != null)
        {
            System.Collections.Generic.List<Graphic> accentGraphics = new System.Collections.Generic.List<Graphic>(4);

            for (int i = 0; i < childImages.Length; i++)
            {
                Image candidateImage = childImages[i];
                if (candidateImage == null || candidateImage == backgroundImageReference)
                {
                    continue;
                }

                RectTransform rectTransform = candidateImage.rectTransform;
                float width = Mathf.Abs(rectTransform.rect.width);
                float height = Mathf.Abs(rectTransform.rect.height);
                float minSize = Mathf.Min(width, height);
                float maxSize = Mathf.Max(width, height);
                bool looksLikePrimaryIcon =
                    iconImageReference == null &&
                    candidateImage.preserveAspect &&
                    minSize >= 28f &&
                    maxSize <= 96f;

                if (looksLikePrimaryIcon)
                {
                    iconImageReference = candidateImage;
                    continue;
                }

                bool looksLikeAccent =
                    minSize <= 18f ||
                    maxSize <= 28f ||
                    (candidateImage.sprite == null && maxSize <= 64f);
                if (looksLikeAccent)
                {
                    accentGraphics.Add(candidateImage);
                }
            }

            if ((accentGraphicReferences == null || accentGraphicReferences.Length == 0) && accentGraphics.Count > 0)
            {
                accentGraphicReferences = accentGraphics.ToArray();
            }
        }
    }
}
