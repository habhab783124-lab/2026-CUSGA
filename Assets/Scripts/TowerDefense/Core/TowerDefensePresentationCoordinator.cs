using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// `TowerDefensePresentationCoordinator` 负责把“局内运行状态”和“表现层输出”接起来。
///
/// 这一层专门解决一个很常见的总控膨胀问题：
/// `TowerDefenseGame` 一边要管理玩法主链，
/// 一边又要亲自拼 HUD 状态、广播状态消息、显示 Game Over、隐藏敌人血条。
///
/// 这些事情虽然都和“当前这局是什么状态”有关，
/// 但它们本质上属于“表现层协调”，不是整局规则本身。
/// 所以这一轮把它们收口到这里，能让职责边界更清楚：
/// - `TowerDefenseSessionState` 负责回答“这局现在是什么状态”
/// - `TowerPlacementInteractionController` 负责回答“玩家正处于什么放置阶段”
/// - 本协调器负责回答“这些状态该如何广播到 HUD 和结算表现”
/// </summary>
public sealed class TowerDefensePresentationCoordinator
{
    private readonly Func<TowerDefenseSessionState> _sessionStateQuery;
    private readonly Func<TowerPlacementInteractionController> _interactionControllerQuery;
    private readonly Func<PlacedStructureHudState> _placedStructureHudStateQuery;
    private readonly Func<PowerGridHudSnapshot> _powerGridHudSnapshotQuery;
    private readonly Func<TowerType, bool> _canAffordTower;
    private readonly Action _refreshStarterZoneMarker;
    private readonly List<HudNoticeEntry> _recentHudNotices = new List<HudNoticeEntry>();
    private HudNoticeEntry _transientHudNotice = new HudNoticeEntry(string.Empty, HudNoticeTone.Neutral);
    private float _transientHudNoticeHideAt = -1f;
    private string _currentStatusMessage = string.Empty;

    private TowerDefenseHudPresenter _hudPresenter;
    private TowerCatalog _towerCatalog;

    private const int MaxHudNoticeHistory = 4;

    public TowerDefensePresentationCoordinator(
        Func<TowerDefenseSessionState> sessionStateQuery,
        Func<TowerPlacementInteractionController> interactionControllerQuery,
        Func<PlacedStructureHudState> placedStructureHudStateQuery,
        Func<PowerGridHudSnapshot> powerGridHudSnapshotQuery,
        Func<TowerType, bool> canAffordTower,
        Action refreshStarterZoneMarker)
    {
        _sessionStateQuery = sessionStateQuery;
        _interactionControllerQuery = interactionControllerQuery;
        _placedStructureHudStateQuery = placedStructureHudStateQuery;
        _powerGridHudSnapshotQuery = powerGridHudSnapshotQuery;
        _canAffordTower = canAffordTower;
        _refreshStarterZoneMarker = refreshStarterZoneMarker;
    }

    /// <summary>
    /// 绑定当前关卡使用的 HUD Presenter 与塔静态目录。
    ///
    /// 之所以把这一步独立出来，是因为：
    /// - 状态协调器本身可以很早创建
    /// - 但 HUD Presenter 与 TowerCatalog 要等总控装配阶段才完全就绪
    /// </summary>
    public void BindPresentation(TowerDefenseHudPresenter hudPresenter, TowerCatalog towerCatalog)
    {
        _hudPresenter = hudPresenter;
        _towerCatalog = towerCatalog;
    }

    /// <summary>
    /// 初始化这局一开始应该呈现给玩家的 HUD 状态。
    ///
    /// 这里主要做三件事：
    /// 1. 配置部署卡文案。
    /// 2. 关闭还不该出现的面板。
    /// 3. 刷一遍当前 HUD 与初始状态消息。
    /// </summary>
    public void InitializePresentation(string initialStatusMessage)
    {
        _hudPresenter?.ConfigureCardLabels(_towerCatalog);
        _hudPresenter?.SetGameOverVisible(false);
        _hudPresenter?.SetDragPreviewVisible(false);
        SetStatusMessage(initialStatusMessage);
        RefreshHud();
    }

    /// <summary>
    /// 对外广播一条状态消息。
    ///
    /// 当前 HUD presenter 内部是否真正显示常驻状态栏，是表现层自己的事情；
    /// 这一层只负责把消息作为统一广播口往下传。
    /// </summary>
    public void SetStatusMessage(string message)
    {
        _currentStatusMessage = message ?? string.Empty;
        _hudPresenter?.SetStatusMessage(message);
        RefreshHud();
    }

