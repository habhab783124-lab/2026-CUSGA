# Tower Defense AI Memory - Rules And History
Version: 1.5.5
Updated: 2026-05-02
Depends on: `docs/ai-memory/td-memory-main.md`

## Standing Rules
- 新需求默认先复述理解，再等待用户说“执行”后再改文件。
- 对“大阶段”任务，只在阶段开始时确认一次；阶段内部可以自行分步推进。
- 文档与脚本注释中的中文必须保持正常显示，禁止乱码。
- 运行时脚本必须按分层目录放到 `Assets/Scripts/TowerDefense/*`。
- 编辑器脚本必须放到 `Assets/Editor/TowerDefense/*`。
- 地图、UI、Prefab 与可视化入口必须尽量显式、Inspector 友好、Scene 友好。
- 当文档与当前代码冲突时，优先相信当前代码/场景状态，然后回写文档。

## Repository Reality
- 远端 `main` 当前受仓库规则保护，不能直接 push。
- 正常上传远端留档时：
  - 要么走 Pull Request
  - 要么先推到快照分支
- 当前已知远端快照分支：
  - `snapshot-2026-04-22-enemy-modules-ui-wiring`

## Recent Delivered Milestones
### Core Gameplay
- 多出怪口地图骨架、BuildZone、路径、防御点、波次主链已经完成。
- 继电器供电、断电停工、升级校验主链已经完成。
- 单体塔、减速塔、炸弹塔主链已经完成。
- 废料经济主链已经完成。

### Scene And UI Authoring
- 主菜单与关卡选择页已经走到“Scene 主导、脚本只接行为”的较稳定状态。
- `LevelSelect.unity` 已显式接上 `LevelSelectCatalog.asset`。
- HUD 已开始支持从单块文本向显式多块文本收口。
- `TowerShopCard` 已显式持有自己的文本引用，不再依赖跨层级猜测。

### Map Authoring Workflow
- `BuildZone` 当前已支持 `ZoneShapes` 多碰撞体工作流，不再只适合单矩形建造区。
- 新增 `BuildZoneEditor`，可显式创建 `ZoneShapes` 根并一键收集形状碰撞体。
- `EnemyPath / EnemySpawnGate / DefensePointFlag` 当前已支持“程序化占位”和“作者接管根节点”双模式。
- `WaveSpawner` 当前默认走显式 `WaveCatalogAsset + EnemyCatalogAsset` 资产主链，不再把旧组件内波次数组当成主工作流。
- `Level02 / Level03 / Level04` 已经进入第一版地图草稿阶段，不再只是空骨架。
- 用户先后要求尝试多个历史提交的 `SampleScene` 版本；当前工作区已撤回这串试切操作，回到“后续地图修改阶段”的兼容基线，接下来应以用户的直接场景修改指令为准继续推进。
- `Level04` 当前额外挂了 `Level04RingGuide + Level04RingGuideEditor`，用于在 Scene 视图中持续提示外环 / 中环 / 内环的作者语义。
- `Level04` 的最终战场几何已做过一轮纠偏：三入口依然保留，但终点与最后汇合段已经拉回三环中枢右侧，不再停留在过长的右下走廊。
- `Level02 / Level03 / Level04` 这一轮还修过一批“与第一关主链不一致”的问题：
  - 战斗塔 prefab 场景引用曾指向错误 fileID，现已纠正
  - 路线提示的程序化可读性开关曾缺项，现已补齐
  - 波次刷怪链曾在 `WaveSpawner.SpawnEnemy()` 里触发 `InvalidCastException`，现已通过更稳的实例化路径修正
  - Gameplay 场景中文 HUD / 波次文案已切到 `zpix SDF` 字体链路，避免继续出现小方块

### Enemy System
- 敌人目录资产、每关波次资产、8 个独立敌人 prefab 已接入。
- `Enemy` 已开始从“单个大脚本”向“基础壳 + 机制模块”重构。
- 当前已拆出的模块：
  - `EnemyStealthModule`
  - `EnemyShieldAuraModule`
  - `EnemyRepairModule`
  - `EnemySplitOnDeathModule`
- 这些模块支持两层参数来源：
  - `EnemyCatalogAsset` 全局默认值
  - prefab 上 `useLocalOverrides` 的本地覆盖值
- `EnemyEditor` 与模块专用 Inspector 已补入。

### Git History Landmarks
- `0a51364`
  `refactor enemy prefabs into base enemy plus mechanic modules`
- `51f7519`
  `tighten scene-authored UI wiring and HUD editor workflow`

## Current Known Gaps
- 编辑器脚本的最终效果仍应优先在 Unity Inspector 里确认，不能只依赖命令行编译。
- `Level02 / Level03 / Level04` 已有第一版草稿，但路径可读性、禁建区、相机视野和三环层次仍需在 Unity 内继续人工打磨。
- `Level05` 仍主要是骨架场景，正式地图内容还需要继续制作。
- `StoryInterludePlaceholder` 仍是占位剧情场景，尚未并入真实 2D 横板内容。
- 当前有一批流程和美术入口已经为正式资源替换准备好，但正式资源本体仍未落地。
- 虽然 `BuildZone` 已支持不规则建造区，但新的 `ZoneShapes` 工作流是否足够顺手，还需要继续人工验证。

## Current Manual Validation Priorities
1. 确认 4 个带特殊模块的敌人 prefab Inspector 显示正确。
2. 确认 `Wolf`、`HeavyArmoredMachine` 的 `EnemyEditor` 摘要符合目录定义。
3. 确认新的 `BuildZone / ZoneShapes` 不规则建造区工作流在 Unity 中正常工作。
4. 确认 `LevelSelect` 页面与卡片在 Unity 中显示、点击与返回正常。
5. 确认剧情占位场景和塔防关卡切换正常。
6. 确认 `Level04` 的三环底板、Scene 标签和高/中/低风险塔位分层在作者视角下足够清晰。

## Current Recommended Next Steps
1. 继续做人工验证，把 Inspector 结果与静态文件状态对齐。
2. 继续细化 `Level02` 到 `Level04` 的地图草稿，并开始制作 `Level05`。
3. 继续替换正式美术资源，同时保持显式作者入口不退化。
4. 等故事横板内容合并后，再推进剧情-塔防整合验证。
