# 项目技术学习手册
Updated: 2026-04-27
Scope: 这是一份只面向你个人学习使用的本地手册，不作为项目正式交付文档。

## 这份手册怎么用
这份手册的目标不是把所有概念一次讲完，而是帮你用“项目里的真实代码”去学技术。

建议学习方式：
1. 先按“推荐学习顺序”走，不要一上来全看
2. 每学一个技术点，都去项目里找到对应脚本或场景
3. 先看它“解决了什么问题”，再看“代码怎么写”
4. 最后自己动手改一个小点验证理解

最重要的原则：
- 先理解职责，再理解语法
- 先理解为什么这样分层，再理解每个函数细节
- 优先在 Unity 里看 Scene / Inspector 结果，再回代码找原因

---

## 一、先总览：这个项目到底用了哪些技术
这个项目当前主要用到这些技术：

### Unity 与引擎层
- Unity 2022.3
- 2D 项目工作流
- Scene 场景编辑
- Prefab 运行时实体
- Inspector 序列化字段
- Gizmos 场景可视化辅助
- LineRenderer 程序化可读性表现

### 编程与架构层
- C#
- MonoBehaviour 组件脚本
- 组件化设计
- 数据驱动设计
- 场景显式引用装配
- 运行时门面 / 协调器模式

### 数据与资源层
- ScriptableObject
- Prefab
- 共享配置资产
- 每关独立配置资产

### 2D 物理与空间判定
- Collider2D
- BoxCollider2D
- CircleCollider2D
- PolygonCollider2D
- CompositeCollider2D
- Trigger 判定
- Physics2D Overlap 检测

### UI 层
- UGUI
- TextMeshPro
- Button / Image / Text
- 场景化 UI 骨架
- HUD 数据展示

### 编辑器扩展层
- 自定义 Editor
- `OnValidate`
- `ContextMenu`
- `AssetDatabase`
- `Undo`
- `EditorSceneManager`

### 流程与版本协作层
- Git
- 分支快照工作流
- Pull Request 约束下的提交流程

---

## 二、推荐学习顺序
不要乱跳，推荐按这个顺序学：

1. Unity 场景、Prefab 和 Inspector 基础
2. C# + MonoBehaviour 组件脚本
3. ScriptableObject 数据驱动
4. 2D 物理与建造判定
5. UGUI / TextMeshPro 界面系统
6. 自定义 Editor 与作者工作流
7. 组件化架构与运行时装配
8. 敌人模块化机制设计
9. 关卡资产与波次配置工作流

这样学的原因是：
- 前三步决定你能不能读懂项目的大部分结构
- 中间三步决定你能不能自己做地图和 UI
- 后三步决定你能不能真正理解“为什么这个项目这样组织”

---

## 三、第一阶段：Unity 场景、Prefab、Inspector
### 你要先学什么
- Scene 是什么
- Hierarchy 里对象如何组成父子结构
- Prefab 是什么
- Inspector 里序列化字段怎么工作
- 场景引用和 Prefab 引用有什么区别

### 这个项目里对应哪里
- 场景：
  - `Assets/Scenes/MainMenu.unity`
  - `Assets/Scenes/LevelSelect.unity`
  - `Assets/Scenes/SampleScene.unity`
- 运行时 Prefab：
  - `Assets/Prefabs/TowerDefense/Runtime`
  - `Assets/Prefabs/TowerDefense/Vfx`

### 你应该观察什么
- `SampleScene` 里的 `BattlefieldMap`
- `GameController`
- `RuntimePrototypes`
- 各种 `...Reference` 字段在 Inspector 里是怎么接线的

### 你可以做的练习
1. 打开 `SampleScene`
2. 点开一个运行时原型对象
3. 看它的 Inspector 里有哪些显式引用
4. 再看对应脚本里这些字段是怎么用的

### 学会的标志
如果你能分清：
- 哪些对象是场景作者对象
- 哪些对象是运行时 prefab
- 哪些字段是“必须接线”的

那这一阶段就过了。

---

## 四、第二阶段：C# 与 MonoBehaviour 组件脚本
### 你要先学什么
- 类、字段、方法
- `private / public / SerializeField`
- `Awake / Start / Update / OnEnable / OnDisable / OnValidate`
- 组件脚本为什么适合 Unity

### 这个项目里优先看哪里
- `Assets/Scripts/TowerDefense/Core/TowerDefenseGame.cs`
- `Assets/Scripts/TowerDefense/Map/BattlefieldMapDefinition.cs`
- `Assets/Scripts/TowerDefense/UI/MainMenuController.cs`

### 你应该重点理解什么
- Unity 不是“一个 main 函数从头跑到尾”
- 而是很多组件挂在对象上，各自接收生命周期回调
- 当前项目已经尽量把职责拆散，不再把所有逻辑堆在一个脚本里

