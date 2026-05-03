# Tower Defense AI Memory - Main
Version: 1.5.5
Updated: 2026-05-02
Scope: 仅用于本项目的 `【塔防开发】` 相关任务。

## Navigation
- 主记忆文档：`docs/ai-memory/td-memory-main.md`
- 架构文档：`docs/ai-memory/td-memory-architecture.md`
- 规则与历史：`docs/ai-memory/td-memory-rules-and-history.md`
- 开发手册：`docs/ai-memory/td-agent-development-playbook.md`
- 文件指南：`docs/ai-memory/td-project-file-guide.md`
- 结构总览：`docs/project-structure-overview.md`
- 新玩法规范：`docs/gameplay-redesign-spec.md`
- 地图开发手册：`docs/map-development-handbook.md`
- 美术替换工作流：`docs/art-replacement-workflow.md`
- 人工验证清单：`docs/manual-validation-checklist.md`
- AI 工作环境方法论：`docs/ai-workspace-bootstrap-methodology.md`
- 远端协作说明：`docs/github-collaborator-setup.md`

## Current Summary
- 阶段 A：完成
- 阶段 B：完成
- 阶段 C：完成
- 阶段 D：完成
- 当前总体状态：核心塔防玩法主链已经成型，编辑器友好化也进入可长期维护状态
- 当前下一阶段：继续细化 Level02~Level04 地图草稿、2D 横板剧情并入、人工验证与正式美术替换

## Long-Term Constraints
- 地图与关卡后续主要由用户自己在 Unity Scene 视图中继续制作和调整。
- 脚本必须持续保持 Inspector 友好、Scene 友好、显式引用友好。
- 当前美术资源仍视为原型占位资源，后续会继续替换。
- 逻辑层必须尽量避免和当前占位 Sprite、材质、颜色、对象名强耦合。
- 文档与脚本注释中的中文必须保持正常显示，不允许再出现乱码。
- 新脚本必须按分层目录放置，不允许重新回到“根目录散放脚本”的状态。
- `docs/ai-workspace-bootstrap-methodology.md` 是长期维护文档；凡是 AI 协作环境方法发生变化，都要同步更新。

## Current Scene Flow
- 自由关卡链：
  - `MainMenu`
  - `LevelSelect`
  - `SampleScene / Level02 / Level03 / Level04 / Level05`
- 剧情-塔防交错链：
  - `MainMenu`
  - `Story_Intro_01`
  - `SampleScene`
  - `StoryInterludePlaceholder`
  - `Level02`
  - `StoryInterludePlaceholder`
  - `Level03`
  - `StoryInterludePlaceholder`
  - `Level04`
  - `StoryInterludePlaceholder`
  - `Level05`
- 当前 `MainMenu` 默认优先启动剧情-塔防交错链，第一段剧情已替换为真实 2D 原型场景 `Story_Intro_01`。
- `LevelSelect` 继续保留，作为自由测试与单关编辑入口。

## Current Gameplay State
### Map And Placement
- `SampleScene` 已具备 `BattlefieldMapDefinition`、双出怪口、路径和防御点旗帜。
- `SampleScene` 当前已撤回多次试切旧版第一关的操作，回到“后续地图修改阶段”的兼容工作基线。
- 现阶段最稳的协作方式是：在当前工作区状态上，由用户直接指出第一关需要改的具体部分，再逐项落修。
- 空地、路径、场景建筑三类区域边界已经明确。
- 建筑只能放在空地上，且战斗塔必须处于继电器供电范围内。
- 首塔起始区、可放置覆盖层、拖拽预览与合法区可视化链已经接入主链。
- `BuildZone` 当前已开始支持通过 `ZoneShapes` 根节点下多个 `Collider2D` 组合出不规则可建造区域。
- `Level02 / Level03 / Level04` 当前已落入第一版地图草稿：
  - `Level02`：废弃货运枢纽，双线汇合型地图，面积约为第一关的 2.1x
  - `Level03`：断桥居住区，碎片化长供电地图，面积约为第一关的 2.6x
  - `Level04`：三环能源中枢方向的大型压力地图草稿，面积约为第一关的 3.0x
- 用户已明确否定“当前这轮 Level02~04 草稿就是正确方向”这一前提。
  现阶段更稳的约束是：先以已恢复的旧版 `SampleScene` 为正确基线，再继续重做 / 调整后续关卡。
