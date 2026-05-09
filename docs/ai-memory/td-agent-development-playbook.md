# Tower Defense Agent Development Playbook
Version: 1.6.0
Updated: 2026-05-09
Audience: 后续继续开发本项目的人类维护者与智能体

## 开工前固定流程
1. 先读 `AGENTS.md`
2. 读 `docs/ai-memory/td-memory-main.md`
3. 如任务涉及场景装配、地图结构、UI、拖拽放置，再读 `td-memory-architecture.md`
4. 如任务涉及规范、历史、验收、路线图，再读 `td-memory-rules-and-history.md`
5. 如任务涉及玩法规则，再读 `docs/gameplay-redesign-spec.md`
6. 最后再读相关脚本、Prefab、Scene

## 每天开工前的仓库同步规则
- 本项目采用 fork 工作流
- 项目创建者自己的 `origin/main` 是权威主线
- 每天开始做实质性工作前：
  1. `fetch origin`
  2. 刷新本地 `main`
  3. 让本地 `main` 对齐 `origin/main`
  4. 再从最新的本地 `main` 继续功能分支工作
- 如果当前工作区不干净：
  - 先留快照分支
  - 或先征求用户确认
- 不要默认从 `upstream/main` 做日常同步

## 当前推荐开发顺序
### 做一张新关卡
1. `LevelTopologyEditorWindow`
2. `EnemyPathAuthoringTool`
3. `Map Development Toolkit > Path Check`
4. `Map Development Toolkit > Road Build`
5. `Map Development Toolkit > Zone Brush`
6. `RoadArtAuthoringWindow`
7. `Wave Preview`
8. `LevelBalanceTuningWindow`
9. `Health Check`
10. `Export Level Design Report`

### 大改已有路线骨架
1. 留档
2. `LevelRouteBlueprintApplier`
3. `LevelTopologyEditorWindow`
4. `Path Check`
5. `Road Build`
6. `RoadArtAuthoringWindow`
7. `TowerDefenseValidationRunner`

### 只做策划平衡
1. `LevelBalanceTuningWindow`
2. `Wave Preview`
3. `Export Level Design Report`

## 当前特殊提醒
- 当前 `main` 的 Build Settings 还没有完全切到正式塔防关卡链，做场景相关任务时必须先确认“这个场景只是存在”还是“已经正式启用”。
- `td-memory-main.md` 与 `td-memory-rules-and-history.md` 不能再丢失。
- `docs/project-tech-learning-handbook.local.md` 是本地私有文件，默认不提交。
- 第三关和第四关仍在持续打磨阶段，自动工具不能代替最终人工关卡判断。

## Docs Preservation Rule
- 在没有用户明确说“删除某个 docs 文件”之前，`docs/` 目录只允许新增和更新，不允许删除。
- 这条规则同样约束仓库同步操作：同步到 `origin/main` 也不能导致 `docs/` 下的记忆文档、方法文档、手册或其他项目文档消失。
- 如果同步目标与本地 `docs/` 冲突，先保全文档，再决定如何继续同步。
