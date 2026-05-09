# Tower Defense AI Memory - Rules And History
Version: 1.2.0
Updated: 2026-05-09
Depends on: `docs/ai-memory/td-memory-main.md`

## 当前项目规则
### 文档规则
- 主记忆文档、架构文档、规则历史文档、执行手册和 AI 工作区方法文档必须一起维护
- `docs/` 内中文必须保持可读，不能出现乱码
- 只要当前代码或场景状态与文档冲突，以代码和场景为准，再回写文档
- 在没有用户明确删除指令时，`docs/` 目录只允许新增和更新，不允许删除
- 这条规则同样约束仓库同步操作：同步到 `origin/main` 也不能导致 `docs/` 下文档消失

### 代码规则
- `【塔防开发】` 范围内的新脚本和改动脚本保持高注释密度
- 改逻辑必须同步改注释
- 新脚本按现有分层目录放置，不散放

### 场景与关卡规则
- 地图主要由用户自己在 Scene 里继续做
- 显式场景引用优先于运行时对象名查找
- 道路功能层与道路美术层保持分离
- 不要把“场景存在”和“已正式进当前 Build Settings”混为一谈

### Git 与协作规则
- 项目采用 fork 工作流
- `origin/main` 是权威主线
- 每天开工前先同步本地 `main` 到 `origin/main`
- 工作区不干净时，不做盲目 destructive sync
- `docs/project-tech-learning-handbook.local.md` 视为本地私有笔记，默认不提交

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
- 第三关、第四关路线与道路美术层仍需继续人工精修。
- `Level04` 仍保留禁用状态的 `Legacy_EnemyPathArchive` 与 `Legacy_DefensePointArchive` 历史残留。
- `RoadArtAuthoringWindow` 已适合接入正式美术 prefab，但道路美术层是否真正落进每张场景，仍应在 Unity 内人工确认。

## 当前高优先 TODO
1. 把当前 `main` 的 Build Settings 与正式塔防关卡链重新对齐
2. 继续打磨 `Level02 ~ Level04`
3. 让道路美术层在各关卡里真正稳定落地
4. 继续完善 `Level04` 双防御点、四出怪口节奏
5. 持续验证 `WaveCatalogAsset` 主工作流覆盖面

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

## 当前路线图
### R1 关卡内容
- 继续打磨 `Level02 ~ Level04`
- 完成 `Level05` 的结构定位

### R2 地图制作工作流
- 继续提高道路美术层生产力
- 继续减少旧残留对象
- 让拓扑编辑与蓝图重构更加稳

### R3 策划与平衡
- 继续扩大 `WaveCatalogAsset` 主工作流覆盖面
- 让关卡报告更适合横向比较不同关卡难度

### R4 剧情与主链收口
- 决定 `Story_Intro_01 / StoryInterludePlaceholder / Story_Demo` 与当前 Build Settings 的最终关系
- 把正式塔防关卡链接回主线

## Docs Preservation Rule
- 在没有用户明确删除指令时，`docs/` 目录只允许新增和更新，不允许删除。
- 如果同步会让 `docs/` 下文档消失，必须先保全这些文档或先征求用户确认。
