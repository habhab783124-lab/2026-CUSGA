# Tower Defense AI Memory - Architecture
Version: 1.9.0
Updated: 2026-05-20
Depends on: `docs/ai-memory/td-memory-main.md`

## 当前架构判断
当前 `main` 分支的真实状态是一个“塔防运行时代码、地图工具链、剧情原型内容并存”的混合态：

- 塔防运行时代码和地图工具链已经较完整
- 项目里存在正式塔防关卡场景与 2D 剧情原型场景
- `Assets/_Project/Story` 下存在 2D 横板剧情系统
- 但当前 `Build Settings` 还没有完全切换到正式塔防关卡链

因此阅读架构时必须区分：

1. 项目里已经存在什么
2. 当前 `main` 实际启用了什么

## 代码分层
当前运行时代码稳定分为：
- `Core`
- `Map`
- `Placement`
- `Towers`
- `Enemies`
- `UI`

编辑器代码稳定分为：
- `Authoring`
- `Validation`

## Core 层职责
- `TowerDefenseGame`
- `TowerDefenseSceneBootstrapper`
- `TowerDefenseSessionState`
- `TowerDefenseInputCoordinator`
- `TowerDefensePresentationCoordinator`
- `CampaignFlowAsset`
- `CampaignFlowController`
- `StorySceneStepController`

## Map 层职责
- `BattlefieldMapDefinition`
- `EnemyPath`
- `EnemySpawnGate`
- `DefensePointFlag`
- `WaveSpawner`
- `WaveCatalogAsset`
- `Level04RingGuide`

## Placement 层职责
- `BuildZone`
- `PlacementBlocker`
- `TowerPlacementRules`
- `TowerPlacementInteractionController`
- `TowerPlacementBuildExecutor`
- `TowerPlacementSupportCoordinator`
- `PlacedTower`
- `TowerPlacementVisualThemeAsset`

## Towers 层职责
- `DefenseTower`
- `RelayTower`
- `RelayTower.PowerGrid`
- `TowerPowerGridCoordinator`
- `TowerCatalog`
- `TowerPresentationCatalogAsset`
- `TowerTypeUtility`

## Enemies 层职责
- `Enemy`
- `EnemyCatalogAsset`
- `EnemyMechanicModule`
- `EnemyStealthModule`
- `EnemyShieldAuraModule`
- `EnemyRepairModule`
- `EnemySplitOnDeathModule`

## 当前敌人表现层约定
- 敌人 prefab 根节点主要承载 `Enemy` 逻辑、血条引用和运行时初始化入口。
- 当前真正显示怪物贴图的 `SpriteRenderer` 在子节点 `VisualScaleRoot` 上，而不是 prefab 根节点。
- 敌人的移动 `Animator` 也应挂在 `VisualScaleRoot`，这样动画换帧、受击缩放和显示层引用保持一致。
- 敌人在静止显示时，`VisualScaleRoot/SpriteRenderer` 的默认 `Sprite` 应直接使用移动动画第 1 帧，保证 Scene 视图下不会退回到空白/方块状占位显示。
- 当前敌人血条规则不再使用统一绝对高度。
- 正确约定是：`HealthBarRoot` 应根据当前怪物主显示 sprite 的最高点动态计算，使“血条最低点 = 怪物最高点 + 固定间距”。
- 也就是说，不同怪物的 `HealthBarRoot.localPosition.y` 可以不同；统一的是血条相对怪物头顶的额外距离。
- 八种基础敌人的移动动画资产当前位于：
  - `Assets/Animations/TowerDefense/Enemies/Clips/`
  - `Assets/Animations/TowerDefense/Enemies/Controllers/`
- 当前批量重建入口位于：
  - `Assets/Editor/TowerDefense/Authoring/EnemyMoveAnimationAuthoringTool.cs`
  - Unity 菜单：`Tools/Tower Defense/Authoring/重建敌人移动动画`
- 当前场景侧预览入口位于：
  - `Assets/Scenes/try.unity`
  - 场景根节点：`EnemyAnimationPreviewRoot`
  - 其下包含 8 个 `Preview_*Enemy` 实例，供 Scene 视图直接检查怪物 prefab 与动画接入状态
- 当前 prefab 调整工作流入口位于：
  - 专用场景：`Assets/Scenes/EnemyPrefabTuning.unity`
  - 工作台：`Assets/Editor/TowerDefense/Authoring/EnemyPrefabTuningWindow.cs`
  - 小范围回写菜单：`Tools/Tower Defense/Authoring/怪物 Prefab 小范围回写/*`
