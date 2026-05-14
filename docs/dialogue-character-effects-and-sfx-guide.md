# 对话角色特效与音效指南

本文档说明如何编辑对话系统中按角色区分的文本特效和打字音效。

相关运行时文件：

- `Assets/_Project/Story/Scripts/Dialogue/DialogueBubbleView.cs`
- `Assets/_Project/Story/Scripts/Dialogue/DialogueRunner.cs`
- `Assets/_Project/Story/Scripts/Dialogue/DialogueEmphasis.cs`
- `Assets/_Project/Story/Scripts/Dialogue/Chapter*.cs`

## 1. 在哪里编辑对话文本

剧情文本目前直接编写在章节脚本文件中，例如：

- `Assets/_Project/Story/Scripts/Dialogue/Chapter1.cs`
- `Assets/_Project/Story/Scripts/Dialogue/Chapter2.cs`
- `Assets/_Project/Story/Scripts/Dialogue/Chapter3.cs`

每一行都是一个 `DialogueLine`，通常通过 `Npc(...)` 或 `Player(...)` 等辅助方法创建。

示例：

```csharp
Npc("第三小队，[pause]<size=150%>重点</size>任务报告已确认。")
Player("[speed=0.5]<size=150%>为了伊甸!</size>", Strong())
```

## 2. 支持的 inline 标签

对话文本解析器当前支持以下 inline 标签：

### `<size=...>...</size>`

使用 TextMesh Pro 富文本 size 标签，让部分文本在视觉上变大或变小。

示例：

```text
普通文本<size=150%>放大文本</size>
普通文本<size=80%>缩小文本</size>
```

说明：

- 这是控制可见字符大小的推荐方式。
- 打字系统会读取此标签，用于：
  - 每个字符的弹跳/脉冲强度
  - 打字音效的音量缩放
- 旧的 `[size=...]` 自定义标签仅作为兼容性回退保留，新内容不应使用。

### `[speed=0.03]`

从此位置开始改变打字速度。

示例：

```text
前半段正常速度[speed=0.01]后半段更快
```

说明：

- 值为每个字符的绝对秒数。
- 数值越小越快。
- `0` 表示按协程能跑的最快速度显示。
- 参考示例：
  - `0.03` = 正常速度
  - `0.01` = 非常快
  - `0.5` = 非常慢

### `[pause]`

在此位置暂停打字，等待玩家输入后继续。

示例：

```text
第一段内容[pause]点击后继续
```

说明：

- 暂停时，点击会恢复打字，而不是跳过整行。

### `[sfx=poolId]`

从此位置开始切换打字音效池。

示例：

```text
[sfx=soft]轻柔语气……[sfx=robot]系统播报……
```

说明：

- `poolId` 必须匹配对话气泡预制体/组件中配置的命名池。
- 使用 `default` 可切换回默认打字音效池：

```text
[sfx=robot]系统语音[sfx=default]恢复默认音效
```

## 3. `Strong()` 如何影响一行对话

按行的强调效果由 `DialogueEmphasis` 控制，通常通过 `Normal()` 和 `Strong()` 等辅助方法使用。

章节脚本中的示例：

```csharp
private static DialogueEmphasis Strong(float scaleMultiplier = 1.35f, float shakeMagnitude = 0.12f)
{
    return new DialogueEmphasis { enabled = true, scaleMultiplier = scaleMultiplier, shakeMagnitude = shakeMagnitude };
}
```

`Strong()` 当前影响：

- 打字时气泡的抖动
- 每个字符出现时的脉冲强度

这意味着：

- 使用 `Strong()` 但没有 `<size>` 标签的行，仍然具有更强的字符弹跳效果。
- 同时使用 `Strong()` 和 `<size=150%>` 的行，会取两者中较强的效果。

推荐用法：

- 整行都需要强烈语气时使用 `Strong()`
- 只有部分文字需要突出时使用 `<size=...>`
- 两者都需强调时组合使用

示例：

```csharp
Player("为了<size=150%>伊甸</size>！", Strong())
```

## 4. 打字音效的工作原理

打字音效在 `DialogueBubbleView` 组件上配置。

主要 Inspector 字段：

- `typingAudioSource`
- `typingSfxPool`
- `typingSfxNamedPools`
- `typingSfxBaseVolume`
- `typingSfxMaxVolume`
- `typingSfxReferenceSizeScale`
- `typingSfxVolumeResponse`
- `typingSfxMinInterval`
- `playTypingSfxForWhitespace`
- `playTypingSfxForPunctuation`

### 默认音效池

`typingSfxPool` 是默认的随机音效池，在没有激活的 `[sfx=...]` 标签时使用。

### 命名音效池

`typingSfxNamedPools` 允许定义多个可选的音效组。

每个命名池包含：

- `id`
- `clips`

配置示例：

- `soft`
  - `soft_1`
  - `soft_2`
  - `soft_3`