### 你可以做的练习
- 选一个小脚本，例如 `DefensePointFlag.cs`
- 先不看细节，只回答：
  - 它挂在哪类对象上
  - 它主要负责什么
  - 它不负责什么

### 学会的标志
如果你能看一个脚本先判断“职责边界”，而不是一行一行死读，就说明进步很大。

---

## 五、第三阶段：ScriptableObject 数据驱动
### 你要先学什么
- 什么是 ScriptableObject
- 为什么它适合存静态配置
- 为什么它比把所有参数都写死在脚本里更适合迭代

### 这个项目里优先看哪些资产和脚本
- 脚本：
  - `TowerPresentationCatalogAsset.cs`
  - `TowerDefenseHudThemeAsset.cs`
  - `TowerDefenseHudCopyAsset.cs`
  - `EnemyCatalogAsset.cs`
  - `WaveCatalogAsset.cs`
  - `LevelSelectCatalogAsset.cs`
- 资产：
  - `Assets/Resources/TowerDefense/Configs/*`

### 你应该重点理解什么
当前项目把“共享静态内容”尽量抽到了资产里，例如：
- 敌人默认属性
- 波次内容
- HUD 主题
- 关卡卡片目录

这就是典型的数据驱动思路：
- 代码负责规则
- 资产负责数据

### 你可以做的练习
- 打开 `EnemyCatalog.asset`
- 随便看一种敌人的参数
- 再去找 `EnemyCatalogAsset.cs`
- 看 Inspector 字段和运行时只读属性是怎么对应的

### 学会的标志
如果你能说清楚“这个参数为什么放在资产里，而不是直接写进 `Enemy.cs`”，就说明理解到了关键。

---

## 六、第四阶段：2D 物理与地图建造判定
### 你要先学什么
- Collider2D 基础
- Trigger 判定
- `OverlapPoint`
- `Physics2D.OverlapCircleNonAlloc`
- 场景几何边界如何参与玩法判断

### 这个项目里优先看哪里
- `BuildZone.cs`
- `PlacementBlocker.cs`
- `TowerPlacementRules.cs`

### 当前项目里这些技术是怎么用的
- `BuildZone`
  定义“原则上能建造的大区域”
- `PlacementBlocker`
  从大区域里扣掉局部禁建区
- `TowerPlacementRules`
  负责最终判断“这个点能不能放”

### 你特别值得学的点
这次项目已经把 `BuildZone` 升级成：
- 单个 Collider 回退模式
- `ZoneShapes` 多碰撞体组合模式

这是一种很典型的“为了作者工作流，放弃写死规则矩形”的做法。

### 你可以做的练习
1. 给 `BuildZone` 新建 `ZoneShapes`
2. 放 2 到 3 个不同形状的碰撞体
3. 点击 `Collect Zone Shape Colliders`
4. 进 Play 测一下边界是不是按你想的工作

### 学会的标志
如果你能自己做出一个不规则可建造区，并理解它为什么不用改玩法算法，那这一块就学到位了。

---

## 七、第五阶段：UGUI 与 TextMeshPro
### 你要先学什么
- Canvas
- RectTransform
- Button / Image / TMP_Text
- TextMeshPro 的文本显示优势
- 场景化 UI 和运行时数据写入的区别

### 这个项目里优先看哪里
- `MainMenuController.cs`
- `LevelSelectController.cs`
- `TowerDefenseHudPresenter.cs`
- `TowerShopCard.cs`

### 你应该重点理解什么
当前项目的 UI 不是“代码全自动生成一切”，而是：
- 场景骨架尽量真实存在
- 脚本只接行为和状态写入

这是一种很适合你自己后续手调 UI 的方式。

### 你可以做的练习
- 打开 `LevelSelect.unity`
- 选中一张关卡卡片
- 看 Scene 和 Inspector 里是如何分离“布局”和“逻辑”的

### 学会的标志
如果你能说清楚：
- 哪些 UI 是场景作者对象
- 哪些 UI 文案来自共享资产
- 哪些只是运行时写入

说明你已经理解这套 UI 工作流了。

---

## 八、第六阶段：自定义 Editor 扩展
### 你要先学什么
- `CustomEditor`
- `SerializedObject`
- `SerializedProperty`
- `EditorGUILayout`
- `Undo`
- `EditorSceneManager`
- 为什么作者工具对大型 Unity 项目很重要

### 这个项目里优先看哪里
- `TowerDefenseGameEditor.cs`
- `BuildZoneEditor.cs`
- `BattlefieldMapDefinitionEditor.cs`
- `EnemyPathEditor.cs`
- `EnemyEditor.cs`
- `EnemyMechanicModuleEditors.cs`

