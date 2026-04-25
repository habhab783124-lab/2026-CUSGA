# 对话 Demo 完整操作指导（当前版本）

> 目标：
> **玩家左右移动，靠近 NPC 后显示提示，按 `E` 开始对话，在 NPC 头顶显示对话框，并通过左键点击推进对白。**

## 当前版本说明（非常重要）

当前项目中，这个 Demo 的主流程是：

- `Assets/PlayerController.cs`
  - 负责玩家移动、翻转、动画、冻结
- `Assets/NPCInteractable.cs`
  - 负责 Trigger 检测、提示显示、按 `E` 开始对话、左键推进、逐字显示
- `Assets/DialogueManager.cs`
- `Assets/TypewriterEffect.cs`
  - 这两者是**可选的全局对话方案**，不是当前 NPC 头顶对话的主路径

也就是说：

- 当前 `SampleScene` 里的 `NPC2` 头顶对话，主要靠 `NPCInteractable` 完成
- `DialogueManager + TypewriterEffect` 更适合做共享面板、排队对白、开场对白等扩展功能

另外，当前场景中对象命名有一点历史遗留：

- 玩家对象叫 `NPC`
- 可交互 NPC 叫 `NPC2`

你可以保留这套命名，也可以在新场景中改成更直观的：

- `Player`
- `NPC_A`

---

## 一、最快的重建方式

如果你只是想快速恢复一套默认测试用 Demo：

1. 打开 Unity 顶部菜单
2. 点击：`Tools > Demo Setup > Rebuild NPC Demo Pair`
3. 工具会自动重建：
   - 玩家对象
   - NPC 对象
   - 提示 UI
   - 对话 UI
   - 黑白占位素材

对应编辑器脚本：

- `Assets/Editor/DemoNpcSetupUtility.cs`

如果你想完全手动搭建，请继续看后面的步骤。

---

## 二、手动搭建时需要的最小对象

你至少需要这些对象：

1. 一个玩家对象
2. 一个 NPC 对象
3. 一个挂在 NPC 下的提示 UI
4. 一个挂在 NPC 下的 World Space 对话 UI

推荐层级：

```text
Player
NPC_A
├─ InteractPrompt
│  └─ PromptCanvas
│     └─ PromptBg
│        └─ PromptText
└─ DialogueCanvas
   └─ DialogueBackground
      └─ DialogueText
```

---

## 三、第一阶段：让玩家可移动

### 1. 创建玩家对象

1. 在 Hierarchy 新建空对象，例如命名 `Player`
2. 添加组件：
   - `SpriteRenderer`
   - `Rigidbody2D`
   - `BoxCollider2D`
   - `PlayerController`
   - `Animator`（推荐）

### 2. 推荐参数

`Rigidbody2D`：
- `Body Type = Dynamic`
- `Gravity Scale = 0`
- `Collision Detection = Continuous`
- `Freeze Rotation Z = true`

`BoxCollider2D`：
- `Is Trigger = false`
- 尺寸按角色调整

`PlayerController`：
- `Move Speed = 5`
- `Use Boundary = true`
- `Min X = -8`
- `Max X = 8`
- `Horizontal Axis = Horizontal`
- `Input Dead Zone = 0.01`
- `Move Smoothing = 20`
- `Walking Bool Parameter = isWalking`

### 3. 动画建议

如果要做待机/行走切换：

1. 在 Animator 中创建 `Bool` 参数：`isWalking`
2. 建两个状态：
   - `Idle`
   - `Walk`
3. 条件：
   - `Idle -> Walk`：`isWalking = true`
   - `Walk -> Idle`：`isWalking = false`

### 4. 运行验证

- 按 `A/D` 或左右方向键
- 角色应能左右移动
- 左右移动时朝向自动翻转
- 若配置了动画，移动时进入 Walk，停止时回到 Idle

---

## 四、第二阶段：建立可交互 NPC

### 1. 创建 NPC 对象

1. 在 Hierarchy 新建空对象，例如命名 `NPC_A`
2. 添加组件：
   - `SpriteRenderer`
   - `BoxCollider2D`
   - `NPCInteractable`
   - `Animator`（可选）