    /// <summary>
    /// `ShowTransientHudNotice()` 负责短时高亮反馈，
    /// 同时也会把消息写入最近事件流。
    ///
    /// 这样同一条反馈会有两层承载：
    /// - 短时间醒目高亮，避免玩家错过
    /// - 最近事件记录，避免一闪而过后彻底丢失
    /// </summary>
    public void ShowTransientHudNotice(string message, float duration = 2.5f, HudNoticeTone tone = HudNoticeTone.Auto)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        HudNoticeEntry notice = new HudNoticeEntry(message, tone);
        _transientHudNotice = notice;
        _transientHudNoticeHideAt = Time.unscaledTime + Mathf.Max(0.25f, duration);
        PushNoticeToHistory(notice);
        RefreshHud();
    }

    /// <summary>
    /// 刷新当前 HUD。
    ///
    /// 这里会先更新首塔起手区标记，再根据会话状态和交互状态拼出 HUD 快照，
    /// 最后交给 HUD presenter 统一刷新。
    /// </summary>
    public void RefreshHud()
    {
        _refreshStarterZoneMarker?.Invoke();

        if (_hudPresenter == null || _towerCatalog == null)
        {
            return;
        }

        _hudPresenter.Refresh(CreateHudState(), _towerCatalog, _canAffordTower);
    }

    /// <summary>
    /// 播放这局失败时的表现层收尾流程。
    ///
    /// 注意这里不负责“把状态标记为 Game Over”，
    /// 那是 `TowerDefenseSessionState` 的职责。
    /// 本方法只负责所有和表现层有关的收尾：
    /// - 隐藏敌人血条
    /// - 显示 Game Over 面板
    /// - 广播失败消息
    /// - 再刷一遍 HUD
    /// </summary>
    public void ShowGameOver()
    {
        HideActiveEnemyHealthBars();

        _hudPresenter?.ShowGameOver(
            title: "GAME OVER",
            hint: "The base has fallen. Exit Play Mode to keep adjusting the level and deployment flow.");

        SetStatusMessage("Base integrity depleted. Operation failed.");
        RefreshHud();
    }

    /// <summary>
    /// 关卡胜利后先停在结果界面，
    /// 等玩家确认后再切到下一段剧情。
    ///
    /// 这里和 `ShowGameOver()` 一样，只负责表现层收尾：
    /// - 显示胜利界面
    /// - 广播“等待继续”的状态消息
    /// - 刷新 HUD
    ///
    /// 真正什么时候继续切场，仍然由总控在点击后决定。
    /// </summary>
    public void ShowVictory()
    {
        _hudPresenter?.ShowVictory(
            title: "VICTORY",
            hint: "Click anywhere to continue.");

        SetStatusMessage("Operation clear. Click anywhere to continue.");
        RefreshHud();
    }

    /// <summary>
    /// 在真正开始跨场景过渡前，把塔防 HUD 和结果面板先整体收掉。
    ///
    /// 这样黑场一旦压上来，玩家不会再看见旧场景 UI 还留在屏幕上闪一帧。
    /// </summary>
    public void HideGameplayPresentationForSceneTransition()
    {
        _hudPresenter?.HideAllGameplayPresentationForSceneTransition();
    }

    /// <summary>
    /// 把当前会话状态与交互状态组装成 HUD 快照。
    /// HUD 只消费这份结果，不反向耦合总控或别的组件的内部字段。
    /// </summary>
    private TowerDefenseHudState CreateHudState()
    {
        TowerDefenseSessionState sessionState = _sessionStateQuery != null ? _sessionStateQuery() : null;
        TowerPlacementInteractionController interactionController = _interactionControllerQuery != null
            ? _interactionControllerQuery()
            : null;

        TowerType selectedTowerType = interactionController != null
            ? interactionController.SelectedTowerType
            : TowerType.None;
        bool isPlacementDragActive = interactionController != null &&
                                     interactionController.IsPlacementDragActive;
        TowerType dragTowerType = interactionController != null
            ? interactionController.DragTowerType
            : TowerType.None;

        return new TowerDefenseHudState(
            currentScrap: sessionState != null ? sessionState.CurrentScrap : 0,
            currentBaseHealth: sessionState != null ? sessionState.CurrentBaseHealth : 0,
            currentWave: sessionState != null ? sessionState.CurrentWave : 0,
            totalWaves: sessionState != null ? sessionState.TotalWaves : 0,
            selectedTowerType: selectedTowerType,
            isPlacementDragActive: isPlacementDragActive,
            dragTowerType: dragTowerType,
            placedStructureState: _placedStructureHudStateQuery != null
                ? _placedStructureHudStateQuery()
                : new PlacedStructureHudState(false, string.Empty, string.Empty),
            powerGridSnapshot: _powerGridHudSnapshotQuery != null
                ? _powerGridHudSnapshotQuery()
                : new PowerGridHudSnapshot(0, 0, 0, 0, 0, 0, 0, string.Empty),
            currentStatusMessage: _currentStatusMessage,
            transientNotice: GetTransientHudNoticeEntry(),
            recentHudNotices: _recentHudNotices.ToArray());
    }

    private HudNoticeEntry GetTransientHudNoticeEntry()
    {
        if (!_transientHudNotice.HasMessage)
        {
            return new HudNoticeEntry(string.Empty, HudNoticeTone.Neutral);
        }

        if (Time.unscaledTime > _transientHudNoticeHideAt)
        {
            _transientHudNotice = new HudNoticeEntry(string.Empty, HudNoticeTone.Neutral);
            _transientHudNoticeHideAt = -1f;
            return new HudNoticeEntry(string.Empty, HudNoticeTone.Neutral);
        }

        return _transientHudNotice;
    }

    private void PushNoticeToHistory(HudNoticeEntry notice)
    {
        _recentHudNotices.RemoveAll(existing => existing.Message == notice.Message && existing.Tone == notice.Tone);
        _recentHudNotices.Insert(0, notice);

        if (_recentHudNotices.Count > MaxHudNoticeHistory)
        {
            _recentHudNotices.RemoveRange(MaxHudNoticeHistory, _recentHudNotices.Count - MaxHudNoticeHistory);
        }
    }

    /// <summary>
    /// 在结算时隐藏所有仍然存活敌人的血条。
    ///
    /// 这件事看起来像是“敌人逻辑”，
    /// 但触发它的时机其实是纯表现层收尾：
    /// Game Over 画面出来后，不应该还有飘在前面的血条继续抢视觉焦点。
    /// </summary>
    private static void HideActiveEnemyHealthBars()
    {
        int activeEnemyCount = Enemy.ActiveEnemyCount;
        for (int i = 0; i < activeEnemyCount; i++)
        {
            Enemy enemy = Enemy.GetActiveEnemy(i);
            if (enemy != null)
            {
                enemy.SetHealthBarVisible(false);
            }
        }
    }
}
