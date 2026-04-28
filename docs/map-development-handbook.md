# 地图开发手册
Updated: 2026-04-27

## 这份手册是干什么的
这份手册专门面向你后续自己在 Unity 编辑器里制作塔防关卡地图。

它重点回答这些问题：
- 新做一关时，先改什么，后改什么
- 哪些内容在 Scene 里做，哪些内容在资产里做
- 可建造区怎么做成不规则形状
- 路径、出怪口、防御点怎么摆
- 波次和敌人怎么配置
- 做完一关后怎么自检

## 一、先理解当前项目的地图工作流
当前项目已经明确分成两类内容：

### 1. Scene 里做的内容
- 地图地形和装饰
- 敌人路径
- 出怪口
- 防御点
- 可建造区
- 禁建区
- 场景里的 UI 骨架和可读性表现根节点

### 2. 资产里做的内容
- 敌人种类定义：`EnemyCatalog.asset`
- 当前关的波次：`WaveCatalog_LevelXX.asset`
- 塔卡展示：`TowerPresentationCatalog.asset`
- HUD 主题与文案：`TowerDefenseHudTheme.asset`、`TowerDefenseHudCopy.asset`

一句话记忆：
- 地图形状在 Scene 里做
- 敌人组合和波次节奏在资产里做

## 二、新做一关的推荐顺序
推荐按这个顺序做：

1. 复制一个已有玩法场景作为新关基础
2. 先把地图骨架对象整理好
3. 再摆路径、出怪口、防御点
4. 再做可建造区和禁建区
5. 再配波次资产
6. 最后再铺美术和做可读性接管

不要一上来先堆美术。
先把玩法骨架接通，后面返工最少。

## 三、场景里必须有的关键对象
一关最少要保证这些对象存在：

- `GameController`
- `BattlefieldMap`
- `BuildZone`
- 一个或多个 `EnemyPath`
- 一个或多个 `EnemySpawnGate`
- 一个或多个 `DefensePointFlag`
- `PlacedTowers`
- `PlacementPreviewRoot`
- `EnemiesRoot`
- `WaveSpawner`

如果你是从现有关卡复制出来，通常这些骨架已经有了。
你主要是在此基础上改位置、改结构、改引用。

## 四、BattlefieldMapDefinition 怎么配
选中 `BattlefieldMap` 上的 `BattlefieldMapDefinition`。

它是当前地图的总入口，负责收口：
- `BuildZone`
- `SpawnGates`
- `DefensePoints`

推荐工作流：

1. 先在场景层级里把这些对象摆好
2. 再点 `BattlefieldMapDefinitionEditor` 里的：
   - `Collect Scene References`
3. 看 Inspector 顶部摘要是否正确

如果摘要里：
- `BuildZone=None`
- `SpawnGates=0`
- `DefensePoints=0`

说明当前地图骨架还没接完整。

## 五、可建造区怎么做
当前 `BuildZone` 支持两种方式。

### 方式 A：简单矩形
适合：
- 临时原型
- 大块规则空地

做法：
- 在 `BuildZone` 根对象上直接挂一个 `Collider2D`
- 通常用 `BoxCollider2D`

### 方式 B：不规则建造区
适合：
- 真实关卡
- 弯曲、碎片化、复杂形状空地

做法：

1. 选中 `BuildZone`
2. 用 `BuildZoneEditor` 点击：
   - `Assign / Create ZoneShapes Root`
3. 在 `ZoneShapes` 下面摆多个碰撞体：
   - `BoxCollider2D`
   - `PolygonCollider2D`
   - `CircleCollider2D`
   - `CompositeCollider2D`
4. 点击：
   - `Collect Zone Shape Colliders`

这之后，这些碰撞体的并集就是可建造区。

### 什么时候用 Tilemap
如果你只是想铺视觉地面，Tilemap 可以用。
但当前玩法判定主链不依赖 Tilemap。

推荐理解：
- Tilemap 负责画地板和道路外观
- `BuildZone / PlacementBlocker / EnemyPath` 负责玩法判定

所以：
- 用不用 Tilemap 都行
- 不需要为了当前项目强行改成 Tilemap 工作流

## 六、禁建区怎么做
当前禁建区通过 `PlacementBlocker` 处理。

适合放在这些对象上：
- 路径区域
- 基地核心区域
- 不允许放塔的场景建筑
- 未来的特殊机关区

做法：

1. 选中你要禁止建造的对象
2. 给它加一个 `Collider2D`
3. 再加一个 `PlacementBlocker`
4. 在 `blockerReason` 里写清楚提示文案

这样玩家拖塔到那里时，会直接收到明确提示。

## 七、敌人路径怎么做
当前推荐用 `EnemyPath + Waypoints`。

做法：

1. 在场景里创建或选中一个 `EnemyPath`
2. 用 `EnemyPathEditor` 点击：
   - `Assign / Create Waypoints Root`
3. 在 `Waypoints` 下摆路径点子物体
4. 路径点顺序按 Hierarchy 顺序决定
5. 点：
   - `Refresh Path Visuals`

### 路径可读性有两种模式

#### 1. 程序化占位模式
保持：
- `proceduralReadabilityOverlay = true`

作用：
- 自动画线
- 自动画箭头
- 自动画转角热点

适合：
- 原型阶段
- 快速搭关卡

