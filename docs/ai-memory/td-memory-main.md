# Tower Defense AI Memory - Main
Version: 1.2.0
Updated: 2026-05-09
Scope: 仅用于本项目的 `【塔防开发】` 相关任务。
Read Priority: 必读主文档；按需补读 `td-memory-architecture.md` 与 `td-memory-rules-and-history.md`。

## Navigation
- 主记忆文档：`docs/ai-memory/td-memory-main.md`
- 架构文档：`docs/ai-memory/td-memory-architecture.md`
- 规则与历史文档：`docs/ai-memory/td-memory-rules-and-history.md`
- 执行手册：`docs/ai-memory/td-agent-development-playbook.md`
- AI 工作区方法文档：`docs/ai-workspace-bootstrap-methodology.md`
- 地图工具手册：`docs/map-development-tools-manual.md`

## 当前项目概况
这是一个基于 Unity `2022.3.62f3c1` 的 2D 塔防项目。

当前 `main` 的真实状态不是“正式塔防主线全部接好”的单一形态，而是一个混合态：

- 塔防运行时代码与地图工具链已经很完整
- `Assets/Scenes` 下已经存在正式塔防关卡和剧情场景
- `Assets/_Project/Story` 下已经存在 2D 横板剧情原型内容
- 但当前 `Build Settings` 仍未完全切到正式塔防关卡链

## 当前 Build Settings
当前启用场景：

1. `MainMenu`
2. `Assets/Settings/Scenes/URP2DSceneTemplate.unity`
3. `Assets/chapter3.unity`
4. `Assets/chapter4.unity`

项目里另外还存在但当前未全部正式启用的关键场景：

- `LevelSelect`
- `Story_Intro_01`
- `StoryInterludePlaceholder`
- `Story_Demo`
- `SampleScene`
- `Level02`
- `Level03`
- `Level04`
- `Level05`

## 当前核心玩法与系统状态
已经稳定存在的主玩法：

- 自由放置：`BuildZone + PlacementBlocker + TowerShopCard`
- 继电器供电：`RelayTower.PowerGrid + TowerPowerGridCoordinator`
- 三种战斗塔共享主链：`DefenseTower`
- 敌人目录化：`EnemyCatalogAsset`
- 波次资产化：`WaveCatalogAsset`
- 多出怪口地图：`EnemySpawnGate + BattlefieldMapDefinition`
- 剧情与塔防交替流程基础：`CampaignFlowAsset + CampaignFlowController + StorySceneStepController`

当前波次主工作流：

- `WaveCatalogAsset + EnemyCatalogAsset` 优先
- `WaveSpawner.waves` 保留为兼容兜底
- 波次预览与策划调参工具已围绕资产主链工作

## 当前地图工具链
项目里已经存在并可使用的地图开发工具：

- `Enemy Path Authoring Tool`
- `Map Development Toolkit`
- `Level Topology Editor`
- `Road Art Authoring Tool`
- `Level Balance Tuning Console`
- `LevelRouteBlueprintApplier`
- `TowerDefenseValidationRunner`
- `LevelDesignReportBuilder`

## 当前协作规则摘要
- 本项目采用 fork 工作流
- 项目创建者自己的 `origin/main` 是权威主线
- 每天开工前先同步本地仓库到 `origin/main`
- 工作区不干净时，不允许盲目 destructive sync
- `docs/` 目录在没有用户明确删除指令时，只允许新增和更新，不允许删除
- 即使同步 `origin/main`，也不能让 `docs/` 下的记忆文档、方法文档或项目文档静默消失

## 文件索引
维护方式：

- 索引源配置：`docs/ai-memory/memory-index.paths.txt`
- 自动刷新脚本：`docs/ai-memory/tools/refresh-memory-index.ps1`

