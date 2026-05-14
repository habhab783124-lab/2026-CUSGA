# AI 工作区搭建与方法论
Version: 2.0.0
Updated: 2026-05-12

## 目的
记录本项目 AI 协作工作区的可复用约定，避免后续智能体重复摸索，也避免规则只存在于单次对话里。

## 仓库模型
- 本项目采用 fork 工作流。
- 用户自己的 `origin` 仓库是权威主仓库。
- `origin/main` 是当前工作区的权威主线。

## 启动方法
1. 读 `AGENTS.md`
2. 读 `docs/ai-memory/td-memory-main.md`
3. 按任务类型补读：
   - `td-memory-architecture.md`
   - `td-memory-rules-and-history.md`
   - `docs/gameplay-redesign-spec.md`
   - `docs/ai-memory/td-agent-task-intake-protocol.md`
   - `docs/ai-memory/td-memory-hygiene-and-lifecycle.md`
4. 再读相关脚本、Prefab、Scene

## 每日同步方法
在开始一天的开发前：
1. `fetch origin`
2. 刷新本地 `main`
3. 让本地 `main` 对齐 `origin/main`
4. 只有在本地 `main` 已经最新的前提下，再继续功能分支工作

## 安全同步规则
- 工作区干净时，可以直接刷新本地 `main`
- 当前分支不是 `main` 时，也应先刷新本地 `main`
- 如果工作区有未提交跟踪改动或重要未跟踪文件：
  - 不要直接 destructive sync
  - 优先先留快照分支
  - 或先征求用户确认

## 任务接单方法
- 对非平凡任务，必须先复述理解，再等待用户确认执行。
- 复述内容必须包括：
  - 用户要什么
  - 这次只做什么
  - 这次不做什么
  - 预计会改什么
- 这条规则高于“默认直接动手”的习惯。
- 固定回复模板为：
  - `我的理解`
  - `本次只做`
  - `本次不做`
  - `预计会动到`
- 如果用户引用或选中了历史文本，不自动把它当成当前任务，而要先判断它是任务本体、补充上下文还是纠错依据。
- 任务来源优先级：
  1. 用户当前这条最新请求里真正表达的任务
  2. 用户选中的引用内容
  3. 当前任务卡
  4. 助手上一条自己提出的“下一步建议”

## 上下文包方法
- 当前项目按任务域装配最小上下文，而不是把所有文档一次性塞进来。
- 上下文包定义在：
  - `docs/workflow-context-packages.md`
- 当前重点包包括：
  - Git / PR
  - 关卡地图制作
  - 运行时玩法
  - UI / HUD
  - 剧情 / 2D 横板

## 记忆分层方法
### L1 当前会话摘要
- 只保留当前任务最关键的信息。

### L2 常驻工作流
- `AGENTS.md`
- `td-agent-development-playbook.md`
- `td-agent-task-intake-protocol.md`
- 本文档

### L3 项目记忆
- `td-memory-main.md`
- `td-memory-architecture.md`
- `td-memory-rules-and-history.md`
- `gameplay-redesign-spec.md`

### L4 历史与索引
- `memory-index.paths.txt`
- 旧摘要
- 已完成决策
- 可检索历史

## 当前任务卡
- `docs/current-task-card.md` 是 L1 的具体载体。
- `docs/current-task-card.json` 是机器可检查的最小任务闸门。
- 新任务开始时先更新任务卡，而不是让当前轮边界只存在于对话里。
- 未更新任务卡，不得进入执行阶段。
- 任务卡必须写明“本轮任务来源于哪条用户请求”。
- 高风险写操作前，可运行 `tools/check-task-gate.ps1` 做最低限度的机器检查。

## 轻量生命周期钩子
项目里没有真正的系统级 hook 时，用固定检查清单代替：
1. 先同步工作区
2. 再读常驻规则
3. 再复述任务
4. 最后执行
5. 完成后更新记忆

## 轻量做梦整理
- 先采用人工触发的轻量整理
- 暂不急着做重型全自动后台整理
- 整理目标：
  - 去重
  - 消解冲突
  - 保留最短、最准确、最可执行的版本
- 具体 checklist 见：
  - `docs/dream-maintenance-checklist.md`

## 文档方法规则
- 记忆文档必须区分：
  - 当前代码/资源真实存在什么
  - 当前 Build Settings / 主链实际启用了什么
- 恢复文档时，不能只看 Git 索引状态，必须确认磁盘实体文件真的存在
- 只要 AI 工作区约定变化，就同步：
  - `AGENTS.md`
  - `docs/ai-memory/td-agent-development-playbook.md`
  - `docs/ai-memory/td-memory-main.md`
  - 本文档

## 本地私有文件约定
- `docs/project-tech-learning-handbook.local.md`
  - 视为本地私有笔记
  - 默认不提交

## Docs Preservation Rule
- 在没有用户明确删除指令时，`docs/` 目录遵循“只增不减”原则。
- 智能体可以新增文档、更新文档、恢复丢失文档，但不能自行删掉文档。
- 同步 `origin/main` 时也不能因为远端缺少某个文档，就把本地 `docs/` 里的文档删除掉。
