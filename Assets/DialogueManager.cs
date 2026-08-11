using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Default UI (optional)")]
    [SerializeField] private TextMeshProUGUI defaultTextLabel;
    [SerializeField] private GameObject defaultDialoguePanel;
    [SerializeField] private float defaultTypingSpeed = 0.05f;
    [SerializeField] private bool hidePanelWhenFinished = true;
    [SerializeField] private bool keepAliveAcrossScenes = true;

    private sealed class DialogueRequest
    {
        public List<string> lines;
        public TextMeshProUGUI textLabel;
        public GameObject dialoguePanel;
        public float typingSpeed;
        public bool hidePanelWhenFinished;
        public Action onComplete;
    }

    private readonly Queue<DialogueRequest> requestQueue = new Queue<DialogueRequest>();
    private DialogueRequest activeRequest;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool cancelCurrentLine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (keepAliveAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Update()
    {
        if (activeRequest == null)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (isTyping)
        {
            cancelCurrentLine = true;
            return;
        }

        PlayNextLine();
    }

    public void ShowDialogue(
        IList<string> lines,
        TextMeshProUGUI textLabel,
        GameObject dialoguePanel = null,
        float typingSpeed = -1f,
        bool queueIfRunning = true,
        bool hidePanelOnFinish = true,
        Action onComplete = null)
    {
        if (lines == null || lines.Count == 0)
        {
            Debug.LogWarning("DialogueManager: Dialogue lines is empty, skipped.");
            return;
        }

        TextMeshProUGUI resolvedLabel = ResolveTextLabel(textLabel);
        if (resolvedLabel == null)
        {
            Debug.LogError("DialogueManager: No valid TextMeshProUGUI target was provided.");
            return;
        }

        var resolvedPanel = ResolveDialoguePanel(dialoguePanel, resolvedLabel);
        DialogueRequest request = new DialogueRequest
        {
            lines = new List<string>(lines),
            textLabel = resolvedLabel,
            dialoguePanel = resolvedPanel,
            typingSpeed = typingSpeed > 0f ? typingSpeed : defaultTypingSpeed,
            hidePanelWhenFinished = hidePanelOnFinish,
            onComplete = onComplete
        };

        if (defaultTextLabel == null)
        {
            defaultTextLabel = resolvedLabel;
        }

        if (defaultDialoguePanel == null && resolvedPanel != null)
        {
            defaultDialoguePanel = resolvedPanel;
        }

        if (activeRequest == null && requestQueue.Count == 0)
        {
            StartRequest(request);
            return;
        }

        if (queueIfRunning)
        {
            requestQueue.Enqueue(request);
            return;
        }

        requestQueue.Clear();
        StopCurrentRequest();
        StartRequest(request);
    }

    public void SkipAllDialogue()
    {
        requestQueue.Clear();
        FinishActiveRequest();
    }

    public bool IsBusy => activeRequest != null;

    private void StartRequest(DialogueRequest request)
    {
        activeRequest = request;
        cancelCurrentLine = false;
        isTyping = false;

        request.lines.RemoveAll(string.IsNullOrEmpty);

        if (request.textLabel != null)
        {
            request.textLabel.text = string.Empty;
        }

        if (request.dialoguePanel != null)
        {
            request.dialoguePanel.SetActive(true);
        }

        if (request.lines.Count == 0)
        {
            FinishActiveRequest();
            return;
        }

        PlayNextLine();
    }

    private void PlayNextLine()
    {
        if (activeRequest == null)
        {
            return;
        }

        if (activeRequest.lines.Count == 0)
        {
            FinishActiveRequest();
            return;
        }

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        string currentLine = activeRequest.lines[0];
        activeRequest.lines.RemoveAt(0);
        typingCoroutine = StartCoroutine(TypeLine(currentLine));
    }

    private IEnumerator TypeLine(string line)
    {
        if (activeRequest == null || activeRequest.textLabel == null)
        {
            yield break;
        }

        isTyping = true;
        cancelCurrentLine = false;
        activeRequest.textLabel.text = string.Empty;

        StringBuilder builder = new StringBuilder(line.Length);

        foreach (char letter in line)
        {
            if (cancelCurrentLine)
            {
                activeRequest.textLabel.text = line;
                break;
            }

            builder.Append(letter);
            activeRequest.textLabel.text = builder.ToString();
            yield return new WaitForSeconds(activeRequest.typingSpeed);
        }

        isTyping = false;
        cancelCurrentLine = false;
        typingCoroutine = null;
    }

    private void FinishActiveRequest()
    {
        if (activeRequest == null)
        {
            return;
        }

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (activeRequest.dialoguePanel != null && activeRequest.hidePanelWhenFinished)
        {
            activeRequest.dialoguePanel.SetActive(false);
        }

        Action callback = activeRequest.onComplete;
        activeRequest = null;
        isTyping = false;
        cancelCurrentLine = false;

        callback?.Invoke();

        if (requestQueue.Count > 0)
        {
            StartRequest(requestQueue.Dequeue());
        }
    }

    private void StopCurrentRequest()
    {
        if (activeRequest == null)
        {
            return;
        }

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (activeRequest.dialoguePanel != null && activeRequest.hidePanelWhenFinished)
        {
            activeRequest.dialoguePanel.SetActive(false);
        }

        activeRequest = null;
        isTyping = false;
        cancelCurrentLine = false;
    }

    private TextMeshProUGUI ResolveTextLabel(TextMeshProUGUI textLabel)
    {
        if (textLabel != null)
        {
            return textLabel;
        }

        return defaultTextLabel;
    }

    private GameObject ResolveDialoguePanel(GameObject dialoguePanel, TextMeshProUGUI fallbackTextLabel)
    {
        if (dialoguePanel != null)
        {
            return dialoguePanel;
        }

        if (defaultDialoguePanel != null)
        {
            return defaultDialoguePanel;
        }

        if (fallbackTextLabel != null && fallbackTextLabel.transform.parent != null)
        {
            return fallbackTextLabel.transform.parent.gameObject;
        }

        return null;
    }
}