- `Level04` 这一轮又补了作者可读性层：
  - 三入口骨架已经接入
  - `EnergyHubOuterPlate / MidPlate / InnerPlate` 已作为空地底板层落入场景
  - `Level04RingGuide + Level04RingGuideEditor` 会在 Scene 中直观标出外环 / 中环 / 内环语义，方便继续手改塔位布局
  - 终局汇合段与防御点已从右下长走廊回收至中枢右侧，三条路径现在会围绕 `EnergyHubVisuals` 附近完成合流
  - `Pad_G / Pad_H` 与 `PathShadow_D / PathShadow_E / CoreRing / Shape` 也已跟着新终局走廊重新对齐
- `Level02 / Level03 / Level04` 当前又补了一轮“与第一关主链对齐”的修复：
  - 三种战斗塔的场景 prefab 引用已改回当前真实存在的运行时原型
  - 路线提示已显式启用程序化可读性开关，避免继续吃旧辅助图形残留
  - Gameplay 场景里的 TMP 字体已切到 `zpix SDF`，用于覆盖中文 HUD / 波次文案显示
  - `WaveSpawner` 已改成更稳的非泛型实例化路径，避免旧敌人 prefab / 目录引用触发 `InvalidCastException`
  - `TowerDefenseSceneBootstrapper` 现在会为旧场景补一个最小可用的 `PlacementPreviewRoot`
  - `TowerDefenseGame` 现在会在 `battlefieldMapReference` 未接线时按类型补一次 `BattlefieldMapDefinition`

### Relay Power System
- 继电器放置免费，升级消耗废料。
- 战斗塔放置与升级消耗废料。
- 断电塔保留在场上，但停止工作。
- 当前供电判定与升级阻断已经由 `TowerPowerGridCoordinator` 主链承接。

### Tower Combat
- 三类战斗塔都已经落在共享 `DefenseTower` 主链上：
  - `SingleTarget`
  - `SlowField`
  - `Bombard`
- 三类塔的升级成长、战斗反馈、等级标记和类型签名都已接入运行时。
- 三类战斗塔现在使用各自独立的运行时 prefab，而不再共用同一个塔 prefab。

### Economy
- 主资源语义已经统一为“废料”。
- 敌人死亡奖励废料。
- 放置、升级、波次回收潜力提示已经能在 HUD 中看到。

### Enemy System
- 敌人系统已经切到“多怪物目录驱动”：
  - `EnemyCatalogAsset`
  - `WaveCatalogAsset`
  - 每关独立 `WaveCatalog_LevelXX.asset`
- 当前已接入敌人类型：
  - 拾荒者
  - 狗 / 狼
  - 旗帜拾荒者
  - 机械师
  - 重甲机械兵
  - 隐身人
  - 憎恶
  - 小拾荒者
- 敌人运行时结构已从“单个大脚本承接所有机制”开始收口为：
  - 基础壳：`Enemy`
  - 特殊机制模块：
    - `EnemyStealthModule`
    - `EnemyShieldAuraModule`
    - `EnemyRepairModule`
    - `EnemySplitOnDeathModule`
- 当前四类需要特殊机制的敌人 prefab 已显式挂上对应模块：
  - `BannerScavengerEnemy`
  - `MechanicEnemy`
  - `StealthStalkerEnemy`
  - `AbominationEnemy`
- 这些模块现在支持两层参数来源：
  - 默认读取 `EnemyCatalogAsset`
  - 勾选 `useLocalOverrides` 后，prefab 本地参数优先生效

### Campaign And Story Placeholder
- 第一段剧情横板已引入真实 2D 可交互原型场景 `Story_Intro_01`。
- 后续剧情横板段暂时仍使用 `StoryInterludePlaceholder` 占位。
- `CampaignFlowAsset` 与 `CampaignFlowController` 已经把“剧情段 <-> 塔防关卡”的切换主链搭通。
- `StorySceneStepController` 已作为 2D 剧情场景与 `CampaignFlowController` 的最小桥接器接入：完成必要对话后，可继续推进到下一段塔防关卡。

## Current Authoring State
- 运行时脚本已按职责整理到：
  - `Core`
  - `Map`
  - `Placement`
  - `Towers`
  - `Enemies`
  - `UI`
- 编辑器脚本已按用途整理到：
  - `Assets/Editor/TowerDefense/Authoring`
  - `Assets/Editor/TowerDefense/Validation`