### 2. 关键设置

`BoxCollider2D`：
- `Is Trigger = true`
- 尺寸按 NPC 实际大小调整

说明：
- 这里必须勾选 `Is Trigger`
- 当前版本靠 `OnTriggerEnter2D / OnTriggerExit2D` 做接近检测

### 3. 配置 NPCInteractable

先填这些：

- `Interactable = true`
- `Interaction Prompt = 按 E 交互`
- `Player Layer =` 包含玩家所在层
- `Typing Speed = 0.05`

后面 UI 做好以后，再回来绑定：

- `Interact Prompt Root`
- `Interact Prompt Text`
- `Dialogue Canvas Root`
- `Dialogue Text`
- `Dialogue Lines`

---

## 五、第三阶段：创建 NPC 头顶提示 UI

### 1. 创建 InteractPrompt 根节点

1. 在 `NPC_A` 下新建空对象：`InteractPrompt`
2. 设置：
   - `Local Position = (0, 2.2, 0)`
   - `Local Scale = (0.01, 0.01, 0.01)`

### 2. 创建 PromptCanvas

1. 在 `InteractPrompt` 下新建对象：`PromptCanvas`
2. 添加组件：
   - `Canvas`
   - `CanvasScaler`
   - `GraphicRaycaster`
3. 设置：
   - `Canvas.Render Mode = World Space`
   - `RectTransform` 尺寸可先设 `180 x 40`

### 3. 创建 PromptBg

1. 在 `PromptCanvas` 下创建 `UI > Image`
2. 命名：`PromptBg`
3. 可选两种做法：
   - 没有美术图：直接用半透明黑色背景
   - 有美术图：把提示框背景拖到 `Source Image`

### 4. 创建 PromptText

1. 在 `PromptBg` 下创建 `TextMeshPro - Text (UI)`
2. 命名：`PromptText`
3. 设置：
   - `Text = 按 E 交互`
   - `Alignment = Center`
   - `Color = 白色`
   - `Font Size = 24 ~ 32`
   - 使用支持中文的 TMP 字体资源

### 5. 绑定回 NPCInteractable

回到 `NPC_A` 的 `NPCInteractable`：

- `Interact Prompt Root = InteractPrompt`
- `Interact Prompt Text = PromptText`

---

## 六、第四阶段：创建 NPC 头顶对话 UI

### 1. 创建 DialogueCanvas 根节点

1. 在 `NPC_A` 下新建空对象：`DialogueCanvas`
2. 设置：
   - `Local Position = (0, 1.9, 0)`
   - `Local Scale = (0.01, 0.01, 0.01)`

### 2. 添加 Canvas 组件

给 `DialogueCanvas` 添加：

- `Canvas`
- `CanvasScaler`
- `GraphicRaycaster`

并设置：

- `Canvas.Render Mode = World Space`
- `Canvas.Sorting Order = 50`

### 3. 创建 DialogueBackground

1. 在 `DialogueCanvas` 下创建 `UI > Image`
2. 命名：`DialogueBackground`
3. 你可以：
   - 用纯色半透明底图
   - 或换成正式对话框 Sprite
4. 如果是边框型 UI 图，建议：
   - 设置 `9-slice`
   - 使用 `Image Type = Sliced`

### 4. 创建 DialogueText

1. 在 `DialogueBackground` 下创建 `TextMeshPro - Text (UI)`
2. 命名：`DialogueText`
3. 设置：
   - `Text = 空`
   - `Alignment = Top Left`
   - `Enable Word Wrapping = true`
   - `Font Size = 24 ~ 32`
   - `Color = 白色`
   - 使用支持中文的 TMP 字体

### 5. 默认隐藏对话框

将 `DialogueCanvas` 默认设为不激活。

说明：
- 当前脚本会在开始对话时自动显示它
- 对话结束时自动隐藏它

### 6. 绑定回 NPCInteractable

回到 `NPC_A` 的 `NPCInteractable`：

