# Tower Defense Project Structure Overview
Updated: 2026-04-20

## 一句话理解项目
这是一个 Unity 2022.3 的 2D 塔防项目，当前正在从旧原型迁移到一套新的“继电器供电 + 废料经济 + 多出怪口地图结构”玩法。

## 配套文档
- `docs/art-replacement-workflow.md`
  以后替换塔、敌人、UI、地图和主菜单美术时，优先看这份文档。
- `docs/manual-validation-checklist.md`
  以后你在 Unity 里做最终人工验证时，直接按这份清单逐项填写结果。

## 当前场景流
- `MainMenu.unity`
  游戏主入口场景。
- `LevelSelect.unity`
  新增的关卡选择页场景，当前已经物化为真实可编辑 UI 场景：
  - 打开场景后能直接在 Hierarchy 里看到 Canvas、返回按钮和 5 张关卡卡片
  - 不再只是“靠脚本首次打开时临时生成”的空壳
- `SampleScene.unity`
  当前第一关 / 当前样例主玩法场景。
- `Level02.unity`
  第二关地图制作场景。
- `Level03.unity`
  第三关地图制作场景。
- `Level04.unity`
  第四关地图制作场景。
- `Level05.unity`
  第五关地图制作场景。

当前跳转关系已经改成：

- 主菜单点击开始 -> 进入 `LevelSelect`
- 在 `LevelSelect` 点击某张关卡卡片 -> 进入对应关卡场景

## 当前制作方式的重要事实
- 关卡地图后续主要由用户自己在 Scene 视图里继续制作和调整。
- 当前美术资源是原型资源，后续会被替换成用户自己的资源。
- 所以后续脚本和场景结构都必须同时满足：
  - Scene 作者友好
  - Inspector 参数友好
  - 美术替换友好

## 当前脚本目录分类
- `Assets/Scripts/TowerDefense/Core`
  放总控、场景装配、输入、表现协调和会话状态。
- `Assets/Scripts/TowerDefense/Map`
  放地图定义、出怪口、防御点、路径和刷怪骨架。
- `Assets/Scripts/TowerDefense/Placement`
  放放置区域、禁建区、放置交互、放置执行和放置可视化支持。
- `Assets/Scripts/TowerDefense/Towers`
  放战斗塔、继电器、供电协调器、塔目录和塔类型工具。
- `Assets/Scripts/TowerDefense/Enemies`
  放敌人本体逻辑。
- `Assets/Scripts/TowerDefense/Enemies/Modules`
  放敌人特殊机制模块。
- `Assets/Scripts/TowerDefense/UI`
  放主菜单、关卡选择页、HUD 和卡片脚本。
- `Assets/Editor/TowerDefense/Authoring`
  放作者化和场景物化工具。
- `Assets/Editor/TowerDefense/Validation`
  放批处理验证工具。

## 新增的运行时 Prefab 入口
- `Assets/Prefabs/TowerDefense/Runtime/RelayTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/SingleTargetTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/SlowFieldTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/BombardTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/EnemyPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/ShotTrace.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/SlowPulse.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/BombProjectile.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/BombExplosion.prefab`

这批资产的意义是：

- 运行时真正实例化的实体现在有了稳定的 Prefab 落点
- 三种战斗塔不再共用一个塔 prefab，而是按塔类型各自独立
- 你后续要换塔、敌人、投掷物、爆炸等美术时，可以直接改 Prefab
- `SampleScene / RuntimePrototypes` 继续保留为作者对照原型；如果你改了场景原型并想同步回 Prefab，需要重新跑一次作者工具

## 新增的关卡选择页脚本
- `Assets/Scripts/TowerDefense/UI/LevelSelectController.cs`
  关卡选择页总控。负责：
  - 首次打开 `LevelSelect` 时自动补默认 UI 骨架
  - 在 Inspector 里承载关卡列表配置
  - 处理返回主菜单和进入关卡的跳转
- `Assets/Scripts/TowerDefense/UI/LevelSelectCard.cs`
  单张关卡卡片组件。负责：
  - 承载卡片内部文本、图片、按钮引用
  - 接收总控下发的显示数据
  - 保持卡片对象本身可在 Scene 里独立调整
- `Assets/Editor/TowerDefense/Authoring/LevelSelectSceneAuthoringTool.cs`
  关卡选择页作者工具。负责：
  - 把 `LevelSelect` 从空壳场景物化成真实可编辑 UI 场景
  - 让当前场景文件直接保存这些 UI 对象，而不是只依赖 `ExecuteAlways` 临时生成

## 当前阶段状态
- 第一阶段：完成
- 第二阶段：完成
- 第三阶段：完成
- 第四阶段：完成
- 下一阶段：进行中（HUD 正式反馈承载已开始）

## 第三阶段当前已经落地的内容
- `DefenseTower` 已支持三类最小可运行行为：
  - 单体攻击
  - 减速场
  - 炸弹投射
- `Enemy` 已支持减速状态。
- `SampleScene` 右侧部署区已经扩展成四张显式场景卡片：
  - `RelayTowerButton`
  - `DefenseTowerButton`
  - `SlowFieldTowerButton`
  - `BombardTowerButton`
