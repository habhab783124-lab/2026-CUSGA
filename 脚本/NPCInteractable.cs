using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("基础交互配置")]
    [Tooltip("是否允许被交互")]
    [SerializeField] private bool interactable = true;

    [Tooltip("交互提示文本")]
    [SerializeField] private string interactionPrompt = "按 E 交互";

    [Header("交互对象过滤")]
    [Tooltip("仅当对方挂有 PlayerController 时生效；如有需要可结合 LayerMask 过滤")]
    [SerializeField] private LayerMask playerLayer = ~0;

    [Header("提示 UI（主角靠近时显示）")]
    [SerializeField] private GameObject interactPromptRoot;
    [SerializeField] private TextMeshProUGUI interactPromptText;

    [Header("对话 UI（World Space Canvas 建议挂在 NPC2 下）")]
    [SerializeField] private GameObject dialogueCanvasRoot;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("对话内容")]
    [Tooltip("在 Inspector 中按顺序填充台词")]
    [SerializeField] private string[] dialogueLines = new string[0];

    [Header("打字机参数")]
    [Tooltip("单字延迟（秒），越小越快")]
    [SerializeField] private float typingSpeed = 0.05f;

    [Header("对话结束后回调")]
    [SerializeField] private UnityEvent onDialogueComplete = new UnityEvent();

    [Header("兼容扩展回调")]
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    // ===== 当前状态缓存 =====
    private bool isPlayerInRange;
    private bool isDialoguePlaying;
    private bool isTyping;
    private bool skipCurrentLine;
    private int currentLineIndex;

    private PlayerController currentPlayer;
    private Coroutine typingRoutine;

    public string InteractionPrompt => interactionPrompt;
    public bool IsInteractable => interactable;

    private void Awake()
    {
        // ========= 初始化可见性状态 =========
        SetPromptVisible(false);
        SetDialogueVisible(false);

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        // ========= 运行时安全检查 =========
        // 要求触发器是 Trigger，避免影响 NPC 的物理移动逻辑
        var trigger = GetComponent<BoxCollider2D>();
        if (trigger != null && !trigger.isTrigger)
        {
            Debug.LogWarning($"[{name}] 的 BoxCollider2D 当前未勾选 Is Trigger，请勾选后作为交互感应区。", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ========= A. 玩家进入触发区：显示交互提示 =========
        if (!interactable || isDialoguePlaying)
        {
            return;
        }

        var player = TryGetPlayer(other);
        if (player == null)
        {
            return;
        }

        isPlayerInRange = true;
        currentPlayer = player;
        SetPromptVisible(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // ========= B. 玩家离开：关闭提示，若对话中则中断并解冻 =========
        if (!interactable)
        {
            return;
        }

        var player = TryGetPlayer(other);
        if (player == null || player != currentPlayer)
        {
            return;
        }

        if (isDialoguePlaying)
        {
            FinishDialogue();
        }

        isPlayerInRange = false;
        currentPlayer = null;
        SetPromptVisible(false);
    }

    private void Update()
    {
        // ========= C. 输入监听：E 开始对话 / 左键推进对话 =========
        if (!interactable || currentPlayer == null)
        {
            return;
        }

        if (!isDialoguePlaying)
        {
            if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
            {
                Interact(currentPlayer);
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            OnAdvanceDialogueInput();
        }
    }

    public void Interact(PlayerController player)
    {
        // ========= D. 开始对话入口 =========
        if (!interactable || player == null || isDialoguePlaying)
        {
            return;
        }

        // 无对话内容时仅走事件，便于保留扩展能力
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            onInteract?.Invoke();
            return;
        }

        isDialoguePlaying = true;
        currentPlayer = player;
        currentLineIndex = 0;

        // 对话开始时冻结主角移动
        currentPlayer.SetFrozen(true);
        SetPromptVisible(false);
        SetDialogueVisible(true);

        StartDialogueLine(currentLineIndex);
    }

    private void OnAdvanceDialogueInput()
    {
        // ========= E. 左键行为：打字中则补全文字，打完则切换下一句 =========
        if (!isDialoguePlaying)
        {
            return;
        }

        if (isTyping)
        {
            skipCurrentLine = true;
            return;
        }

        currentLineIndex++;

        if (currentLineIndex >= dialogueLines.Length)
        {
            FinishDialogue();
            return;
        }

        StartDialogueLine(currentLineIndex);
    }

    private void StartDialogueLine(int lineIndex)
    {
        // ========= F. 启动逐字显示协程 =========
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (dialogueText == null)
        {
            Debug.LogWarning($"[{name}] 未指定 dialogueText，无法显示对话文本。", this);
            FinishDialogue();
            return;
        }

        typingRoutine = StartCoroutine(TypeLine(dialogueLines[lineIndex]));
    }

    private IEnumerator TypeLine(string line)
    {
        isTyping = true;
        skipCurrentLine = false;
        dialogueText.text = string.Empty;

        if (string.IsNullOrEmpty(line))
        {
            isTyping = false;
            typingRoutine = null;
            yield break;
        }

        for (int i = 0; i < line.Length; i++)
        {
            if (skipCurrentLine)
            {
                break;
            }

            dialogueText.text += line[i];
            yield return new WaitForSeconds(typingSpeed);
        }

        // ========= G. 若中途被跳过，补齐整行 =========
        if (skipCurrentLine)
        {
            dialogueText.text = line;
            skipCurrentLine = false;
        }

        isTyping = false;
        typingRoutine = null;
    }

    private void FinishDialogue()
    {
        // ========= H. 结束对话，恢复角色移动 =========
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
        skipCurrentLine = false;
        isDialoguePlaying = false;
        SetDialogueVisible(false);

        if (currentPlayer != null)
        {
            currentPlayer.SetFrozen(false);
            currentPlayer = null;
        }

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (isPlayerInRange)
        {
            SetPromptVisible(true);
        }

        onDialogueComplete?.Invoke();
        onInteract?.Invoke();
    }

    private void SetPromptVisible(bool visible)
    {
        // ========= I. 提示 UI 显隐 =========
        if (interactPromptRoot != null)
        {
            interactPromptRoot.SetActive(visible);
        }

        if (interactPromptText != null)
        {
            interactPromptText.text = interactionPrompt;
        }
    }

    private void SetDialogueVisible(bool visible)
    {
        // ========= J. 对话 UI 显隐 =========
        if (dialogueCanvasRoot != null)
        {
            dialogueCanvasRoot.SetActive(visible);
        }
    }

    private PlayerController TryGetPlayer(Collider2D collider)
    {
        // ========= K. 过滤是否玩家对象 =========
        if (!interactable)
        {
            return null;
        }

        if (playerLayer.value != 0 && (playerLayer.value & (1 << collider.gameObject.layer)) == 0)
        {
            return null;
        }

        return collider.GetComponentInParent<PlayerController>();
    }
}
