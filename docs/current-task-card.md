# 当前任务卡
Status: 已完成
Updated: 2026-05-20

## 当前任务
- 把当前正式剧情场景与四个正式塔防关卡之间的切换链路接通。
- 本次确认的真实场景链路是：
  - `chapter1 -> level 1 -> chapter2`
  - `chapter4 -> Level 2 -> chapter5 -> Level 3 -> chapter6`
  - `chapter8 -> level 4 -> chapter9`

## 本次只做
- 让 `chapter1` 剧情结束后切到 `level 1`。
- 让 `chapter4` 剧情结束后切到 `Level 2`。
- 让 `chapter5` 剧情结束后切到 `Level 3`。
- 让 `chapter8` 剧情结束后切到 `level 4`。
- 让 `level 1 / Level 2 / Level 3 / level 4` 胜利后分别切到 `chapter2 / chapter5 / chapter6 / chapter9`。
- 让塔防关卡失败后在 `Game Over` 出现后，玩家点击任意位置重开当前关卡。
- 把这四个正式塔防关卡加入 `Build Settings`，确保运行时能正确切场景。

## 本次明确不做
- 不改 `chapter` 与 `chapter` 之间已经做好的切换。
- 不改主界面或 Opening 到 `chapter1` 的入口。
- 不改塔防数值、波次配置、地图布局或 UI 视觉。
- 不启用或重做整套 `CampaignFlowAsset` 主线流程，只补当前实际使用的场景跳转。

## 完成标准
- `chapter1 / chapter4 / chapter5 / chapter8` 在剧情结束时会自动切到对应塔防关卡。
- 四个塔防关卡胜利后会自动切到对应剧情场景。
- 四个塔防关卡失败后，`Game Over` 出现后点击任意位置会重开当前关卡。
- 四个正式塔防关卡都已经进入 `ProjectSettings/EditorBuildSettings.asset`。

## 直接相关文件
- `Assets/_Project/Story/Scripts/Dialogue/Chapter1.cs`
- `Assets/_Project/Story/Scripts/Dialogue/Chapter5.cs`
- `Assets/_Project/Story/Scripts/StoryNpcWalkIntro2D.cs`
- `Assets/Scripts/TowerDefense/Core/TowerDefenseGame.cs`
- `Assets/Scripts/TowerDefense/Map/WaveSpawner.cs`
- `Assets/chapter1.unity`
- `Assets/chapter4.unity`
- `Assets/chapter5.unity`
- `Assets/chapter8.unity`
- `Assets/Scenes/level 1.unity`
- `Assets/Scenes/Level 2.unity`
- `Assets/Scenes/Level 3.unity`
- `Assets/Scenes/level 4.unity`
- `ProjectSettings/EditorBuildSettings.asset`
- `docs/current-task-card.md`
- `docs/current-task-card.json`

## 任务来源
- 本轮任务直接来源于用户最新要求：只实现 `chapter` 与 `level` 之间的场景切换，不处理主界面入口，不重做 `chapter` 之间已经完成的切换。
