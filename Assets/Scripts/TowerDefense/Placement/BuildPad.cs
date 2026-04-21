using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// BuildPad 表示“地图上允许玩家放置防御塔的一个建造点”。
///
/// 在这套塔防原型里，地图不会允许玩家在任意位置随意建造，
/// 而是会预先摆好一批可交互的塔位，让玩家在这些位置上做策略选择。
/// 这个脚本就是这些塔位中的单个实现。
///
/// 这个类刻意只承担与“塔位自身”直接相关的职责：
/// 1. 作为一个可点击对象，接收玩家对当前塔位的点击输入。
/// 2. 识别这次点击是否其实发生在 UI 上，避免按钮点击误伤地图。
/// 3. 保存当前塔位是否已经被塔占用的状态。
/// 4. 根据占用状态更新颜色反馈，让玩家一眼分辨可建和已占用状态。
///
/// 它不负责真正生成塔、不负责扣除资源，也不负责判断关卡是否结束。
/// 这些“全局规则”统一交给 TowerDefenseGame 处理。
/// 这样做的好处是职责边界清晰：
/// - BuildPad 只关心“我这个点位发生了什么”
/// - TowerDefenseGame 只关心“整个游戏规则如何推进”
///
/// 这种分工在原型阶段尤其有价值，因为以后无论你是增加塔类型、
/// 加入建造限制，还是改成网格化地图，塔位脚本都能保持相对稳定。
/// </summary>
/// <remarks>
/// 这里显式声明了两个依赖组件：
/// - SpriteRenderer：用于给塔位本体显示颜色状态。
/// - BoxCollider2D：用于接收鼠标点击。
///
/// RequireComponent 能让 Unity 在挂载脚本时自动确保这些依赖存在，
/// 降低因为缺少组件而导致“看得见但点不到”或“能点到但不会变色”的问题。
/// </remarks>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class BuildPad : MonoBehaviour
{
    [Header("Visuals")]

    /// <summary>
    /// 当塔位处于空闲状态、允许放置防御塔时显示的颜色。
    ///
    /// 这里默认使用偏亮的绿色，是为了向玩家传达“这是一个正向、可操作状态”。
    /// 在塔防游戏里，建造反馈必须足够直观，否则玩家很容易在高压节奏下误判。
    /// </summary>
    [SerializeField] private Color availableColor = new Color(0.15f, 0.82f, 0.42f, 1f);

    /// <summary>
    /// 当塔位已经被某个塔占据、不能再次放置时显示的颜色。
    ///
    /// 这里用更暗、更沉稳的颜色，是为了让玩家知道：
    /// “这个位置仍然存在，但它现在已经不是可用槽位了”。
    /// 这比直接隐藏对象更有助于保持地图布局的可读性。
    /// </summary>
    [SerializeField] private Color occupiedColor = new Color(0.18f, 0.32f, 0.22f, 1f);

    /// <summary>
    /// 对本体 SpriteRenderer 的缓存引用。
    ///
    /// 我们在 Awake 阶段只获取一次组件，后续刷新外观时重复使用，
    /// 这样能减少重复 GetComponent 调用，也让代码意图更加明确。
    /// </summary>
    private SpriteRenderer _spriteRenderer;

    /// <summary>
    /// 当前占据该塔位的塔对象。
    ///
    /// 这个字段既是一个“状态标记”，也是未来扩展的入口。
    /// 现在我们主要用它判断塔位是否已占用；
    /// 以后如果要做升级、出售、选中当前塔等功能，也可以通过它找到对应实例。
    ///
    /// 约定非常简单：
    /// - null：当前塔位为空
    /// - 非 null：当前塔位已被占用
    /// </summary>
    private GameObject _occupant;

    /// <summary>
    /// 当前塔位是否已经有塔驻留。
    ///
    /// 之所以提供只读属性，而不是让外部直接访问 _occupant，
    /// 是因为大多数系统真正关心的是“有没有被占用”这个业务结果，
    /// 而不是具体通过哪个字段来实现该状态。
    /// 这种封装方式能让外部依赖更稳定，也更符合“暴露意图而不是暴露细节”的思路。
    /// </summary>
    public bool IsOccupied => _occupant != null;

    /// <summary>
    /// Unity 生命周期回调，在对象初始化时执行。
    ///
    /// 这里做两件准备工作：
    /// 1. 缓存 SpriteRenderer 组件，供后续刷新显示时直接使用。
    /// 2. 根据当前占用状态主动同步一次视觉状态，确保场景刚载入时颜色正确。
    ///
    /// 即使当前项目默认所有塔位初始都为空，也建议保持这种主动初始化方式，
    /// 因为它能减少“运行时状态”和“场景初始显示”不同步的风险。
    /// </summary>
    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        RefreshVisual();
    }

    /// <summary>
    /// Unity 的鼠标点击回调。
    ///
    /// 当玩家点击了当前这个带有 Collider 的对象时，Unity 会自动调用这里。
    /// 对塔位来说，这就是“玩家试图在这里进行建造交互”的入口。
    ///
    /// 需要注意的是，这个方法依然不直接建塔。
    /// 它只负责把“点击意图”转交给 TowerDefenseGame，
    /// 由总控统一决定当前选中了哪种塔、资源够不够、能不能真的放下去。
    ///
    /// 这样做可以避免每个塔位各自复制一份建造规则，
    /// 让游戏规则保持集中、易于维护。
    /// </summary>
    private void OnMouseDown()
    {
        // 如果玩家这次点击其实点在 UI 上，就不要继续执行地图交互。
        // 否则在点击商店按钮、弹窗、底部操作区时，
        // 很可能会同时误触到 UI 后面的塔位，造成很糟糕的操作体验。
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // 如果总控对象当前不存在，直接放弃本次请求。
        // 这是一层稳妥的保护，避免在测试场景、初始化顺序异常、
        // 或场景搭建还不完整时出现空引用错误。
        if (TowerDefenseGame.Instance == null)
        {
            return;
        }

        // 把“我这个塔位被点击了”的事件上报给总控，
        // 由总控统一处理放塔规则和资源结算。
        TowerDefenseGame.Instance.TryPlaceTower(this);
    }

    /// <summary>
    /// 获取当前塔位的建造世界坐标。
    ///
    /// 现在的实现很直接，就是返回塔位物体自身的位置。
    /// 这意味着当前 BuildPad 同时承担了：
    /// - 输入交互热点
    /// - 塔实例放置锚点
    ///
    /// 之所以单独封装成方法，而不是让外部直接访问 transform.position，
    /// 是因为以后你很可能想在这里加入额外规则，例如：
    /// - 把塔在 Y 轴上微微抬高一点，避免与地面重叠
    /// - 改为使用某个子节点作为精确挂点
    /// - 按地形中心或美术需求做轻微偏移
    ///
    /// 外部系统只需要继续调用这个方法，不必知道内部实现如何变化。
    /// </summary>
    public Vector3 GetBuildPosition()
    {
        return transform.position;
    }

    /// <summary>
    /// 设置当前塔位的占用者，并立即刷新塔位外观。
    ///
    /// 通常在建塔成功之后，会把新生成的塔对象传进来，
    /// 让塔位记录“这个位置现在已经被使用了”。
    ///
    /// 如果未来实现了拆塔、卖塔、重建等功能，
    /// 也可以传入 null，把这个塔位重新恢复成可建状态。
    ///
    /// 这里每次设置状态后都立刻调用 RefreshVisual，
    /// 是为了保证逻辑状态和视觉反馈永远同步，
    /// 避免程序内部已经占用，但画面仍显示绿色可建的情况。
    /// </summary>
    /// <param name="tower">
    /// 当前应该登记为占用者的塔对象。
    /// 传入有效对象表示占用，传入 null 表示清空占用。
    /// </param>
    public void SetOccupant(GameObject tower)
    {
        _occupant = tower;
        RefreshVisual();
    }

    /// <summary>
    /// 仅当传入对象确实还是当前登记的占用者时，才清空塔位占用状态。
    ///
    /// 这个接口是为“塔实例生命周期反向通知塔位”准备的。
    /// 例如未来如果你实现了卖塔、拆塔、替换建筑、塔被敌人破坏等功能，
    /// 塔对象在销毁时就可以回头告诉 BuildPad：
    /// “我已经离场了，如果你记录的占用者还是我，请把塔位恢复为空闲。”
    ///
    /// 这里故意做“对象匹配后才清空”的判断，而不是无条件清空，
    /// 是为了避免未来出现一种微妙但真实的竞态问题：
    /// - 旧塔正在销毁
    /// - 同一个塔位已经被放上了新塔
    /// - 如果旧塔销毁时无条件清空，就会误把新塔对应的占用状态也一起抹掉
    ///
    /// 这种“只释放自己占用的坑位”的思路，
    /// 是很多对象生命周期管理里都非常常见的一条稳妥原则。
    /// </summary>
    public void ClearOccupantIfMatches(GameObject tower)
    {
        if (_occupant != tower)
        {
            return;
        }

        _occupant = null;
        RefreshVisual();
    }

    /// <summary>
    /// 根据当前占用状态刷新塔位颜色。
    ///
    /// 这是一个典型的“把数据状态映射成视觉反馈”的小方法。
    /// 它把内部业务状态 IsOccupied 转换为玩家能立刻感知到的颜色区别：
    /// - false：使用 availableColor
    /// - true：使用 occupiedColor
    ///
    /// 单独抽成方法的意义在于复用。
    /// Awake 初始化需要它，SetOccupant 修改状态后也需要它。
    /// 以后如果你还想加入悬停高亮、选中描边等效果，也可以继续以这里为中心扩展。
    /// </summary>
    private void RefreshVisual()
    {
        // 正常情况下 Awake 已经会缓存到 SpriteRenderer。
        // 这里额外做空判断，是为了让这个方法本身更稳健，
        // 即使在非常规调用顺序下，也不会直接因为空引用而中断。
        if (_spriteRenderer == null)
        {
            return;
        }

        // 通过一个简单的条件表达式，把“是否占用”转换成对应颜色。
        _spriteRenderer.color = IsOccupied ? occupiedColor : availableColor;
    }
}
