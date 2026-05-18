# 剧情系统使用说明

本文档基于当前 `Assets/_Project/Story/Scripts/Dialogue` 目录中的运行时代码整理，目标是说明这套 2D 横版剧情系统的实际接入方式和维护方式。

## 1. 系统概览

这套剧情系统当前主要覆盖以下能力：

- 多章节剧情播放
- 世界空间角色气泡对白
- 屏幕底部旁白 / 叙述文本框
- 中心气泡展开 / 收起转场
- 打字机效果
- 文本强化效果：放大、震动
- 打字音效
- 章节内角色移动与演出
- 章节之间切场景
- 剧情场景切到塔防 / 其他玩法场景
- 场景 BGM 播放

推荐把它理解成三层：

- 数据层：`DialogueLine`、`DialogueScripts`
- 表现层：`DialogueRunner`、`DialogueBubbleView`、`NarrationPresenter`
- 流程层：各章节控制器、`StorySceneContext`、`StoryActorRegistry`、`ScreenFadeTransition`

## 2. 核心脚本职责

### 2.1 数据

- `DialogueLine.cs`
  - 单句对白数据。
  - 字段包含 `speaker`、`text`、`emphasis`。

- `DialogueEmphasis.cs`
  - 单句对白的强化参数。
  - 控制是否启用强化、缩放倍率、震动强度。

- `DialogueScripts.cs`
  - 剧情对白总入口。
  - 通过 `DialogueScripts.Get(id)` 统一向各章节取对白数据。

### 2.2 表现

- `DialogueRunner.cs`
  - 世界空间对白调度器。
  - 负责玩家 / NPC 气泡切换、输入推进、打字结束回调、移动锁定。

- `DialogueBubbleView.cs`
  - 单个气泡的显示脚本。
  - 负责跟随锚点、文本排版、打字机、强化动画、打字音效、底部固定布局。

- `Framework/NarrationPresenter.cs`
  - 底部旁白框。
  - 适合独白、章节说明、叙述性文本。

- `Framework/CenterBubbleTransitionDriver.cs`
  - 中心气泡开合动画底层驱动。

- `Chapter1CenterBubbleScreen.cs`
  - 章节 1 的中心气泡展开器。

- `Chapter2CenterBubbleController.cs`
  - 中心气泡 + 打字旁白的完整控制器。

### 2.3 流程

- `Framework/StorySceneContext.cs`
  - 场景级上下文。
  - 统一解析 `DialogueRunner`、玩家、默认气泡预制体、Actor 注册表。

- `Framework/StoryActorRegistry.cs`
  - 角色与对白锚点注册表。
  - 用 `id -> actor / dialogueAnchor` 的方式减少硬编码查找。

- `Framework/StoryCutsceneControllerBase.cs`
  - 章节控制器基类。
  - 提供角色解析、锚点创建、运行时锚点更新、玩家锁定等公共能力。

- `ScreenFadeTransition.cs`
  - 全屏淡入淡出切场景。

- `SceneBgmPlayer.cs`
  - 简单的场景 BGM 播放器。

## 3. 对白数据怎么写

### 3.1 基础结构

```csharp
new DialogueLine
{
    speaker = DialogueSpeaker.NPC,
    text = "欢迎来到伊甸。",
    emphasis = DialogueBubbleView.CreateNormalEmphasis()
}
```

`DialogueSpeaker` 当前只有两个值：

- `Player`
- `NPC`

### 3.2 强化效果

当前可直接复用的强调预设：

- `DialogueBubbleView.CreateNormalEmphasis()`
- `DialogueBubbleView.CreateStrongEmphasis()`
- `DialogueBubbleView.CreatePulseEmphasis()`

常见写法：

```csharp
private static DialogueLine Player(string text)
{
    return new DialogueLine
    {
        speaker = DialogueSpeaker.Player,
        text = text,
        emphasis = DialogueBubbleView.CreateNormalEmphasis()
    };
}
```

### 3.3 推荐的数据组织方式

推荐把章节对白放在章节脚本的静态 `Get(string id)` 里，再由 `DialogueScripts.Get(id)` 汇总。

例如：

```csharp
public static IReadOnlyList<DialogueLine> Get(string id)
{
    switch (id)
    {
        case "chapter9_intro":
            return Chen();
        default:
            return null;
    }
}
```

这样做的优点：

- 场景控制器只关心流程，不直接堆台词
- 后续更容易拆成 ScriptableObject / JSON / 表格导入
- 多章节统一入口，调用方式一致

