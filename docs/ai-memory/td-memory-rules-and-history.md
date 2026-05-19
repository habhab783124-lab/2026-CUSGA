# 塔防开发规则与历史
Version: 2.1.0
Updated: 2026-05-18
Depends on: `docs/ai-memory/td-memory-main.md`

## 当前项目规则
### 文档规则
- 主记忆文档、架构文档、规则历史文档、执行手册、任务接单协议和工作区方法文档必须一起维护。
- `docs/` 内中文必须保持可读，不能出现新的乱码。
- 只要当前代码或场景状态与文档冲突，以代码和场景为准，再回写文档。
- 在没有用户明确删除指令时，`docs/` 目录只允许新增和更新，不允许删除。
- 这条规则同样约束仓库同步操作：同步到 `origin/main` 也不能导致 `docs/` 下文档消失。

### 执行规则
- 对非平凡任务，必须先复述理解，再等待用户确认 `执行` 后动手。
- 复述时必须明确：
  - 本次要做什么；
  - 本次不做什么；
  - 可能改哪些文件、场景或系统。
- 不允许把自己上一次提出的“下一步建议”误当成这一次的正式任务。
- 不允许在执行中途擅自切换到旧任务或相邻任务。
- 不允许自动把用户选中的历史文本直接当成当前任务，必须先判断它和当前请求的关系。
- 当前任务来源优先级：
  1. 用户当前这条最新请求里真正表达的任务
  2. 用户选中的引用内容
  3. 当前任务卡
  4. 助手上一条自己提出的“下一步建议”

### 代码规则
- `【塔防开发】` 范围内的新脚本和改动脚本保持高注释密度。
- 改逻辑必须同步改注释。
- 新脚本按现有分层目录放置，不散放。

### 场景与关卡规则
- 地图主要由用户自己在 Scene 里继续做。
- 显式场景引用优先于运行时对象名查找。
- 道路功能层与道路美术层保持分离。
- 不要把“场景存在”和“已正式进当前 Build Settings”混为一谈。
- 敌人移动动画优先挂在 `VisualScaleRoot` 显示层，不要误绑到 prefab 根节点的空 `SpriteRenderer`。

### Git 与协作规则
- 项目采用 fork 工作流。
- `origin/main` 是权威主线。
- 每天开工前先同步本地 `main` 到 `origin/main`。
- 工作区不干净时，不做盲目 destructive sync。
- `docs/project-tech-learning-handbook.local.md` 视为本地私有笔记，默认不提交。

## 当前记忆工作流
- 采用分层记忆：
  - 当前会话摘要
  - 常驻流程文档
  - 项目主记忆
  - 历史与索引
- 采用轻量整理：
  - 去重
  - 消解冲突
  - 保留最短、最准确、最可执行的版本
- 当前阶段不优先引入重型自动整理和多智能体并行，先确保主任务对齐和规则持久化。
- 当前阶段采用 `docs/current-task-card.md` 作为 L1 任务边界载体。
- 当前阶段采用 `docs/workflow-context-packages.md` 控制上下文装配范围。
- 当前阶段采用 `docs/ai-memory/td-decision-log.md` 作为轻量长期决策档案层。

## 当前验证规则
- 运行时代码改动后至少执行一次：
  - `dotnet build Assembly-CSharp.csproj -nologo`
- 地图结构修改后优先使用：
  - `TowerDefenseValidationRunner`
  - `Map Development Toolkit > Health Check`
- 策划确认波次和难度时优先使用：
  - `Wave Preview`
  - `Level Design Report`

## 当前已知问题
- 项目自己的三级 AI 记忆文档曾在历史中被移出仓库，恢复后不得再次丢失。
- 当前 `main` 的 Build Settings 还没有完全切到正式塔防关卡链。
- `Assembly-CSharp-Editor.csproj` 的命令行 `dotnet build` 在当前环境下不稳定，不能完全代表 Unity 内部编辑器脚本编译结果。
- 第三关、第四关的路线与道路美术层仍需继续人工精修。

## 当前高优先级 TODO
1. 把当前 `main` 的 Build Settings 与正式塔防关卡链重新对齐
2. 继续打磨 `Level02 ~ Level04`
3. 让道路美术层在各关卡里真正稳定落地
4. 持续验证 `WaveCatalogAsset` 主工作流覆盖面

## 近期开发历史
- 2026-04：引入剧情场景与 `CampaignFlow` 链路
- 2026-04：项目逐步迁移到 `Core / Map / Placement / Towers / Enemies / UI`
- 2026-04：恢复并确认 `SampleScene` 作为第一关标准模板
- 2026-05：开始系统化重构 `Level02 ~ Level04` 的地图结构与出怪口拓扑
- 2026-05：完成第一批地图开发工具链
- 2026-05：`WaveSpawner` 切换到 `WaveCatalogAsset` 优先主链
- 2026-05：恢复项目自己的三级 AI 记忆文档主文件
- 2026-05：增加“每天开工前同步到 `origin/main`”规则
- 2026-05：增加“docs 目录只增不减，连同步也不能冲掉文档”的规则
- 2026-05-12：增加“先复述确认再执行”的接单规则
- 2026-05-12：增加“分层记忆 + 轻量整理 + 历史检索”的工作流
- 2026-05-12：新增 L1 当前任务卡、上下文包、做梦整理 checklist 和决策日志
- 2026-05-18：完成 8 种敌人的基础移动动画接入，并新增 `EnemyMoveAnimationAuthoringTool` 作为可重复执行的批量重建入口
- 2026-05-18：在 `try.unity` 中新增 `EnemyAnimationPreviewRoot` 和 8 个 `Preview_*Enemy` 场景实例，用于 Scene 视图下直接检查怪物动画接入情况
- 2026-05-18：把 8 个敌人 prefab 的血条规则从“统一绝对高度”修正为“怪物最高点 + 固定间距”，并让作者工具按该规则动态计算 `HealthBarRoot` 位置
- 2026-05-18：新增 `EnemyPrefabTuning.unity` 和 `EnemyPrefabTuningWindow`，把怪物 prefab 的 Scene 微调流程收口为“专用场景 + 小范围 Transform 回写”，不再依赖对整只实例 `Apply All`

## 当前路线图
### R1 关卡内容
- 继续打磨 `Level02 ~ Level04`
- 完成 `Level05` 的结构定位

### R2 地图制作工作流
- 继续提高道路美术层生产力
- 继续减少旧残留对象
- 让拓扑编辑与蓝图重构更加稳定

### R3 策划与平衡
- 继续扩大 `WaveCatalogAsset` 主工作流覆盖面
- 让关卡报告更适合横向比较不同关卡难度

### R4 剧情与主链收口
- 决定 `Story_Intro_01 / StoryInterludePlaceholder / Story_Demo` 与当前 Build Settings 的最终关系
- 把正式塔防关卡链接回主线