- `Dialogue Canvas Root = DialogueCanvas`
- `Dialogue Text = DialogueText`

---

## 七、第五阶段：填写对白内容

在 `NPC_A` 的 `NPCInteractable` 中，找到：

- `Dialogue Lines`

一项填一句。例如：

1. `你好，欢迎来到这里。`
2. `当你靠近我时，我会显示交互提示。`
3. `按 E 可以开始对话。`
4. `左键可以补齐当前句，或继续下一句。`

推荐：
- 一句不要过长
- 优先“一项一句”
- 中文字体要确保覆盖你输入的字符

---

## 八、当前实际运行流程

当前脚本实际流程如下：

1. 玩家左右移动
2. 玩家进入 NPC 的 Trigger 区域
3. `NPCInteractable` 记录当前玩家并显示提示 UI
4. 玩家按 `E`
5. `NPCInteractable`：
   - 冻结玩家移动
   - 隐藏提示 UI
   - 显示对话 UI
   - 开始逐字显示第一句
6. 玩家左键：
   - 若当前句还在打字：补齐当前句
   - 若当前句已打完：进入下一句
7. 最后一句结束后：
   - 隐藏对话 UI
   - 恢复玩家移动
   - 若玩家仍在范围内，重新显示提示 UI

这套逻辑全部由 `NPCInteractable.cs` 自己完成。

---

## 九、最小验收清单

以下 5 条全部满足，说明 Demo 已搭成：

1. 玩家能左右移动
2. 靠近 NPC 时会显示提示
3. 按 `E` 可以开始对话
4. 左键可以推进对白
5. 对话结束后玩家恢复移动

---

## 十、常见问题排查

### 1）按 `E` 没反应

检查：
- NPC 是否挂了 `NPCInteractable`
- NPC 的 `BoxCollider2D` 是否勾选 `Is Trigger`
- 玩家是否有 `Rigidbody2D`
- `NPCInteractable.playerLayer` 是否包含玩家所在层
- `Dialogue Lines` 是否为空

### 2）对话框没有出现

检查：
- `Dialogue Canvas Root` 是否绑定
- `Dialogue Text` 是否绑定
- `DialogueCanvas` 是否放在 NPC 下
- `DialogueLines` 是否为空

### 3）提示框没有出现

检查：
- `Interact Prompt Root` 是否绑定
- `Interact Prompt Text` 是否绑定
- 玩家是否真的进入了 NPC 的 Trigger 区

### 4）对话期间玩家还能移动

检查：
- `NPCInteractable` 是否正确找到玩家对象上的 `PlayerController`
- 是否有其他脚本在同时移动玩家

### 5）中文显示异常

检查：
- TMP Font Asset 是否支持当前字符
- 文本颜色是否和背景过于接近

---

## 十一、可选增强（后续扩展）

### 1. 给 NPC 增加待机动画

直接给 NPC 添加 `Animator`，建立一个循环播放的 `Idle` 状态即可。

### 2. 给 NPC 增加说话动画

可在后续扩展中：
- 给 NPC Animator 增加参数 `isTalking`
- 对话开始时切入说话状态
- 对话结束时回到待机状态

### 3. 改成屏幕固定对话框

如果你不想让对话框显示在 NPC 头顶，而想做屏幕底部统一对白框，建议改用：

- `Assets/DialogueManager.cs`
- `Assets/TypewriterEffect.cs`

### 4. 使用 Demo Setup 快速生成占位版

如果你只是要快速起一版默认测试场景，可以直接用：

- `Tools > Demo Setup > Rebuild NPC Demo Pair`

---

## 十二、结论

当前项目中，“角色控制 + NPC 触发交互 + 头顶对话框”这条 Demo 路线，应该这样理解：

- `PlayerController`：玩家移动与动画
- `NPCInteractable`：NPC 近距离交互与逐字对话
- `DialogueManager / TypewriterEffect`：可选扩展系统，不是当前 NPC2 对话的主路径

如果你后续继续更新这个 Demo，请优先对照当前脚本实现，而不是旧版本文档描述。
