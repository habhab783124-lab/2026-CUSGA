using UnityEngine;

/// <summary>
/// PlacementBlocker 是自由放置系统里的“禁建标记器”。
///
/// 当我们允许玩家在大片区域内自由放塔后，
/// 仅仅知道“哪里大体可以建”还不够，
/// 还必须明确指出哪些局部区域绝对不能放置建筑，最典型的就是：
/// - 敌人行走路径
/// - 出怪口附近
/// - 基地区域
/// - 以后可能出现的特殊机关区
///
/// 这个脚本本身不负责计算位置是否合法，
/// 它的职责更像是在场景里插一面牌子，告诉总控：
/// “只要建造检测碰到我，这里就应该判定为不可放置。”
///
/// 这种 Marker Component（标记组件）模式在 Unity 里非常实用：
/// - 它很轻量
/// - 不要求复杂逻辑
/// - 但能把场景语义表达得很清楚
///
/// 你可以把它理解为：
/// BuildZone 决定“原则上哪里允许建”，
/// PlacementBlocker 决定“这里虽然在大区里，但因为特殊原因依然禁止建”。
/// </summary>
[DisallowMultipleComponent]
public class PlacementBlocker : MonoBehaviour
{
    [Header("Placement")]

    /// <summary>
    /// 当玩家把塔拖到这个阻挡区上时，界面上显示的提示原因。
    ///
    /// 把文案直接暴露到 Inspector，
    /// 能让不同阻挡物在以后拥有不同的提示语，
    /// 比如“这里是敌人路径”或“这里是基地核心区”。
    /// </summary>
    [SerializeField] private string blockerReason = "这里是敌人的行进区域，不能部署塔。"; // 中文：阻挡原因

    [Header("Gizmo")]

    /// <summary>
    /// Scene 视图调试颜色。
    ///
    /// 它不会影响游戏运行时画面，
    /// 只是帮助你在编辑器里更快看清当前哪些对象承担禁建语义。
    /// </summary>
    [SerializeField] private Color gizmoColor = new Color(1f, 0.45f, 0.2f, 0.85f); // 中文：Gizmo颜色

    /// <summary>
    /// 对当前阻挡体 Collider2D 的缓存引用。
    ///
    /// PlacementBlocker 自己并不提供几何形状，
    /// 真正的阻挡范围来自它所在对象的 Collider2D。
    /// </summary>
    private Collider2D _collider; // 中文：碰撞体

    /// <summary>
    /// 对外暴露当前阻挡区的提示语。
    ///
    /// 这样总控在判定失败时，就能直接读取更贴近场景语义的失败原因。
    /// </summary>
    public string BlockerReason => blockerReason; // 中文：阻挡原因

    /// <summary>
    /// 在运行时缓存碰撞体引用。
    /// </summary>
    private void Awake()
    {
        CacheReference();
    }

    /// <summary>
    /// 在编辑器参数变化时同步刷新引用。
    ///
    /// 这样即便你后续替换了 Collider 类型，
    /// 这个脚本也能尽快拿到新的范围对象。
    /// </summary>
    private void OnValidate()
    {
        CacheReference();
    }

    /// <summary>
    /// 缓存当前对象上的 Collider2D。
    ///
    /// 如果场景搭建时忘记给阻挡物加碰撞体，
    /// PlacementBlocker 依然能正常存在，但它就失去了真正的几何判定范围。
    /// 所以后面在场景装配阶段，我们会明确给路径段补上 BoxCollider2D。
    /// </summary>
    private void CacheReference()
    {
        if (_collider == null)
        {
            _collider = GetComponent<Collider2D>();
        }
    }

    /// <summary>
    /// 在 Scene 视图里把阻挡区轮廓画出来。
    ///
    /// 这个 Gizmo 的意义在于：
    /// 当自由放置系统出问题时，你能直观看见“系统到底把哪里当成禁建区了”。
    /// </summary>
    private void OnDrawGizmos()
    {
        CacheReference();
        if (_collider == null)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(_collider.bounds.center, _collider.bounds.size);
    }
}