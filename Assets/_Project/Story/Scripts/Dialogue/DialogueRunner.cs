using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;

public sealed class DialogueRunner : MonoBehaviour
{
    [Header("打字")]
    [SerializeField] private float secondsPerChar = 0.03f;

    [Header("对话气泡预制体")]
    [SerializeField] private DialogueBubbleView bubblePrefab;

    [Header("中文字体（建议指定，避免豆腐块）")]
    [SerializeField] private TMP_FontAsset dialogueFontAsset;

    [Header("气泡：头顶偏移与文字区域")]
    [Tooltip("从锚点世界坐标再往上，一般锚点在脚或体心时略大，在子物体「头」上可略小。")]
    [SerializeField] private Vector3 bubbleWorldOffset = new Vector3(0f, 2.05f, 0f);

    [SerializeField] private Vector2 bubbleContentPadding = new Vector2(0.55f, 0.35f);
    [Tooltip("单条对话气泡可分配的最大总宽度，文本在此宽度内自动换行，超过则顶到最宽后换行。")]
    [SerializeField] private float bubbleMaxWidth = 7.2f;
    [SerializeField] private float bubbleMinWidth = 2f;
    [SerializeField] private float bubbleMinHeight = 1.2f;

    [Header("输入")]
    [SerializeField] private int mouseButton = 0;
    [SerializeField] private KeyCode advanceKey = KeyCode.E;
    [Tooltip("为 true 时仅鼠标推进下一句，忽略 advanceKey（仍可用 Escape 取消）。")]
    [SerializeField] private bool mouseButtonOnlyAdvance;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    [Header("运行状态")]
    [SerializeField] private bool isPlaying;

    private readonly List<DialogueLine> lines = new List<DialogueLine>();
    private int index;
    private DialogueBubbleView bubble;
    private DialogueBubbleView active;
    private PlayerInteractor2D interactor;
    private Transform playerAnchor;
    private Transform npcAnchor;
    private Action onConversationEnded;
    private bool deferFirstLine;
    private bool suppressBubbleHideOnComplete;

    public bool IsPlaying => isPlaying;
    public DialogueBubbleView BubbleView => bubble;

    private void Awake()
    {
        if (bubblePrefab == null)
        {
            bubblePrefab = TryLoadBubblePrefab();
        }

        if (dialogueFontAsset == null)
        {
            dialogueFontAsset = TryLoadDialogueFont();
        }
    }

    private void OnDisable()
    {
        if (isPlaying)
        {
            StopAll();
        }
    }

    private static DialogueBubbleView TryLoadBubblePrefab()
    {
        // Optional runtime fallback: place prefab under a Resources folder with one of these paths.
        // Example: Assets/Resources/DialogueBubble.prefab  -> "DialogueBubble"
        //          Assets/Resources/Prefabs/DialogueBubble.prefab -> "Prefabs/DialogueBubble"
        //          Assets/Resources/Dialogue/DialogueBubble.prefab -> "Dialogue/DialogueBubble"
        string[] paths = { "DialogueBubble", "Prefabs/DialogueBubble", "Dialogue/DialogueBubble" };
        foreach (string p in paths)
        {
            var view = Resources.Load<DialogueBubbleView>(p);
            if (view != null)
            {
                return view;
            }

            var go = Resources.Load<GameObject>(p);
            if (go != null)
            {
                var v = go.GetComponent<DialogueBubbleView>();
                if (v != null)
                {
                    return v;
                }
            }
        }

        return null;
    }

    private static TMP_FontAsset TryLoadDialogueFont()
    {
        string[] paths = { "DialogueFont", "Fonts/DialogueFont", "Fonts/SCfont SDF" };
        foreach (string p in paths)
        {
            var f = Resources.Load<TMP_FontAsset>(p);
            if (f != null)
            {
                return f;
            }
        }

        return null;
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        if (deferFirstLine)
        {
            if (Input.GetKeyDown(cancelKey))
            {
                StopAll();
            }

            return;
        }

        if (Input.GetKeyDown(cancelKey))
        {
            StopAll();
            return;
        }

        bool advance =
            Input.GetMouseButtonDown(mouseButton)
            || (!mouseButtonOnlyAdvance && Input.GetKeyDown(advanceKey));
        if (!advance)
        {
            return;
        }

        if (active != null && active.IsTyping)
        {
            active.SkipTyping();
            return;
        }

        Advance();
    }

