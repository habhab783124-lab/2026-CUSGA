# 从零搭建对话 Demo 指南

## 适用范围

本文档用于指导你在当前 Unity 项目中，从素材准备开始，手动搭建一个最小可运行的 2D 对话 Demo。

目标效果：

- 玩家可以左右移动
- 玩家靠近 NPC 时显示交互提示
- 按 `E` 开始对话
- 对话框显示在 NPC 头顶并随 NPC 一起移动
- 对话文字逐字显示
- 鼠标左键可补齐当前句或进入下一句
- 对话结束后玩家恢复移动

当前最小实现主要依赖以下脚本：

- `Assets/PlayerController.cs`
- `Assets/NPCInteractable.cs`

以下脚本属于扩展用途，不是最小 Demo 的必需项：

- `Assets/DialogueManager.cs`
- `Assets/TypewriterEffect.cs`

---

## 一、主要项目结构

建议按下面的方式整理资源和脚本：

```text
Assets
├─ Scenes
│  └─ DialogueDemo.unity
├─ Art
│  ├─ Characters
│  │  ├─ Player
│  │  └─ NPC
│  └─ UI
│     ├─ Dialogue
│     └─ Prompt
├─ Fonts
│  └─ TMP_FontAssets
├─ Animations
│  ├─ Player
│  └─ NPC
├─ Prefabs
│  ├─ Characters
│  └─ UI
└─ Scripts
   ├─ PlayerController.cs
   ├─ NPCInteractable.cs
   ├─ DialogueManager.cs
   └─ TypewriterEffect.cs
```

说明：

- `Scenes` 放场景
- `Art` 放角色素材和 UI 素材
- `Fonts` 放 TMP 字体资源
- `Animations` 放动画片段和控制器
- `Prefabs` 放可复用角色和 UI 预制体
- `Scripts` 放逻辑脚本

---

## 二、推荐的场景层级

建议场景中的对象结构如下：

```text
DialogueDemo
├─ Main Camera
├─ Player
├─ NPC_A
│  ├─ InteractPrompt
│  │  └─ PromptCanvas
│  │     └─ PromptBg
│  │        └─ PromptText
│  └─ DialogueCanvas
│     └─ DialogueBackground
│        └─ DialogueText
└─ EventSystem
```

说明：

- `Player` 是玩家角色
- `NPC_A` 是可交互 NPC
- `InteractPrompt` 是 NPC 头顶提示
- `DialogueCanvas` 是 NPC 头顶对话框
- `EventSystem` 是 UI 系统常规对象，Unity 创建 UI 时通常会自动生成

---

## 三、对象与组件总表

| 对象 | 必需组件 | 作用 |
| --- | --- | --- |
| `Player` | `SpriteRenderer`、`Rigidbody2D`、`BoxCollider2D`、`PlayerController`、`Animator` | 玩家移动、翻转、动画 |
| `NPC_A` | `SpriteRenderer`、`BoxCollider2D`、`NPCInteractable`、`Animator`（可选） | 近距离交互、逐字对话 |
| `InteractPrompt` | 无 | 提示 UI 根节点，挂在 NPC 下 |
| `PromptCanvas` | `Canvas`、`CanvasScaler`、`GraphicRaycaster` | 世界空间提示框 |
| `PromptBg` | `Image` | 提示背景 |
| `PromptText` | `TextMeshProUGUI` | 提示文字 |
| `DialogueCanvas` | `Canvas`、`CanvasScaler`、`GraphicRaycaster` | 世界空间对话框 |
| `DialogueBackground` | `Image` | 对话框背景 |
| `DialogueText` | `TextMeshProUGUI` | 对话文字 |

---

## 四、素材准备

先准备最小素材，不要一开始就做完整正式资源。

| 用途 | 最低需要 | 推荐 |
| --- | --- | --- |
| 玩家角色 | 1 张静止图 | 1 组 Idle + 1 组 Walk |
| NPC 角色 | 1 张静止图 | 1 组 Idle |
| 提示框 | 无图也可以，纯色 `Image` 即可 | 1 张提示框背景图 |
| 对话框 | 无图也可以，纯色 `Image` 即可 | 1 张气泡或面板背景图 |
| 字体 | 1 个支持中文的 TMP Font Asset | 统一项目字体资源 |

