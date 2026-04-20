# 对话Demo完整操作指导（可直接在 Unity 按此执行）

> 目标：
> **主角走到 NPC 附近，按 `E` 开始对话，在 NPC 头上显示对话框，并通过左键点击任意位置推进对话。**

本项目已提供的脚本：
- `Assets/DialogueManager.cs`：全局对话管理器（统一打字机与翻页）
- `Assets/TypewriterEffect.cs`：对话触发入口（保留兼容）
- `Assets/PlayerController.cs`：角色控制与交互检测
- `Assets/PlayerMovement.cs`：兼容过渡脚本（继承 `PlayerController`）
- `Assets/IInteractable.cs`：可交互对象接口
- `Assets/NPCInteractable.cs`：NPC 示例交互组件

---

## 一、准备清单

确认当前场景至少有这些组件：

1. **主角角色对象**（可先用当前场景里的 `NPC` 临时代替测试）
   - `SpriteRenderer`
   - `Rigidbody2D`（建议，能更稳定）
   - `Collider2D`（建议加）
   - `PlayerController`（或保留 `PlayerMovement` 兼容层也可以）
2. **NPC 对象**（可交互对象）
   - `Collider2D`
   - `SpriteRenderer`（可选）
   - `NPCInteractable`
3. **对话 UI（挂在 NPC 头上的）**
   - 一个 `TMP Text`（`TextMeshPro - Text (UI)`）用于显示文字
   - 一个对话框背景图（Image）
   - 二者可放在同一个面板下（例如 `DialogueBox`）

---

## 二、第一阶段：让角色可移动且播放走路动画

### 1. 配置 PlayerController

1. 选中主角对象，添加组件：
   - `PlayerController`（推荐）
   - 或保留原有 `PlayerMovement`，它会自动继承 `PlayerController`。
2. Inspector 配置：
   - `Move Speed`：比如 `5`
   - `Use Boundary` 勾选（边界）
   - `Min X`、`Max X` 按场景范围设置
3. Animator：
   - 指定 `Animator`（若留空，会自动 `GetComponent<Animator>()`）
   - `Walking Bool Parameter` 默认是 `isWalking`
   - 在 Animator 里创建同名 `Bool` 参数并与待机/行走状态机绑定

### 2. 运行验证

- 按 `A/D` 或左右方向键：角色应移动
- 走路时 `isWalking=true`，停止时 `isWalking=false`
- 左右移动时角色朝向会翻转

---

## 三、第二阶段：建立“可交互对象”

### 1. 给 NPC 加上交互触发

1. 在 NPC 上添加 `Collider2D`（如 BoxCollider2D）
2. 添加 `NPCInteractable` 组件
3. 填写关键字段：
   - `Interaction Prompt`：例如「按 E 与我交话」
   - `Use Dialogue` 勾选
   - `Dialogue Lines`：填入要显示的台词列表（每行一条）
   - `Dialogue Text`：拖入 NPC 头上 `TMP Text`
   - `Dialogue Panel`：拖入 NPC 头上对话框父节点（如 `DialogueBox`）
4. `Typing Speed` 和 `Hide Panel When Finish` 可按需调整

### 2. 玩家检测设置（关键）

在 `PlayerController` 中：

- `Interactable Layer` 要包含 NPC 所在层
- 若你用的是 Tag 过滤：
  - `Interactable Tag` 填 `NPC`，并在 NPC 上设置同样 Tag
  - 如果不想用 Tag 过滤，留空
- `Use Raycast Detection`：默认 `true`，用射线检测
- `Interact Distance`：约 `1.2`
- `Interaction Offset`：可留默认，或调到角色前方一点

---

## 四、第三阶段：让 NPC 对话显示在 NPC 头上

默认 `DialogueManager` 显示逻辑由 `NPCInteractable` 传入 `dialogueText / dialoguePanel` 决定。

### 推荐挂载结构（示例）

```
NPC
├─ Sprite
├─ DialogueBox（Panel / Image）
│  └─ DialogueText（TextMeshPro - Text UI）
└─ 其他子物件
```

并确保：
- `NPCInteractable.dialogueText` 指向 `DialogueText`
- `NPCInteractable.dialoguePanel` 指向 `DialogueBox`

`DialogueBox` 是 NPC 的子物体时，对话会随 NPC 移动（“头上”显示效果更自然）。

---

## 五、完整联动流程（按键动作）

1. 角色向右/左移动，走近 NPC
2. `PlayerController` 持续检测到附近交互对象（射线/范围）
3. 玩家按 `E`
4. `PlayerController` 调用该 NPC 的 `IInteractable.Interact(player)`
5. `NPCInteractable` 调用 `DialogueManager.ShowDialogue(...)`
6. 对话框出现，逐字显示第一句
7. 玩家左键点击（任意位置）：
   - 若当前句仍在打字，立即显示完整当前句
   - 若当前句已显示完，进入下一句
8. 最后一句结束后，按 `DialogueManager` 配置可自动隐藏对话框

---

## 六、左键推进确认（默认行为）

`DialogueManager` 使用的是：

- `Input.GetMouseButtonDown(0)`（左键）

所以只要左键有点击动作，就会触发对话推进。当前设置满足「任意地方点击」。

---

## 七、常见问题排查

### 1）按 E 没反应
- NPC 无 `Collider2D`
- `NPCInteractable` 未挂载
- `PlayerController` 的 `Interactable Layer` 没包含 NPC 所在 Layer
- `interactableTag` 被设置但 NPC Tag 不匹配（或留空以避免误过滤）

### 2）对话框没有出现
- `NPCInteractable.dialogueText` 为空
- `dialogueText` 或 `dialoguePanel` 没拖正确
- `dialogueLines` 为空

### 3）左右键能动但无动画
- Animator 未挂或参数名不对（默认 `isWalking`）
- 参数类型不是 Bool

### 4）左键不推进
- 屏幕有多个点击拦截（UI Block）可先在空白区域测试
- `DialogueManager` 没有成功接收该请求（可在 Console 看警告）

### 5）对话出现在错误 UI 上
- 先检查 `NPCInteractable` 的 `dialogueText/panel` 是否是 NPC 头部 UI，不是全局对话 UI

---

## 八、建议的最终配置（推荐）

- `PlayerController`：在场景主角上使用，`interact key=E`
- NPC 身上只要挂 `NPCInteractable` + Collider2D
- 对话 UI 作为 NPC 子对象，绑定到 `dialogueText` 和 `dialoguePanel`
- `DialogueManager`：放一个全局对象 `GlobalDialogueManager`，不重复创建

---

## 九、最小成功验收清单

1. 角色能移动并播放走路动画
2. 接近 NPC 可通过 `E` 触发
3. NPC 头上出现对话框并逐字显示
4. 左键点击任意位置可推进到下一句
5. 全部台词播放完成后对话框按预期隐藏

---

## 十、可选增强（后续）

如果你希望：
- 交互显示提示文本（“按 E 对话”）
- 走到 NPC 自动高亮
- 左键仅在对话中有效，平时点击只走逻辑

可以在 `PlayerController` 和 `DialogueManager` 上继续扩展状态控制（例如：对话开始时禁用玩家移动、加互动提示 UI）。

---

到这里，三部分（角色控制 + 交互 + 对话系统）就形成了完整闭环。