- 主菜单与关卡选择页已继续向“Scene 主导、脚本只接行为”收口：
  - 去掉自动回填共享资产
  - 去掉旧的对象名字段误导
  - 保留显式作者命令进行物化
- HUD 继续向显式场景文本块收口：
  - `TowerDefenseGameEditor` 已能提示当前 HUD 是否仍在用旧的单块 `SelectionText`
  - `TowerShopCard` 已显式承载自己的文本引用
  - `TowerDefenseHudPresenter` 不再跨层级猜按钮文本
- 敌人与敌人模块的作者工具已补齐：
  - `EnemyEditor`
  - `EnemyMechanicModuleEditors`
  - 能直接看目录匹配、被动特征、机制模块摘要、参数来源
  - 如果 prefab 缺少目录要求的模块，`EnemyEditor` 会提供 `Attach Missing Catalog Modules`
- 地图作者工作流这轮又补强了一轮：
  - `BuildZone` 现在支持 `ZoneShapes` 多碰撞体工作流
  - 新增 `BuildZoneEditor`
  - `EnemyPath / EnemySpawnGate / DefensePointFlag` 现在支持“程序化占位”与“作者自接管根节点”的双模式
- `WaveSpawnerEditor` 现在会直接强调“地图在 Scene 里做、波次在资产里做”的边界
- 2D 横板原型内容当前以增量方式导入到 `Assets/_Project/Story`，没有覆盖原有塔防主线场景和脚本目录。
- `Level04` 当前已经有专门的作者语义组件与 Scene 标签辅助：
  - `Level04RingGuide`
  - `Level04RingGuideEditor`
  这层只服务于关卡继续制作，不介入运行时战斗逻辑。

## Current Risks / Pending Validation
- 运行时代码命令行编译当前可通过。
- 编辑器脚本仍应优先在 Unity 内部实看 Inspector 效果，不应只依赖本地 `dotnet` 编译链。
- 最值得继续人工验证的点：
  - 4 个带特殊机制的敌人 prefab Inspector 是否按预期显示
  - `Wolf`、`HeavyArmoredMachine` 的 `EnemyEditor` 摘要是否正确
  - `LevelSelect` 页面与卡片在 Unity 内的显示是否与当前场景接线一致
  - 剧情占位场景与塔防关卡的切换是否符合预期
  - `Level04` 的三环标识、底板层和塔位分层在 Scene 视图中是否足够清晰

## Recent Important Commits
- `0a51364`
  `refactor enemy prefabs into base enemy plus mechanic modules`
- `51f7519`
  `tighten scene-authored UI wiring and HUD editor workflow`

## Current Git / Collaboration Reality
- 本地 `main` 当前领先 `origin/main` 多个提交。
- 远端 `main` 有保护规则，不能直接 push，必须通过 Pull Request。
- 当前已存在远端留档分支：
  - `snapshot-2026-04-22-enemy-modules-ui-wiring`

## Current Recommended Next Focus
1. 在 Unity 内人工验证 `Level02 / Level03 / Level04` 的路径、禁建区、出怪口、防御点和相机视野是否符合草稿设计。
2. 继续细化 `Level02 / Level03 / Level04` 的空地形状、场景建筑和地图可读性表现。
3. 继续把 2D 横板剧情内容从占位场景替换成真实内容。
4. 逐步把原型美术替换成正式资源，并持续维护显式作者入口。
5. 继续把 `Story_Intro_01` 的交互桥接模式抽象成模板，再逐段替换后续 `StoryInterludePlaceholder`。