### 4.1 素材导入建议

1. 所有角色图片设置为 `Texture Type = Sprite (2D and UI)`。
2. 如果是像素风：
   - `Filter Mode = Point (no filter)`
   - `Compression = None`
3. 如果是普通插画风：
   - `Filter Mode = Bilinear`
4. 玩家和 NPC 的 `Pixels Per Unit` 尽量统一。
5. 对话框背景如果后面要缩放不变形，建议使用 `9-slice`。
6. 第一次使用 TMP 时，如 Unity 提示导入 `TMP Essentials`，直接导入。
7. 如果没有中文字体，可先使用项目已有字体资源：
   - `Assets/txt/ark-pixel-12px-proportional-zh_cn SDF.asset`

---

## 五、创建基础场景

### 5.1 新建场景

1. 新建场景，保存为 `DialogueDemo.unity`
2. 保留 `Main Camera`
3. 如有需要，可添加简单背景或地面 Sprite 作为场景参考

### 5.2 配置 Layer 和 Sorting Layer

建议做以下设置：

1. 在 `Tags and Layers` 中新增一个 Layer：
   - `Player`
2. 在 `Sorting Layers` 中新增：
   - `Characters`
   - `WorldUI`

说明：

- `Player` Layer 用于 NPC 识别玩家
- `Characters` 用于玩家和 NPC 渲染排序
- `WorldUI` 用于提示框和对话框显示在角色前面

---

## 六、创建玩家 Player

### 6.1 创建对象

1. 在 `Hierarchy` 新建空对象
2. 命名为 `Player`
3. 设置 `Layer = Player`
4. 初始位置可设为：
   - `Position = (-3, -1.5, 0)`

### 6.2 添加组件

给 `Player` 添加以下组件：

- `SpriteRenderer`
- `Rigidbody2D`
- `BoxCollider2D`
- `Animator`
- `PlayerController`

### 6.3 配置 SpriteRenderer

1. 把玩家静止图拖到 `SpriteRenderer.Sprite`
2. 设置：
   - `Sorting Layer = Characters`
   - `Order in Layer = 10`

### 6.4 配置 Rigidbody2D

| 参数 | 建议值 |
| --- | --- |
| `Body Type` | `Dynamic` |
| `Gravity Scale` | `0` |
| `Collision Detection` | `Continuous` |
| `Freeze Rotation Z` | 勾选 |

说明：

- 本 Demo 只做横向移动，不做跳跃，所以重力设为 `0`

### 6.5 配置 BoxCollider2D

| 参数 | 建议值 |
| --- | --- |
| `Is Trigger` | 不勾 |
| `Size` | `0.8, 1.8` |
| `Offset` | `0, 0.9` |

说明：

- 具体数值需根据玩家图片实际尺寸调整
- 原则是碰撞盒下边缘贴近脚底

### 6.6 配置 PlayerController

在 `PlayerController` 中设置：

| 字段 | 建议值 | 说明 |
| --- | --- | --- |
| `Move Speed` | `5` | 左右移动速度 |
| `Use Boundary` | 勾选 | 防止跑出画面 |
| `Min X` | `-8` | 左边界 |
| `Max X` | `8` | 右边界 |
| `Horizontal Axis` | `Horizontal` | A/D 或左右方向键 |
| `Input Dead Zone` | `0.01` | 保持默认 |
| `Move Smoothing` | `20` | 响应速度 |
| `Animator` | 拖入 `Player` 上的 `Animator` | 便于动画切换 |
| `Walking Bool Parameter` | `isWalking` | 要和 Animator 参数一致 |
| `Move X Float Parameter` | 留空 | 初版可不使用 |
| `Is Frozen` | 不勾 | 对话时由脚本控制 |

说明：