- 三类战斗塔现在既能通过右侧卡片点击/拖拽选择，也能继续通过热键 `2 / 3 / 4` 快速切换。
- 三类战斗塔已经开始具备更明确的升级成长差异。
- 炸弹塔已经拥有可替换美术入口的飞行物与爆炸占位反馈。
- 单体塔与减速塔也已经补入了基础 tracer / 脉冲反馈。
- `Enemy` 也已经补入了对应的受击反馈层。
- 战斗塔已经有持续等级标记，HUD 选择区也能更明确提示塔的定位与升级方向。

## 当前脚本结构怎么理解
### 总控与装配层
- `Assets/Scripts/TowerDefense/Core/TowerDefenseGame.cs`
- `Assets/Scripts/TowerDefense/Core/TowerDefenseSceneBootstrapper.cs`
- `Assets/Scripts/TowerDefense/Core/TowerDefenseInputCoordinator.cs`
- `Assets/Editor/TowerDefense/Validation/TowerDefenseValidationRunner.cs`
- `Assets/Editor/TowerDefense/Authoring/TowerDefensePrefabAuthoringTool.cs`

### 放置链
- `Assets/Scripts/TowerDefense/Placement/TowerPlacementRules.cs`
- `Assets/Scripts/TowerDefense/Placement/TowerPlacementVisualController.cs`
- `Assets/Scripts/TowerDefense/Placement/TowerPlacementInteractionController.cs`
- `Assets/Scripts/TowerDefense/Placement/TowerPlacementBuildExecutor.cs`
- `Assets/Scripts/TowerDefense/Placement/TowerPlacementSupportCoordinator.cs`

### 地图结构骨架
- `Assets/Scripts/TowerDefense/Map/BattlefieldMapDefinition.cs`
- `Assets/Scripts/TowerDefense/Map/EnemySpawnGate.cs`
- `Assets/Scripts/TowerDefense/Map/DefensePointFlag.cs`
- `Assets/Scripts/TowerDefense/Map/WaveSpawner.cs`

### 阶段 B 供电系统
- `Assets/Scripts/TowerDefense/Towers/TowerPowerGridCoordinator.cs`
- `Assets/Scripts/TowerDefense/Towers/RelayTower.cs`
- `Assets/Scripts/TowerDefense/Towers/RelayTower.PowerGrid.cs`

### 第三阶段塔类型体系
- `Assets/Scripts/TowerDefense/Towers/DefenseTower.cs`
- `Assets/Scripts/TowerDefense/Enemies/Enemy.cs`
- `Assets/Scripts/TowerDefense/Enemies/Modules/EnemyMechanicModule.cs`
- `Assets/Scripts/TowerDefense/Enemies/Modules/EnemyStealthModule.cs`
- `Assets/Scripts/TowerDefense/Enemies/Modules/EnemyShieldAuraModule.cs`
- `Assets/Scripts/TowerDefense/Enemies/Modules/EnemyRepairModule.cs`
- `Assets/Scripts/TowerDefense/Enemies/Modules/EnemySplitOnDeathModule.cs`
- `Assets/Editor/TowerDefense/Authoring/EnemyMechanicModuleEditors.cs`
- `Assets/Scripts/TowerDefense/Towers/TowerCatalog.cs`
- `Assets/Scripts/TowerDefense/Towers/TowerTypeUtility.cs`

## 当前最重要的事实
- 第一阶段和第二阶段已经完成。
- 第三阶段也已经完成，而且 Scene 卡片入口、升级差异底座、持续等级标记和战斗反馈都已经补到运行链里。
- 第四阶段也已经完成：敌人死亡奖励废料、废料主循环、第一版经济曲线和运行时资源反馈都已经接进主链。
- 当前样例场景的运行时原型字段也已经同步到现行脚本结构。
- 当前下一步更值得继续做的是 UI、关卡表现和正式美术承载。
- 其中下一阶段的第一轮已经开始：
  - 操作区现在会同时承载当前状态消息
  - 操作区会显示供电全局摘要
  - 瞬时提示会在高亮后继续留在最近事件流里
- 下一阶段的第二轮也已经开始：
  - `EnemyPath` 会自动补路径描边、方向箭头和转弯热点
  - `EnemySpawnGate` 会自动补更明显的出怪口标记
  - `DefensePointFlag` 会自动补更明确的核心目标区提示
- 下一阶段的第三轮也已经开始：
  - `TowerDefenseGame` 开始承载塔卡展示配置和 HUD 主题配置
  - `TowerShopCard` 开始承载卡片图标/底板/强调图形的样式入口
  - `DefenseTower`、`Enemy`、`RelayTower` 都开始暴露更明确的视觉挂点或主渲染器入口
  - `DefenseTower` 还进一步支持三种战斗塔各自独立主塔身 Sprite
  - 放置圆环资源现在优先支持显式 Sprite 引用
  - `SampleScene` 里这些新入口也已经补了一轮实际场景接线
  - `DefenseTowerPrototype` 还额外拆出了 `FeedbackRoot / TypeSignatureRoot / LevelMarkerRoot` 三个真实子物体
  - `EnemyPrototype` 也额外拆出了 `VisualScaleRoot`，让敌人身体缩放和血条层级分离
  - `RelayTowerPrototype` 也额外拆出了 `VisualRoot`，并重新接好主渲染器入口
  - `MainMenuController` 也已经把主菜单主题、字体、Sprite 和文案入口大幅收回到 Inspector
  - 路径 / 出怪口 / 防御点现在也支持显式 `readabilityRootReference` 和 `readabilityMaterialOverride`
