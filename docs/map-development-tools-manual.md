# 塔防地图开发工具使用手册

版本：1.0  
适用项目：`【塔防开发】` 当前工作区  
目标读者：关卡作者、策划、后续接手地图制作的人

## 1. 先看这份手册时你要知道什么

当前这套地图开发工具，已经覆盖了地图制作里最常见的几类工作：

1. 路径点收集、排序、插入、交换顺序
2. 怪物路径与道路对齐检查
3. 功能性道路段自动生成
4. 多出怪口 / 多防御点拓扑编辑
5. BuildZone / 禁建区快速绘制
6. 道路美术层铺设
7. 波次预览、波次资产调参
8. 当前关卡综合健康检查
9. 正式关卡报告导出

建议你把这些工具理解成三层：

- 第一层：`拓扑和路径`
- 第二层：`功能性路面和建造区`
- 第三层：`波次、平衡、报告、美术层`

不要一上来就先铺美术。  
最稳的顺序一定是：先结构，再功能，再数值，最后视觉。

---

## 2. 推荐的完整地图制作流程

如果你现在要从零做一关，建议按这个顺序：

1. 先更新 `docs/current-task-card.md`
2. 复述理解并等待用户回复 `执行`
3. 打开目标场景，例如 `Level02 / Level03 / Level04`
4. 用 `Level Topology Editor` 先把：
   - `SpawnGate`
   - `DefensePoint`
   - `EnemyPath`
   搭出来
5. 在 Scene 里摆路径点
6. 用 `Enemy Path Authoring Tool` 整理路径点顺序
7. 用 `Map Development Toolkit > Path Check` 检查怪路是否贴路
8. 用 `Map Development Toolkit > Road Build` 生成功能性 `PathSegment`
9. 用 `Map Development Toolkit > Zone Brush` 画可建造区和禁建区
10. 用 `Road Art Authoring Tool` 铺道路美术层
11. 用 `Wave Preview` 和 `Level Balance Tuning Console` 调波次与数值
12. 用 `Health Check` 和 `TowerDefenseValidationRunner` 做最终检查
13. 用 `Export Level Design Report` 导出策划报告

---

## 3. 工具总览

### 3.1 Enemy Path Authoring Tool

菜单入口：

- `Tools > Tower Defense > Enemy Path Authoring Tool`

用途：

- 把 Scene 里散放的路径点收成正式路径
- 调整路径点顺序
- 重排、交换、插入、追加路径点

适合什么时候用：

- 你已经摆好点，但顺序还乱
- 你想往现有路径里补几个点
- 你发现某两个点的前后顺序错了

---

### 3.2 Map Development Toolkit

菜单入口：

- `Tools > Tower Defense > Map Development Toolkit`

它是总工具箱，里面有 6 个页签：

1. `Path Check`
2. `Road Build`
3. `Template Sync`
4. `Health Check`
5. `Zone Brush`
6. `Wave Preview`

这是你平时最常开的窗口。

---

### 3.3 Level Topology Editor

菜单入口：

- `Tools > Tower Defense > Authoring > Level Topology Editor`

用途：

- 管理当前关卡的：
  - 出怪口
  - 防御点
  - 路径
- 编辑 `SpawnGate -> EnemyPath -> DefensePoint` 的关系

适合什么时候用：

- 第三关、第四关这种多入口、多终点地图
- 你想统一看当前关的拓扑，而不想来回点层级

---

### 3.4 Road Art Authoring Tool

菜单入口：

- `Tools > Tower Defense > Authoring > Road Art Authoring Tool`

用途：

- 在功能性道路之上，单独生成道路美术层

重要说明：

- 这个工具生成的是**视觉层**
- 不会替代 `PathSegment`
- 不负责怪物路线判定
- 不负责禁建区

也就是说：

- `PathSegment` 决定“能不能走、能不能建”
- `RoadArt` 决定“看起来像不像一条正式的路”

---

### 3.5 Level Balance Tuning Console

菜单入口：

- `Tools > Tower Defense > Authoring > Level Balance Tuning Console`

用途：

- 给策划和你自己调关卡数值
- 统一调整：
  - 开局资源
  - 基地生命
  - 建造成本
  - 继电器限制
  - 波次
  - 三种塔

重要说明：

- 现在波次调参已经优先走 `WaveCatalogAsset`
- Scene 里的 `waves` 只是兼容兜底

---

### 3.6 LevelRouteBlueprintApplier

菜单入口：

- `Tools > Tower Defense > Authoring > Apply Level03 Advanced Blueprint`
- `Tools > Tower Defense > Authoring > Apply Level04 Expanded Blueprint`

