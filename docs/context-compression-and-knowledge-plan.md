# 上下文压缩与知识沉淀方案
Version: 1.0.0
Updated: 2026-05-12

## 目标
把“长会话管理”和“长期知识沉淀”落成可执行、低自动化风险的工作机制。

## 当前策略
### L1 当前任务卡
文件：
- `docs/current-task-card.md`

作用：
- 只保留这一轮任务最必要的信息。

内容：
- 当前任务
- 本轮要做
- 本轮明确不做
- 完成标准
- 直接相关文件
- 风险与注意事项

### L2 常驻项目记忆
目录：
- `docs/ai-memory/`

代表文件：
- `td-memory-main.md`
- `td-memory-architecture.md`
- `td-memory-rules-and-history.md`
- `td-agent-development-playbook.md`
- `td-agent-task-intake-protocol.md`
- `td-memory-hygiene-and-lifecycle.md`

作用：
- 保存稳定规则、结构、入口、历史摘要和执行方法。

### L3 完整历史
当前不做数据库系统。

主要来源：
- git 历史
- PR 历史
- 长文档本体
- 规则历史和决策日志
- `memory-index.paths.txt`

作用：
- 只作为需要时的长期可检索档案层。

## 渐进式压缩的当前做法
- 新任务优先写 L1。
- 当前轮默认只读：
  - L1 当前任务卡
  - 相关上下文包
  - 必要 L2 文档
- 较旧信息压缩成摘要留在 L2。
- 更老、更细的内容只保留在 L3，不默认加载。

## 当前不做的事
- 不做自动上下文压缩
- 不做自动聊天迁移到 L2/L3
- 不做向量数据库
- 不做自动语义召回

## 设计原则
- 先人工，后自动
- 先结构清晰，后追求智能化
- 先避免误删和误压缩，再追求更省上下文

