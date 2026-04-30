using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// `TowerDefenseInputCoordinator` 负责把“这一局有哪些输入入口”整理成一个单独组件。
///
/// 当前它主要收口四类职责：
/// 1. 热键切塔与取消部署。
/// 2. 选中塔型后的快速点击放置。
/// 3. 屏幕坐标到世界坐标的换算。
/// 4. 鼠标是否压在真正会阻挡玩法的 UI 上。
///
/// 这样拆分以后，`TowerDefenseGame` 不需要再自己同时背负：
/// - 输入轮询
/// - 指针与 UI 过滤
/// - 屏幕坐标换算
/// 这些虽然都和“输入”有关，但本质上不属于整局玩法规则本身。
/// </summary>
public sealed class TowerDefenseInputCoordinator
{
    private readonly Func<bool> _isGameOverQuery; // 中文：是否游戏结束查询
    private readonly Func<bool> _tryQuickPlacementAtCurrentMouse; // 中文：尝试Quick放置At当前Mouse
    private readonly Func<bool> _tryUpgradeSelectedStructure; // 中文：尝试升级选中Structure
    private readonly Func<bool> _tryDemolishSelectedStructure; // 中文：尝试Demolish选中Structure
    private readonly Action _selectRelayTower; // 中文：select继电器塔
    private readonly Action _selectSingleTargetTower; // 中文：select单体目标塔
    private readonly Action _selectSlowFieldTower; // 中文：select减速区域塔
    private readonly Action _selectBombardTower; // 中文：select炸弹塔
    private readonly Action _clearSelection; // 中文：清除Selection

    private Camera _mainCamera; // 中文：主相机

    public TowerDefenseInputCoordinator(
        Func<bool> isGameOverQuery,
        Func<bool> tryQuickPlacementAtCurrentMouse,
        Func<bool> tryUpgradeSelectedStructure,
        Func<bool> tryDemolishSelectedStructure,
        Action selectRelayTower,
        Action selectSingleTargetTower,
        Action selectSlowFieldTower,
        Action selectBombardTower,
        Action clearSelection)
    {
        _isGameOverQuery = isGameOverQuery;
        _tryQuickPlacementAtCurrentMouse = tryQuickPlacementAtCurrentMouse;
        _tryUpgradeSelectedStructure = tryUpgradeSelectedStructure;
        _tryDemolishSelectedStructure = tryDemolishSelectedStructure;
        _selectRelayTower = selectRelayTower;
        _selectSingleTargetTower = selectSingleTargetTower;
        _selectSlowFieldTower = selectSlowFieldTower;
        _selectBombardTower = selectBombardTower;
        _clearSelection = clearSelection;
    }

    /// <summary>
    /// 绑定当前玩法使用的主相机。
    /// 输入层之所以单独保存相机，是因为屏幕坐标换算本来就属于输入辅助能力，
    /// 不应该继续散落在总控里作为一个无归属的小工具方法。
    /// </summary>
    public void BindMainCamera(Camera mainCamera)
    {
        _mainCamera = mainCamera;
    }

    /// <summary>
    /// 每帧处理当前局内的输入入口。
    /// 这里故意保持非常薄，只做“该响应什么输入”的路由，不做真正玩法执行。
    /// </summary>
    public void Tick()
    {
        HandleHotkeys();
        HandleQuickPlacementInput();
    }

    /// <summary>
    /// 读取当前鼠标对应的世界坐标。
    /// </summary>
    public Vector3 GetMouseWorldPosition()
    {
        return ScreenToWorldPosition(Input.mousePosition);
    }

    /// <summary>
    /// 把屏幕坐标转换到玩法所在的世界平面。
    /// 如果当前没有可用相机，就退回到 `Vector3.zero`，避免上层直接空引用崩掉。
    /// </summary>
    public Vector3 ScreenToWorldPosition(Vector2 screenPosition)
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            return Vector3.zero;
        }

        Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(_mainCamera.transform.position.z));
        Vector3 worldPosition = _mainCamera.ScreenToWorldPoint(screenPoint);
        worldPosition.z = 0f;
        return worldPosition;
    }

    /// <summary>
    /// 判断当前鼠标是否压在“会拦截玩法”的 UI 上。
    /// 这层过滤是为了解决装饰性 UI 误伤快速放置的问题。
    /// </summary>
    public bool IsPointerOverUserInterface()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        System.Collections.Generic.List<RaycastResult> raycastResults = new System.Collections.Generic.List<RaycastResult>(8);
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            GameObject target = raycastResults[i].gameObject;
            if (IsGameplayBlockingUserInterface(target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 处理本场景约定的快捷键。
    /// `1 / 2` 选塔，`Esc / 右键` 取消部署。
    /// </summary>
    private void HandleHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            _selectRelayTower?.Invoke();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            _selectSingleTargetTower?.Invoke();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            _selectSlowFieldTower?.Invoke();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            _selectBombardTower?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            _clearSelection?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            _tryUpgradeSelectedStructure?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
        {
            _tryDemolishSelectedStructure?.Invoke();
        }
    }

    /// <summary>
    /// 处理“已选中塔型但没有拖拽时”的快速点击放置入口。
    /// 这里不判断具体选中了什么塔型，而是把最终是否能放交给交互层决定，
    /// 自己只负责挡掉明显不应该继续向下的情况。
    /// </summary>
    private void HandleQuickPlacementInput()
    {
        if ((_isGameOverQuery != null && _isGameOverQuery()) || _tryQuickPlacementAtCurrentMouse == null)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(0) || IsPointerOverUserInterface())
        {
            return;
        }

        _tryQuickPlacementAtCurrentMouse();
    }

    /// <summary>
    /// 判断某个 UI 对象是否真的应该阻挡玩法输入。
    /// 当前仍然只把部署卡与 `Selectable` 系交互控件视为阻挡，
    /// 避免装饰文本、边框和标签误伤地图点击。
    /// </summary>
    private static bool IsGameplayBlockingUserInterface(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.GetComponentInParent<TowerShopCard>() != null)
        {
            return true;
        }

        return target.GetComponentInParent<Selectable>() != null;
    }
}