用途：

- 当你决定“大改一关路线骨架”时，用它快速重构

适合什么时候用：

- 不适合微调
- 适合整张图的结构重做

注意：

- 这是重型工具
- 用之前最好先留档

---

### 3.7 TowerDefenseValidationRunner

主要用途：

- 最终自动验证

它会检查：

- `MainMenu`
- `SampleScene`
- `Level02`
- `Level03`
- `Level04`
- prefab 主链
- 出怪口、路径、防御点合同
- 路线是否贴路

它更适合：

- 关卡阶段性完成后跑一次
- 准备交给别人继续做之前跑一次

---

## 4. Enemy Path Authoring Tool 详细用法

### 4.1 最常见用法：把选中的点收成一条路径

步骤：

1. 在 Scene 里先摆一串空物体作为路径点
2. 选中目标 `EnemyPath`
3. 打开 `Enemy Path Authoring Tool`
4. 在窗口顶部确认 `Current Path`
5. 再回到 Scene 里选中那批路径点
6. 在 `Collect Selected Points` 区选择排序规则
7. 点：
   - `用选中点替换当前路径`

排序规则怎么选：

- `HierarchyOrder`
  - 你已经手工排过层级顺序时用
- `LeftToRight`
  - 大致横向推进的路
- `TopToBottom`
  - 大致纵向推进的路
- `NearestChain`
  - 路线拐弯比较多时最好用

---

### 4.2 给现有路径追加新点

步骤：

1. 打开目标 `EnemyPath`
2. 选中新的点
3. 在工具里点：
   - `把选中点追加到当前路径`

---

### 4.3 两个点顺序错了

步骤：

1. 同时选中这两个已经属于路径的点
2. 打开工具
3. 在 `Quick Fix` 区点：
   - `交换两个已选路径点`

---

### 4.4 某个新点应该插到中间

步骤：

1. 选中新点
2. 再选中路径里那个“前一个点”
3. 在工具里点：
   - `已选点插到当前点后面`

---

### 4.5 直接拖拽改顺序

你也可以直接在 `Waypoint Order` 列表里拖拽。

改完后点：

- `应用当前列表到场景`

这一步很重要。  
因为只有写回 Scene 层级后，运行时才会真的按这个顺序走。

---

## 5. Map Development Toolkit 详细用法

## 5.1 Path Check

用途：

- 检查怪物路径和道路是否对齐

主要按钮：

- `Analyze Current Path`
- `Analyze Selected Segment`
- `Analyze All Paths In Map`
- `Snap Waypoints For All Point Issues`
- `Bulk Repair Current Scene`
- `Bulk Repair Selected EnemyPath`
- `Snap Selected Transform To Nearest Road`

推荐用法：

### 检查单条路径

1. 在 `Scene Context` 指定：
   - `Map`
   - `Enemy Path`
2. 点 `Analyze Current Path`

### 只检查某一段

1. 在 Scene 里选中两个相邻路径点
2. 点 `Analyze Selected Segment`

### 检查整张图

1. 指定 `Map`
2. 点 `Analyze All Paths In Map`

### 一键修点

如果只是路径点略微偏出道路，可以先试：

- `Snap Waypoints For All Point Issues`

如果问题更多，可以试：

- `Bulk Repair Selected EnemyPath`

注意：

- 这类自动修复适合“轻微错位”
- 不适合“整关路线设计错误”

---

## 5.2 Road Build

用途：

- 从路径点自动生成功能性道路段 `PathSegment`

主要字段：

- `Road Parent`
- `Road Template`
- `Turn Mode`
- `Auto Fit Turn Mode To Existing Roads`
- `Auto Inherit Road Thickness`
- `Auto Snap Spawn Gates`
- `Auto Snap Defense Point`
- `Road Thickness`
- `Replace Existing PathSegment_* Under Parent`

主要按钮：

- `Generate Road From Current Enemy Path`

推荐流程：

1. 先用 `Enemy Path Authoring Tool` 确认路径顺序
2. 在 `Path Check` 里确认基本没大错
3. 再进 `Road Build`
4. 设定 `Road Parent`
5. 点 `Generate Road From Current Enemy Path`

什么时候勾 `Replace Existing PathSegment_* Under Parent`：

- 你想整条路重建时勾上
- 你只是补一点新段时先别勾

---

## 5.3 Template Sync

用途：

- 把 `SampleScene` 的共享关卡骨架同步到 `Level02~04`

会同步什么：