<!-- MEMORY_INDEX:START -->
| Path | Lines | Role |
| --- | ---: | --- |
| `AGENTS.md` | 68 | 项目入口准则，规定启动顺序、文档同步与 docs 保全规则 |
| `ProjectSettings/ProjectVersion.txt` | 2 | Unity 版本锁定文件 |
| `ProjectSettings/EditorBuildSettings.asset` | 22 | 当前 Build Settings 场景清单 |
| `Assets/Scenes/MainMenu.unity` | 1953 | 当前 Build Settings 主菜单入口 |
| `Assets/Settings/Scenes/URP2DSceneTemplate.unity` | 281 | 当前 Build Settings 中的 URP 2D 模板场景 |
| `Assets/chapter3.unity` | 403 | 当前 Build Settings 中的章节场景 3 |
| `Assets/chapter4.unity` | 401 | 当前 Build Settings 中的章节场景 4 |
| `Assets/Scenes/LevelSelect.unity` | 5823 | 已存在但当前未进 Build Settings 的关卡选择场景 |
| `Assets/Scenes/Story_Intro_01.unity` | 1009 | 已存在的剧情开场场景 |
| `Assets/Scenes/StoryInterludePlaceholder.unity` | 259 | 剧情占位场景 |
| `Assets/Scenes/Story_Demo.unity` | 618 | 额外存在的剧情测试场景 |
| `Assets/Scenes/SampleScene.unity` | 22817 | 当前第一关标准模板场景 |
| `Assets/Scenes/Level02.unity` | 22831 | 第二关地图场景 |
| `Assets/Scenes/Level03.unity` | 60349 | 第三关地图场景 |
| `Assets/Scenes/Level04.unity` | 84291 | 第四关地图场景 |
| `Assets/Scenes/Level05.unity` | 22817 | 第五关地图场景 |
| `Assets/_Project/Story/Scripts/Dialogue/DialogueRunner.cs` | 309 | 2D 横板剧情系统对话入口脚本 |
| `Assets/Scripts/TowerDefense/Core/TowerDefenseGame.cs` | 1177 | 核心总控与运行时装配门面 |
| `Assets/Scripts/TowerDefense/Core/TowerDefenseSceneBootstrapper.cs` | 187 | 场景引用装配与启动引导 |
| `Assets/Scripts/TowerDefense/Core/CampaignFlowAsset.cs` | 127 | 剧情与塔防交替流程资产 |
| `Assets/Scripts/TowerDefense/Core/CampaignFlowController.cs` | 135 | 跨场景流程控制器 |
| `Assets/Scripts/TowerDefense/Core/StorySceneStepController.cs` | 124 | 剧情段推进桥接器 |
| `Assets/Scripts/TowerDefense/Map/BattlefieldMapDefinition.cs` | 180 | 地图显式入口，收口 BuildZone、SpawnGate、DefensePoint |
| `Assets/Scripts/TowerDefense/Map/EnemyPath.cs` | 768 | 敌人路径与路径可读性表现 |
| `Assets/Scripts/TowerDefense/Map/EnemySpawnGate.cs` | 275 | 出怪口组件 |
| `Assets/Scripts/TowerDefense/Map/DefensePointFlag.cs` | 242 | 防御点组件 |
| `Assets/Scripts/TowerDefense/Map/WaveSpawner.cs` | 551 | 波次运行时状态机，现为 WaveCatalogAsset 优先 |
| `Assets/Scripts/TowerDefense/Map/WaveCatalogAsset.cs` | 61 | 波次共享资产 |
| `Assets/Scripts/TowerDefense/Enemies/Enemy.cs` | 592 | 敌人运行时基础实现 |
| `Assets/Scripts/TowerDefense/Enemies/EnemyCatalogAsset.cs` | 159 | 敌人类型目录资产 |
| `Assets/Scripts/TowerDefense/Placement/BuildZone.cs` | 258 | 可建造区定义 |
| `Assets/Scripts/TowerDefense/Placement/PlacementBlocker.cs` | 106 | 禁建区定义 |
| `Assets/Scripts/TowerDefense/Towers/DefenseTower.cs` | 1227 | 共享战斗塔逻辑 |
| `Assets/Scripts/TowerDefense/Towers/RelayTower.PowerGrid.cs` | 178 | 继电器供电实现 |
| `Assets/Scripts/TowerDefense/Towers/TowerPowerGridCoordinator.cs` | 622 | 全局供电重算协调器 |
| `Assets/Scripts/TowerDefense/UI/TowerDefenseHudPresenter.cs` | 889 | HUD 刷新与表现桥接 |
| `Assets/Resources/TowerDefense/Configs/EnemyCatalog.asset` | 231 | 敌人目录资产实例 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level01.asset` | 51 | 第一关波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level02.asset` | 60 | 第二关波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level03.asset` | 66 | 第三关波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level04.asset` | 81 | 第四关波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level05.asset` | 57 | 第五关波次资产 |
| `Assets/Editor/TowerDefense/Authoring/EnemyPathAuthoringTool.cs` | 911 | 路径点收集、排序、插入、交换工具 |
| `Assets/Editor/TowerDefense/Authoring/TowerDefenseMapToolkitWindow.cs` | 2274 | 地图综合工具箱：路径校验、道路生成、健康检查、波次预览、报告导出 |
| `Assets/Editor/TowerDefense/Authoring/LevelTopologyEditorWindow.cs` | 495 | 多出怪口/多防御点拓扑编辑器 |
| `Assets/Editor/TowerDefense/Authoring/LevelRouteBlueprintApplier.cs` | 803 | 大规模关卡路线蓝图应用器 |
| `Assets/Editor/TowerDefense/Authoring/RoadArtAuthoringWindow.cs` | 376 | 道路美术层铺设工具 |
| `Assets/Editor/TowerDefense/Authoring/LevelBalanceTuningWindow.cs` | 954 | 策划数值调参台 |
| `Assets/Editor/TowerDefense/Authoring/LevelDesignReportBuilder.cs` | 404 | 正式关卡报告构建器 |
| `Assets/Editor/TowerDefense/Validation/TowerDefenseValidationRunner.cs` | 511 | 全关卡结构验证器 |
| `docs/ai-memory/td-memory-main.md` | 110 | 主记忆文档：项目概况、索引、当前状态 |
| `docs/ai-memory/td-memory-architecture.md` | 90 | 架构与场景合同文档 |
| `docs/ai-memory/td-memory-rules-and-history.md` | 59 | 规则、已知问题、历史与路线图文档 |
| `docs/ai-memory/td-agent-development-playbook.md` | 47 | 面向后续协作者与智能体的执行手册 |
| `docs/ai-workspace-bootstrap-methodology.md` | 37 | AI 工作区搭建与每日同步方法文档 |
| `docs/map-development-tools-manual.md` | 552 | 地图制作工具链详细使用手册 |
| `docs/gameplay-redesign-spec.md` | 91 | 当前玩法重设基线 |
<!-- MEMORY_INDEX:END -->

## 读取策略
- 如果任务涉及场景装配、关卡链、共享根对象、地图结构：先读 `td-memory-architecture.md`
- 如果任务涉及规则、已知问题、协作历史、路线图：先读 `td-memory-rules-and-history.md`
- 如果任务涉及工具链操作顺序：读 `td-agent-development-playbook.md`
- 如果任务涉及日常同步、工作区约定、文档保全：读 `docs/ai-workspace-bootstrap-methodology.md`

如果记忆文档与当前代码或场景冲突：
- 以当前代码和场景状态为准
- 然后回写记忆文档

## Docs Preservation Rule
- 在没有用户明确删除指令时，`docs/` 目录只允许新增和更新，不允许删除。
- 即使同步 `origin/main`，也不能让 `docs/` 下的记忆文档、方法文档或项目文档静默消失。
