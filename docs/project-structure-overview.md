# Tower Defense Project Structure Overview
Updated: 2026-04-27

## 一句话理解项目
这是一个 Unity 2022.3 的 2D 塔防项目，当前核心玩法已经从旧原型迁移到“继电器供电 + 废料经济 + 多出怪口地图 + 多怪物目录 + 剧情占位切换链”这套结构上。

## 当前主要场景
- `MainMenu.unity`
  主入口场景。
- `LevelSelect.unity`
  关卡选择页。
- `SampleScene.unity`
  第一关 / 样例主玩法场景。
- `Level02.unity` 到 `Level05.unity`
  预留关卡场景。
- `StoryInterludePlaceholder.unity`
  剧情横板占位场景。

## 当前主要脚本目录
- `Assets/Scripts/TowerDefense/Core`
  总控、会话状态、输入、表现协调、剧情流程。
- `Assets/Scripts/TowerDefense/Map`
  地图、路径、出怪口、防御点、波次。
- `Assets/Scripts/TowerDefense/Placement`
  放置规则、放置交互、放置执行、放置可视化支持。
- `Assets/Scripts/TowerDefense/Towers`
  继电器、战斗塔、供电协调器、塔目录。
- `Assets/Scripts/TowerDefense/Enemies`
  敌人基础壳与目录。
- `Assets/Scripts/TowerDefense/Enemies/Modules`
  敌人特殊机制模块。
- `Assets/Scripts/TowerDefense/UI`
  主菜单、关卡选择页、HUD、部署卡。

## 当前主要编辑器目录
- `Assets/Editor/TowerDefense/Authoring`
  自定义检查器、场景物化工具、Prefab 作者工具。
- `Assets/Editor/TowerDefense/Validation`
  验证与自检工具。

## 当前主要 Prefab 入口
### 塔与敌人
- `Assets/Prefabs/TowerDefense/Runtime/RelayTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/SingleTargetTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/SlowFieldTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/BombardTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/Enemies/*`

### 战斗反馈
- `Assets/Prefabs/TowerDefense/Vfx/ShotTrace.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/SlowPulse.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/BombProjectile.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/BombExplosion.prefab`

## 当前共享资产入口
- `Assets/Resources/TowerDefense/Configs/TowerPresentationCatalog.asset`
- `Assets/Resources/TowerDefense/Configs/TowerDefenseHudTheme.asset`
- `Assets/Resources/TowerDefense/Configs/TowerDefenseHudCopy.asset`
- `Assets/Resources/TowerDefense/Configs/TowerPlacementVisualTheme.asset`
- `Assets/Resources/TowerDefense/Configs/EnemyCatalog.asset`
- `Assets/Resources/TowerDefense/Configs/WaveCatalog.asset`
- `Assets/Resources/TowerDefense/Configs/Waves/WaveCatalog_Level01.asset` 到 `WaveCatalog_Level05.asset`
- `Assets/Resources/TowerDefense/Configs/LevelSelectCatalog.asset`
- `Assets/Resources/TowerDefense/Configs/StoryTowerDefenseCampaign.asset`

## 当前系统怎么理解
### 1. 总控不是再包办一切
`TowerDefenseGame` 现在更像装配层和门面层。  
输入、表现、放置、供电、地图、敌人能力这些逻辑都已经下沉到独立模块。

### 2. 地图优先让 Scene 承担
路径点、出怪口、防御点、BuildZone、UI 骨架都优先由场景显式承载。  
后续做地图时，优先直接改 Scene，而不是先回脚本。

当前这条工作流又往前走了一步：
- `BuildZone` 不再只适合单矩形
- 现在可以用 `ZoneShapes` 根节点下的多个 `Collider2D` 拼不规则建造区

### 3. 塔是“共享逻辑 + 独立 prefab”
三类战斗塔共用 `DefenseTower`，但已经使用各自独立运行时 prefab。  
所以以后换美术时，优先改 prefab 和调参，不必为了三种塔复制三份玩法脚本。

### 4. 敌人是“基础壳 + 模块组合”
敌人系统现在不再推荐“每种敌人一份大脚本”。  
当前做法是：
- 基础壳：`Enemy`
- 静态目录：`EnemyCatalogAsset`
- 特殊模块：
  - `EnemyStealthModule`
  - `EnemyShieldAuraModule`
  - `EnemyRepairModule`
  - `EnemySplitOnDeathModule`

这样：
- 狼、重甲怪这类只有被动特征的敌人，不需要额外模块
- 旗帜怪、机械师、隐身怪、憎恶这类带特殊机制的敌人，直接在 prefab 上组合模块

### 5. 地图可读性表现是“双模式”
路径、出怪口、防御点的可读性表现现在不是只有一种程序化占位形式了。  
当前支持：
- 程序化占位模式
- 作者自己接管 `readabilityRootReference` 根节点的模式

这意味着你以后换正式场景资源时，不需要一定删脚本，只要把程序化开关关掉，再接自己的根节点即可。

## 当前编辑器友好状态
- 主菜单与关卡选择页已经基本收口到“Scene 主导、脚本只接行为”。
- HUD 已支持向显式文本块结构继续收口。
- `EnemyEditor` 和敌人模块专用 Inspector 已补齐。
- `TowerDefenseGameEditor` 能直接提醒 HUD 当前还是旧单块文本，还是已拆成多块。
- `BuildZoneEditor` 已补入，不规则建造区工作流已显式化。
- `WaveSpawnerEditor` 现在会直接强调：地图在 Scene 中做，波次在资产中做。

## 当前最值得记住的事实
- 核心塔防玩法主链已经成型。
- 敌人系统的目录与模块链已经成型。
- UI 和总控的显式接线工作流已经明显稳定。
- 后续工作的重点已经不是重新推翻结构，而是继续做内容、验证和正式美术替换。