- HUD
- 塔按钮
- prototype 引用
- `PlacedTowers`
- `PlacementPreviewRoot`
- `EnemiesRoot`
- `TowerDefenseGame / WaveSpawner` 共享接线

不会同步什么：

- 你的路径点坐标
- 关卡自己的路
- 塔位布局
- 地图装饰

推荐场景：

- 某关 UI 跑偏了
- 某关少了共享根对象
- 某关 prototype 引用断了

---

## 5.4 Health Check

用途：

- 检查当前关卡有没有结构性问题

主要按钮：

- `Check Current Scene`
- `Export Findings Markdown`
- `Export Level Design Report`

它会检查：

- 缺 `BuildZone`
- 缺 `SpawnGate`
- 缺 `DefensePoint`
- 路径点不在路上
- 出怪口不在第一点上
- HUD 是否和 `SampleScene` 漂移

推荐流程：

1. 关卡结构做完后点 `Check Current Scene`
2. 先修 `Error`
3. 再看 `Warning`
4. 最后导出：
   - `Export Findings Markdown`
   - 或 `Export Level Design Report`

区别：

- `Findings Markdown`
  - 更像修 Bug 清单
- `Level Design Report`
  - 更像给策划和自己看难度摘要

---

## 5.5 Zone Brush

用途：

- 快速画 `BuildZone` 或 `PlacementBlocker`

模式：

- `BuildZoneShape`
- `PlacementBlocker`

使用步骤：

1. 打开 `Zone Brush`
2. 选 `Brush Mode`
3. 点 `Start Brush`
4. 回到 Scene 里拖一个矩形
5. 结束后点 `Stop Brush`

适合：

- 快速做可建造区
- 快速做道路禁建区
- 快速做建筑遮挡区

---

## 5.6 Wave Preview

用途：

- 不进 Play 先看这一关波次压力

两种来源：

- `Preview From Current Scene WaveSpawner`
- `Preview From WaveCatalogAsset`

推荐现在用法：

- 优先点 `Preview From Current Scene WaveSpawner`
  - 因为现在它会优先吃 `WaveCatalogAsset`

你会看到：

- 每波怪数
- 总废料
- 各出怪口分配
- 波次备注

---

## 6. Level Topology Editor 详细用法

用途：

- 管理多出怪口 / 多防御点关卡的拓扑

这个窗口最适合第三关、第四关。

### 6.1 打开后先做什么

1. 打开目标关卡
2. 打开 `Level Topology Editor`
3. 点 `Adopt Active Scene`

这时窗口会自动抓当前场景里的：

- `Spawn Gates`
- `Defense Points`
- `Enemy Paths`

---

### 6.2 看当前关卡结构

窗口里你会直接看到：

- `Topology Summary`
- `Defense Points`
- `Spawn Gates`
- `Enemy Paths`
- `Target Usage Matrix`

最有用的是：

- 哪个 Gate 打哪个 DefensePoint
- 哪个 Gate 走哪条 Path
- 哪个 DefensePoint 当前根本没人打

---

### 6.3 把 Scene 里的当前顺序写回地图

按钮：

- `Apply Current Gate Order To Map`
- `Sort Gates By Name + Apply`
- `Sort Defense Points By Name + Apply`

什么时候用：

- 你手工改完场景层级后
- 想让 `BattlefieldMapDefinition` 的数组和当前层级顺序一致时

---

### 6.4 快速创建拓扑对象

按钮：

- `Create Spawn Gate`
- `Create Defense Point`
- `Create Enemy Path`

推荐用法：

1. 先建对象
2. 再在窗口里补 `Gate Id / Display Name / Path / Target`
3. 最后点 `Collect Scene References`

---

## 7. Road Art Authoring Tool 详细用法

用途：

- 在不影响玩法层的前提下，给道路铺正式美术

### 7.1 最重要的原则

先有：

- 正确路径
- 正确 `PathSegment`

再铺：

- `RoadArt`

不要反过来。

---

### 7.2 使用自己的美术 prefab

字段：

- `Straight Template`
- `Corner Template`
- `End Cap Template`

现在工具行为是：

- 保留你的表现组件
- 只剥掉会干扰玩法的组件

所以你自己的道路 prefab 可以带：

- `Animator`
- 粒子
- 音频
- 渲染组件
- 自己的表现脚本

工具只会去掉：

- Collider
- Rigidbody
- PlacementBlocker
- 玩法主链脚本

---

### 7.3 铺单条路

步骤：

1. 指定 `Target EnemyPath`
2. 设置模板 prefab
3. 点 `Generate From Selected EnemyPath`

---

### 7.4 给整张图铺路

步骤：

