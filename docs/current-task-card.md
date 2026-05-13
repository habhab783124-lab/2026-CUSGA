# 当前任务卡
Status: 进行中
Updated: 2026-05-13

## 当前任务
- 先把这批工作流构建文档整理并推到新分支 `构建工作流`，提交备注为“加强工作流约束”；之后再开始实现最小闭环版的机器检查闸门。

## 本轮要做
- 只筛选工作流相关文档改动。
- 新建分支 `构建工作流`。
- 提交并 push 这批工作流文档，备注为“加强工作流约束”。
- push 完成后，再回到最小闭环版的实现准备。

## 本轮明确不做
- 不把关卡、场景、运行时工具改动一起带进这次 workflow 提交。
- 不顺手处理别的功能开发任务。

## 完成标准
- `构建工作流` 分支创建完成。
- 仅工作流相关文档被提交并 push。
- 本次提交备注为“加强工作流约束”。

## 直接相关文件
- `AGENTS.md`
- `docs/current-task-card.md`
- `docs/ai-memory/td-agent-development-playbook.md`
- `docs/ai-workspace-bootstrap-methodology.md`
- `docs/ai-memory/td-memory-rules-and-history.md`
- `docs/ai-memory/td-memory-main.md`
- `docs/ai-memory/td-agent-task-intake-protocol.md`
- `docs/ai-memory/td-memory-hygiene-and-lifecycle.md`
- `docs/workflow-context-packages.md`
- `docs/context-compression-and-knowledge-plan.md`
- `docs/dream-maintenance-checklist.md`
- `docs/ai-memory/td-decision-log.md`
- `docs/ai-memory/memory-index.paths.txt`
- `docs/map-development-tools-manual.md`

## 风险与注意事项
- 只能提交 workflow 文档，不能误带当前分支上的工具链和关卡改动。
- `docs/` 只增不减规则仍然生效。

## 任务来源
- 本轮任务直接来源于：用户最新请求“执行，先做最小闭环版；不过在执行这个前先帮我把这批关于工作流构建的所有文档push一次到一个新分支……”
- 是否引用了历史文本：否
- 如果与旧任务卡或助手上一条建议冲突，以哪一条为准：以用户最新请求为准

## 执行前强制检查
- [x] 我已经用自己的话复述了需求
- [x] 我已经明确写出“本轮要做什么”
- [x] 我已经明确写出“本轮不做什么”
- [x] 我已经列出预计会动到的文件 / 场景 / 系统
- [x] 我已经写明“本轮任务来源于哪条用户请求”
- [x] 用户已经明确回复 `执行`
- [x] 在上面这些都完成之前，不允许改文件、不允许改场景、不允许跑写操作

## 使用规则
- 每次非平凡任务开始前先更新这张卡。
- 这张卡不是总结文档，而是执行前约束。
- 如果这张卡还是旧任务内容，视为当前轮还没有完成接单。
- 未更新任务卡，不得进入执行阶段。
- 任务完成或切换后，更新状态或重置为下一轮复用。