- `robot`
  - `robot_1`
  - `robot_2`
- `default`
  - 继续使用 `typingSfxPool`，而不是创建名为 `default` 的命名池

### 随机选择行为

当一个字符出现时：

- 使用当前激活的音效池
- 随机选择一个 AudioClip
- 当有多个 clip 时，系统会尽量避免连续两次播放完全相同的 clip

### 音量缩放行为

字符音效音量基于字符大小。

当前逻辑：

- 基准大小使用 `typingSfxBaseVolume`
- 更大字符的音量向 `typingSfxMaxVolume` 靠近
- 过渡曲线由以下参数控制：
  - `typingSfxReferenceSizeScale`
  - `typingSfxVolumeResponse`

实用调校建议：

如果大小差异导致的音量变化不明显：
- 提高 `typingSfxMaxVolume`
- 降低 `typingSfxBaseVolume`
- 降低 `typingSfxReferenceSizeScale`
- 提高 `typingSfxVolumeResponse`

建议的起始值：

- `typingSfxBaseVolume = 0.12`
- `typingSfxMaxVolume = 0.85`
- `typingSfxReferenceSizeScale = 1.5`
- `typingSfxVolumeResponse = 1.8`

## 5. 音频文件存放位置

推荐位置：

- `Assets/_Project/Story/Audio/Dialogue/SFX/`

这仅仅是组织建议。打字音效系统不需要 `Resources`。

推荐工作流：

1. 在 `Assets/` 下的任意位置导入音频文件
2. 将对话打字音效放入专用文件夹
3. 在 Inspector 中将音频文件拖入 `typingSfxPool` 或 `typingSfxNamedPools`

仅当你还需要通过代码路径加载音频文件时，才需要使用 `Resources`。

## 6. 典型的编辑模式

### 普通对话

```csharp
Npc("这是普通对话。")
```

### 整行强语气

```csharp
Player("明白。", Strong())
```

### 关键词强调

```csharp
Npc("任务目标是<size=150%>核心区域</size>。")
```

### 中间暂停

```csharp
Npc("请确认你的身份。[pause]验证通过。")
```

### 中间改变速度

```csharp
Npc("系统初始化中……[speed=0.01]警报！警报！警报！")
```

### 切换打字音效池

```csharp
Npc("[sfx=robot]系统广播启动。[sfx=default]恢复普通对话。")
```

### 组合使用

```csharp
Player("[sfx=robot][speed=0.02]为了<size=150%>伊甸</size>！", Strong())
```

## 7. 当前实现细节

关键运行时流程：

1. `DialogueRunner` 将一行对话发送给 `DialogueBubbleView.TypeLine(...)`
2. `DialogueBubbleView` 从显示文本中去除控制标签
3. 预处理：
   - 暂停位置
   - 每个字符的大小数据
   - SFX 音效池切换点
4. 使用 `maxVisibleCharacters` 逐字显示字符
5. 每个字符显示时：
   - 可能播放音效
   - 可能播放字符脉冲动画
   - 应用速度规则

## 8. 已知规则和注意事项

- `<size=...>` 是改变可见文字大小的支持方式
- `[speed=...]` 从标签位置开始改变后续打字速度，而不仅仅是单个字符
- `[sfx=...]` 从标签位置开始切换激活的音效池，而不仅仅是单个字符
- 字符脉冲是临时效果，动画结束后应恢复到原始字形几何
- `Strong()` 即使没有 `<size>` 标签也会增强脉冲强度
- 如果当前行所选音效池中没有有效的 AudioClip，打字会静音继续

## 9. 故障排除

### `speed` 感觉不对

检查文本是否使用了：

```text
[speed=0.01]
```

而不是错误格式如：

```text
[speed = 0.01]
```

使用小数点 `.` 而不是逗号 `,`。

### 音效池没有切换

检查：

- 标签写法是否为 `[sfx=robot]`
- `typingSfxNamedPools` 中的池 `id` 是否精确为 `robot`
- 该音效池中至少有一个非空的 `AudioClip`

### 更大的文字音量没有变大

检查：

- 被强调的文字是否使用了 `<size=...>`
- `typingSfxMaxVolume` 是否明显高于 `typingSfxBaseVolume`
- `typingSfxReferenceSizeScale` 是否设置得过高

### Strong() 行感觉不够强

检查：

- 章节代码中的对话行是否确实传入了 `Strong()`
- `Strong()` 中的 `scaleMultiplier` 是否足够高
- `Strong()` 中的 `shakeMagnitude` 是否足够高

## 10. 建议的维护规则

- 整行情绪重量用 `Strong()`
- 局部词语强调用 `<size=...>`
- `[speed=...]` 应谨慎使用，通常只用于一个戏剧性片段
- 命名 SFX 音效池用于表现语音个性变化，如机器人、耳语、电台、系统音
- 保持音效池名称简短且稳定：`default`、`robot`、`soft`、`radio`、`warning`