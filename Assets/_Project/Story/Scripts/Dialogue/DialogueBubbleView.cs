using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界空间对话气泡：跟随锚点、按文本测量宽高、背景 Image 使用 Sliced+Sprite Border 铺满面框、屏幕内夹紧。
/// 预制体要求：见菜单 Tools/Story/Create Dialogue Bubble Prefab
/// </summary>
public sealed class DialogueBubbleView : MonoBehaviour
{
    [Header("Refs（由预制体绑定）")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform bubbleFrame;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform tailRect;
    [SerializeField] private Image tailImage;
    [SerializeField] private DialogueBubbleTailGraphic tailGraphic;
    [SerializeField] private TextMeshProUGUI lineText;

    [Header("跟随")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.05f, 0f);

    [Header("内边距与尺寸（与 DialogueRunner 一致，单位随 Canvas 世界缩放）")]
    [SerializeField] private Vector2 contentPadding = new Vector2(0.55f, 0.35f);
    [SerializeField] private float maxBubbleWidth = 7.2f;
    [SerializeField] private float minBubbleWidth = 2f;
    [SerializeField] private float minBubbleHeight = 1.2f;

    [Header("背景图")]
    [Tooltip("Sliced 使用 Sprite 在 Sprite Editor 中设置的 Border；不要勾 Image 的 Preserve Aspect。")]
    [SerializeField] private Image.Type backgroundImageType = Image.Type.Sliced;

    [Tooltip("Sliced 专用：在 UI 中缩放 Sprite 的「像素/单位」，可单独调九宫格角/边在屏幕上的视觉粗细，而不改原图 PPU。过小会让中心区看起来异常。")]
    [SerializeField] [Range(0.1f, 4f)]
    private float imagePixelsPerUnitMultiplier = 1f;

    [Header("尾巴（倒三角 tail）")]
    [SerializeField] private bool showTail = true;
    private bool hideTailInCurrentBottomLayout;
    [Tooltip("尾巴的宽高（世界空间单位，随 Canvas 缩放）。")]
    [SerializeField] private Vector2 tailSize = new Vector2(0.8f, 0.55f);
    [Tooltip("尾巴相对气泡底部中心的偏移。X: 左右；Y: 往下（正数表示更向下）。")]
    [SerializeField] private Vector2 tailOffset = new Vector2(0f, 0.0f);
    [Tooltip("尾巴图片（可选：小三角形 Sprite）。为空时会使用程序化三角形（tailGraphic）。")]
    [SerializeField] private Sprite tailSpriteOverride;

    [Header("屏幕夹紧")]
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float screenEdgePadding = 8f;

    [Header("字体（可空，由 Runner 设置）")]
    [SerializeField] private TMP_FontAsset fontOverride;
    [SerializeField] private float fontSizeOverride = -1f;

    private Transform follow;
    private Coroutine typeRoutine;
    private bool isTyping;
    private bool skipType;
    private bool isPaused;
    private bool wasPausedByControlTag;
    private float currentSecondsPerChar;
    private List<CharAnimationData> charAnimations;
    private HashSet<int> pauseAtCharIndex;
    private Vector3 bubbleBaseLocalScale = Vector3.one;
    private float shakeMag;
    private Vector3 shake;
    private Vector3 contentBaseLocalPos = Vector3.zero;
    private bool useScreenBottomLayout;
    private Vector2 screenBottomSize = new Vector2(1280f, 220f);
    private Vector2 screenBottomOffset = new Vector2(0f, 120f);
    private float screenBottomAlpha = 0.6f;

    private bool _built;
    private Coroutine _imageRefreshRoutine;

    public bool IsTyping => isTyping;
    public bool IsPaused => isPaused;
    public bool WasPausedByControlTag => wasPausedByControlTag;

    [System.Serializable]
    public sealed class CharAnimationData
    {
        public int charIndex;
        public float fontSizeScale = 1f;
        public float speedMultiplier = 1f;

        public CharAnimationData(int index, float sizeScale, float speedMult)
        {
            charIndex = index;
            fontSizeScale = sizeScale;
            speedMultiplier = speedMult;
        }
    }

    private void OnDisable()
    {
        if (_imageRefreshRoutine != null)
        {
            StopCoroutine(_imageRefreshRoutine);
            _imageRefreshRoutine = null;
        }
    }

    public void SetFont(TMP_FontAsset f)
    {
        fontOverride = f;
        if (lineText != null && f != null)
        {
            lineText.font = f;
        }
    }

    public void SetFontSizeOverride(float size)
    {
        fontSizeOverride = size;
        if (lineText != null && size > 0f)
        {
            lineText.fontSize = size;
        }
    }

    public void SetBottomScreenLayout(bool enabled, Vector2 size, Vector2 offset, float alpha, bool hideTail)
    {
        useScreenBottomLayout = enabled;
        screenBottomSize = size;
        screenBottomOffset = offset;
        screenBottomAlpha = Mathf.Clamp01(alpha);
        hideTailInCurrentBottomLayout = hideTail;

        if (!_built)
        {
            return;
        }

        ApplyBottomScreenLayout();
        ApplyTailLayout();
    }

    /// <summary>是否绘制尾巴（小三角）。关闭后仅保留矩形气泡与文本。</summary>
    public void SetShowTail(bool visible)
    {
        showTail = visible;
        if (tailRect != null)
        {
            ApplyTailSetup();
        }
    }

    /// <summary>按完整文本排好气泡尺寸后清空可见文字（用于先闪框再打字）。</summary>
    public void PrepareLayoutEmptyText(string fullContent)
    {
        BuildIfNeeded();
        LayoutForFullString(fullContent ?? string.Empty);
        if (lineText != null)
        {
            lineText.text = string.Empty;
        }
    }

    /// <summary>对话气泡「出现」闪烁：框体从不透明渐显并略带脉冲（非全屏）。</summary>
    public IEnumerator FlashAppearChromeRoutine(float halfDuration, float peakAlpha)
    {
        BuildIfNeeded();
        if (bubbleFrame == null)
        {
            yield break;
        }

        CanvasGroup cg = EnsureChromeCanvasGroup();
        float saved = cg.alpha;
        cg.alpha = 0f;
        float half = Mathf.Max(0.02f, halfDuration * 0.5f);
        float peak = Mathf.Clamp01(peakAlpha);
        yield return FadeCanvasGroupUnscaled(cg, 0f, peak, half);
        yield return FadeCanvasGroupUnscaled(cg, peak, 1f, half);
        cg.alpha = Mathf.Max(saved, cg.alpha);
    }

    /// <summary>对话气泡「关机」感：白闪几次后整体渐隐（仅气泡，非全屏）。</summary>
    public IEnumerator ShutdownChromeRoutine(int whiteFlickerCount, float flickerSegmentDuration, float fadeOutDuration)
    {
        BuildIfNeeded();
        if (bubbleFrame == null)
        {
            yield break;
        }

        CanvasGroup cg = EnsureChromeCanvasGroup();
        Color bg0 = backgroundImage != null ? backgroundImage.color : Color.white;
        float seg = Mathf.Max(0.01f, flickerSegmentDuration);
        cg.alpha = Mathf.Max(0.01f, cg.alpha);

        for (int i = 0; i < whiteFlickerCount; i++)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(bg0, Color.white, 0.88f);
            }

            yield return FadeCanvasGroupUnscaled(cg, cg.alpha, 1f, seg * 0.35f);
            if (backgroundImage != null)
            {
                backgroundImage.color = bg0;
            }

            yield return FadeCanvasGroupUnscaled(cg, cg.alpha, 0.75f, seg * 0.35f);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = Color.black;
        }