## 4. 三种常见表现方式

## 4.1 世界空间角色气泡

适用场景：

- 玩家和 NPC 面对面对话
- 对话跟随角色头顶
- 需要左右角色切换说话人

核心脚本：

- `DialogueRunner`
- `DialogueBubbleView`

最常用调用：

```csharp
dialogueRunner.PlayConversation(
    playerInteractor,
    playerBubbleAnchor,
    npcBubbleAnchor,
    resolvedLines,
    onEnded: OnDialogueEnded);
```

参数含义：

- `playerInteractor`
  - 传入后，对话期间会锁住玩家移动，结束时自动解锁。
- `playerBubbleAnchor`
  - 玩家气泡跟随锚点。
- `npcBubbleAnchor`
  - NPC 气泡跟随锚点。
- `resolvedLines`
  - `IList<DialogueLine>`。
- `onEnded`
  - 全部对白播完后的回调。

如果只播一句话，也可以直接传单条列表：

```csharp
dialogueRunner.PlayConversation(
    null,
    playerBubbleAnchor,
    null,
    new List<DialogueLine> { line },
    OnLineFinished);
```

### 4.1.1 气泡预制体

`DialogueRunner` 依赖 `DialogueBubbleView` 预制体。

推荐方式：

- 在 Inspector 直接指定 `bubblePrefab`
- 或确保 `Resources/DialogueBubble` 可被加载

### 4.1.2 锚点

锚点本质上就是一个 `Transform`。

常见来源：

- 角色头顶空物体
- 运行时创建的 Anchor
- `StoryActorRegistry` 中注册的 `dialogueAnchor`

如果角色会移动，建议像 `Chapter7SceneController` 一样在 `LateUpdate` 中持续更新运行时锚点位置。

### 4.1.3 底部固定气泡模式

`DialogueRunner` 也支持把气泡固定到底部：

```csharp
dialogueRunner.ConfigureBottomLayout(
    true,
    new Vector2(1280f, 220f),
    new Vector2(0f, 120f),
    0.6f,
    true);
```

这个模式适合想复用气泡样式但不跟随角色时使用。纯旁白更推荐直接用 `NarrationPresenter`。

## 4.2 底部旁白 / 叙述框

适用场景：

- 章节开场说明
- 内心独白
- 不需要区分左右说话人的文本

核心脚本：

- `Framework/NarrationPresenter`

最常见初始化流程：

```csharp
narrationPresenter.SetUiNames("Chapter5Canvas", "DialogueBox", "DialogueText");
narrationPresenter.ConfigureAppearance(
    dialogueFontAsset,
    boxSize,
    bottomOffset,
    backgroundAlpha,
    backgroundColor,
    textColor,
    minFontSize,
    maxFontSize,
    textPadding);
narrationPresenter.ConfigureTyping(secondsPerChar, dialogueBubblePrefab, "Chapter5IntroTypingSfx");
```

播放方式有两种：

```csharp
narrationPresenter.SetLines(lines);
narrationPresenter.ShowLine(0);
```

或

```csharp
narrationPresenter.PlayText("战场上到处都是尸体。", OnTypingComplete);
```

输入推进：

```csharp
if (narrationPresenter.TryHandleAdvanceInput())
{
    return;
}
```

说明：

- 如果当前正在打字，点击会跳过当前打字
- 如果因为 `[pause]` 暂停，点击会恢复
- 打完之后是否进入下一句，由章节控制器自己决定

## 4.3 中心气泡转场

适用场景：

- 某些章节的中屏提示
- 视觉上需要“气泡展开再打字”
- NPC 说话不跟随角色，而是走中心演出

核心组合：

- `CenterBubbleTransitionDriver`
- `Chapter1CenterBubbleScreen`
- `Chapter2CenterBubbleController`
- `NarrationPresenter`

当前项目里比较完整的参考是 `Chapter2CutsceneController`：

- NPC 台词走中心气泡
- Player 台词走普通对白气泡
- 鼠标点击推进
- 最后自动切下一个场景

如果只是做中心气泡开合，不需要自己重写开合曲线逻辑，直接复用现有驱动即可。

## 5. 文本特效与打字控制

### 5.1 强调效果

`DialogueEmphasis` 控制的是整句文本在打字期间的表现：

- `enabled`
- `scaleMultiplier`
- `shakeMagnitude`

一般规则：

