using UnityEngine;

/// <summary>
/// TowerDefinition 表示“一种塔的静态说明书”。
///
/// 这里刻意只放“相对稳定、不会随运行时瞬时变化的数据”，例如：
/// - 给玩家看的展示名
/// - 建造消耗
/// - 占地半径
/// - HUD / 部署卡使用的强调色与职能描述
///
/// 这样做的核心目的是把“塔是什么”与“当前这局游戏里发生了什么”拆开：
/// - `TowerDefenseGame` 继续处理资源、放置、波次、胜负状态这些运行时编排
/// - `TowerDefinition` 只回答某种塔的固定配置是什么
///
/// 对原型项目来说，这种拆分已经足够带来结构收益，
/// 但又不会把我们带进过早的 ScriptableObject 资产化或复杂配置系统。
/// </summary>
public sealed class TowerDefinition
{
    /// <summary>
    /// 用构造函数一次性写死静态信息，
    /// 是为了强调这份数据在运行期应当被当作只读配置看待。
    /// </summary>
    public TowerDefinition(
        TowerType towerType,
        string displayName,
        int buildCost,
        float placementRadius,
        float expansionSquareSize,
        string cardRoleSummary,
        string selectionHint,
        string upgradeFocusSummary,
        Color accentColor,
        Sprite cardIconSprite,
        Color cardIconTint,
        Color cardBackgroundTint,
        Color cardAccentTint)
    {
        TowerType = towerType;
        DisplayName = displayName;
        BuildCost = buildCost;
        PlacementRadius = placementRadius;
        ExpansionSquareSize = expansionSquareSize;
        CardRoleSummary = cardRoleSummary;
        SelectionHint = selectionHint;
        UpgradeFocusSummary = upgradeFocusSummary;
        AccentColor = accentColor;
        CardIconSprite = cardIconSprite;
        CardIconTint = cardIconTint;
        CardBackgroundTint = cardBackgroundTint;
        CardAccentTint = cardAccentTint;
    }

    /// <summary>
    /// 这份定义对应哪一种塔。
    /// </summary>
    public TowerType TowerType { get; } // 中文：塔类型

    /// <summary>
    /// 给 HUD、提示文案、部署成功消息使用的玩家可读名称。
    /// </summary>
    public string DisplayName { get; } // 中文：显示名称

    /// <summary>
    /// 建造这类塔需要消耗多少电量。
    /// </summary>
    public int BuildCost { get; } // 中文：建造费用

    /// <summary>
    /// 这类塔用于占地判定的半径。
    ///
    /// 注意这里说的是“建造判定半径”，
    /// 不是攻击范围，也不是碰撞物理半径。
    /// </summary>
    public float PlacementRadius { get; } // 中文：放置半径

    /// <summary>
    /// 这类塔在“部署网络扩张”里提供的方形范围边长。
    ///
    /// 这次玩法改动后，塔不只是一个占地物件，
    /// 它还会把后续允许放塔的范围继续向外扩张。
    ///
    /// 这里直接记录“方形边长”，是为了让规则读起来更贴近设计语言：
    /// 讨论时我们说的是“这座塔能往外扩多大一格”，
    /// 而不是再把方形规则绕回圆形半径。
    /// </summary>
    public float ExpansionSquareSize { get; } // 中文：Expansion方格大小

    /// <summary>
    /// 部署卡第二行的功能摘要文案。
    /// </summary>
    public string CardRoleSummary { get; } // 中文：卡片RoleSummary

    /// <summary>
    /// 选中某种塔卡时显示在操作区的即时提示文案。
    /// 这行更偏“这座塔现在最适合干什么”。
    /// </summary>
    public string SelectionHint { get; } // 中文：Selection提示

    /// <summary>
    /// 用于说明升级后主要会往哪个方向成长。
    /// 这能帮助玩家在升级前就理解不同塔的投资价值差异。
    /// </summary>
    public string UpgradeFocusSummary { get; } // 中文：升级FocusSummary

    /// <summary>
    /// 这类塔在 HUD / 部署卡里使用的强调色。
    /// </summary>
    public Color AccentColor { get; } // 中文：Accent颜色

    /// <summary>
    /// 商店卡片使用的图标。
    ///
    /// 这样图标资源不再散落在场景和脚本的多个角落，
    /// 而是跟着塔定义一起走。
    /// </summary>
    public Sprite CardIconSprite { get; } // 中文：卡片图标精灵