- 当前 `PlayerController` 会自动处理左右翻转
- 最好让角色原图默认面向右侧

---

## 七、给玩家制作静止和行走动画

### 7.1 准备动画帧

至少准备两组素材：

- `Idle` 静止动画
- `Walk` 行走动画

### 7.2 创建动画片段

建议在 `Assets/Animations/Player` 下创建：

- `Player_Idle.anim`
- `Player_Walk.anim`

### 7.3 创建 Animator Controller

1. 新建 `Animator Controller`
2. 命名为 `Player.controller`
3. 挂到 `Player` 的 `Animator` 组件上

### 7.4 配置 Animator

1. 打开 Animator 窗口
2. 新增一个 `Bool` 参数：
   - `isWalking`
3. 创建两个状态：
   - `Idle`
   - `Walk`
4. 将 `Idle` 设为默认状态
5. 添加两个切换：
   - `Idle -> Walk` 条件：`isWalking == true`
   - `Walk -> Idle` 条件：`isWalking == false`
6. 对两个切换都设置：
   - `Has Exit Time = false`
   - `Transition Duration = 0` 或 `0.05`

说明：

- `PlayerController` 会自动在移动时设置 `isWalking`
- 如果动画不切换，先检查参数名是否一致

---

## 八、创建 NPC_A

### 8.1 创建对象

1. 在 `Hierarchy` 新建空对象
2. 命名为 `NPC_A`
3. 放在玩家右侧，例如：
   - `Position = (1, -1.5, 0)`

### 8.2 添加组件

给 `NPC_A` 添加：

- `SpriteRenderer`
- `BoxCollider2D`
- `NPCInteractable`
- `Animator`（可选）

### 8.3 配置 SpriteRenderer

1. 把 NPC 图片拖到 `SpriteRenderer.Sprite`
2. 设置：
   - `Sorting Layer = Characters`
   - `Order in Layer = 10`

### 8.4 配置 BoxCollider2D

| 参数 | 建议值 |
| --- | --- |
| `Is Trigger` | 必须勾选 |
| `Size` | `1.2, 2` |
| `Offset` | `0, 0.9` |

说明：

- `NPCInteractable` 依赖触发器范围检测
- 如果不勾 `Is Trigger`，靠近 NPC 时不会正常显示提示

---

## 九、创建提示 UI

提示 UI 负责在玩家靠近时显示“按 E 交互”。

### 9.1 创建 InteractPrompt 根对象

1. 在 `NPC_A` 下新建空对象
2. 命名为 `InteractPrompt`
3. 设置：
   - `Local Position = (0, 2.2, 0)`
   - `Local Scale = (0.01, 0.01, 0.01)`

### 9.2 创建 PromptCanvas

1. 在 `InteractPrompt` 下新建对象
2. 命名为 `PromptCanvas`
3. 添加组件：
   - `Canvas`
   - `CanvasScaler`
   - `GraphicRaycaster`
4. 设置 `Canvas.Render Mode = World Space`
5. 设置 `RectTransform` 尺寸为：
   - `180 x 40`

### 9.3 创建 PromptBg

1. 在 `PromptCanvas` 下创建 `UI > Image`
2. 命名为 `PromptBg`
3. 设置 `RectTransform` 尺寸：
   - `180 x 40`
4. 若没有背景图，可直接设置：
   - `Image.Color = 半透明黑色`
5. 若有提示框贴图，可拖到：
   - `Image.Source Image`

### 9.4 创建 PromptText

1. 在 `PromptBg` 下创建 `TextMeshPro - Text (UI)`
2. 命名为 `PromptText`
3. 设置：
   - `RectTransform` 尺寸：`170 x 30`
   - `Text = 按 E 交互`
   - `Alignment = Center`
   - `Color = 白色`
   - `Font Size = 24 ~ 32`
   - `Font Asset = 支持中文的 TMP 字体`

---

## 十、创建对话 UI

对话 UI 负责显示逐字对白。

### 10.1 创建 DialogueCanvas 根对象

