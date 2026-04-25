using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TypewriterEffect : MonoBehaviour
{
    [Header("UI 组件绑定")]
    [Tooltip("对话文本框（写在这里，交给全局对话管理器控制显示）")]
    [SerializeField]
    private TextMeshProUGUI textLabel;

    [Header("UI 面板（可选）")]
    [Tooltip("若为空，默认尝试使用 textLabel 的父节点")]
    [SerializeField]
    private GameObject dialoguePanel;

    [Header("打字机设置")]
    [SerializeField]
    private float typingSpeed = 0.05f;

    [Header("剧本内容")]
    [TextArea(3, 10)]
    [SerializeField]
    private List<string> dialogueLines = new List<string>();

    [Header("播放设置")]
    [SerializeField]
    private bool playOnStart = true;

    [Tooltip("当前对话正在播放时，新的对话是否加入队列")]
    [SerializeField]
    private bool queueIfRunning = true;

    [Tooltip("对话结束后是否自动隐藏面板")]
    [SerializeField]
    private bool hidePanelWhenFinished = true;

    private void Awake()
    {
        if (dialoguePanel == null && textLabel != null && textLabel.transform.parent != null)
        {
            dialoguePanel = textLabel.transform.parent.gameObject;
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (dialogueLines == null || dialogueLines.Count == 0)
        {
            Debug.LogWarning("TypewriterEffect: 当前台词列表为空，不会播放。");
            return;
        }

        var manager = DialogueManager.Instance;
        if (manager == null)
        {
            var autoManager = new GameObject("GlobalDialogueManager");
            manager = autoManager.AddComponent<DialogueManager>();
        }

        manager.ShowDialogue(
            dialogueLines,
            textLabel,
            dialoguePanel,
            typingSpeed,
            queueIfRunning,
            hidePanelWhenFinished);
    }
}