        yield return FadeCanvasGroupUnscaled(cg, cg.alpha, 0f, Mathf.Max(0.04f, fadeOutDuration));
        if (backgroundImage != null)
        {
            backgroundImage.color = bg0;
        }
    }

    private CanvasGroup EnsureChromeCanvasGroup()
    {
        if (bubbleFrame == null)
        {
            return null;
        }

        var cg = bubbleFrame.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = bubbleFrame.gameObject.AddComponent<CanvasGroup>();
        }

        return cg;
    }

    private static IEnumerator FadeCanvasGroupUnscaled(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            cg.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }

        cg.alpha = to;
    }

    /// <param name="maxWidth">单条气泡最大总宽度（含内边距）</param>
    public void SetLayout(
        Vector3 newWorldOffset,
        Vector2 newPadding,
        float maxWidth,
        float minWidth,
        float minHeight)
    {
        worldOffset = newWorldOffset;
        contentPadding = newPadding;
        maxBubbleWidth = maxWidth;
        minBubbleWidth = minWidth;
        minBubbleHeight = minHeight;
    }

    public void SetFollow(Transform t)
    {
        follow = t;
        if (enabled && follow != null && !useScreenBottomLayout)
        {
            UpdateFollowPosition();
        }
    }

    public void BuildIfNeeded()
    {
        if (_built)
        {
            return;
        }

        if (rootCanvas == null || bubbleFrame == null || lineText == null)
        {
            Debug.LogError("DialogueBubbleView: 缺少 rootCanvas / bubbleFrame / lineText 引用。", this);
            return;
        }

        if (rootCanvas.renderMode == RenderMode.WorldSpace)
        {
            rootCanvas.worldCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
        }

        lineText.enableWordWrapping = true;
        lineText.overflowMode = TextOverflowModes.Overflow;
        if (fontOverride != null)
        {
            lineText.font = fontOverride;
        }

        if (fontSizeOverride > 0f)
        {
            lineText.fontSize = fontSizeOverride;
        }

        EnsureContentRoot();
        ApplyBackgroundFill();
        ApplyImageSlicedSettings();
        if (!useScreenBottomLayout)
        {
            hideTailInCurrentBottomLayout = false;
        }
        ApplyTailSetup();
        ApplyBottomScreenLayout();
        bubbleBaseLocalScale = bubbleFrame != null ? bubbleFrame.localScale : Vector3.one;
        _built = true;
    }

    public void Show(bool visible)
    {
        BuildIfNeeded();
        if (rootCanvas != null)
        {
            rootCanvas.gameObject.SetActive(visible);
        }
    }

    public void ClearText()
    {
        BuildIfNeeded();
        if (lineText != null)
        {
            lineText.text = string.Empty;
        }
    }

    public void SkipTyping()
    {
        if (isTyping)
        {
            skipType = true;
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        if (!paused)
        {
            wasPausedByControlTag = false;
        }
    }

    public void ResumeTyping()
    {
        if (isPaused)
        {
            isPaused = false;
            wasPausedByControlTag = false;
        }
    }

    public void SetTypingSpeed(float secondsPerChar)
    {
        currentSecondsPerChar = Mathf.Max(0f, secondsPerChar);
    }

    public void ShowInstantLine(string content, DialogueEmphasis emphasis)
    {
        BuildIfNeeded();
        if (lineText == null)
        {
            return;
        }

        if (typeRoutine != null)
        {
            StopCoroutine(typeRoutine);
            typeRoutine = null;
        }

        ApplyEmphasis(emphasis, true);
        string raw = content ?? string.Empty;
        string display = StripControlTags(raw);
        LayoutForFullString(display);
        lineText.richText = true;
        lineText.text = display;
        lineText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        skipType = false;
        isPaused = false;
    }

    public void TypeLine(string content, float secondsPerChar, DialogueEmphasis emphasis)
    {
        BuildIfNeeded();
        if (lineText == null)
        {
            return;
        }

        if (typeRoutine != null)
        {
            StopCoroutine(typeRoutine);
            typeRoutine = null;
        }

        ApplyEmphasis(emphasis, true);
        string raw = content ?? string.Empty;
        string display = StripControlTags(raw);

        charAnimations = ParseCharAnimations(raw);
        pauseAtCharIndex = ParsePausePoints(raw);

        LayoutForFullString(display);
        typeRoutine = StartCoroutine(TypeRoutine(raw, display, secondsPerChar, emphasis));
    }

    
private IEnumerator TypeRoutine(string raw, string display, float secPer, DialogueEmphasis em)
{
    isTyping = true;
    skipType = false;
    isPaused = false;

    float currentSecPer = Mathf.Max(0f, secPer);

    lineText.richText = true;
    lineText.text = display ?? string.Empty;
    lineText.maxVisibleCharacters = 0;
    lineText.ForceMeshUpdate();

    if (string.IsNullOrEmpty(raw))
    {
        isTyping = false;
        ApplyEmphasis(em, false);
        yield break;
    }

    int visible = 0;
    int rawIndex = 0;

    while (rawIndex < raw.Length)
    {
        // 点击跳过整句
        if (skipType)
        {
            lineText.maxVisibleCharacters = int.MaxValue;
            break;
        }

        // pause 状态
        if (isPaused)
        {
            yield return null;
            continue;
        }

        // =========================
        // [pause]
        // =========================
        if (MatchLiteral(raw, rawIndex, "[pause]"))
        {
            rawIndex += "[pause]".Length;
            isPaused = true;
            continue;
        }

        // =========================
        // [speed=0.01]
        // =========================
        if (MatchLiteral(raw, rawIndex, "[speed="))
        {
            int end = raw.IndexOf(']', rawIndex);

            if (end != -1)
            {
                string num = raw.Substring(rawIndex + 7, end - rawIndex - 7);

                if (float.TryParse(
                    num,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float sp))
                {
                    currentSecPer = Mathf.Max(0f, sp);
                }

                rawIndex = end + 1;
                continue;
            }
        }

        // =========================
        // TMP RichText 标签
        // <size=150%>
        // <color=red>
        // </size>
        // =========================
        if (raw[rawIndex] == '<')
        {
            int end = raw.IndexOf('>', rawIndex);

            if (end != -1)
            {
                rawIndex = end + 1;
                continue;
            }
        }

        // =========================
        // 普通字符
        // =========================
        visible++;
        lineText.maxVisibleCharacters = visible;

        rawIndex++;

        if (currentSecPer > 0f)
        {
            yield return new WaitForSeconds(currentSecPer);
        }
        else
        {
            yield return null;
        }
    }

    lineText.maxVisibleCharacters = int.MaxValue;

    isTyping = false;
    skipType = false;
    isPaused = false;
    typeRoutine = null;

    ApplyEmphasis(em, false);
}


    private static YieldInstruction WaitSecondsScaled(float seconds)
    {
        return seconds <= 0f ? null : new WaitForSeconds(seconds);
    }

    private static string StripControlTags(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        string result = s;
        result = Regex.Replace(result, @"\[/?pause\]", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\[/?resume\]", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\[speed=[^\]]*\]", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\[size=[^\]]*\]", "", RegexOptions.IgnoreCase);
        return result;
    }

    private static bool MatchLiteral(string s, int index, string lit)
    {
        if (index + lit.Length > s.Length)
        {
            return false;
        }

        for (int i = 0; i < lit.Length; i++)
        {
            if (s[index + i] != lit[i])
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<int> ParsePausePoints(string raw)
    {
        var pauses = new HashSet<int>();
        if (string.IsNullOrEmpty(raw))
        {
            return pauses;
        }

        int rawIndex = 0;
        int displayIndex = 0;
        while (rawIndex < raw.Length)
        {
            if (raw[rawIndex] == '[')
            {
                if (MatchLiteral(raw, rawIndex, "[pause]") || MatchLiteral(raw, rawIndex, "[PAUSE]"))
                {
                    pauses.Add(displayIndex);
                    rawIndex += 7;
                    continue;
                }

                if (MatchLiteral(raw, rawIndex, "[resume]") || MatchLiteral(raw, rawIndex, "[RESUME]"))
                {
                    rawIndex += 8;
                    continue;
                }

                if (MatchLiteral(raw, rawIndex, "[speed=") || MatchLiteral(raw, rawIndex, "[SPEED="))
                {
                    int start = rawIndex + 7;
                    int end = raw.IndexOf(']', start);
                    if (end > start)
                    {
                        rawIndex = end + 1;
                        continue;
                    }
                }

                if (MatchLiteral(raw, rawIndex, "[size=") || MatchLiteral(raw, rawIndex, "[SIZE="))
                {
                    int start = rawIndex + 6;
                    int end = raw.IndexOf(']', start);
                    if (end > start)
                    {
                        rawIndex = end + 1;
                        continue;
                    }
                }
            }

            if (raw[rawIndex] != '[')
            {
                displayIndex++;
            }

            rawIndex++;
        }

        return pauses;
    }

    // Back-compat with requested signature; parsing is precomputed in ParsePausePoints/ParseCharAnimations.
    private bool TryProcessControlTags(string raw, ref int index, ref float secondsPerChar, int currentVisibleCount)
    {
        index = string.IsNullOrEmpty(raw) ? 0 : raw.Length;
        secondsPerChar = currentSecondsPerChar;
        return false;
    }

    private static List<CharAnimationData> ParseCharAnimations(string raw)
    {
        var animations = new List<CharAnimationData>();
        if (string.IsNullOrEmpty(raw))
        {
            return animations;
        }

        int rawIndex = 0;
        int displayCharCount = 0;
        float currentSizeScale = 1f;
        float currentSpeedMult = 1f;

        while (rawIndex < raw.Length)
        {
            if (raw[rawIndex] == '[')
            {
                if (MatchLiteral(raw, rawIndex, "[size=") || MatchLiteral(raw, rawIndex, "[SIZE="))
                {
                    int start = rawIndex + 6;
                    int end = raw.IndexOf(']', start);
                    if (end > start)
                    {
                        string num = raw.Substring(start, end - start);
                        if (float.TryParse(
                                num,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out float sizeScale))
                        {
                            currentSizeScale = sizeScale;
                        }

                        rawIndex = end + 1;
                        continue;
                    }
                }
                else if (MatchLiteral(raw, rawIndex, "[speed=") || MatchLiteral(raw, rawIndex, "[SPEED="))
                {
                    int start = rawIndex + 7;
                    int end = raw.IndexOf(']', start);
                    if (end > start)
                    {
                        string num = raw.Substring(start, end - start);
                        if (float.TryParse(
                                num,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out float speedMult))
                        {
                            currentSpeedMult = speedMult;
                        }

                        rawIndex = end + 1;
                        continue;
                    }
                }
                else if (MatchLiteral(raw, rawIndex, "[pause]") || MatchLiteral(raw, rawIndex, "[PAUSE]"))
                {
                    rawIndex += 7;
                    continue;
                }
                else if (MatchLiteral(raw, rawIndex, "[resume]") || MatchLiteral(raw, rawIndex, "[RESUME]"))
                {
                    rawIndex += 8;
                    continue;
                }
            }

            if (raw[rawIndex] != '[')
            {
                animations.Add(new CharAnimationData(displayCharCount, currentSizeScale, currentSpeedMult));
                displayCharCount++;
            }

            rawIndex++;
        }

        return animations;
    }

    private CharAnimationData GetCharAnimation(int charIndex)
    {
        if (charAnimations == null || charIndex < 0 || charIndex >= charAnimations.Count)
        {
            return null;
        }

        return charAnimations[charIndex];
    }

    private IEnumerator AnimateCharSize(int charIndex, float targetScale)
    {
        if (lineText == null || lineText.textInfo == null)
        {
            yield break;
        }

        if (charIndex >= lineText.textInfo.characterCount)
        {
            yield break;
        }

        // We need valid mesh data for this character.
        lineText.ForceMeshUpdate();

        TMP_CharacterInfo charInfo = lineText.textInfo.characterInfo[charIndex];
        if (!charInfo.isVisible)
        {
            yield break;
        }

        int materialIndex = charInfo.materialReferenceIndex;
        int vertexIndex = charInfo.vertexIndex;

        Vector3[] vertices = lineText.textInfo.meshInfo[materialIndex].vertices;
        Vector3 originalCenter = (vertices[vertexIndex + 0] + vertices[vertexIndex + 2]) / 2f;

        float duration = 0.1f;
        float elapsed = 0f;
        float startScale = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration <= 0f ? 1f : elapsed / duration;
            float currentScale = Mathf.Lerp(startScale, targetScale, t);

            for (int i = 0; i < 4; i++)
            {
                Vector3 vertex = vertices[vertexIndex + i];
                Vector3 dir = vertex - originalCenter;
                vertices[vertexIndex + i] = originalCenter + dir * currentScale;
            }

            lineText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            yield return null;
        }

        for (int i = 0; i < 4; i++)
        {
            Vector3 vertex = vertices[vertexIndex + i];
            Vector3 dir = vertex - originalCenter;
            vertices[vertexIndex + i] = originalCenter + dir * targetScale;
        }

        lineText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    public void ApplyEmphasisFromRunner(DialogueEmphasis e, bool typing)
    {
        ApplyEmphasis(e, typing);
    }

    private void ApplyEmphasis(DialogueEmphasis e, bool typing)
    {
        if (bubbleFrame == null)
        {
            return;
        }

        if (!e.enabled)
        {
            shakeMag = 0f;
            bubbleFrame.localScale = bubbleBaseLocalScale;
            if (contentRoot != null)
            {
                contentRoot.localPosition = contentBaseLocalPos;
            }
            return;
        }

        float scale = typing ? Mathf.Max(1f, e.scaleMultiplier) : 1f;
        bubbleFrame.localScale = bubbleBaseLocalScale * scale;
        shakeMag = typing ? Mathf.Max(0f, e.shakeMagnitude) : 0f;
    }

    private void LayoutForFullString(string full)
    {
        if (lineText == null || bubbleFrame == null)
        {
            return;
        }

        if (useScreenBottomLayout)
        {
            bubbleFrame.sizeDelta = screenBottomSize;
        }
        else
        {
            float innerW = Mathf.Max(0.1f, maxBubbleWidth - contentPadding.x * 2f);
            Vector2 pref = lineText.GetPreferredValues(full, innerW, 0f);
            float requiredMinW = minBubbleWidth;
            float requiredMinH = minBubbleHeight;
            if (backgroundImageType == Image.Type.Sliced && backgroundImage != null && backgroundImage.sprite != null)
            {
                // Sliced 时，Rect 不能小于 Border 四边之和，否则中心区为负，可能直接不绘制。
                Vector4 b = backgroundImage.sprite.border; // L,B,R,T (pixels)
                float effPpu = Mathf.Max(0.01f, backgroundImage.sprite.pixelsPerUnit * Mathf.Max(0.01f, imagePixelsPerUnitMultiplier));
                float minWFromBorder = (b.x + b.z) / effPpu + 0.01f;
                float minHFromBorder = (b.y + b.w) / effPpu + 0.01f;
                requiredMinW = Mathf.Max(requiredMinW, minWFromBorder);
                requiredMinH = Mathf.Max(requiredMinH, minHFromBorder);
            }

            float w = Mathf.Clamp(pref.x + contentPadding.x * 2f, requiredMinW, maxBubbleWidth);
            float h = Mathf.Max(pref.y + contentPadding.y * 2f, requiredMinH);
            bubbleFrame.sizeDelta = new Vector2(w, h);
        }

        var tr = lineText.rectTransform;
        tr.offsetMin = new Vector2(contentPadding.x, contentPadding.y);
        tr.offsetMax = new Vector2(-contentPadding.x, -contentPadding.y);

        ApplyBackgroundFill();
        ApplyImageSlicedSettings();
        ApplyBottomScreenLayout();
        ApplyTailLayout();
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleFrame);
        if (rootCanvas != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootCanvas.GetComponent<RectTransform>());
        }

        Canvas.ForceUpdateCanvases();

        if (_imageRefreshRoutine != null)
        {
            StopCoroutine(_imageRefreshRoutine);
        }

        _imageRefreshRoutine = StartCoroutine(RefreshImageMeshNextFrame());
    }

    private IEnumerator RefreshImageMeshNextFrame()
    {
        yield return null;
        ApplyImageSlicedSettings();
        if (backgroundImage != null)
        {
            backgroundImage.SetAllDirty();
            backgroundImage.SetLayoutDirty();
        }

        Canvas.ForceUpdateCanvases();
        _imageRefreshRoutine = null;
    }

    /// <summary>背景铺满 Bubble 的矩形，供 Sliced 以整框为外边界拉伸。</summary>
    private void ApplyBackgroundFill()
    {
        if (bubbleFrame == null || backgroundImage == null)
        {
            return;
        }

        EnsureContentRoot();

        RectTransform bg = backgroundImage.rectTransform;
        if (bg == bubbleFrame)
        {
            return;
        }

        if (contentRoot != null && bg.parent != contentRoot)
        {
            // worldPositionStays: true 避免 reparent 时一帧的 rect 错乱
            bg.SetParent(contentRoot, true);
        }

        bg.SetAsFirstSibling();
        bg.localScale = Vector3.one;
        bg.localRotation = Quaternion.identity;
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.pivot = new Vector2(0.5f, 0.5f);
        bg.anchoredPosition3D = Vector3.zero;
        bg.offsetMin = Vector2.zero;
        bg.offsetMax = Vector2.zero;
        bg.sizeDelta = Vector2.zero;

        Color color = backgroundImage.color;
        if (useScreenBottomLayout)
        {
            color.a = screenBottomAlpha;
            backgroundImage.color = color;
        }
    }

    /// <summary>
    /// Sliced 未勾选「Fill Center」时中间不绘制/不拉伸，看起来就像「只有一绺」或没铺满。
    /// </summary>
    private void ApplyImageSlicedSettings()
    {
        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.type = backgroundImageType;
        if (backgroundImageType == Image.Type.Sliced)
        {
            backgroundImage.preserveAspect = false;
            backgroundImage.useSpriteMesh = false;
            backgroundImage.fillCenter = true;
        }

        backgroundImage.pixelsPerUnitMultiplier = imagePixelsPerUnitMultiplier;
    }

    private void ApplyTailSetup()
    {
        if (tailRect == null)
        {
            return;
        }

        if (tailRect.parent != bubbleFrame)
        {
            tailRect.SetParent(bubbleFrame, true);
        }

        if (tailImage != null)
        {
            tailImage.raycastTarget = false;
            tailImage.type = Image.Type.Simple;
            tailImage.preserveAspect = true;
        }

        if (tailSpriteOverride != null)
        {
            if (tailImage != null)
            {
                tailImage.sprite = tailSpriteOverride;
            }
        }

        // If we have a procedural tail, keep its color consistent with background.
        if (tailGraphic != null && backgroundImage != null)
        {
            tailGraphic.color = backgroundImage.color;
        }

        ApplyTailLayout();
    }

    private void EnsureContentRoot()
    {
        if (bubbleFrame == null)
        {
            return;
        }

        if (contentRoot == null)
        {
            Transform t = bubbleFrame.Find("Content");
            if (t != null)
            {
                contentRoot = t as RectTransform;
            }
        }

        if (contentRoot == null)
        {
            var go = new GameObject("Content", typeof(RectTransform));
            contentRoot = go.GetComponent<RectTransform>();
            contentRoot.SetParent(bubbleFrame, false);
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.anchoredPosition3D = Vector3.zero;
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;
        }

        contentBaseLocalPos = contentRoot.localPosition;

        // Ensure background + text live under Content so tail can stay stable when shaking.
        if (backgroundImage != null && backgroundImage.rectTransform.parent != contentRoot)
        {
            backgroundImage.rectTransform.SetParent(contentRoot, true);
        }

        if (lineText != null && lineText.rectTransform.parent != contentRoot)
        {
            lineText.rectTransform.SetParent(contentRoot, true);
        }

        if (backgroundImage != null)
        {
            backgroundImage.rectTransform.SetAsFirstSibling();
        }

        if (lineText != null)
        {
            lineText.rectTransform.SetAsLastSibling();
        }
    }

    private void ApplyTailLayout()
    {
        if (tailRect == null)
        {
            return;
        }

        bool useImage = tailImage != null;
        bool useProcedural = !useImage && tailGraphic != null;
        bool hiddenByBottomLayout = useScreenBottomLayout && hideTailInCurrentBottomLayout;
        bool visible = showTail && !hiddenByBottomLayout && (useImage || useProcedural);

        tailRect.gameObject.SetActive(visible);

        if (tailImage != null)
        {
            tailImage.enabled = visible;
        }

        if (tailGraphic != null)
        {
            tailGraphic.enabled = visible && useProcedural;
        }

        if (!visible)
        {
            return;
        }

        if (tailImage != null && tailSpriteOverride != null)
        {
            tailImage.sprite = tailSpriteOverride;
        }

        tailRect.SetAsLastSibling();
        tailRect.anchorMin = new Vector2(0.5f, 0f);
        tailRect.anchorMax = new Vector2(0.5f, 0f);
        tailRect.pivot = new Vector2(0.5f, 1f);
        tailRect.sizeDelta = new Vector2(Mathf.Max(0.01f, tailSize.x), Mathf.Max(0.01f, tailSize.y));

        // Seamless connection: top edge of tail touches bubble bottom edge (y=0 with pivot at top).
        // Y offset is interpreted as "downwards is positive".
        float down = Mathf.Max(0f, tailOffset.y);
        tailRect.anchoredPosition = new Vector2(tailOffset.x, -down);
        tailRect.localRotation = Quaternion.identity;

        ApplyTailScaleCompensation();
    }

    private void ApplyTailScaleCompensation()
    {
        if (tailRect == null || bubbleFrame == null || useScreenBottomLayout)
        {
            return;
        }

        // Keep tail world size constant even if bubbleFrame scales (e.g., emphasis effect).
        Vector3 s = bubbleFrame.localScale;
        float sx = Mathf.Abs(s.x) < 0.0001f ? 1f : s.x;
        float sy = Mathf.Abs(s.y) < 0.0001f ? 1f : s.y;
        tailRect.localScale = new Vector3(1f / sx, 1f / sy, 1f);

        // If tail sprite isn't a triangle but a bubble piece, users can override rotation in prefab.
        // We keep default rotation as identity.
    }

    private void LateUpdate()
    {
        if (rootCanvas == null || !rootCanvas.gameObject.activeInHierarchy)
        {
            return;
        }

        if (shakeMag > 0f)
        {
            shake = new Vector3(
                Random.Range(-shakeMag, shakeMag),
                Random.Range(-shakeMag, shakeMag),
                0f);
        }
        else
        {
            shake = Vector3.zero;
        }

        if (useScreenBottomLayout)
        {
            ApplyBottomScreenLayout();
        }
        else
        {
            if (follow == null)
            {
                return;
            }

            UpdateFollowPosition();
            ApplyTailScaleCompensation();

            if (clampToScreen)
            {
                ClampToSafeArea();
            }
        }

        if (contentRoot != null)
        {
            contentRoot.localPosition = contentBaseLocalPos + shake;
        }
    }

    private void ApplyBottomScreenLayout()
    {
        if (!useScreenBottomLayout || rootCanvas == null || bubbleFrame == null)
        {
            return;
        }

        if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.worldCamera = null;
        }

        RectTransform rootRect = rootCanvas.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        bubbleFrame.anchorMin = new Vector2(0.5f, 0f);
        bubbleFrame.anchorMax = new Vector2(0.5f, 0f);
        bubbleFrame.pivot = new Vector2(0.5f, 0.5f);
        bubbleFrame.anchoredPosition = screenBottomOffset;
        bubbleFrame.sizeDelta = screenBottomSize;
        bubbleFrame.localRotation = Quaternion.identity;
        bubbleFrame.localScale = bubbleBaseLocalScale;
    }

    private void UpdateFollowPosition()
    {
        // Follow anchor only; shake is applied to contentRoot so tail stays stable.
        transform.SetPositionAndRotation(follow.position + worldOffset, Quaternion.identity);
    }

    private void ClampToSafeArea()
    {
        if (rootCanvas == null || bubbleFrame == null)
        {
            return;
        }

        if (rootCanvas.renderMode != RenderMode.WorldSpace)
        {
            return;
        }

        var cam = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        if (!GetScreenExtrema(bubbleFrame, tailRect, cam, out float x0, out float y0, out float x1, out float y1))
        {
            return;
        }

        Rect a = Screen.safeArea;
        float l = a.xMin + screenEdgePadding;
        float r = a.xMax - screenEdgePadding;
        float b = a.yMin + screenEdgePadding;
        float t = a.yMax - screenEdgePadding;

        float dx = 0f;
        if (x0 < l)
        {
            dx = l - x0;
        }

        if (x1 + dx > r)
        {
            dx += r - (x1 + dx);
        }

        float dy = 0f;
        if (y0 < b)
        {
            dy = b - y0;
        }

        if (y1 + dy > t)
        {
            dy += t - (y1 + dy);
        }

        if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
        {
            return;
        }

        Vector3 s = cam.WorldToScreenPoint(transform.position);
        if (s.z < 0.01f)
        {
            return;
        }

        Vector3 p0 = cam.ScreenToWorldPoint(new Vector3(s.x, s.y, s.z));
        Vector3 p1 = cam.ScreenToWorldPoint(new Vector3(s.x + dx, s.y + dy, s.z));
        transform.position += p1 - p0;
    }

    private static bool GetScreenExtrema(RectTransform bubble, RectTransform tail, Camera cam, out float x0, out float y0, out float x1, out float y1)
    {
        x0 = y0 = float.MaxValue;
        x1 = y1 = float.MinValue;
        if (!AccumulateExtrema(bubble, cam, ref x0, ref y0, ref x1, ref y1))
        {
            return false;
        }

        // Tail might be null or disabled; we still include its rect if present & active.
        if (tail != null && tail.gameObject.activeInHierarchy)
        {
            // If the Image is disabled, its rect is still meaningful for visuals; include it anyway.
            if (!AccumulateExtrema(tail, cam, ref x0, ref y0, ref x1, ref y1))
            {
                return false;
            }
        }

        return x0 <= x1 && y0 <= y1;
    }

    private static bool AccumulateExtrema(RectTransform rt, Camera cam, ref float x0, ref float y0, ref float x1, ref float y1)
    {
        if (rt == null)
        {
            return true;
        }

        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = cam.WorldToScreenPoint(c[i]);
            if (p.z < 0.01f)
            {
                return false;
            }

            if (p.x < x0)
            {
                x0 = p.x;
            }

            if (p.y < y0)
            {
                y0 = p.y;
            }

            if (p.x > x1)
            {
                x1 = p.x;
            }

            if (p.y > y1)
            {
                y1 = p.y;
            }
        }

        return true;
    }
}
