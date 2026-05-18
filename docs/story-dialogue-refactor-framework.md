# 剧情系统重构整体框架

## 1. 文档目标

本文档用于规划 `Assets/_Project/Story/Scripts/Dialogue` 下 2D 横版剧情系统的重构方向，目标是：

- 保留当前可复用、已经验证有效的基础能力
- 整合章节脚本中的重复逻辑
- 降低章节脚本与场景对象、塔防流程、切场景逻辑之间的耦合
- 建立统一的剧情播放框架，支持后续新增章节和特殊演出

本文档先定义整体架构，不直接约束具体类名的最终实现；后续编码以本框架为基线逐步落地。

## 2. 当前系统现状

当前剧情系统已经覆盖以下能力：

- 多章节剧情播放
- 世界气泡对话
- 屏幕底部字幕框
- 屏幕中心气泡/中心文本演出
- 打字机效果
- 文字振动、放大、强调效果
- 打字音效
- 背景音乐播放
- 角色入场、退场、移动
- 黑幕淡入淡出切场景
- 章节之间切换
- 剧情与塔防玩法之间切换

但当前代码结构存在明显重复和耦合问题。

## 3. 现有代码分层判断

### 3.1 可保留的基础层

以下脚本适合作为重构后的核心基础能力继续保留：

- [DialogueBubbleView](../Assets/_Project/Story/Scripts/Dialogue/DialogueBubbleView.cs)
- [DialogueRunner](../Assets/_Project/Story/Scripts/Dialogue/DialogueRunner.cs)
- [DialogueLine](../Assets/_Project/Story/Scripts/Dialogue/DialogueLine.cs)
- [DialogueEmphasis](../Assets/_Project/Story/Scripts/Dialogue/DialogueEmphasis.cs)
- [ScreenFadeTransition](../Assets/_Project/Story/Scripts/Dialogue/ScreenFadeTransition.cs)
- [SceneBgmPlayer](../Assets/_Project/Story/Scripts/Dialogue/SceneBgmPlayer.cs)
- [NpcDialogue](../Assets/_Project/Story/Scripts/Dialogue/NpcDialogue.cs)

保留原因：

- `DialogueBubbleView` 已经具备统一的文字播放能力，包括打字、暂停、跳字、底部字幕布局、强调效果、打字音效和气泡表现
- `DialogueRunner` 已经承担基础对话调度职责，适合作为“世界气泡对话播放器”
- `ScreenFadeTransition` 已经是一个相对独立的场景切换能力
- `SceneBgmPlayer` 可以继续作为简单场景 BGM 播放器，后续再决定是否并入更高层服务

### 3.2 重复严重的章节层

以下脚本中存在大量重复实现，属于重点重构对象：

- `Chapter1.cs`
- `Chapter2CenterBubbleController.cs`
- `Chapter5.cs`
- `Chapter9.cs`
- `TruthRevealed.cs`
- `Chapter1CenterBubbleScreen.cs`
- `Chapter9SceneController.cs`
- `Chapter5Controller.cs`

重复点主要包括：

- 章节内部重复创建 UI Canvas 和文字组件
- 重复实现打字机和跳字逻辑
- 重复实现打字音效播放器
- 重复实现中心气泡开关动画
- 重复实现底部字幕框
- 重复实现 BGM 加载和播放
- 重复实现淡入淡出逻辑
- 通过 `GameObject.Find`、`FindObjectOfType`、`Resources.Load` 等方式临时查找依赖

### 3.3 数据层不统一

对白数据目前分散在多个位置：

- `static ChapterX.Get(string id)` 中
- 某些 `MonoBehaviour` 的默认字段里
- 某些章节控制器内部硬编码里

同时 [DialogueScripts](../Assets/_Project/Story/Scripts/Dialogue/DialogueScripts.cs) 采用手工串联 `Chapter1 -> Chapter10` 的方式统一入口，扩展性较差。

## 4. 重构核心原则

### 4.1 保留已有有效能力，不做推倒重写

本次重构以“抽象、整合、复用”为主，不以“全部重写”为目标。

### 4.2 把剧情内容和剧情播放方式拆开

章节脚本不应同时负责：

- 存对白文本
- 控制角色移动
- 创建 UI
- 播放打字效果
- 切场景
- 切换到塔防

后续应分离为：

- 数据层：剧情内容与步骤定义
- 演出层：气泡、字幕框、转场、BGM、角色动作
- 导演层：按顺序驱动整段剧情

### 4.3 优先收敛通用流程，再保留少量章节特例

不是每个章节都要强行抽成完全一样的模板。

原则是：

- 通用能力统一
- 特殊演出挂接到统一框架上
- 特例保留在少量可控的章节脚本里

