# 工作流上下文包
Version: 1.0.0
Updated: 2026-05-12

## 目的
把“不同任务要读什么”从泛化建议，收成可执行的上下文包，减少长会话中的跑题和误装上下文。

## 使用规则
- 新任务开始时，先判断它属于哪个上下文包。
- 默认只装：
  - `AGENTS.md`
  - `docs/current-task-card.md`
  - 相关上下文包
  - 必要的主记忆文档
- 不默认把所有历史和所有文档一口气装进来。

## 上下文包列表
### 1. Git / PR 上下文包
适用：
- 同步 `origin/main`
- 开分支
- 提交
- push
- PR 准备

优先读取：
- `AGENTS.md`
- `docs/current-task-card.md`
- `docs/ai-memory/td-agent-task-intake-protocol.md`
- `docs/ai-workspace-bootstrap-methodology.md`
- `docs/ai-memory/td-memory-rules-and-history.md`

### 2. 关卡地图制作上下文包
适用：
- 新建关卡
- 调整路线
- 调整 BuildZone
- 调整 SpawnGate / DefensePoint
- 使用地图工具链

优先读取：
- `AGENTS.md`
- `docs/current-task-card.md`
- `docs/ai-memory/td-memory-main.md`
- `docs/ai-memory/td-memory-architecture.md`
- `docs/ai-memory/td-agent-development-playbook.md`
- `docs/map-development-tools-manual.md`
- `docs/map-toolchain-complete-level-workflow-illustrated.md`

### 3. 运行时玩法上下文包
适用：
- 放置规则
- 继电器供电
- 刷怪与波次
- 经济规则
- 烟测 / 可玩性修复

优先读取：
- `AGENTS.md`
- `docs/current-task-card.md`
- `docs/gameplay-redesign-spec.md`
- `docs/ai-memory/td-memory-main.md`
- `docs/ai-memory/td-memory-architecture.md`
- `docs/ai-memory/td-memory-rules-and-history.md`

### 4. UI / HUD 上下文包
适用：
- HUD 布局
- 放置卡 UI
- 关卡可视区与 UI 适配
- UI 文案

优先读取：
- `AGENTS.md`
- `docs/current-task-card.md`
- `docs/ai-memory/td-memory-main.md`
- `docs/ai-memory/td-memory-architecture.md`
- `docs/ai-memory/td-memory-rules-and-history.md`

### 5. 剧情 / 2D 横板上下文包
适用：
- `Story_Intro_01`
- `StoryInterludePlaceholder`
- `_Project/Story`
- 对话推进与 CampaignFlow

优先读取：
- `AGENTS.md`
- `docs/current-task-card.md`
- `docs/ai-memory/td-memory-main.md`
- `docs/ai-memory/td-memory-architecture.md`
- `docs/ai-memory/td-memory-rules-and-history.md`

## 维护规则
- 新任务域出现三次以上，就考虑新增上下文包。
- 如果上下文包内容过大，优先继续拆分，不要让单个包重新膨胀成“所有都读”。

