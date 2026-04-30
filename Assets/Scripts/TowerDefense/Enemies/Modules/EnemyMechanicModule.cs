using UnityEngine;

/// <summary>
/// `EnemyMechanicModule` 是敌人特殊机制组件的统一基类。
///
/// 这次重构的核心目标不是把每种敌人都拆成一份独立脚本，
/// 而是把“通用敌人主链”和“少数特殊机制”拆开：
/// - `Enemy` 继续负责移动、血量、受击、死亡、基础表现
/// - 机制模块负责隐身、护盾光环、修理、死亡分裂这类额外能力
///
/// 这样以后新增怪物时，优先考虑“给 prefab 组合几个机制模块”，
/// 而不是复制出第九份、第十份敌人主脚本。
/// </summary>
public abstract class EnemyMechanicModule : MonoBehaviour
{
    /// <summary>
    /// 当前模块所服务的敌人主体。
    /// 由 `Enemy` 在初始化时统一绑定。
    /// </summary>
    protected Enemy Owner { get; private set; } // 中文：归属

    /// <summary>
    /// 为了减少每个模块里反复判空，这里统一暴露当前敌人的目录定义。
    /// 如果当前敌人不是按目录驱动初始化的，这里可能为 `null`，
    /// 模块应当把这种情况视为“当前机制不启用”。
    /// </summary>
    protected EnemyCatalogAsset.EnemyArchetypeDefinition Definition => Owner != null ? Owner.CurrentDefinition : null; // 中文：定义

    /// <summary>
    /// 由 `Enemy` 在完成自身初始化后调用，通知模块：
    /// - 现在已经知道自己服务的是哪一个敌人
    /// - 当前敌人的目录定义也已经可用
    ///
    /// 这里统一先走一次 `ResetRuntimeState()`，
    /// 这样场景里预制体复用、死亡分裂子怪再初始化时，
    /// 模块都能稳定回到干净状态。
    /// </summary>
    public void BindOwner(Enemy owner)
    {
        Owner = owner;
        ResetRuntimeState();
        OnOwnerBound();
    }

    /// <summary>
    /// 子类如果需要在绑定时缓存额外引用，可以覆写这里。
    /// </summary>
    protected virtual void OnOwnerBound()
    {
    }

    /// <summary>
    /// 把运行时计时器、一次性状态等清回初始值。
    /// </summary>
    public virtual void ResetRuntimeState()
    {
    }

    /// <summary>
    /// 敌人每帧会把 `deltaTime` 转发给所有模块。
    /// 只有真正需要持续运行的模块才在这里做事。
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
    }

    /// <summary>
    /// 当敌人刚刚结算完一次伤害后，会把这次命中的上下文通知给模块。
    /// 目前主要给隐身模块使用。
    /// </summary>
    public virtual void OnDamageResolved(bool isAreaDamage)
    {
    }

    /// <summary>
    /// 当外部想要对敌人施加“显形 / 探测”时，会走这个入口。
    /// 没有相关机制的模块可以保持空实现。
    /// </summary>
    public virtual void ApplyDetection(float duration)
    {
    }

    /// <summary>
    /// 敌人在正式死亡销毁前，会先通知模块。
    /// 目前主要给死亡分裂模块使用。
    /// </summary>
    public virtual void OnBeforeDeath()
    {
    }

    /// <summary>
    /// 默认情况下，敌人是可被直接锁定的。
    /// 只有隐身模块会覆写这条规则。
    /// </summary>
    public virtual bool CanBeDirectlyTargeted => true; // 中文：能否BeDirectlyTargeted

    /// <summary>
    /// 默认情况下，模块不会额外压低身体透明度。
    /// 只有隐身模块会返回更小的透明度倍率。
    /// </summary>
    public virtual float BodyAlphaMultiplier => 1f; // 中文：主体透明度倍率
}