    public void PlayConversation(
        PlayerInteractor2D who,
        Transform playerBubbleAnchor,
        Transform npcBubbleAnchor,
        IList<DialogueLine> dialogue,
        Action onEnded = null,
        Action<DialogueBubbleView> prepareBubble = null,
        bool deferFirstLineUntilExternal = false,
        bool suppressBubbleHideOnConversationEnd = false)
    {
        if (bubblePrefab == null)
        {
            Debug.LogError("DialogueRunner: 请指定 Bubble Prefab（菜单 Tools/Story/Create Dialogue Bubble Prefab 可生成）。", this);
            return;
        }

        if (dialogue == null || dialogue.Count == 0)
        {
            return;
        }

        interactor = who;
        playerAnchor = playerBubbleAnchor;
        npcAnchor = npcBubbleAnchor;
        lines.Clear();
        lines.AddRange(dialogue);
        index = 0;
        onConversationEnded = onEnded;
        deferFirstLine = deferFirstLineUntilExternal;
        suppressBubbleHideOnComplete = suppressBubbleHideOnConversationEnd;

        EnsureRuntimeBubble();
        prepareBubble?.Invoke(bubble);
        isPlaying = true;

        if (interactor != null && interactor.Motor != null)
        {
            interactor.Motor.SetMovementLocked(true);
        }

        if (deferFirstLine)
        {
            ShowDeferredFirstLineLayoutOnly(lines[0]);
        }
        else
        {
            ShowLine(lines[0]);
        }
    }

    /// <summary>与 deferFirstLine 配合：排好首句布局并显示空框后，由外部闪框再调用本方法开始打字。</summary>
    public void PlayDeferredFirstLine()
    {
        if (!isPlaying || !deferFirstLine || lines.Count == 0 || bubble == null)
        {
            return;
        }

        deferFirstLine = false;
        DialogueLine line = lines[0];
        bubble.ClearText();
        bubble.TypeLine(line.text, secondsPerChar, line.emphasis);
    }

    public void HideDialogueBubble()
    {
        if (bubble != null)
        {
            bubble.Show(false);
        }
    }

    private void ShowDeferredFirstLineLayoutOnly(DialogueLine line)
    {
        if (line == null || bubble == null)
        {
            return;
        }

        Transform t = line.speaker == DialogueSpeaker.Player ? playerAnchor : npcAnchor;
        if (t == null)
        {
            t = interactor != null ? interactor.transform : null;
        }

        if (t != null)
        {
            bubble.SetFollow(t);
        }

        active = bubble;
        bubble.Show(true);
        bubble.ApplyEmphasisFromRunner(line.emphasis, true);
        bubble.PrepareLayoutEmptyText(line.text);
    }

    private void EnsureRuntimeBubble()
    {
        if (bubble != null)
        {
            ApplyBubbleConfig();
            return;
        }

        var existing = GetComponentsInChildren<DialogueBubbleView>(includeInactive: true);
        if (existing.Length > 0)
        {
            bubble = existing[0];
            for (int i = 1; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    Destroy(existing[i].gameObject);
                }
            }
        }
        else
        {
            var go = Instantiate(bubblePrefab, transform);
            go.name = "DialogueBubble";
            bubble = go.GetComponent<DialogueBubbleView>();
        }

        if (dialogueFontAsset != null)
        {
            bubble.SetFont(dialogueFontAsset);
        }

        ApplyBubbleConfig();
        bubble.Show(false);
    }

    private void ApplyBubbleConfig()
    {
        bubble.SetLayout(
            bubbleWorldOffset,
            bubbleContentPadding,
            bubbleMaxWidth,
            bubbleMinWidth,
            bubbleMinHeight);
    }

    private void ShowLine(DialogueLine line)
    {
        if (line == null || bubble == null)
        {
            return;
        }

        Transform t = line.speaker == DialogueSpeaker.Player ? playerAnchor : npcAnchor;
        if (t == null)
        {
            t = interactor != null ? interactor.transform : null;
        }

        if (t != null)
        {
            bubble.SetFollow(t);
        }

        active = bubble;
        bubble.Show(true);
        bubble.ClearText();
        bubble.TypeLine(line.text, secondsPerChar, line.emphasis);
    }

    private void Advance()
    {
        index++;
        if (index >= lines.Count)
        {
            StopAll();
            return;
        }

        ShowLine(lines[index]);
    }

    private void StopAll()
    {
        bool hideBubble = !suppressBubbleHideOnComplete;
        deferFirstLine = false;
        suppressBubbleHideOnComplete = false;

        isPlaying = false;
        index = 0;
        lines.Clear();
        if (bubble != null && hideBubble)
        {
            bubble.Show(false);
        }

        active = null;

        if (interactor != null && interactor.Motor != null)
        {
            interactor.Motor.SetMovementLocked(false);
        }

        interactor = null;
        playerAnchor = null;
        npcAnchor = null;

        Action callback = onConversationEnded;
        onConversationEnded = null;
        callback?.Invoke();
    }
}