### 你应该重点理解什么
这个项目大量自定义 Editor 的目的不是“炫技”，而是：
- 把关键状态摘要抬到 Inspector 顶部
- 把重复作者操作做成按钮
- 把错误接线尽早暴露

### 你可以做的练习
- 点开 `BuildZone` 看 `BuildZoneEditor`
- 点开 `Enemy` 看 `EnemyEditor`
- 点开敌人模块看参数来源提示

### 学会的标志
如果你能理解“为什么这个按钮放在 Inspector 上，而不是让用户自己去层级里慢慢找”，那就抓到自定义 Editor 的核心价值了。

---

## 九、第七阶段：组件化架构与运行时装配
### 你要先学什么
- 门面层 / 协调器 / 支持组件的基本思想
- 为什么要把输入、表现、放置、供电拆开
- 为什么显式场景引用比对象名查找更稳

### 这个项目里优先看哪里
- `TowerDefenseGame.cs`
- `TowerDefenseSceneBootstrapper.cs`
- `TowerDefenseInputCoordinator.cs`
- `TowerDefensePresentationCoordinator.cs`
- `TowerPlacementSupportCoordinator.cs`

### 你应该重点理解什么
当前项目不是没有“总控”，而是：
- 有总控
- 但总控尽量只负责串系统，不再自己包办细节

这是你以后做中型 Unity 项目非常值得学的一点。

### 学会的标志
如果你能说清楚：
- 为什么 `TowerDefenseGame` 还存在
- 但为什么又不应该把所有逻辑都继续塞回它

说明你已经开始理解架构层了。

---

## 十、第八阶段：敌人模块化设计
### 你要先学什么
- 为什么不是每种敌人一个大脚本
- 什么叫“基础壳 + 机制模块”
- 什么叫“目录默认值 + prefab 本地覆盖值”

### 这个项目里优先看哪里
- `Enemy.cs`
- `EnemyCatalogAsset.cs`
- `EnemyMechanicModule.cs`
- 四个敌人机制模块
- `EnemyEditor.cs`
- `EnemyMechanicModuleEditors.cs`

### 你应该重点理解什么
这套设计的关键不是语法，而是思想：
- 共同部分共享
- 差异部分拆模块
- 参数优先资产化
- 特例允许 prefab 本地覆盖

### 你可以做的练习
- 打开 `BannerScavengerEnemy.prefab`
- 看它为什么既有目录定义，又有模块参数入口
- 再对比 `WolfEnemy.prefab`
- 看为什么它没有模块也能表达“忽略减速”这种被动特征

### 学会的标志
如果你能自己回答：
- 什么情况适合加新模块
- 什么情况只改目录参数就够了

那你已经真的理解这套敌人系统了。

---

## 十一、第九阶段：关卡资产与波次工作流
### 你要先学什么
- “地图形状在 Scene，波次在资产”这条边界
- 为什么关卡设计不一定所有东西都放在场景里

### 这个项目里优先看哪里
- `WaveSpawner.cs`
- `WaveSpawnerEditor.cs`
- `WaveCatalogAsset.cs`
- `WaveCatalog_Level01.asset` 到 `WaveCatalog_Level05.asset`

### 你应该重点理解什么
很多初学者会觉得：
- “既然地图在 Scene 做，那波次也该全在 Scene 里”

但当前项目不是这样。
这里更合理的分工是：
- Scene 负责空间
- 资产负责刷怪节奏

### 学会的标志
如果你能自己配一波新敌人组合，并理解为什么改的是资产不是脚本，就说明这部分已经过关。

---

## 十二、你后续最推荐的学习实践路线
如果你想边学边做，最推荐这样练：

### 第一轮练习：只动 Scene
- 自己复制一关
- 改路径
- 改出怪口
- 改防御点
- 改 `BuildZone`

### 第二轮练习：只动资产
- 改波次资产
- 改敌人目录
- 改 HUD 主题和塔展示资产

### 第三轮练习：小改脚本
- 试着自己给一个敌人模块加一个新字段
- 试着自己在 `BuildZoneEditor` 里加一个小提示

这样学得最快，也最不容易被复杂代码压垮。

---

## 十三、学习时最应该避免的误区
- 不要一开始就硬啃所有脚本细节。
- 不要把“场景结构问题”误当成“玩法算法问题”。
- 不要一看到 ScriptableObject 就只背概念，不去看项目里真实资产。
- 不要只看代码，不去 Unity 编辑器里看 Inspector 实际效果。

---

## 十四、给你的一条最实用建议
你后续学习这个项目时，最有效的方法不是“从头读完整个代码库”，而是：

**每次只围绕一个明确问题学一层。**

例如：
- “这一关的路径怎么做？”
- “敌人为什么能隐身？”
- “为什么波次在资产里改？”
- “为什么这个建造区能做成不规则？”

这样你会学得更快，也更容易把这些技术真正变成你自己的能力。
