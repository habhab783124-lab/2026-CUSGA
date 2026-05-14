# 塔防开发主记忆
Version: 2.0.0
Updated: 2026-05-12
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

## 文件索引
维护方式：
- 索引源配置：`docs/ai-memory/memory-index.paths.txt`
- 自动刷新脚本：`docs/ai-memory/tools/refresh-memory-index.ps1`

<!-- MEMORY_INDEX:START -->
| Path | Lines | Role |
| --- | ---: | --- |
| `AGENTS.md` | 92 | 项目入口准则，规定启动顺序、确认后执行、文档保全与同步规则 |
| `docs/ai-memory/td-memory-main.md` | 73 | 主记忆文档：项目概况、导航、当前规则摘要 |
| `docs/ai-memory/td-memory-architecture.md` | 90 | 架构与场景装配说明 |
| `docs/ai-memory/td-memory-rules-and-history.md` | 88 | 规则、已知问题、历史与路线图 |
| `docs/ai-memory/td-agent-development-playbook.md` | 84 | 执行顺序、风险分级和推荐开发流程 |
| `docs/ai-memory/td-agent-task-intake-protocol.md` | 55 | 先复述理解、等待确认后执行的任务接单协议 |
| `docs/ai-memory/td-memory-hygiene-and-lifecycle.md` | 66 | 分层记忆、轻量整理、历史检索与压缩方法 |
| `docs/ai-workspace-bootstrap-methodology.md` | 74 | AI 工作区搭建、同步和方法论文档 |
| `docs/current-task-card.md` | 21 | 当前轮任务卡，约束本轮只做什么/不做什么 |
| `docs/current-task-card.json` | 10 | 机器可检查的最小任务闸门数据 |
| `docs/workflow-context-packages.md` | 70 | 按任务域装配最小上下文的上下文包文档 |
| `docs/context-compression-and-knowledge-plan.md` | 55 | L1/L2/L3 协同、人工压缩和长期知识沉淀方案 |
| `docs/dream-maintenance-checklist.md` | 40 | 定期整理记忆与 skill 层的人工 checklist |
| `docs/ai-memory/td-decision-log.md` | 42 | 长期决策日志与变更原因记录 |
| `tools/check-task-gate.ps1` | 40 | 高风险写操作前的机器可检查执行闸门 |
| `docs/map-development-tools-manual.md` | 552 | 地图工具详细使用手册 |
| `docs/map-toolchain-complete-level-workflow.md` | 455 | 从 0 到 1 制作完整关卡的操作教程 |
| `docs/map-toolchain-complete-level-workflow-illustrated.md` | 262 | 配图版地图工具链工作流教程 |
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
