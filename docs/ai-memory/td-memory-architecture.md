# Tower Defense AI Memory - Architecture
Version: 1.8.0
Updated: 2026-05-09
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
