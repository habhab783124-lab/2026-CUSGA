using UnityEngine;

/// <summary>
/// PlacedTower 表示“已经成功放到地图上的一座塔实例”。
///
/// 这个脚本本身不负责攻击、不负责产能、也不负责 UI，
/// 它做的是一件更偏基础架构的事：
/// 把“塔实例的生命周期”与“塔位的占用状态”稳定连接起来。
///
/// 现在把它从 `BuildPad.cs` 中独立拆出来，原因很明确：
/// - 这样它就拥有了自己独立的脚本资产身份
/// - 运行时塔 Prefab 才能直接把它挂在 Inspector 上
/// - 后续作者查看 Prefab 时，也能一眼看见“这座塔已经具备占位归属组件”
///
/// 这正是“Prefab 自洽资产”工作流里很关键的一步。
/// </summary>
public class PlacedTower : MonoBehaviour
{
    /// <summary>
    /// 当前这座塔所属的塔位。
    ///
    /// 未来如果你要做“点击塔时高亮所属塔位”、
    /// “卖塔后在原位置重新开放建造”等功能，
    /// 这个引用都会成为非常直接的入口。
    /// </summary>
    private BuildPad _ownerPad; // 中文：归属Pad

    /// <summary>
    /// 当前这座塔的类型。
    ///
    /// 现在它主要承担记录作用，
    /// 以后可以自然扩展到升级分支、出售价格、说明面板等系统里。
    /// </summary>
    public TowerType TowerType { get; private set; } = TowerType.None; // 中文：塔类型

    /// <summary>
    /// 是否已经完成初始化。
    ///
    /// 这个标记的作用是防止对象在“尚未绑定塔位信息”时就被销毁，
    /// 从而错误触发清空塔位的逻辑。
    /// </summary>
    private bool _isInitialized; // 中文：是否Initialized

    /// <summary>
    /// 对外暴露所属塔位的只读访问。
    /// </summary>
    public BuildPad OwnerPad => _ownerPad; // 中文：归属Pad

    /// <summary>
    /// 在塔被成功放置后，向它注入所属塔位和塔类型。
    ///
    /// 这里故意不放到 Awake/Start 里自动查找，
    /// 是因为“这座塔是在哪个塔位上被生成的”属于生成时上下文信息，
    /// 最可靠的来源就是创建它的总控逻辑，而不是塔自己去猜。
    /// </summary>
    public void Initialize(BuildPad ownerPad, TowerType towerType)
    {
        _ownerPad = ownerPad;
        TowerType = towerType;
        _isInitialized = true;
    }

    /// <summary>
    /// 当塔对象销毁时，尝试把对应塔位恢复为空闲状态。
    ///
    /// 注意这里并不是“销毁任何塔都随便清空一个坑位”，
    /// 而是把当前对象自身传回给 BuildPad，让塔位自行确认：
    /// “如果我现在登记的占用者还是这个对象，才真正释放。”
    ///
    /// 这种双向确认能让生命周期管理更加稳妥。
    /// </summary>
    private void OnDestroy()
    {
        if (!_isInitialized || _ownerPad == null)
        {
            return;
        }

        _ownerPad.ClearOccupantIfMatches(gameObject);
    }
}