1. 在 `NPC_A` 下新建空对象
2. 命名为 `DialogueCanvas`
3. 设置：
   - `Local Position = (0, 1.9, 0)`
   - `Local Scale = (0.01, 0.01, 0.01)`

### 10.2 添加 Canvas 相关组件

给 `DialogueCanvas` 添加：

- `Canvas`
- `CanvasScaler`
- `GraphicRaycaster`

并设置：

- `Canvas.Render Mode = World Space`
- `Canvas.Sorting Order = 50`

设置 `RectTransform` 尺寸：

- `260 x 120`

### 10.3 创建 DialogueBackground

1. 在 `DialogueCanvas` 下创建 `UI > Image`
2. 命名为 `DialogueBackground`
3. 设置 `RectTransform` 尺寸：
   - `260 x 120`
4. 若没有背景图，可设置：
   - `Image.Color = 半透明黑色`
5. 若有对话框贴图：
   - 拖到 `Image.Source Image`
6. 如果背景图是边框式 UI，建议：
   - 先设置 `9-slice`
   - 再将 `Image Type` 改为 `Sliced`

### 10.4 创建 DialogueText

1. 在 `DialogueBackground` 下创建 `TextMeshPro - Text (UI)`
2. 命名为 `DialogueText`
3. 设置：
   - `RectTransform` 尺寸：`240 x 100`
   - `Text = 空`
   - `Alignment = Top Left`
   - `Enable Word Wrapping = true`
   - `Font Size = 24 ~ 32`
   - `Color = 白色`
   - `Font Asset = 支持中文的 TMP 字体`

### 10.5 默认隐藏对话框

将 `DialogueCanvas` 设置为默认不激活：

- 取消 `DialogueCanvas` 左侧的勾选

说明：

- 对话开始时会由 `NPCInteractable` 自动激活
- 对话结束后会自动隐藏

---

## 十一、绑定 NPCInteractable 字段

选中 `NPC_A`，在 `NPCInteractable` 中按下表填写：

| 字段 | 填写内容 |
| --- | --- |
| `Interactable` | 勾选 |
| `Interaction Prompt` | `按 E 交互` 或你自己的文案 |
| `Player Layer` | 只勾选 `Player` |
| `Interact Prompt Root` | 拖入 `InteractPrompt` |
| `Interact Prompt Text` | 拖入 `PromptText` |
| `Dialogue Canvas Root` | 拖入 `DialogueCanvas` |
| `Dialogue Text` | 拖入 `DialogueText` |
| `Dialogue Lines` | 填对白内容 |
| `Typing Speed` | `0.05` |

### 11.1 对白填写方式

`Dialogue Lines` 中每一条就是一句对白。例如：

1. `你好，欢迎来到这里。`
2. `当你靠近我时，会显示交互提示。`
3. `按 E 可以开始对话。`
4. `左键可以补齐当前句，或者进入下一句。`

说明：

- 当前 `NPCInteractable` 已经自带逐字显示逻辑
- 这套最小 Demo 不需要额外挂 `TypewriterEffect`

---

## 十二、运行测试流程

建议按以下顺序验证：

1. 点击 Play
2. 按 `A/D` 或左右方向键，确认 `Player` 能左右移动
3. 让玩家进入 `NPC_A` 的触发器范围
4. 确认 `InteractPrompt` 出现
5. 按 `E` 开始对话
6. 确认玩家移动被锁定
7. 确认 `DialogueCanvas` 出现并逐字显示第一句
8. 在逐字显示过程中点击左键，确认当前句可立即补齐
9. 再次左键，确认可进入下一句
10. 最后一句结束后，确认对话框隐藏，玩家恢复移动

---

## 十三、常见问题排查

| 现象 | 常见原因 |
| --- | --- |
| 靠近 NPC 没有提示 | `NPC_A.BoxCollider2D` 没勾 `Is Trigger` |
| 靠近 NPC 没有提示 | `Player` 没有 `Rigidbody2D` |
| 靠近 NPC 没有提示 | `NPCInteractable.Player Layer` 没包含玩家所在层 |
| 按 `E` 没反应 | `Dialogue Lines` 为空 |
| 对话框不显示文字 | `Dialogue Text` 没绑定 |
| 中文显示为方块或乱码 | TMP 字体不支持中文 |
| 玩家移动但动画不切换 | `Walking Bool Parameter` 与 Animator 参数名不一致 |
| 玩家翻转方向不对 | 原始角色图默认朝向与脚本假设不一致 |