#### 2. 作者接管模式
设置：
- `proceduralReadabilityOverlay = false`

然后：
- 自己准备 `readabilityRootReference`
- 在这个根节点下放正式场景资源

适合：
- 正式美术阶段
- 想完全控制场景表现

## 八、出怪口怎么做
当前出怪口用 `EnemySpawnGate`。

做法：

1. 在场景中摆一个出怪口对象
2. 挂 `EnemySpawnGate`
3. 接好：
   - `enemyPathReference`
   - `targetDefensePointReference`
4. 如果要可读性占位，点：
   - `Assign / Create Readability Root`
   - `Refresh Marker`

### 出怪口可读性也有双模式
- `proceduralReadabilityMarker = true`
  继续用脚本生成占位标记
- `proceduralReadabilityMarker = false`
  由你自己接管 `readabilityRootReference`

## 九、防御点怎么做
当前防御点用 `DefensePointFlag`。

做法和出怪口类似：

1. 在场景中摆一个防御点对象
2. 挂 `DefensePointFlag`
3. 如果要可读性占位，点：
   - `Assign / Create Readability Root`
   - `Refresh Marker`

同样支持双模式：
- `proceduralReadabilityMarker = true`
- `proceduralReadabilityMarker = false`

## 十、波次怎么做
当前波次主链不建议直接改场景脚本数组。
推荐直接改每关自己的波次资产。

例如：
- `WaveCatalog_Level01.asset`
- `WaveCatalog_Level02.asset`
- `WaveCatalog_Level03.asset`
- `WaveCatalog_Level04.asset`
- `WaveCatalog_Level05.asset`

### 波次配置流程

1. 先确认当前关场景里的 `WaveSpawner`
2. 看它引用的是哪一个 `WaveCatalog_LevelXX.asset`
3. 打开对应资产
4. 配每一波的 `SpawnGroups`

每个 `SpawnGroup` 主要决定：
- 敌人类型
- 数量
- 刷新间隔

### 敌人种类在哪里改
敌人类型本身不在波次资产里定义。
它们来自：
- `EnemyCatalog.asset`

你要改：
- 血量
- 速度
- 护甲
- 奖励
- 是否隐身
- 是否护盾光环

优先改 `EnemyCatalog.asset`。

## 十一、敌人 prefab 怎么配合地图
地图开发时，你通常不需要去改敌人逻辑脚本。
你只需要知道：

- 波次资产决定刷哪种敌人
- 敌人目录决定这类敌人的默认属性
- 敌人 prefab 决定它的外观和本地机制覆盖

如果你想在某一关里测试特殊怪的表现：
- 先在波次资产里把它配进去
- 再进入 Play 看效果

## 十二、做美术时怎么接管当前程序化占位
当前地图相关的可读性系统都支持“从程序化占位过渡到作者接管”。

推荐顺序：

1. 先保留程序化占位，把地图搭通
2. 再逐步关掉：
   - `proceduralReadabilityOverlay`
   - `proceduralReadabilityMarker`
3. 把自己的正式场景资源挂到：
   - `readabilityRootReference`

这样不会一下子把关卡可读性全弄没。

## 十三、做完一关后的最小自检
至少检查这些：

### 静态检查
- `BattlefieldMapDefinition` 摘要正确
- `BuildZone` 引用正确
- 路径点数量和层级顺序正确
- 出怪口有路径引用
- 防御点存在
- `WaveSpawner` 引用的波次资产正确

### Play 检查
- 首塔可以正常放下
- 不规则建造区边界符合预期
- 路线预告按预期显示和隐藏
- 敌人从正确出怪口进入
- 敌人沿正确路径走
- 到达防御点会正常结算
- 波次顺序和敌人种类正确

## 十四、常见错误和排查
### 1. 地图里明明是空地，但不能放塔
先查：
- `BuildZone` 是否覆盖到那里
- 有没有 `PlacementBlocker`
- 是否在继电器供电范围内

### 2. 敌人不刷
先查：
- `WaveSpawner` 的 `WaveCatalogAsset`
- `EnemyCatalogAsset`
- `EnemiesRoot`
- `BattlefieldMapDefinition`

### 3. 敌人刷了但不走
先查：
- `EnemyPath`
- `Waypoints`
- 路径点顺序

### 4. 可读性标记不显示
先查：
- `showReadabilityOverlay / showReadabilityMarker`
- `proceduralReadabilityOverlay / proceduralReadabilityMarker`
- `readabilityRootReference`

### 5. 关卡看起来正常，但实际波次不对
先查：
- 当前场景 `WaveSpawner` 接的是不是正确的 `WaveCatalog_LevelXX.asset`

## 十五、最推荐你的实际工作方法
如果你现在就要开始自己做一关，最推荐这样做：

1. 复制一个现有 `Level0X.unity`
2. 先整理 `BattlefieldMap`
3. 用 `BuildZone + ZoneShapes` 定可建造区
4. 用 `PlacementBlocker` 扣路径和建筑禁建区
5. 摆 `EnemyPath + Waypoints`
6. 摆 `EnemySpawnGate`
7. 摆 `DefensePointFlag`
8. 配 `WaveCatalog_LevelXX.asset`
9. 最后再铺美术和接管可读性根节点

这就是按当前项目结构，最稳、最省返工的地图开发流程。