- 普通对白用 `CreateNormalEmphasis()`
- 情绪强烈的句子用 `CreateStrongEmphasis()`
- 轻微脉冲感可用 `CreatePulseEmphasis()`

### 5.2 内联标签

当前气泡和旁白都支持一套内联控制标签：

- `[pause]`
  - 到下一个可见字符前暂停，等待玩家点击继续
- `[resume]`
  - 清除暂停标记
- `[speed=0.01]`
  - 修改后续字符打字速度
- `[size=1.4]`
  - 修改后续字符的字号倍率
- `[sfx=poolId]`
  - 切换打字音效池

示例：

```text
你终于[pause]……看见我了。
```

### 5.3 TMP 富文本

当前解析逻辑保留 TMP 富文本标签，可直接使用：

- `<color=red>...</color>`
- `<size=120%>...</size>`

推荐：

- 情绪效果优先用 `DialogueEmphasis`
- 局部字词强调再用 TMP 富文本或 `[size=...]`

## 6. 新章节接入流程

### 6.1 先决定章节类型

优先按下面三类来选：

- 纯气泡对白
  - 参考 `Chapter6SceneController`、`Chapter7SceneController`
- 纯旁白 / 说明文本
  - 参考 `TruthRevealed`
- 旁白 + 气泡混合
  - 参考 `Chapter9`
- 中心气泡 + 角色气泡混合
  - 参考 `Chapter2CutsceneController`

### 6.2 场景对象建议

建议场景里至少有一个“剧情控制器对象”，挂以下脚本中的一部分：

- `StorySceneContext`
- `StoryActorRegistry`
- `DialogueRunner`
- 章节控制器脚本
- `SceneBgmPlayer` 或自定义 `AudioSource`

### 6.3 推荐的角色注册方式

如果章节里玩家 / NPC / 锚点都比较固定，推荐配置 `StoryActorRegistry`：

- `id`
  - 例如 `player`、`shen`
- `actor`
  - 角色 Transform
- `dialogueAnchor`
  - 对话锚点 Transform
- `fallbackSceneObjectName`
  - 兜底场景对象名

这样章节控制器里可以直接用：

```csharp
player = ResolvePlayerTransform(player);
shen = ResolveActor(shen, "shen", "Shen");
playerBubbleAnchor = ResolveDialogueAnchor(playerBubbleAnchor, "player", player, "Player");
```

### 6.4 新章节脚本推荐写法

如果是常规剧情过场，推荐直接继承 `StoryCutsceneControllerBase`。

好处：

- 减少 `FindObjectOfType` / `GameObject.Find`
- 公共逻辑已经封装
- 更容易统一维护

最小骨架：

```csharp
public sealed class ChapterXSceneController : StoryCutsceneControllerBase
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private DialogueBubbleView dialogueBubblePrefab;
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform npcBubbleAnchor;
    [SerializeField] private string dialogueId = "chapterX_intro";

    protected override void Awake()
    {
        base.Awake();
        dialogueRunner = ResolveDialogueRunner(dialogueRunner);
        playerInteractor = ResolvePlayerInteractor(playerInteractor);
    }

    private void Start()
    {
        if (!TryConfigureDialogueRunner(ref dialogueRunner, dialogueBubblePrefab, nameof(ChapterXSceneController)))
        {
            return;
        }

        IReadOnlyList<DialogueLine> lines = DialogueScripts.Get(dialogueId);
        dialogueRunner.PlayConversation(
            playerInteractor,
            playerBubbleAnchor,
            npcBubbleAnchor,
            lines as IList<DialogueLine> ?? new List<DialogueLine>(lines));
    }
}
```

## 7. 章节切换与塔防切换

剧情系统本身并不区分“下一个是剧情场景”还是“下一个是塔防场景”，它只负责切场景。

统一做法：

```csharp
ScreenFadeTransition.Play(nextSceneName, 0.75f, 0.75f);
```

如果下一个场景就是塔防场景，直接把 `nextSceneName` 配成塔防场景名即可。

当前已有参考：

- `Chapter2CutsceneController`
  - 对话结束后切到下一章
- `TruthRevealedSceneController`
  - 对话完成后等待点击再切场景
- `Chapter9SceneController`
  - 黑场开场 -> 剧情 -> 黑场结尾 -> 切到 `truth_revealed`

推荐规则：

- 普通章节结束，统一通过 `ScreenFadeTransition` 走黑场切换
- 不要在多个地方同时调用 `LoadScene`
- 需要黑场开场时，参考 `Chapter9SceneController`

## 8. BGM 与音效

