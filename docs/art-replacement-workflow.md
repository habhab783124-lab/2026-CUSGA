# 美术替换工作流
Updated: 2026-04-27

## 这份文档是干什么的
这份文档专门告诉你：
- 以后如果你要替换塔、敌人、UI、地图和菜单的美术资源
- 应该优先改哪些 Prefab、哪些资产、哪些 Inspector 字段
- 哪些地方已经收口到“不需要改玩法代码”的状态

目标只有一个：
让你以后换正式美术资源时，尽量不需要重写玩法脚本。

## 最推荐的替换顺序
1. 先替换运行时 prefab 外观
2. 再替换塔卡与 HUD 样式
3. 再替换地图可读性与场景装饰
4. 最后替换主菜单与关卡选择页

## 一、塔的美术入口
### 运行时 prefab
- `Assets/Prefabs/TowerDefense/Runtime/RelayTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/SingleTargetTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/SlowFieldTowerPrototype.prefab`
- `Assets/Prefabs/TowerDefense/Runtime/BombardTowerPrototype.prefab`

### 三类战斗塔
三类战斗塔现在共用 `DefenseTower` 主逻辑，但已经分成独立运行时 prefab。  
所以你以后换正式塔身外观时，优先改各自 prefab 和 `DefenseTower` 的对应 tuning。

重点入口：
- `singleTargetTuning.bodySprite`
- `slowFieldTuning.bodySprite`
- `bombardTuning.bodySprite`

### 塔反馈与挂点
在 `DefenseTower` 这边，当前已显式收口这些视觉挂点：
- `bodyRendererReference`
- `feedbackRootReference`
- `typeSignatureRootReference`
- `levelMarkerRootReference`

如果你只是改外观、层级位置或反馈 prefab，优先改这些入口，不要先改玩法逻辑。

### 战斗反馈 prefab
- `Assets/Prefabs/TowerDefense/Vfx/ShotTrace.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/SlowPulse.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/BombProjectile.prefab`
- `Assets/Prefabs/TowerDefense/Vfx/BombExplosion.prefab`

## 二、敌人的美术入口
### 运行时敌人 prefab
每种敌人现在都有自己的运行时 prefab：
- `ScavengerEnemy.prefab`
- `WolfEnemy.prefab`
- `BannerScavengerEnemy.prefab`
- `MechanicEnemy.prefab`
- `HeavyArmoredMachineEnemy.prefab`
- `StealthStalkerEnemy.prefab`
- `AbominationEnemy.prefab`
- `SmallScavengerEnemy.prefab`

这意味着以后你改敌人外观时，优先改对应 prefab，而不是去复制敌人脚本。

### 全局敌人静态参数
如果你改的是“这类敌人的默认速度、血量、护甲、奖励、被动特征”，优先改：
- `Assets/Resources/TowerDefense/Configs/EnemyCatalog.asset`

### 敌人特殊机制参数
如果你改的是某个具体 prefab 的特殊机制参数，优先改 prefab 上挂着的模块：
- `EnemyStealthModule`
- `EnemyShieldAuraModule`
- `EnemyRepairModule`
- `EnemySplitOnDeathModule`

工作方式：
- `useLocalOverrides = false`
  继续吃 `EnemyCatalogAsset` 的默认值
- `useLocalOverrides = true`
  当前 prefab 本地参数优先生效

### 敌人编辑器入口
- `EnemyEditor`
  用来看这只 prefab 在目录里被识别成哪一类怪、有哪些被动特征、是否缺少应挂模块
- `EnemyMechanicModuleEditors`
  用来看模块当前到底在吃目录默认值还是本地覆盖值

如果目录要求某个敌人有模块，但 prefab 上没挂出来，`EnemyEditor` 里会出现：
- `Attach Missing Catalog Modules`

## 三、塔卡与 HUD 样式入口
### 塔卡
塔卡样式主要由两层决定：
- 共享目录：`TowerPresentationCatalog.asset`
- 场景卡片组件：`TowerShopCard`

`TowerShopCard` 当前已显式持有：
- `backgroundImageReference`
- `iconImageReference`
- `labelTextReference`
- `accentGraphicReferences`

### HUD
HUD 当前主要入口：
- `TowerDefenseHudTheme.asset`
- `TowerDefenseHudCopy.asset`

如果你要继续把 HUD 拆成更多显式场景文本块：
- 选中 `TowerDefenseGame`
- 用 `TowerDefenseGameEditor` 的 `Materialize HUD Split Texts`

## 四、地图与可读性表现入口
### 地图骨架
地图内容后续优先由 Scene 视图维护。  
所以地板、路径、建筑、阴影、装饰这些内容，优先改场景对象本身，而不是先改脚本。

### 可建造区形状
当前 `BuildZone` 已支持两种工作流：
- 简单地图：继续使用根对象上的默认 Collider
- 不规则地图：在 `ZoneShapes` 根节点下摆多个 `Collider2D`

推荐做法：
1. 选中 `BuildZone`
2. 用 `BuildZoneEditor` 创建或指定 `ZoneShapes`
3. 在其下摆 `BoxCollider2D / PolygonCollider2D / CompositeCollider2D / CircleCollider2D`
4. 点击 `Collect Zone Shape Colliders`

这样后续改地图形状时，你主要是在 Scene 里改碰撞体，而不是回玩法代码。

### 路径 / 出怪口 / 防御点可读性
当前这些脚本仍支持程序化占位表现，但已经开始支持显式作者接管：
- `EnemyPath`
- `EnemySpawnGate`
- `DefensePointFlag`

常见入口：
- `readabilityRootReference`
- `readabilityMaterialOverride`
- `autoCreateReadabilityRoot`
- `proceduralReadabilityOverlay`
- `proceduralReadabilityMarker`

现在这条链已经支持两种模式：
- 程序化占位开启
  继续使用脚本生成的程序化占位表现
- 程序化占位关闭
  保留根节点，但由你自己在 `readabilityRootReference` 下接正式场景资源

## 五、菜单与关卡页样式入口
### 主菜单
主菜单样式主要在：
- `MainMenu.unity`
- `MainMenuController`

当前已经显式暴露：
- 背景色
- 强调色
- 文字色
- Sprite
- 字体
- 文案

### 关卡选择页
关卡选择页主要在：
- `LevelSelect.unity`
- `LevelSelectController`
- `LevelSelectCatalog.asset`

页面骨架已经物化到场景里，后续布局优先直接改 Scene。

## 六、什么情况才需要回脚本
只有下面这些情况，才优先改代码：
- 你要新增一种全新的玩法表现规则
- 你要改变生成规则，而不是换资源
- 你要新增一种新的敌人特殊机制模块
- 你要改变塔或敌人的玩法逻辑本身

如果只是下面这些情况，通常不需要先改玩法代码：
- 换 Sprite
- 换材质
- 换颜色
- 换字体
- 调整 prefab 子层级
- 调整 Scene 里对象位置和排序

## 七、改完后的最小自检
1. 打开对应 prefab，看显式引用有没有丢。
2. 进 Unity Play：
   - 塔能否正常放置
   - 不规则建造区边界是否符合预期
   - 敌人血条是否正常
   - 三类塔反馈是否正常
   - 特殊敌人机制是否正常
3. 打开菜单和关卡页，看按钮、卡片、标题和文案是否仍正常显示。
