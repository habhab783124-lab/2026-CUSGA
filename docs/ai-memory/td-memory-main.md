# 塔防开发主记忆
Version: 2.1.0
Updated: 2026-05-20
Scope: 仅用于本项目 `【塔防开发】` 相关任务

## Navigation
- 主记忆文档：`docs/ai-memory/td-memory-main.md`
- 架构文档：`docs/ai-memory/td-memory-architecture.md`
- 规则与历史文档：`docs/ai-memory/td-memory-rules-and-history.md`
- 执行手册：`docs/ai-memory/td-agent-development-playbook.md`
- 任务接单协议：`docs/ai-memory/td-agent-task-intake-protocol.md`
- 记忆整理与生命周期：`docs/ai-memory/td-memory-hygiene-and-lifecycle.md`
- AI 工作区方法文档：`docs/ai-workspace-bootstrap-methodology.md`
- 地图工具手册：`docs/map-development-tools-manual.md`
- 地图工具图解手册：`docs/map-toolchain-complete-level-workflow-illustrated.md`
- 当前任务卡：`docs/current-task-card.md`
- 当前任务卡 JSON：`docs/current-task-card.json`
- 工作流上下文包：`docs/workflow-context-packages.md`
- 上下文压缩与知识沉淀方案：`docs/context-compression-and-knowledge-plan.md`
- 记忆整理检查清单：`docs/dream-maintenance-checklist.md`
- 决策日志：`docs/ai-memory/td-decision-log.md`

## 当前项目概况
这是一个基于 Unity `2022.3.62f3c1` 的 2D 塔防项目。

当前项目状态不是“所有正式关卡链已经完全接入主线”的单一形态，而是一个混合状态：
- 塔防运行时代码与地图工具链已经较完整；
- `Assets/Scenes` 下已经存在正式塔防关卡和剧情场景；
- `Assets/_Project/Story` 下已经存在 2D 横板剧情原型内容；
- 但当前 `Build Settings` 仍未完全切到正式塔防关卡链。

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
已稳定存在的主玩法：
- 自由放置：`BuildZone + PlacementBlocker + TowerShopCard`
- 继电器供电：`RelayTower.PowerGrid + TowerPowerGridCoordinator`
- 三种战斗塔共享主链：`DefenseTower`
- 敌人目录化：`EnemyCatalogAsset`
- 八种基础敌人的移动动画链已接入：`Frames -> EnemyMoveAnimationAuthoringTool -> AnimatorController -> Enemy prefab VisualScaleRoot`
- 怪物 prefab 调整工作流已补齐：`EnemyPrefabTuning.unity + EnemyPrefabTuningWindow`
- 波次资产化：`WaveCatalogAsset`
- 多出怪口地图：`EnemySpawnGate + BattlefieldMapDefinition`
- 剧情与塔防交替流程基础：`CampaignFlowAsset + CampaignFlowController + StorySceneStepController`

当前波次主工作流：
- `WaveCatalogAsset + EnemyCatalogAsset` 优先
- `WaveSpawner.waves` 保留为兼容兜底

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
- `LevelSceneSanityProbe`
- `LevelAuthoringWorkbenchWindow`

## 当前协作规则摘要
- 本项目采用 fork 工作流。
- 用户自己的 `origin/main` 是权威主线。
- 每天开工前先同步本地仓库到 `origin/main`。
- 工作区不干净时，不允许盲目 destructive sync。
- `docs/` 目录在没有用户明确删除指令时，只允许新增和更新。
- 对非平凡任务，必须先复述理解、明确范围，并等待用户回复 `执行` 后再开始改文件或场景。
- 当前推荐记忆工作流是：分层记忆 + 轻量整理 + 历史检索。
- 当前推荐上下文装配方式是：L1 任务卡 + 相关上下文包 + 必要 L2 文档。
- 当前高频高风险操作已经开始沉淀成 workflow skill，而不是继续依赖临时命令链。
- 新增视觉一致性准则：`Scene` 视图中的作者化视觉结果是权威来源；后续开发默认必须保证 `Play` 视图与 `Scene` 视图视觉效果一致，运行时脚本不得擅自覆盖用户在 `Scene` 里做的视觉修改。