### 8.1 简单场景 BGM

最简单的做法是挂 `SceneBgmPlayer`：

- `resourcePath`
- `volume`
- `loop`
- `playOnStart`

适合：

- 进场就播，过程中不需要复杂同步的 BGM

### 8.2 章节自定义音乐流程

如果剧情需要更强控制，直接在章节脚本里挂 `AudioSource` 并自己控制：

- 开场播放
- 结尾停止
- 转场前切换音频

参考：

- `Chapter1`
- `Chapter9`
- `Chapter10`

### 8.3 打字音效

打字音效来源于 `DialogueBubbleView`。

旁白要复用这套打字音效时，不是自己重写，而是把一个气泡预制体作为“音效参考源”传给：

```csharp
narrationPresenter.ConfigureTyping(secondsPerChar, dialogueBubblePrefab, "TypingSfxChild");
```

这样旁白和气泡可以共用同一套打字音池配置。

## 9. 常见用法范式

### 9.1 可交互 NPC

参考 `NpcDialogue.cs`。

特点：

- 玩家靠近并按交互键触发
- 对话结束后可禁用重复触发
- 直接调用 `interactor.DialogueRunner`

适合平时散布在场景中的普通 NPC 对话。

### 9.2 开场先移动角色，再开始对白

参考 `Chapter6SceneController.cs`。

特点：

- 先锁玩家
- 把角色从屏幕外移动到目标点
- 到位后再调用 `DialogueRunner.PlayConversation`

适合章节开场演出。

### 9.3 底部旁白开场，再切角色对白

参考 `Chapter9.cs`。

特点：

- 第一段用底部旁白框
- 旁白结束后切到气泡对白
- 同一章节内混合两种表现形式

### 9.4 中心气泡和角色气泡混用

参考 `Chapter2CutsceneController.cs`。

特点：

- NPC 台词走中心气泡
- 玩家台词走世界气泡
- 同一条流程中按说话人切表现层

## 10. 常见问题排查

### 10.1 中文显示成方块

通常是字体资源问题。

检查：

- `dialogueFontAsset` 是否已指定
- 字体图集是否包含中文字符
- `Resources/Fonts/...` 中的字体是否能正常加载

### 10.2 第九章或某章节文字乱码

通常是脚本文件编码问题，不是运行逻辑问题。

建议：

- 剧情脚本统一保存为 UTF-8
- 不要混用本地 ANSI 编码
- 文本从外部粘贴后先确认 `.cs` 文件编码

### 10.3 气泡不显示

检查：

- `DialogueRunner` 是否拿到了 `bubblePrefab`
- `DialogueBubbleView` 预制体引用是否为空
- 锚点是否为空或位置跑飞
- 对话是否已经被 `HideDialogueBubble()` 隐藏

### 10.4 只能点一下就没反应

检查：

- 当前是否还在打字
- 是否命中了 `[pause]`
- 当前控制器是否把“点击跳过打字”和“点击进入下一句”分开处理

### 10.5 对话结束后玩家不能移动

检查：

- 调用 `PlayConversation` 时是否传入了 `playerInteractor`
- 结束回调是否正常执行
- 自定义剧情脚本里是否额外加了 `SetMovementLocked(true)` 却没解锁

### 10.6 切场景无效

检查：

- `nextSceneName` 是否正确
- 目标场景是否已加入 Build Settings
- 是否有多个地方重复触发切场景

## 11. 推荐实践

- 新章节优先写成“章节控制器 + 数据函数”的结构，不要把所有逻辑都塞进一个 `Update`
- 共用对白数据时优先走 `DialogueScripts.Get(id)`，不要每个地方各写一份同样台词
- 角色和锚点尽量挂到 `StoryActorRegistry`，减少硬编码对象名
- 普通对白优先复用 `DialogueRunner`
- 纯叙述优先复用 `NarrationPresenter`
- 场景切换统一走 `ScreenFadeTransition`
- 简单 BGM 优先用 `SceneBgmPlayer`
- 如果章节内既有旁白又有角色对白，参考 `Chapter9` 的分段组织方式

## 12. 建议的后续维护方向

如果后面还要继续迭代这套系统，推荐按下面顺序做：

- 先继续把各章节对白统一收口到 `DialogueScripts.Get(id)`
- 再把常见章节流程收敛到 `StoryCutsceneControllerBase` 子类
- 最后再考虑把对白数据从代码迁移到外部资源

这样风险最小，也最不容易再把能跑通的章节改坏。
