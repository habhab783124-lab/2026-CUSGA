using System;

/// <summary>
/// `TowerDefenseSessionState` 负责保存“这一局正在进行中的核心运行状态”。
///
/// 这里专门把它从 `TowerDefenseGame` 里拆出来，主要是为了把两类东西分开：
/// 1. 整局状态本身。
/// 2. 围绕这些状态展开的输入、放置、HUD 和场景装配流程。
///
/// 这层对象不关心：
/// - 玩家是怎么拖拽卡片的
/// - 塔是怎么真正实例化出来的
/// - HUD 最后怎么显示
///
/// 它只关心状态值本身如何变化，例如：
/// - 当前废料是多少
/// - 基地生命还剩多少
/// - 当前波次进行到哪一波
/// - 这一局是否已经进入 Game Over
///
/// 这样后面如果继续扩展存档、结算页、关卡统计或者战斗回放，
/// 都更适合围绕这份状态对象演进，而不是继续把字段散落在总控里。
/// </summary>
public sealed class TowerDefenseSessionState
{
    public TowerDefenseSessionState(int startingScrap, int startingBaseHealth)
    {
        CurrentScrap = Math.Max(0, startingScrap);
        CurrentBaseHealth = Math.Max(0, startingBaseHealth);
        CurrentWave = 0;
        TotalWaves = 0;
        IsGameOver = false;
    }

    /// <summary>
    /// 当前可用于建造和升级的废料。
    /// </summary>
    public int CurrentScrap { get; private set; } // 中文：当前废料

    /// <summary>
    /// `CurrentEnergy` 保留为兼容别名，避免旧链路在迁移过渡期断掉。
    /// 新玩法语义请统一使用 `CurrentScrap`。
    /// </summary>
    public int CurrentEnergy => CurrentScrap; // 中文：当前Energy

    /// <summary>
    /// 当前基地剩余生命值。
    /// </summary>
    public int CurrentBaseHealth { get; private set; } // 中文：当前基础生命

    /// <summary>
    /// 当前正在进行到第几波。
    /// </summary>
    public int CurrentWave { get; private set; } // 中文：当前波次

    /// <summary>
    /// 本关总波次数。
    /// </summary>
    public int TotalWaves { get; private set; } // 中文：总波次列表

    /// <summary>
    /// 当前这一局是否已经进入结算失败状态。
    /// </summary>
    public bool IsGameOver { get; private set; } // 中文：是否游戏结束

    /// <summary>
    /// 判断当前资源是否足够支付某次操作的成本。
    /// </summary>
    public bool CanAfford(int cost)
    {
        if (cost < 0)
        {
            return false;
        }

        return CurrentScrap >= cost;
    }

    /// <summary>
    /// 增加废料。
    ///
    /// 这里只接受正数收入，并且在 Game Over 后不再继续改动局内资源，
    /// 这样资源状态不会在结算后被后台逻辑继续污染。
    /// </summary>
    public bool TryAddScrap(int amount)
    {
        if (IsGameOver || amount <= 0)
        {
            return false;
        }

        CurrentScrap += amount;
        return true;
    }

    /// <summary>
    /// 旧入口保留成废料接口的兼容壳，方便当前迁移链逐步切换。
    /// </summary>
    public bool TryAddEnergy(int amount)
    {
        return TryAddScrap(amount);
    }

    /// <summary>
    /// 直接设置当前废料。
    ///
    /// 这个入口主要给“真正建塔执行链”使用。
    /// 因为建塔已经在别的地方完成了合法性与扣费判定，这里只做状态写回和下限保护。
    /// </summary>
    public void SetCurrentScrap(int value)
    {
        CurrentScrap = Math.Max(0, value);
    }

    /// <summary>
    /// 兼容旧的能量命名入口。
    /// </summary>
    public void SetCurrentEnergy(int value)
    {
        SetCurrentScrap(value);
    }

    /// <summary>
    /// 让基地承受一次伤害，并返回这次是否把基地打到 0。
    ///
    /// 这里额外返回 `actualDamage`，是为了让外层提示文案用真实扣掉的数值，
    /// 而不是总是假设输入的伤害值一定完整生效。
    /// </summary>
    public bool TryApplyBaseDamage(int amount, out int actualDamage, out bool baseDepleted)
    {
        actualDamage = 0;
        baseDepleted = false;

        if (IsGameOver || amount <= 0)
        {
            return false;
        }

        int previousHealth = CurrentBaseHealth;
        CurrentBaseHealth = Math.Max(0, CurrentBaseHealth - amount);
        actualDamage = previousHealth - CurrentBaseHealth;
        baseDepleted = CurrentBaseHealth == 0;
        return true;
    }

    /// <summary>
    /// 同步当前波次进度。
    /// 这里不做额外玩法判断，只负责把刷怪器给出的状态写进来。
    /// </summary>
    public void SetWaveProgress(int currentWave, int totalWaves)
    {
        CurrentWave = currentWave;
        TotalWaves = totalWaves;
    }

    /// <summary>
    /// 标记这一局进入 Game Over。
    /// 如果已经是结算态，就不会重复返回成功。
    /// </summary>
    public bool MarkGameOver()
    {
        if (IsGameOver)
        {
            return false;
        }

        IsGameOver = true;
        return true;
    }
}
