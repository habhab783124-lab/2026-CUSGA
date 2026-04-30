using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Phase-two runtime implementation for relay nodes.
///
/// We keep the implementation split from `RelayTower.cs` for two practical reasons:
/// 1. The Unity scene script asset should stay stable and easy to recognize in the Inspector.
/// 2. The power-grid logic can keep growing without turning the script asset file into a giant mixed-purpose blob.
///
/// The relay is intentionally editor-friendly:
/// - supply range is adjustable in the Inspector
/// - supply capacity is adjustable in the Inspector
/// - the coverage preview uses Gizmos
/// - no visual dependency is hardcoded to a specific art asset
/// </summary>
public partial class RelayTower
{
    [Header("供电")]
    [SerializeField, InspectorName("供电范围")] private float supplyRange = 4.5f; // 中文：供电范围
    [SerializeField, InspectorName("基础供电容量")] private int baseSupplyCapacity = 6; // 中文：基础供电容量
    [SerializeField, InspectorName("每级供电增量")] private int supplyCapacityPerUpgrade = 2; // 中文：供电容量Per升级

    [Header("成长")]
    [SerializeField, InspectorName("当前等级")] private int currentLevel = 1; // 中文：当前等级
    [SerializeField, InspectorName("最大等级")] private int maxLevel = 3; // 中文：最大等级
    [SerializeField, InspectorName("升级基础费用")] private int upgradeCostBase = 16; // 中文：升级费用基础
    [SerializeField, InspectorName("每级升级增量")] private int upgradeCostPerLevel = 12; // 中文：升级费用Per等级

    [Header("视觉引用")]

    /// <summary>
    /// 继电器的视觉根节点。
    ///
    /// 这让原型根对象可以继续承担玩法脚本和稳定身份，
    /// 而真正可替换的外观可以下沉到独立子物体里。
    /// 后续如果你要给继电器补正式美术、额外装饰或发光层，
    /// 直接围绕这个根节点继续扩展会更清楚。
    /// </summary>
    [SerializeField, InspectorName("视觉根节点")] private Transform visualRootReference; // 中文：视觉根节点引用

    /// <summary>
    /// 当前真正代表继电器本体外观的主渲染器。
    ///
    /// 如果已经明确指定了 `visualRootReference`，
    /// 这里优先读取那个根节点上的 `SpriteRenderer`。
    /// </summary>
    [SerializeField, InspectorName("主体渲染器")] private SpriteRenderer bodyRendererReference; // 中文：主体Renderer引用

    [Header("视觉")]
    [SerializeField, InspectorName("正常颜色")] private Color normalColor = new Color(1f, 0.85f, 0.2f, 1f); // 中文：正常颜色
    [SerializeField, InspectorName("满载颜色")] private Color saturatedColor = new Color(1f, 0.5f, 0.18f, 1f); // 中文：饱和颜色
    [SerializeField, InspectorName("Gizmo 颜色")] private Color gizmoColor = new Color(1f, 0.78f, 0.28f, 0.8f); // 中文：Gizmo颜色

    private SpriteRenderer _spriteRenderer; // 中文：精灵Renderer
    private int _currentAssignedLoad; // 中文：当前已分配加载

    public int RelayNumber { get; private set; } = 100; // 中文：继电器Number
    public int CurrentLevel => Mathf.Max(1, currentLevel); // 中文：当前等级
    public int MaxLevel => Mathf.Max(1, maxLevel); // 中文：最大等级
    public int SupplyCapacity => Mathf.Max(0, baseSupplyCapacity + (CurrentLevel - 1) * supplyCapacityPerUpgrade); // 中文：供电容量
    public float SupplyRange => Mathf.Max(0.1f, supplyRange); // 中文：供电范围
    public int CurrentAssignedLoad => _currentAssignedLoad; // 中文：当前已分配加载
    public int RemainingCapacity => Mathf.Max(0, SupplyCapacity - _currentAssignedLoad); // 中文：剩余容量

    public Bounds CoverageBounds => // 中文：CoverageBounds
        new Bounds(transform.position, new Vector3(SupplyRange * 2f, SupplyRange * 2f, 0.01f));

    private void Awake()
    {
        _spriteRenderer = ResolveBodyRenderer();
        bodyRendererReference = _spriteRenderer;
        RefreshVisualState();
    }

    private void OnValidate()
    {
        if (visualRootReference == null && bodyRendererReference != null && bodyRendererReference.transform != transform)
        {
            visualRootReference = bodyRendererReference.transform;
        }

        if (bodyRendererReference == null)
        {
            bodyRendererReference = ResolveBodyRenderer();
        }
    }

    public void AssignRelayNumber(int relayNumber)
    {
        RelayNumber = Mathf.Clamp(relayNumber, 1, 100);
    }

    public void ResetRuntimeLoad()
    {
        _currentAssignedLoad = 0;
        RefreshVisualState();
    }

    public void SetRuntimeLoad(int assignedLoad)
    {
        _currentAssignedLoad = Mathf.Max(0, assignedLoad);
        RefreshVisualState();
    }

    public bool CanUpgrade => CurrentLevel < MaxLevel; // 中文：能否升级

    public int GetUpgradeCost()
    {
        return upgradeCostBase + (CurrentLevel - 1) * upgradeCostPerLevel;
    }

    public int PreviewUpgradedSupplyCapacity()
    {
        if (!CanUpgrade)
        {
            return SupplyCapacity;
        }

        return Mathf.Max(0, baseSupplyCapacity + CurrentLevel * supplyCapacityPerUpgrade);
    }

    public void ApplyUpgrade()
    {
        if (!CanUpgrade)
        {
            return;
        }

        currentLevel++;
        RefreshVisualState();
    }

    public bool ContainsPoint(Vector3 worldPosition)
    {
        Vector3 offset = worldPosition - transform.position;
        return Mathf.Abs(offset.x) <= SupplyRange
            && Mathf.Abs(offset.y) <= SupplyRange;
    }

    private void RefreshVisualState()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        _spriteRenderer.color = RemainingCapacity > 0 ? normalColor : saturatedColor;
    }

    /// <summary>
    /// 统一解析当前应该使用哪个 `SpriteRenderer` 作为继电器主体外观。
    ///
    /// 优先级是：
    /// 1. 显式指定的 `bodyRendererReference`
    /// 2. `visualRootReference` 上的渲染器
    /// 3. 根对象自己的渲染器
    ///
    /// 这样既兼容旧原型，也让新整理出的 `VisualRoot` 可以立刻生效。
    /// </summary>
    private SpriteRenderer ResolveBodyRenderer()
    {
        if (bodyRendererReference != null)
        {
            return bodyRendererReference;
        }

        if (visualRootReference != null)
        {
            SpriteRenderer visualRootRenderer = visualRootReference.GetComponent<SpriteRenderer>();
            if (visualRootRenderer != null)
            {
                return visualRootRenderer;
            }
        }

        return GetComponent<SpriteRenderer>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(SupplyRange * 2f, SupplyRange * 2f, 0.01f));
    }

    private void OnDestroy()
    {
        if (TowerDefenseGame.Instance != null)
        {
            TowerDefenseGame.Instance.NotifyStructureTopologyChanged();
        }
    }

    private void OnMouseDown()
    {
        if (TowerDefenseGame.Instance == null)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        TowerDefenseGame.Instance.SelectPlacedStructure(this);
    }
}