- 这套调整工作流的目标不是让你对整只场景实例 `Apply All`，
  而是让你在 Scene 视图里选中具体子节点，再把该子节点的局部 Transform 精确回写到源 prefab。

## UI 层职责
- `MainMenuController`
- `LevelSelectController`
- `LevelSelectCard`
- `TowerShopCard`
- `TowerDefenseHudPresenter`
- `TowerDefenseHudThemeAsset`
- `TowerDefenseHudCopyAsset`
- `LevelSelectCatalogAsset`

## 场景层当前实际状态
### 项目里存在的关键场景
- `MainMenu`
- `LevelSelect`
- `Story_Intro_01`
- `StoryInterludePlaceholder`
- `Story_Demo`
- `SampleScene`
- `Level02`
- `Level03`
- `Level04`
- `Level05`

### 当前 Build Settings 实际启用
- `MainMenu`
- `Assets/Settings/Scenes/URP2DSceneTemplate.unity`
- `Assets/chapter3.unity`
- `Assets/chapter4.unity`

这说明当前 `main` 上，场景资源存在与正式主链启用是分离的。

## 当前场景合同
### 所有关卡战斗场景都应有
- `TowerDefenseGame`
- `BattlefieldMapDefinition`
- `BuildZone`
- 至少一个 `EnemySpawnGate`
- 至少一个 `EnemyPath`
- 至少一个 `DefensePointFlag`
- `WaveSpawner`
- `PlacedTowers`
- `PlacementPreviewRoot`
- `EnemiesRoot`
- HUD Canvas

### 当前塔防关卡目标约定
- `SampleScene`
  - 第一关标准模板
- `Level02`
  - 2 个 `SpawnGate`
  - 1 个 `DefensePoint`
- `Level03`
  - 3 个 `SpawnGate`
  - 1 个 `DefensePoint`
- `Level04`
  - 4 个 `SpawnGate`
  - 2 个 `DefensePoint`

## 当前波次工作流
### 主工作流
- `WaveCatalogAsset + EnemyCatalogAsset`
- `WaveSpawner` 运行时优先使用这条链
- `Wave Preview` 优先预览这条链
- `LevelBalanceTuningWindow` 优先修改这条链

### 兼容兜底
- `WaveSpawner.waves`
- 只作为旧场景和过渡期兜底

## 当前地图工具链定位
### 路线与拓扑
- `EnemyPathAuthoringTool`
- `LevelTopologyEditorWindow`
- `LevelRouteBlueprintApplier`

### 功能性路面与建造区
- `TowerDefenseMapToolkitWindow`
  - `Path Check`
  - `Road Build`
  - `Zone Brush`

### 波次、平衡、报告
- `LevelBalanceTuningWindow`
- `LevelDesignReportBuilder`
- `TowerDefenseValidationRunner`

### 道路美术层
- `RoadArtAuthoringWindow`

## 当前架构原则
- 地图以 `Scene` 为主，不回退到硬编码坐标
- 波次以 `WaveCatalogAsset` 为主
- 道路功能层和道路美术层分离
- 多入口 / 多防御点关系由拓扑编辑器和场景引用共同维护
- 文档必须明确“项目里存在”与“当前主链启用”不是一回事

## 2026-05-20 Chapter-Level Wiring
- 当前正式主线的 `chapter` 与 `level` 桥接先走 scene-local wiring，而不是完全依赖 `CampaignFlowAsset`。
- `Chapter1`
  - 自己收口开场剧情
  - 剧情结束后直接切到 `level 1`
- `StoryNpcWalkIntro2D`
  - 当前负责 `chapter4` 与 `chapter8` 里的“NPC 入场 -> 自动对话 -> 切塔防关卡”
  - 通过场景序列化字段显式指定下一关分别是 `Level 2` 与 `level 4`
- `Chapter5`
  - 自己收口过场与角色对白
  - 对话结束后直接切到 `Level 3`
- `WaveSpawner`
  - 当关卡清完且没有激活 `CampaignFlow` 时，使用 `fallbackNextSceneNameAfterClear` 进入下一章
  - 当前四关分别回到 `chapter2 / chapter5 / chapter6 / chapter9`
- `TowerDefenseGame`
  - 负责失败态收口
  - `Game Over` 面板出现后，点击任意位置重开当前关卡
- 当前 `Build Settings` 已显式包含：
  - `level 1`
  - `Level 2`
  - `Level 3`
  - `level 4`
