using System;
using UnityEngine;

/// <summary>
/// `TowerPlacementBuildExecutor` 负责“在一个已经决定要建塔的时刻，怎样把塔真正放进场景里”。
///
/// 这一层和 `TowerPlacementInteractionController` 的区别要讲清楚：
/// 1. 交互控制器负责“玩家现在处于什么放置阶段”。
/// 2. 本执行器负责“当决定真的要建时，具体怎样扣费、校验、实例化和收尾”。
///
/// 这样拆分以后，`TowerDefenseGame` 不需要再同时背两种压力：
/// - 一种是输入/状态机压力
/// - 一种是资源扣减/对象落地压力
///
/// 对后续维护来说，这个边界很重要。
/// 因为以后如果要继续加：
/// - 卖塔退款
/// - 塔升级替换
/// - 放置成功音效
/// - 建塔特效
/// 更适合继续围绕“执行链”扩，而不是重新塞回总控或交互控制器。
/// </summary>
public sealed class TowerPlacementBuildExecutor
{
    /// <summary>
    /// 这里沿用和规则层相同的校验签名。
    /// 这样执行器在真正落塔前，还能再做一次统一的最终合法性确认。
    /// </summary>
    public delegate bool PlacementValidator(Vector3 worldPosition, TowerType towerType, out string invalidReason);

    private readonly Func<bool> _isGameOverQuery; // 中文：是否游戏结束查询
    private readonly Func<int> _currentScrapQuery; // 中文：当前废料查询
    private readonly Action<int> _setCurrentScrap; // 中文：设置当前废料
    private readonly Func<TowerType, int> _getTowerCost; // 中文：获取塔费用
    private readonly Func<TowerType, string> _getTowerDisplayName; // 中文：获取塔显示名称
    private readonly Func<TowerType, GameObject> _getPrototype; // 中文：获取原型
    private readonly Func<Transform> _getPlacedTowerRoot; // 中文：获取已放置塔根节点
    private readonly Func<TowerType, float> _getPlacementRadius; // 中文：获取放置半径
    private readonly PlacementValidator _validatePlacementPosition; // 中文：validate放置位置
    private readonly Action<GameObject, TowerType> _registerPlacedStructure; // 中文：register已放置Structure
    private readonly Action _invalidatePlacementAreaOverlayCache; // 中文：invalidate放置Area覆盖层缓存
    private readonly Action _refreshHud; // 中文：刷新HUD
    private readonly Action<string> _setStatusMessage; // 中文：设置状态消息
    private readonly Action<string> _logPlacementDiagnostic; // 中文：日志放置诊断

    /// <summary>
    /// 这里全部通过委托注入，而不是直接依赖 `TowerDefenseGame`。
    /// 这样执行器只知道自己需要什么能力，不知道总控内部字段长什么样。
    /// </summary>
    public TowerPlacementBuildExecutor(
        Func<bool> isGameOverQuery,
        Func<int> currentScrapQuery,
        Action<int> setCurrentScrap,
        Func<TowerType, int> getTowerCost,
        Func<TowerType, string> getTowerDisplayName,
        Func<TowerType, GameObject> getPrototype,
        Func<Transform> getPlacedTowerRoot,
        Func<TowerType, float> getPlacementRadius,
        PlacementValidator validatePlacementPosition,
        Action<GameObject, TowerType> registerPlacedStructure,
        Action invalidatePlacementAreaOverlayCache,
        Action refreshHud,
        Action<string> setStatusMessage,
        Action<string> logPlacementDiagnostic)
    {
        _isGameOverQuery = isGameOverQuery;
        _currentScrapQuery = currentScrapQuery;
        _setCurrentScrap = setCurrentScrap;
        _getTowerCost = getTowerCost;
        _getTowerDisplayName = getTowerDisplayName;
        _getPrototype = getPrototype;
        _getPlacedTowerRoot = getPlacedTowerRoot;
        _getPlacementRadius = getPlacementRadius;
        _validatePlacementPosition = validatePlacementPosition;
        _registerPlacedStructure = registerPlacedStructure;
        _invalidatePlacementAreaOverlayCache = invalidatePlacementAreaOverlayCache;
        _refreshHud = refreshHud;
        _setStatusMessage = setStatusMessage;
        _logPlacementDiagnostic = logPlacementDiagnostic;
    }