### 4.4 场景依赖显式化

后续应减少以下模式：

- 运行时字符串查找对象
- 运行时到处 `Resources.Load`
- 章节脚本直接猜测场景对象名称

改为：

- Inspector 显式绑定
- 场景注册表统一提供引用
- 集中资源配置

## 5. 目标架构

建议将剧情系统整理为以下五层。

### 5.1 数据层

职责：

- 定义对白内容
- 定义剧情段落
- 定义章节流程步骤

建议对象：

- `StoryLine`
- `StorySequence`
- `StoryChapterDefinition`
- `StoryStep`

建议支持的步骤类型：

- `PlayDialogue`
- `PlayNarration`
- `ShowCenterBubble`
- `HideCenterBubble`
- `MoveActor`
- `PlayActorAnimation`
- `PlayBgm`
- `StopBgm`
- `FadeIn`
- `FadeOut`
- `LoadScene`
- `EnterTowerDefense`
- `Wait`

说明：

- `DialogueLine` 可以先继续沿用
- 中长期再决定是否升级为 ScriptableObject 数据资产

### 5.2 上下文层

职责：

- 给剧情播放过程提供统一运行环境
- 统一管理场景内角色、锚点、服务入口

建议对象：

- `StoryContext`
- `StoryActorRegistry`
- `StorySceneBindings`

`StoryContext` 建议统一暴露：

- `PlayerInteractor2D`
- `DialogueRunner`
- `NarrationPresenter`
- `CenterBubblePresenter`
- `ScreenFadeTransition` 调用入口
- BGM 播放入口
- 场景切换入口
- 塔防切换入口
- 角色引用与锚点引用

### 5.3 表现层

职责：

- 只负责“如何演出”
- 不负责章节顺序

建议拆分为三个方向：

- `DialoguePresenter`
  负责玩家/NPC 世界气泡对话，核心复用 `DialogueRunner`
- `NarrationPresenter`
  负责底部字幕框、全屏旁白、中心屏幕文字
- `CenterBubblePresenter`
  负责中心气泡外观展开、关闭、文字播放

当前可复用基础：

- `DialogueBubbleView` 继续作为统一文字播放视图核心
- 中心气泡建议不要再维护一套独立打字逻辑，而是尽可能复用 `DialogueBubbleView`

### 5.4 导演层

职责：

- 按步骤驱动一整段章节流程
- 处理“开始、推进、结束、异常中断”

建议对象：

- `StoryDirector`
- `StorySequencePlayer`
- `StoryStepExecutor`

导演层负责：

- 读取章节定义
- 顺序执行步骤
- 在步骤之间等待完成
- 控制玩家输入锁定
- 处理章节结束回调
- 调用场景切换和塔防切换

### 5.5 桥接层

职责：

- 连接剧情系统和其它游戏系统

建议对象：

- `StoryFlowBridge`
- `StoryToTowerDefenseTransition`

桥接层用于统一处理：

- 章节到章节切换
- 剧情到塔防切换
- 塔防结束后回到剧情
- 持久化章节进度

## 6. 建议保留与替换策略

### 6.1 保留

- `DialogueBubbleView`
- `DialogueRunner`
- `ScreenFadeTransition`
- `SceneBgmPlayer`
- `DialogueLine`
- `DialogueEmphasis`

### 6.2 合并

- `Chapter1CenterBubbleScreen` 与 `Chapter2CenterBubbleController`
- `Chapter5` / `Chapter9` / `TruthRevealed` 中底部文字框逻辑
- 各章节里的打字音效逻辑
- 各章节里的字体加载逻辑
- 各章节里的 BGM 加载逻辑

### 6.3 删除或替换

- 章节脚本内部重复的 UI 创建逻辑
- 章节脚本内部重复的打字机协程
- `Chapter5Controller` 中通过反射读取 `Chapter5` 私有状态的实现
- `DialogueScripts` 这种手工级联聚合方式

## 7. 章节脚本未来形态

重构后，章节脚本不再承载所有细节，而应该尽量只做以下三类事情之一。

### 7.1 纯数据章节

只声明剧情步骤，不写运行逻辑。

适用章节：

- 对话为主
- 演出结构固定
- 特殊镜头和角色行为较少

### 7.2 轻量导演章节

章节脚本只负责少量特例调度，实际播放仍走统一框架。

适用章节：

- 角色入场、退场
- 特定镜头或特殊时间点切换
- 局部演出需要自定义

### 7.3 特殊演出章节

极少数章节保留独立控制器，但必须复用统一的表现层和上下文层，不允许重复造一整套字幕/打字/切场景系统。

## 8. 推荐的第一版目录方向

建议后续逐步整理为如下结构：