## 文件索引
维护方式：
- 索引源配置：`docs/ai-memory/memory-index.paths.txt`
- 自动刷新脚本：`docs/ai-memory/tools/refresh-memory-index.ps1`

<!-- MEMORY_INDEX:START -->
| Path | Lines | Role |
| --- | ---: | --- |
| `AGENTS.md` | 108 | 项目入口准则，规定启动顺序、确认后执行、文档保全与每日 origin/main 同步规则 |
| `ProjectSettings/ProjectVersion.txt` | 2 | Unity 版本锁定文件 |
| `ProjectSettings/EditorBuildSettings.asset` | 77 | 当前 Build Settings 场景清单 |
| `Assets/Scenes/MainMenu.unity` | MISSING | 当前 Build Settings 主菜单入口 |
| `Assets/Settings/Scenes/URP2DSceneTemplate.unity` | 350 | 当前 Build Settings 中的 URP 2D 模板场景 |
| `Assets/chapter3.unity` | 839 | 当前 Build Settings 中的章节场景 3 |
| `Assets/chapter4.unity` | 874 | 当前 Build Settings 中的章节场景 4 |
| `Assets/Scenes/LevelSelect.unity` | MISSING | 已存在但当前未进 Build Settings 的关卡选择场景 |
| `Assets/Scenes/Story_Intro_01.unity` | MISSING | 已存在的剧情开场场景 |
| `Assets/Scenes/StoryInterludePlaceholder.unity` | MISSING | 剧情占位场景 |
| `Assets/Scenes/Story_Demo.unity` | MISSING | 额外存在的剧情测试场景 |
| `Assets/Scenes/SampleScene.unity` | MISSING | 当前第一关标准模板场景 |
| `Assets/Scenes/Level02.unity` | MISSING | 第二关地图场景 |
| `Assets/Scenes/Level03.unity` | MISSING | 第三关地图场景 |
| `Assets/Scenes/Level04.unity` | MISSING | 第四关地图场景 |
| `Assets/Scenes/Level05.unity` | MISSING | 第五关地图场景 |
| `Assets/_Project/Story/Scripts/Dialogue/DialogueRunner.cs` | 413 | 2D 横板剧情系统对话入口脚本 |
| `Assets/Scripts/TowerDefense/Core/TowerDefenseGame.cs` | 1529 | 核心总控与运行时装配门面 |
| `Assets/Scripts/TowerDefense/Core/TowerDefenseSceneBootstrapper.cs` | 191 | 场景引用装配与启动引导 |
| `Assets/Scripts/TowerDefense/Core/CampaignFlowAsset.cs` | 127 | 剧情与塔防交替流程资产 |
| `Assets/Scripts/TowerDefense/Core/CampaignFlowController.cs` | 135 | 跨场景流程控制器 |
| `Assets/Scripts/TowerDefense/Core/StorySceneStepController.cs` | 124 | 剧情段推进桥接器 |
| `Assets/Scripts/TowerDefense/Map/BattlefieldMapDefinition.cs` | 180 | 地图显式入口，收口 BuildZone、SpawnGate、DefensePoint |
| `Assets/Scripts/TowerDefense/Map/EnemyPath.cs` | 777 | 敌人路径与路径可读性表现 |
| `Assets/Scripts/TowerDefense/Map/EnemySpawnGate.cs` | 275 | 出怪口组件 |
| `Assets/Scripts/TowerDefense/Map/DefensePointFlag.cs` | 260 | 防御点组件 |
| `Assets/Scripts/TowerDefense/Map/WaveSpawner.cs` | 785 | 波次运行时状态机，现为 WaveCatalogAsset 优先 |
| `Assets/Scripts/TowerDefense/Map/WaveCatalogAsset.cs` | 65 | 波次共享资产 |
| `Assets/Scripts/TowerDefense/Enemies/Enemy.cs` | 766 | 敌人运行时基础实现 |
| `Assets/Scripts/TowerDefense/Enemies/EnemyCatalogAsset.cs` | 163 | 敌人类型目录资产 |
| `Assets/Scripts/TowerDefense/Placement/BuildZone.cs` | 345 | 可建造区定义 |
| `Assets/Scripts/TowerDefense/Placement/PlacementBlocker.cs` | 159 | 禁建区定义 |
| `Assets/Scripts/TowerDefense/Towers/DefenseTower.cs` | 1237 | 共享战斗塔逻辑 |
| `Assets/Scripts/TowerDefense/Towers/RelayTower.PowerGrid.cs` | 146 | 继电器供电实现 |
| `Assets/Scripts/TowerDefense/Towers/TowerPowerGridCoordinator.cs` | 622 | 全局供电重算协调器 |
| `Assets/Scripts/TowerDefense/UI/TowerDefenseHudPresenter.cs` | 966 | HUD 刷新与表现桥接 |
| `Assets/Resources/TowerDefense/Configs/EnemyCatalog.asset` | 247 | 敌人目录资产实例 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level01.asset` | 120 | 第一关波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level02.asset` | 92 | 第二关波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level03.asset` | 92 | 第三关波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level04.asset` | 81 | 第四关波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level05.asset` | 57 | 第五关波次资产 |
| `Assets/Editor/TowerDefense/Authoring/EnemyPathAuthoringTool.cs` | 1153 | 路径点收集、排序、插入、交换工具 |
| `Assets/Editor/TowerDefense/Authoring/TowerDefenseMapToolkitWindow.cs` | 3178 | 地图综合工具箱：路径校验、道路生成、健康检查、波次预览、报告导出 |
| `Assets/Editor/TowerDefense/Authoring/LevelTopologyEditorWindow.cs` | 543 | 多出怪口/多防御点拓扑编辑器 |
| `Assets/Editor/TowerDefense/Authoring/LevelRouteBlueprintApplier.cs` | 771 | 大规模关卡路线蓝图应用器 |
| `Assets/Editor/TowerDefense/Authoring/RoadArtAuthoringWindow.cs` | 425 | 道路美术层铺设工具 |
| `Assets/Editor/TowerDefense/Authoring/LevelBalanceTuningWindow.cs` | 1117 | 策划数值调参台 |
| `Assets/Editor/TowerDefense/Authoring/LevelDesignReportBuilder.cs` | 399 | 正式关卡报告构建器 |
| `Assets/Editor/TowerDefense/Validation/TowerDefenseValidationRunner.cs` | 511 | 全关卡结构验证器 |
| `docs/ai-memory/td-memory-main.md` | 142 | 主记忆文档：项目概况、导航、当前状态 |
| `docs/ai-memory/td-memory-architecture.md` | 195 | 架构与场景装配说明 |
| `docs/ai-memory/td-memory-rules-and-history.md` | 118 | 规则、已知问题、历史与路线图 |
| `docs/ai-memory/td-agent-development-playbook.md` | 138 | 执行顺序、风险分级和推荐开发流程 |
| `docs/ai-memory/td-agent-task-intake-protocol.md` | 63 | 任务接单协议：先复述理解，等待用户确认后再执行 |
| `docs/ai-memory/td-memory-hygiene-and-lifecycle.md` | 60 | 记忆分层、轻量整理、历史检索与压缩方法 |
| `docs/ai-workspace-bootstrap-methodology.md` | 120 | AI 工作区搭建与每日同步方法文档 |
| `docs/current-task-card.md` | 46 | 当前任务卡：只保留这一轮任务目标、边界和完成标准 |
| `docs/workflow-context-packages.md` | 83 | 按任务域装配最小上下文的上下文包文档 |
| `docs/context-compression-and-knowledge-plan.md` | 57 | L1/L2/L3 协同、人工压缩与长期知识沉淀方案 |
| `docs/dream-maintenance-checklist.md` | 40 | 定期整理记忆、索引和 skill 层的 checklist |
| `docs/ai-memory/td-decision-log.md` | 44 | 长期决策日志：记录结论、原因和涉及文件 |
| `docs/map-development-tools-manual.md` | 540 | 地图制作工具链详细使用手册 |
| `docs/map-toolchain-complete-level-workflow.md` | 633 | 从 0 到 1 制作可游玩关卡的完整教程 |
| `docs/map-toolchain-complete-level-workflow-illustrated.md` | 646 | 地图工具链配图版教程 |
| `docs/gameplay-redesign-spec.md` | 91 | 当前玩法重设计基线 |
<!-- MEMORY_INDEX:END -->

## 读取策略
- 如果任务涉及场景装配、关卡链、共享根对象、地图结构：先读 `td-memory-architecture.md`
- 如果任务涉及规则、已知问题、协作历史、路线图：先读 `td-memory-rules-and-history.md`
- 如果任务涉及工具链操作顺序：读 `td-agent-development-playbook.md`
- 如果任务涉及接单确认、范围复述、执行边界：读 `td-agent-task-intake-protocol.md`
- 如果任务涉及分层记忆、清理、压缩和历史检索：读 `td-memory-hygiene-and-lifecycle.md`
- 如果任务涉及当前轮边界和完成标准：读 `docs/current-task-card.md`
- 如果任务涉及机器可检查的执行闸门：读 `docs/current-task-card.json` 和 `tools/check-task-gate.ps1`
- 如果任务涉及按任务域装配最小上下文：读 `docs/workflow-context-packages.md`
- 如果任务涉及知识沉淀、L1/L2/L3 分工或压缩策略：读 `docs/context-compression-and-knowledge-plan.md`
- 如果任务涉及定期清理和规则收口：读 `docs/dream-maintenance-checklist.md`
- 如果任务涉及长期结论、为什么这么定：读 `docs/ai-memory/td-decision-log.md`
- 如果任务涉及日常同步、工作区约定、文档保全：读 `docs/ai-workspace-bootstrap-methodology.md`

如果记忆文档与当前代码或场景冲突：
- 以当前代码和场景状态为准；
- 然后回写记忆文档。

## Docs Preservation Rule
- 在没有用户明确删除指令时，`docs/` 目录只允许新增和更新，不允许删除。
- 即使同步 `origin/main`，也不能让 `docs/` 下的记忆文档、方法文档、手册或项目文档静默消失。
 
## 2026-05-20 场景切换更新
- 当前正式接通的 `chapter <-> level` 链路是：
  - `chapter1 -> level 1 -> chapter2`
  - `chapter4 -> Level 2 -> chapter5 -> Level 3 -> chapter6`
  - `chapter8 -> level 4 -> chapter9`
- 当前 `Build Settings` 已显式加入四个正式塔防关卡场景：
  - `Assets/Scenes/level 1.unity`
  - `Assets/Scenes/Level 2.unity`
  - `Assets/Scenes/Level 3.unity`
  - `Assets/Scenes/level 4.unity`
- 当前正式主线的桥接方式不是完全依赖 `CampaignFlowAsset`，而是混合使用显式场景接线：
  - `Chapter1` 在剧情结束后直接进入 `level 1`
  - `StoryNpcWalkIntro2D` 负责 `chapter4 -> Level 2` 与 `chapter8 -> level 4`
  - `Chapter5` 在剧情结束后直接进入 `Level 3`
  - `WaveSpawner.fallbackNextSceneNameAfterClear` 负责关卡胜利后的回章
  - `TowerDefenseGame` 负责 `Game Over` 后点击任意位置重开当前关卡