    /// <summary>
    /// 卡片主图标的着色。
    /// </summary>
    public Color CardIconTint { get; } // 中文：卡片图标Tint

    /// <summary>
    /// 卡片底板的主色。
    /// 这能让不同塔型在 UI 上有更稳定的视觉分层。
    /// </summary>
    public Color CardBackgroundTint { get; } // 中文：卡片背景Tint

    /// <summary>
    /// 卡片细节高亮色，例如边条、徽记或小装饰。
    /// </summary>
    public Color CardAccentTint { get; } // 中文：卡片AccentTint

    /// <summary>
    /// 统一格式化建造成本展示。
    /// 继电器免费时直接写成 `FREE`，比显示 `0 SCRAP` 更像正式规则文案。
    /// </summary>
    public string BuildCostLabel => BuildCost > 0 ? $"{BuildCost} 废料" : "免费"; // 中文：建造费用标签

    /// <summary>
    /// 生成部署卡的多行富文本。
    ///
    /// 把这段格式化逻辑放在定义对象里，
    /// 是为了让“卡片长什么样”跟着“塔定义”一起走，
    /// 避免 HUD 层又重新复制一套关于塔文案的分支逻辑。
    /// </summary>
    public string BuildCardLabelMarkup(Color secondaryTextColor)
    {
        string accentHex = ColorUtility.ToHtmlStringRGB(CardAccentTint);
        string secondaryHex = ColorUtility.ToHtmlStringRGB(secondaryTextColor);
        return
            $"{DisplayName}\n" +
            $"<size=20><color=#{secondaryHex}>{CardRoleSummary} / 扩张 {ExpansionSquareSize:0.0}</color></size>\n" +
            $"<size=32><color=#{accentHex}>{BuildCostLabel}</color></size>";
    }
}

/// <summary>
/// TowerCatalog 是当前原型里“塔静态定义的统一入口”。
///
/// 为什么这里不用更重的字典资产或配置表？
/// - 当前只有两种塔
/// - 原型还处在快速迭代期
/// - 我们需要的是“把分散的 switch 收口”，而不是引入更重的系统
///
/// 所以这里选择一个非常轻的目录对象：
/// - 让总控能统一查成本、展示名、占地半径
/// - 给 HUD 一份稳定的塔展示信息来源
/// - 为后面继续拆 `Placement / HUD / Economy` 打基础
/// </summary>
public sealed class TowerCatalog
{
    private readonly TowerDefinition _relayDefinition; // 中文：继电器定义
    private readonly TowerDefinition _singleTargetDefinition; // 中文：单体目标定义
    private readonly TowerDefinition _slowFieldDefinition; // 中文：减速区域定义
    private readonly TowerDefinition _bombardDefinition; // 中文：炸弹定义

    public TowerCatalog(
        TowerDefinition relayDefinition,
        TowerDefinition singleTargetDefinition,
        TowerDefinition slowFieldDefinition,
        TowerDefinition bombardDefinition)
    {
        _relayDefinition = relayDefinition;
        _singleTargetDefinition = singleTargetDefinition;
        _slowFieldDefinition = slowFieldDefinition;
        _bombardDefinition = bombardDefinition;
    }

    /// <summary>
    /// 尝试获取指定塔类型的定义。
    ///
    /// 用 Try 风格而不是直接抛异常，
    /// 是因为当前原型里 `TowerType.None` 本来就是一个合法中间态。
    /// </summary>
    public bool TryGetDefinition(TowerType towerType, out TowerDefinition definition)
    {
        switch (towerType)
        {
            case TowerType.Relay:
                definition = _relayDefinition;
                return true;
            case TowerType.SingleTarget:
                definition = _singleTargetDefinition;
                return true;
            case TowerType.SlowField:
                definition = _slowFieldDefinition;
                return true;
            case TowerType.Bombard:
                definition = _bombardDefinition;
                return true;
            default:
                definition = null;
                return false;
        }
    }

    /// <summary>
    /// 获取定义；若没有则返回 null。
    ///
    /// 保留这个便捷入口，是为了让调用侧在“预期应该存在定义”的场景里
    /// 写起来更直观一些。
    /// </summary>
    public TowerDefinition GetDefinition(TowerType towerType)
    {
        TryGetDefinition(towerType, out TowerDefinition definition);
        return definition;
    }
}