---

## 十四、可扩展部分

### 14.1 NPC 增加待机动画

这是最简单的扩展方式，不需要改脚本。

做法：

1. 给 `NPC_A` 添加 `Animator`
2. 准备 `NPC_Idle.anim`
3. 创建 `NPC.controller`
4. 把 `NPC_Idle` 设为默认状态并循环播放

效果：

- NPC 即使不说话，也会有轻微呼吸、摆动、眨眼等表现

### 14.2 NPC 增加说话动画

可做两种方案：

方案 A：仅美术循环

- 保持 NPC 一直播放待机或轻微口型动画
- 优点：不需要改代码
- 缺点：说话和不说话时状态一致

方案 B：脚本驱动动画状态

1. 给 `NPC_A.Animator` 增加一个 `Bool` 参数：
   - `isTalking`
2. 在 `NPCInteractable` 中扩展逻辑：
   - 对话开始时设 `isTalking = true`
   - 对话结束时设 `isTalking = false`

这样可以做：

- Idle
- Talk
- Happy
- Angry

等不同状态切换。

### 14.3 更换正式角色素材

替换角色后，通常要同步调整：

- `SpriteRenderer.Sprite`
- `BoxCollider2D.Size`
- `BoxCollider2D.Offset`
- `Transform.Position`
- `Animator Controller`
- `PlayerController.Animator`

说明：

- 图变了以后，碰撞盒几乎都需要重调
- 不建议直接沿用旧碰撞盒

### 14.4 更换对话框样式

主要修改以下对象：

- `PromptBg.Image`
- `PromptText`
- `DialogueBackground.Image`
- `DialogueText`

可调整内容包括：

- 背景图
- 字体
- 字号
- 字色
- 对齐
- 气泡大小
- 气泡位置
- 角色名
- 头像

### 14.5 做成屏幕固定对话框

当前方案是 NPC 头顶的 `World Space UI`。

如果你以后要做：

- 底部统一对话栏
- 剧情对话
- 多 NPC 共用一套对话框

则更适合改用：

- `Assets/DialogueManager.cs`
- `Assets/TypewriterEffect.cs`

### 14.6 对话结束后触发事件

`NPCInteractable` 已经带有 `UnityEvent`，可用于扩展：

- `On Dialogue Complete`
- `On Interact`

可以用来做：

- 开门
- 切场景
- 发任务
- 播放音效
- 改变 NPC 状态

---

## 十五、推荐搭建顺序

建议按以下顺序完成，最容易排查问题：

1. 先放玩家和 NPC 两张静态图
2. 跑通玩家左右移动
3. 跑通 NPC 触发提示
4. 跑通提示 UI
5. 跑通对话 UI
6. 跑通逐字显示和左键推进
7. 再添加玩家动画
8. 再添加 NPC 待机动画
9. 最后替换正式美术和正式字体

---

## 十六、最小完成标准

当以下 6 条都满足时，可认为“从零搭建对话 Demo”已经完成：

1. 玩家能左右移动
2. 玩家接近 NPC 时出现提示
3. 按 `E` 可以开始对话
4. 对话框显示在 NPC 头顶并随 NPC 一起移动
5. 左键可以补齐和推进对白
6. 对话结束后玩家恢复移动

---

## 十七、补充说明

当前项目中最小可运行方案的关键逻辑是：

- `PlayerController.cs` 负责玩家移动、翻转和动画状态更新
- `NPCInteractable.cs` 负责玩家靠近检测、按键交互、逐字显示和对话结束恢复

也就是说，这个 Demo 的运行核心不是 Unity 自带功能，而是依靠项目中的自定义脚本完成。