```text
Assets/_Project/Story/Scripts/Dialogue/
  Core/
    DialogueBubbleView.cs
    DialogueRunner.cs
    DialogueLine.cs
    DialogueEmphasis.cs
  Context/
    StoryContext.cs
    StoryActorRegistry.cs
    StorySceneBindings.cs
  Presentation/
    DialoguePresenter.cs
    NarrationPresenter.cs
    CenterBubblePresenter.cs
    StoryBgmController.cs
    StoryTransitionController.cs
  Director/
    StoryDirector.cs
    StorySequencePlayer.cs
    StoryStepExecutor.cs
  Data/
    StorySequence.cs
    StoryChapterDefinition.cs
    StoryStep.cs
    DialogueScriptDatabase.cs
  Bridge/
    StoryFlowBridge.cs
    StoryToTowerDefenseTransition.cs
  Chapters/
    Chapter6SceneController.cs
    Chapter7SceneController.cs
    ...
```

说明：

- 这是重构目标结构，不要求一次性搬迁完成
- 第一阶段可以先新增目录和新类，不急着移动旧文件

## 9. 迁移优先级

### 第一阶段：先搭骨架，不改剧情内容

目标：

- 新建 `StoryContext`
- 新建 `StoryActorRegistry`
- 新建 `NarrationPresenter`
- 新建 `CenterBubblePresenter`
- 让基础服务先统一起来

这一阶段不追求把所有章节立刻接入。

### 第二阶段：选择低风险章节做模板迁移

优先迁移：

- `Chapter6SceneController`
- `Chapter7SceneController`

原因：

- 结构相对简单
- 更接近标准“角色移动 + 对话”的理想流程
- 适合先验证新框架

### 第三阶段：统一旁白/底部字幕系统

重点迁移：

- `Chapter5`
- `Chapter9`
- `TruthRevealed`

目标：

- 底部字幕框统一走 `NarrationPresenter`
- 删除章节内重复打字逻辑

### 第四阶段：统一中心气泡系统

重点迁移：

- `Chapter1`
- `Chapter2CenterBubbleController`

目标：

- 将中心气泡的展开、关闭、打字、音效统一到 `CenterBubblePresenter`

### 第五阶段：处理复杂章节和桥接流程

重点迁移：

- `Chapter3CutsceneController`
- `Chapter35PlayerIntro`
- 章节与塔防之间的切换逻辑

目标：

- 把复杂角色入场/退场流程接入统一导演层
- 确立剧情与塔防之间的标准切换接口

### 第六阶段：统一对白数据入口

目标：

- 逐步淘汰 `DialogueScripts -> ChapterX.Get()` 级联方式
- 将对白和步骤收敛到统一数据源

## 10. 编码约束建议

后续重构时应遵守以下原则：

- 不再在章节脚本里重复创建底部字幕框
- 不再在章节脚本里重复写打字机协程
- 不再用反射读其它脚本私有字段
- 尽量避免 `GameObject.Find` 和 `FindObjectOfType`
- 统一通过上下文层或显式绑定获取依赖
- 剧情数据和表现实现尽量解耦
- 特例演出必须建立在通用表现层上，不允许复制整套逻辑

## 11. 第一版落地目标

第一版重构不追求一次性完成全部章节迁移，目标应明确为：

- 先形成可复用的框架
- 先迁两个模板章节
- 先统一三类核心表现
  - 世界气泡对话
  - 底部字幕/旁白
  - 中心气泡/中心文字
- 先打通剧情到切场景、剧情到塔防的标准接口

只有在这一步稳定后，再批量改造剩余章节。

## 12. 验收标准

当第一轮重构完成时，应至少满足以下标准：

- 新章节不需要复制旧章节脚本才能制作
- 普通对话章节可以只靠配置完成
- 底部字幕与中心气泡不再各自维护一套打字机
- 场景切换和 BGM 调用不再分散在各章节中
- 剧情到塔防、塔防回剧情有明确统一入口
- 删除至少一批明显重复实现

## 13. 下一步执行建议

按照当前代码基础，建议下一步按以下顺序推进：

1. 新建 `StoryContext`、`StoryActorRegistry`、`NarrationPresenter`、`CenterBubblePresenter`
2. 让 `Chapter6SceneController` 和 `Chapter7SceneController` 接入新框架
3. 再统一 `Chapter5`、`Chapter9`、`TruthRevealed` 的底部字幕播放
4. 最后处理 `Chapter1`、`Chapter2`、`Chapter3` 这类特例较多的章节

---

本文档是剧情系统重构的总纲。后续如果进入具体编码阶段，应基于本文档再补充：

- 类关系图
- 章节迁移清单
- 测试清单
- 场景绑定规范