    /// <summary>
    /// 尝试在目标位置真正放下一座塔。
    ///
    /// 这里的顺序刻意固定为：
    /// 1. 先挡掉结算态和空塔型。
    /// 2. 再挡掉旧 BuildPad 已占用情况。
    /// 3. 再检查资源、原型体和最终合法性。
    /// 4. 只有全部通过，才真正实例化、挂接兼容组件并扣费。
    ///
    /// 这个顺序能保证：
    /// - 错误提示尽量准确。
    /// - 不会出现先建对象、后发现失败的脏状态。
    /// </summary>
    public bool TryPlaceTowerAt(Vector3 worldPosition, TowerType towerType, BuildPad ownerPad = null)
    {
        if ((_isGameOverQuery != null && _isGameOverQuery()) || towerType == TowerType.None)
        {
            return false;
        }

        if (ownerPad != null && ownerPad.IsOccupied)
        {
            _setStatusMessage?.Invoke("This legacy build pad is already occupied.");
            _logPlacementDiagnostic?.Invoke($"TryPlace rejected: legacy BuildPad already occupied. tower={towerType} world={worldPosition}");
            return false;
        }

        int currentScrap = _currentScrapQuery != null ? _currentScrapQuery() : 0;
        int cost = _getTowerCost != null ? _getTowerCost(towerType) : 0;
        if (currentScrap < cost)
        {
            _setStatusMessage?.Invoke($"废料不足。你当前只有 {currentScrap} 废料。");
            _logPlacementDiagnostic?.Invoke($"TryPlace rejected: insufficient scrap. tower={towerType} cost={cost} currentScrap={currentScrap}");
            return false;
        }

        GameObject prototype = _getPrototype != null ? _getPrototype(towerType) : null;
        if (prototype == null)
        {
            _setStatusMessage?.Invoke("缺少塔原型体，请检查场景配置。");
            _logPlacementDiagnostic?.Invoke($"TryPlace rejected: missing prototype. tower={towerType}");
            return false;
        }

        if (_validatePlacementPosition != null &&
            !_validatePlacementPosition(worldPosition, towerType, out string invalidReason))
        {
            _setStatusMessage?.Invoke(invalidReason);
            _logPlacementDiagnostic?.Invoke($"TryPlace rejected by validation: tower={towerType} world={worldPosition} reason={invalidReason}");
            return false;
        }

        Transform placedTowerRoot = _getPlacedTowerRoot != null ? _getPlacedTowerRoot() : null;
        if (placedTowerRoot == null)
        {
            _setStatusMessage?.Invoke("缺少已放置塔根节点，请检查运行时场景接线。");
            _logPlacementDiagnostic?.Invoke($"TryPlace rejected: missing placed tower root. tower={towerType} world={worldPosition}");
            return false;
        }

        GameObject tower = UnityEngine.Object.Instantiate(prototype, worldPosition, Quaternion.identity, placedTowerRoot);
        string towerDisplayName = _getTowerDisplayName != null ? _getTowerDisplayName(towerType) : towerType.ToString();
        tower.name = ownerPad != null
            ? $"{towerDisplayName}_{ownerPad.name}"
            : $"{towerDisplayName}_{placedTowerRoot.childCount:00}";
        tower.SetActive(true);

        DefenseTower defenseTower = tower.GetComponent<DefenseTower>();
        if (defenseTower != null && TowerTypeUtility.IsCombatTower(towerType))
        {
            defenseTower.ConfigureBuildType(towerType);
        }

        if (!PreparePlacedTowerComponents(tower, towerType, ownerPad, out string prefabError))
        {
            _setStatusMessage?.Invoke(prefabError);
            _logPlacementDiagnostic?.Invoke($"TryPlace rejected after instantiate: tower={towerType} world={worldPosition} reason={prefabError}");
            UnityEngine.Object.Destroy(tower);
            return false;
        }

        if (ownerPad != null)
        {
            ownerPad.SetOccupant(tower);
        }

        _registerPlacedStructure?.Invoke(tower, towerType);
        _setCurrentScrap?.Invoke(currentScrap - cost);
        _invalidatePlacementAreaOverlayCache?.Invoke();
        _setStatusMessage?.Invoke($"已部署 {towerDisplayName}，消耗 {cost} 废料。");
        TowerDefenseGame.Instance?.ShowTransientHudNotice($"-{cost} 废料消耗。", 2.2f);
        _logPlacementDiagnostic?.Invoke($"TryPlace succeeded: tower={towerType} world={worldPosition} cost={cost} remainingScrap={currentScrap - cost}");
        _refreshHud?.Invoke();
        return true;
    }

    /// <summary>
    /// 给正式落地的塔补上用于放置判定的圆形触发碰撞体。
    ///
    /// 这里特别强调一次：
    /// 它不是为了让塔在物理世界里互相顶开，
    /// 而是为了让“塔之间不能贴太近”这条放置规则有稳定的运行时依据。
    /// </summary>
    private bool PreparePlacedTowerComponents(GameObject tower, TowerType towerType, BuildPad ownerPad, out string invalidReason)
    {
        invalidReason = string.Empty;

        if (tower == null)
        {
            invalidReason = "放置后的塔实例为空。";
            return false;
        }

        CircleCollider2D circleCollider = tower.GetComponent<CircleCollider2D>();
        if (circleCollider == null)
        {
            invalidReason = "塔 Prefab 缺少 CircleCollider2D。请直接修正 Prefab 资产，而不要依赖运行时补丁。";
            return false;
        }

        circleCollider.isTrigger = true;
        circleCollider.radius = _getPlacementRadius != null ? _getPlacementRadius(towerType) : 0.5f;

        PlacedTower placedTower = tower.GetComponent<PlacedTower>();
        if (placedTower == null)
        {
            invalidReason = "塔 Prefab 缺少 PlacedTower 组件。请直接修正 Prefab 资产，而不要依赖运行时补丁。";
            return false;
        }

        placedTower.Initialize(ownerPad, towerType);
        return true;
    }
}