1. 打开目标场景
2. 设置模板 prefab
3. 点 `Generate For All Scene EnemyPaths`

---

### 7.5 清理旧美术层

按钮：

- `Clear Generated Road Art In Active Scene`

建议：

- 大改路线后先清一次
- 再重新铺

---

## 8. Level Balance Tuning Console 详细用法

用途：

- 给策划和你自己做当前关卡数值调整

入口：

- `Tools > Tower Defense > Authoring > Level Balance Tuning Console`

---

### 8.1 先绑定当前场景

按钮：

- `Adopt Current Scene`

先点这个，让窗口抓到：

- `TowerDefenseGame`
- `WaveSpawner`
- `BattlefieldMap`

---

### 8.2 常用区块

1. `Preset Difficulty Profiles`
   - `Simple`
   - `Standard`
   - `Hard`

2. `Core Economy And Placement`
   - 开局废料
   - 基地生命
   - 建造成本
   - 继电器限制

3. `Wave Tuning`
   - `initialDelay`
   - `delayBetweenWaves`
   - `routePreviewLeadTime`
   - `waveCatalogAsset`
   - `enemyCatalogAsset`
   - 波次列表

4. `Relay Prototype Tuning`

5. 三种塔各自的 Tuning

6. `Quick Batch Helpers`

---

### 8.3 现在怎么调波次

如果当前关已经接了 `WaveCatalogAsset`：

- 直接在 `Wave Tuning` 面板里改 `WaveCatalogAsset`

如果没接：

- 才改 `Fallback Scene Wave Array`

建议：

- 以后默认都接波次资产

---

## 9. LevelRouteBlueprintApplier 什么时候用

这个不是日常微调工具。  
它是“整关路线骨架重做工具”。

适合：

- 你决定整张图推倒重来
- 第三关、第四关要切到全新的路线草图

不适合：

- 微调几个点
- 顺手补一段路

做之前建议：

1. 先留档
2. 再执行 Blueprint
3. 然后重新跑：
   - `Topology Editor`
   - `Path Check`
   - `Health Check`

---

## 10. TowerDefenseValidationRunner 怎么用

这个更像“最终验收工具”。

它适合在这些时机跑：

1. 关卡大改后
2. 准备提交前
3. 交给别人继续做之前

它现在会覆盖：

- `MainMenu`
- `SampleScene`
- `Level02`
- `Level03`
- `Level04`

会检查：

- prefab 主链
- 共享场景壳
- 路径和道路贴合
- Gate/Path/DefensePoint 合同
- 多防御点、多路径、多入口场景结构

---

## 11. 你平时最常见的实际工作流

### 场景从零做一关

1. `Level Topology Editor`
2. `Enemy Path Authoring Tool`
3. `Map Development Toolkit > Path Check`
4. `Map Development Toolkit > Road Build`
5. `Map Development Toolkit > Zone Brush`
6. `Road Art Authoring Tool`
7. `Wave Preview`
8. `Level Balance Tuning Console`
9. `Health Check`
10. `Export Level Design Report`

### 大改一关

1. 留档
2. `LevelRouteBlueprintApplier`
3. `Level Topology Editor`
4. `Path Check`
5. `Road Build`
6. `Road Art Authoring Tool`
7. `Health Check`

### 只修“怪路和地面不一致”

1. `Enemy Path Authoring Tool`
2. `Path Check`
3. `Bulk Repair Selected EnemyPath`
4. 必要时 `Road Build`

---

## 12. 常见问题

### 问题 1：怪物路线没变

先检查：

1. 你是不是只改了工具窗口里的缓冲列表，没点“应用到场景”
2. `EnemyPath` 的层级顺序有没有真的写回 Scene

---

### 问题 2：道路看起来有了，但怪还是走偏

先检查：

1. `PathSegment` 是不是功能层
2. `RoadArt` 是不是只是视觉层

不要把 `RoadArt` 当成怪物路径判定。

---

### 问题 3：第三关、第四关拓扑很乱

不要硬在层级里找。  
直接开：

- `Level Topology Editor`

先看：

- 哪些 Gate 指错了 Path
- 哪些 Gate 指错了 DefensePoint

---

### 问题 4：策划调了波次但运行时没变

先确认：

1. 当前关有没有接 `WaveCatalogAsset`
2. 你改的是不是 `WaveCatalogAsset`，而不是旧的 Scene `waves`

---

## 13. 最后给你的建议

如果你只记住一句话，就记这个：

**先用拓扑和路径工具把关卡结构做对，再用路面和美术工具把关卡做漂亮，最后才是数值和平衡。**

这个顺序能帮你少返工很多。
