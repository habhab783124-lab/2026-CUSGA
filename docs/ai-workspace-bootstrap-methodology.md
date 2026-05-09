# AI Workspace Bootstrap Methodology

Version: 1.3.0
Updated: 2026-05-09

## 目的
记录本项目 AI 协作工作区的可复用约定，避免后续智能体重复摸索。

## 仓库模型
- 本项目采用 fork 工作流
- 项目创建者自己的 `origin` 仓库是权威主仓库
- `origin/main` 是当前工作区的权威主线

## 启动方法
1. 先读 `AGENTS.md`
2. 再读 `docs/ai-memory/td-memory-main.md`
3. 按任务类型补读：
   - `td-memory-architecture.md`
   - `td-memory-rules-and-history.md`
   - `docs/gameplay-redesign-spec.md`
4. 再读相关脚本、Prefab、Scene

## 每日同步方法
在开始一天的开发前：
1. `fetch origin`
2. 刷新本地 `main`
3. 让本地 `main` 对齐 `origin/main`
4. 只有在本地 `main` 已最新的前提下，再继续功能分支工作

## 安全同步规则
- 工作区干净时，可以直接更新本地 `main`
- 当前分支不是 `main` 时，也应先刷新本地 `main`
- 如果工作区有未提交跟踪改动或重要未跟踪文件：
  - 不要直接 destructive sync
  - 优先先留快照分支
  - 或先征求用户确认

## 文档方法规则
- 记忆文档必须区分：
  - 当前代码/资源真实存在什么
  - 当前 Build Settings / 主链实际启用了什么
- 恢复文档时，不能只看 Git 索引状态，必须确认磁盘实体文件是否真的存在
- 只要 AI 工作区约定变化，就同步：
  - `AGENTS.md`
  - `docs/ai-memory/td-agent-development-playbook.md`
  - `docs/ai-memory/td-memory-main.md`
  - 本文档

## 本地私有文件约定
- `docs/project-tech-learning-handbook.local.md`
  - 视为本地私有笔记
  - 默认不提交

## 当前项目特别说明
- 项目自己的三级 AI 记忆文档曾在历史中被移出仓库，现已恢复。
- 后续不得再让以下主文档缺失：
  - `td-memory-main.md`
  - `td-memory-architecture.md`
  - `td-memory-rules-and-history.md`

## Docs Preservation Rule
- 在没有用户明确删除指令时，`docs/` 目录遵循“只增不减”原则。
- 智能体可以新增文档、更新文档、恢复丢失文档，但不能自行删掉文档。
- 同步 `origin/main` 时也不能因为远端缺少某个文档，就把本地 `docs/` 里的文档删除掉。
