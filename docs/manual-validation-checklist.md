# 最终人工验证清单
Updated: 2026-04-27

## 使用方式
- 这份清单给你在 Unity 中逐项人工验证当前项目状态使用。
- 每一项后面都留有“结果”和“备注”，你可以直接把检查结果写回这份文档。
- 以后如果你让我“读取人工验证结果”，我就优先读取这份文档。

建议填写格式：
- `结果：通过`
- `结果：不通过`
- `备注：……`

---

## 一、编译与控制台
### 1. 项目打开后控制台
- 检查点：
  - 没有红色编译错误
  - 没有 Missing Script / Missing Reference
- 结果：
- 备注：

---

## 二、MainMenu 与 LevelSelect
### 1. MainMenu 静态检查
- 对象：`MainMenu / MainMenuController`
- 检查点：
  - 主题色、Sprite、字体、文案入口都可见
  - 场景中的主菜单对象层级真实存在
- 结果：
- 备注：

### 2. MainMenu Play 检查
- 检查点：
  - 主菜单正常显示
  - 点击开始后能正常跳转
- 结果：
- 备注：

### 3. LevelSelect 静态检查
- 对象：`LevelSelect / LevelSelectController`
- 检查点：
  - 已显式接上 `LevelSelectCatalog.asset`
  - 关卡卡片、返回按钮、标题等对象真实存在
- 结果：
- 备注：

### 4. LevelSelect Play 检查
- 检查点：
  - 能看到 5 张关卡卡片
  - 点击卡片能进对应关卡
  - 点击返回能回到主菜单
- 结果：
- 备注：

---

## 三、SampleScene 基础检查
### 1. GameController
- 对象：`SampleScene / GameController`
- 检查点：
  - `TowerDefenseGame` 关键场景引用都已接齐
  - `TowerPresentationCatalogAsset`
  - `TowerDefenseHudThemeAsset`
  - `TowerDefenseHudCopyAsset`
  - `TowerPlacementVisualThemeAsset`
- 结果：
- 备注：

### 2. RuntimePrototypes
- 检查点：
  - `RelayTowerPrototype`
  - `DefenseTowerPrototype`
  - `EnemyPrototype`
  - 这些原型对象层级正常、挂点正常
- 结果：
- 备注：

### 3. 部署卡
- 检查点：
  - `RelayTowerButton`
  - `DefenseTowerButton`
  - `SlowFieldTowerButton`
  - `BombardTowerButton`
  - 每张卡的 `TowerShopCard` 引用完整
- 结果：
- 备注：

### 4. BuildZone 静态检查
- 对象：`SampleScene / BattlefieldMap / BuildZone`
- 检查点：
  - `BuildZone` Inspector 里能看到 `ZoneShapes` 相关作者入口
  - 如果当前关卡使用不规则可建造区，`ZoneShapeColliders` 数量正确
- 结果：
- 备注：

---

## 四、Play 模式核心玩法
### 1. 放置与供电
- 检查点：
  - 首塔起始区可见
  - 继电器可正常放置
  - 战斗塔只能放在继电器供电范围内
  - 断电塔保留在场上但停止工作
  - 如果当前关卡使用不规则建造区，边界与 Scene 中实际形状一致
- 结果：
- 备注：

### 2. 路线预告
- 检查点：
  - 第一波出怪前约 2 秒显示路线
  - 相邻两波路线完全相同时，不重复显示
  - 出怪开始后路线自动隐藏
  - 如果关闭了程序化路径占位，作者接管的可读性根节点仍能正常显示
- 结果：
- 备注：

### 3. 三类战斗塔
- 检查点：
  - 单体塔有 tracer
  - 减速塔有范围脉冲
  - 炸弹塔有飞行物与爆炸
  - 升级后等级标记与类型签名正常
- 结果：
- 备注：

### 4. 资源与结算
- 检查点：
  - 敌人死亡会奖励废料
  - 建造、升级会消耗废料
  - 生命归零时 Game Over 正常显示
- 结果：
- 备注：

---

## 五、敌人 prefab 与 Inspector
### 1. WolfEnemy
- 检查点：
  - `EnemyEditor` 顶部能显示目录匹配摘要
  - 能看出 `Ignores Slow`
- 结果：
- 备注：

### 2. HeavyArmoredMachineEnemy
- 检查点：
  - `EnemyEditor` 能显示重甲与非穿甲伤害倍率
  - 能看出是否可被修理
- 结果：
- 备注：

### 3. BannerScavengerEnemy
- 检查点：
  - `EnemyEditor` 显示 `Shield Aura: Yes`
  - 下方能看到 `EnemyShieldAuraModule`
  - 模块面板能区分目录默认值 / 本地覆盖值
- 结果：
- 备注：

### 4. MechanicEnemy
- 检查点：
  - `EnemyEditor` 显示 `Repair: Yes`
  - 下方能看到 `EnemyRepairModule`
- 结果：
- 备注：

### 5. StealthStalkerEnemy
- 检查点：
  - `EnemyEditor` 显示 `Stealth: Yes`
  - 下方能看到 `EnemyStealthModule`
- 结果：
- 备注：

### 6. AbominationEnemy
- 检查点：
  - `EnemyEditor` 显示 `Split On Death: Yes`
  - 下方能看到 `EnemySplitOnDeathModule`
- 结果：
- 备注：

---

## 六、剧情-塔防切换
### 1. 剧情占位链
- 检查点：
  - `MainMenu` 能进入剧情-塔防交错链
  - `StoryInterludePlaceholder` 能继续推进到下一关塔防
- 结果：
- 备注：

### 2. 塔防清场后推进
- 检查点：
  - 清场后能根据当前活动流程继续推进到下一段
- 结果：
- 备注：

---

## 七、最终结论
### 当前版本是否可以继续进入内容制作 / 美术替换阶段
- 结果：
- 备注：

### 还需要我继续处理的问题
- 问题 1：
- 问题 2：
- 问题 3：