## File Index
<!-- MEMORY_INDEX:START -->
| Path | Lines | Role |
| --- | ---: | --- |
| `Assets/Scenes/MainMenu.unity` | 1955 | 游戏主页面场景：启动入口，点击开始后切换到 LevelSelect |
| `Assets/Scenes/StoryInterludePlaceholder.unity` | 259 | 剧情横板占位场景：用于先跑通剧情段与塔防段之间的切换主链 |
| `Assets/Scenes/Story_Intro_01.unity` | 1009 | 第一段真实 2D 横板剧情场景：当前用于剧情-塔防交错链开场 |
| `Assets/Scripts/TowerDefense/UI/MainMenuController.cs` | 864 | 主菜单控制器：运行时生成首页 UI，并在点击开始时加载 LevelSelect |
| `Assets/Scenes/LevelSelect.unity` | 5812 | 关卡选择页场景：当前已物化为真实可编辑 UI 场景 |
| `Assets/Scripts/TowerDefense/UI/LevelSelectController.cs` | 973 | 关卡选择页总控：负责关卡列表展示、返回主菜单与进入关卡 |
| `Assets/Scripts/TowerDefense/UI/LevelSelectCard.cs` | 154 | 单张关卡卡片组件：负责卡片显示与点击入口 |
| `Assets/Scenes/SampleScene.unity` | 22935 | 当前第一关样例主玩法场景 |
| `Assets/Scenes/Level02.unity` | 22813 | 第二关地图草稿：废弃货运枢纽，双线汇合型放大地图 |
| `Assets/Scenes/Level03.unity` | 22813 | 第三关地图草稿：断桥居住区，碎片化长供电地图 |
| `Assets/Scenes/Level04.unity` | 24525 | 第四关地图草稿：三环能源中枢，当前已接入三入口与三环作者可读性层 |
| `Assets/Scripts/TowerDefense/Core/TowerDefenseGame.cs` | 1290 | 总控装配层 |
| `Assets/Scripts/TowerDefense/Core/CampaignFlowAsset.cs` | 127 | 剧情段与塔防段交错顺序的流程资产 |
| `Assets/Scripts/TowerDefense/Core/CampaignFlowController.cs` | 135 | 常驻流程控制器：负责跨场景推进剧情段与塔防段 |
| `Assets/Scripts/TowerDefense/Core/StoryInterludePlaceholderController.cs` | 84 | 剧情占位场景控制器：当前用于测试剧情段插入塔防关卡的主链 |
| `Assets/Scripts/TowerDefense/Core/StorySceneStepController.cs` | 124 | 2D 剧情场景桥接器：完成必要对话后推进 CampaignFlow |
| `Assets/Scripts/TowerDefense/Map/BattlefieldMapDefinition.cs` | 309 | 地图总配置入口 |
| `Assets/Scripts/TowerDefense/Map/Level04RingGuide.cs` | 42 | Level04 三环作者标识组件：序列化外环/中环/内环标签、颜色与锚点 |
| `Assets/Scripts/TowerDefense/Map/EnemyPath.cs` | 720 | 敌人路径组件：承载路径点与路径可读性表现 |
| `Assets/Scripts/TowerDefense/Map/EnemySpawnGate.cs` | 261 | 出怪口旗帜组件 |
| `Assets/Scripts/TowerDefense/Map/DefensePointFlag.cs` | 224 | 防御点旗帜组件 |
| `Assets/Scripts/TowerDefense/Map/WaveSpawner.cs` | 526 | 波次状态机：当前主链已切到显式波次资产 |
| `Assets/Scripts/TowerDefense/Towers/TowerPowerGridCoordinator.cs` | 622 | 供电域模型协调器 |
| `Assets/Scripts/TowerDefense/Towers/RelayTower.cs` | 12 | 继电器组件壳文件，保持 Scene 脚本身份稳定 |
| `Assets/Scripts/TowerDefense/Towers/RelayTower.PowerGrid.cs` | 178 | 阶段 B 继电器供电实现 |
| `Assets/Scripts/TowerDefense/Towers/DefenseTower.cs` | 1258 | 共享战斗塔脚本：承载单体、减速、炸弹三类行为 |
| `Assets/Scripts/TowerDefense/Enemies/Enemy.cs` | 708 | 敌人基础壳：移动、生命、受击、死亡与基础表现 |
| `Assets/Scripts/TowerDefense/Enemies/Modules/EnemyMechanicModule.cs` | 91 | 敌人机制模块基类 |
| `Assets/Scripts/TowerDefense/Enemies/Modules/EnemyStealthModule.cs` | 105 | 敌人隐身机制模块 |
| `Assets/Scripts/TowerDefense/Enemies/Modules/EnemyShieldAuraModule.cs` | 92 | 敌人护盾光环机制模块 |
| `Assets/Scripts/TowerDefense/Enemies/Modules/EnemyRepairModule.cs` | 102 | 敌人修理支援机制模块 |
| `Assets/Scripts/TowerDefense/Enemies/Modules/EnemySplitOnDeathModule.cs` | 73 | 敌人死亡分裂机制模块 |
| `Assets/Scripts/TowerDefense/Towers/TowerTypeUtility.cs` | 17 | 塔类型分类辅助方法 |
| `Assets/Scripts/TowerDefense/Towers/TowerPresentationCatalogAsset.cs` | 67 | 塔展示配置共享资产 |
| `Assets/Scripts/TowerDefense/Enemies/EnemyCatalogAsset.cs` | 159 | 敌人类型目录共享资产 |
| `Assets/Scripts/TowerDefense/Map/WaveCatalogAsset.cs` | 61 | 波次内容共享资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level01.asset` | 51 | 第一关独立波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level02.asset` | 60 | 第二关独立波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level03.asset` | 66 | 第三关独立波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level04.asset` | 81 | 第四关独立波次资产 |
| `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level05.asset` | 57 | 第五关独立波次资产 |
| `Assets/Prefabs/TowerDefense/Runtime/Enemies/ScavengerEnemy.prefab` | 410 | 拾荒者运行时 Prefab |
| `Assets/Prefabs/TowerDefense/Runtime/Enemies/WolfEnemy.prefab` | 410 | 狗/狼运行时 Prefab |
| `Assets/Prefabs/TowerDefense/Runtime/Enemies/BannerScavengerEnemy.prefab` | 428 | 旗帜拾荒者运行时 Prefab |
| `Assets/Prefabs/TowerDefense/Runtime/Enemies/MechanicEnemy.prefab` | 428 | 机械师运行时 Prefab |
| `Assets/Prefabs/TowerDefense/Runtime/Enemies/HeavyArmoredMachineEnemy.prefab` | 410 | 重甲机械兵运行时 Prefab |
| `Assets/Prefabs/TowerDefense/Runtime/Enemies/StealthStalkerEnemy.prefab` | 428 | 隐身人运行时 Prefab |
| `Assets/Prefabs/TowerDefense/Runtime/Enemies/AbominationEnemy.prefab` | 428 | 憎恶运行时 Prefab |
| `Assets/Prefabs/TowerDefense/Runtime/Enemies/SmallScavengerEnemy.prefab` | 410 | 小拾荒者运行时 Prefab |
| `Assets/Scripts/TowerDefense/Placement/BuildZone.cs` | 317 | 可建造区定义组件：当前已支持 ZoneShapes 多碰撞体工作流 |
| `Assets/Scripts/TowerDefense/Placement/PlacementBlocker.cs` | 106 | 禁建区标记组件：用于从大建造区中扣除局部禁建区域 |
| `Assets/Scripts/TowerDefense/Placement/TowerPlacementRules.cs` | 270 | 放置规则组件 |
| `Assets/Scripts/TowerDefense/Placement/TowerPlacementInteractionController.cs` | 355 | 放置交互流程控制器 |
| `Assets/Scripts/TowerDefense/Placement/TowerPlacementBuildExecutor.cs` | 195 | 建塔执行组件 |
| `Assets/Scripts/TowerDefense/Placement/PlacedTower.cs` | 74 | 正式落地塔实例的归属桥接组件 |
| `Assets/Scripts/TowerDefense/Placement/TowerPlacementVisualThemeAsset.cs` | 42 | 放置可视化共享主题资产 |
| `Assets/Scripts/TowerDefense/Core/TowerDefenseSessionState.cs` | 150 | 局内运行状态组件 |
| `Assets/Scripts/TowerDefense/Core/TowerDefensePresentationCoordinator.cs` | 221 | 表现协调组件 |
| `Assets/Scripts/TowerDefense/Core/TowerDefenseSceneBootstrapper.cs` | 230 | 场景装配组件 |
| `Assets/Scripts/TowerDefense/Core/TowerDefenseInputCoordinator.cs` | 192 | 输入协调组件 |
| `Assets/Scripts/TowerDefense/Placement/TowerPlacementSupportCoordinator.cs` | 469 | 放置支持组件 |
| `Assets/Scripts/TowerDefense/UI/TowerDefenseHudThemeAsset.cs` | 59 | HUD 主题共享资产 |
| `Assets/Scripts/TowerDefense/UI/TowerDefenseHudCopyAsset.cs` | 79 | HUD 固定文案共享资产 |
| `Assets/Scripts/TowerDefense/UI/LevelSelectCatalogAsset.cs` | 90 | 关卡选择页数据共享资产 |
| `Assets/Editor/TowerDefense/Validation/TowerDefenseValidationRunner.cs` | 232 | 批处理结构自检工具 |
| `Assets/Editor/TowerDefense/Authoring/BattlefieldMapDefinitionEditor.cs` | 70 | 地图总配置作者检查器：显示地图骨架摘要并提供一键收集入口 |
| `Assets/Editor/TowerDefense/Authoring/BuildZoneEditor.cs` | 68 | BuildZone 作者检查器：支持 ZoneShapes 多碰撞体工作流 |
| `Assets/Editor/TowerDefense/Authoring/EnemySpawnGateEditor.cs` | 74 | 出怪口作者检查器：提供表现根物化与刷新入口 |
| `Assets/Editor/TowerDefense/Authoring/DefensePointFlagEditor.cs` | 70 | 防御点作者检查器：提供表现根物化与刷新入口 |
| `Assets/Editor/TowerDefense/Authoring/EnemyPathEditor.cs` | 75 | 路径作者检查器：支持 Waypoints 根工作流与路径表现刷新 |
| `Assets/Editor/TowerDefense/Authoring/Level04RingGuideEditor.cs` | 45 | Level04 三环作者标识编辑器：在 Scene 视图绘制外环/中环/内环标签 |
| `Assets/Editor/TowerDefense/Authoring/TowerDefenseGameEditor.cs` | 358 | 总控作者检查器：集中提示关键缺项并分区展示配置 |
| `Assets/Editor/TowerDefense/Authoring/DefenseTowerEditor.cs` | 276 | 战斗塔作者检查器：默认只展示当前塔型真正使用的 tuning |
| `Assets/Editor/TowerDefense/Authoring/MainMenuControllerEditor.cs` | 88 | 主菜单作者检查器：提供物化 / 同步按钮和缺项摘要 |
| `Assets/Editor/TowerDefense/Authoring/LevelSelectControllerEditor.cs` | 77 | 关卡选择页作者检查器：提示是否已切到目录资产主链 |
| `Assets/Editor/TowerDefense/Authoring/WaveSpawnerEditor.cs` | 78 | 波次作者检查器：强调地图在 Scene 中做、波次在资产中做 |
| `Assets/Editor/TowerDefense/Authoring/RelayTowerEditor.cs` | 41 | 继电器作者检查器：显示供电参数、视觉根和运行时负载摘要 |
| `Assets/Editor/TowerDefense/Authoring/EnemyEditor.cs` | 248 | 敌人作者检查器：显示目录匹配、被动特征与机制模块摘要 |
| `Assets/Editor/TowerDefense/Authoring/EnemyMechanicModuleEditors.cs` | 184 | 敌人特殊机制模块专用检查器 |
| `Assets/Editor/TowerDefense/Authoring/TowerDefensePrefabAuthoringTool.cs` | 235 | 把场景作者原型重新落成 Prefab 的工具 |
| `Assets/Editor/TowerDefense/Authoring/MainMenuSceneAuthoringTool.cs` | 34 | 把主菜单场景显式物化成真实 UI 的工具 |
| `Assets/Editor/TowerDefense/Authoring/LevelSelectSceneAuthoringTool.cs` | 35 | 把关卡选择页场景物化成真实 UI 的工具 |
| `Packages/manifest.json` | 45 | 当前包依赖清单 |
| `ProjectSettings/ProjectVersion.txt` | 2 | Unity 版本锁定 |
| `AGENTS.md` | 54 | 项目入口说明 |
| `docs/gameplay-redesign-spec.md` | 119 | 新版玩法设计基线 |
| `docs/map-development-handbook.md` | 276 | 面向用户自己做关卡的详细地图开发手册 |
| `docs/ai-memory/td-memory-main.md` | 283 | 主记忆文档 |
| `docs/ai-memory/td-memory-architecture.md` | 168 | 架构文档 |
| `docs/ai-memory/td-memory-rules-and-history.md` | 79 | 规则与历史文档 |
| `docs/ai-memory/td-agent-development-playbook.md` | 47 | 后续开发执行手册 |
| `docs/ai-memory/td-project-file-guide.md` | 147 | 项目文件指南 |
| `docs/project-structure-overview.md` | 104 | 项目结构说明 |
| `docs/ai-workspace-bootstrap-methodology.md` | 217 | 新项目 AI 工作环境搭建方法论文档 |
<!-- MEMORY_INDEX:END -->
