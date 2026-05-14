using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueRunner))]
public sealed class Chapter7SceneController : MonoBehaviour
{
    [Header("对白")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string dialogueId = "chapter7_intro";
    [SerializeField] private bool playOnStart = true;

    [Header("对话气泡")]
    [SerializeField] private DialogueBubbleView dialogueBubblePrefab;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform npcBubbleAnchor;

    [Header("气泡位置微调")]
    [SerializeField] private Vector3 playerBubbleAnchorOffset = new Vector3(-2f, 1.8f, 0f);
    [SerializeField] private Vector3 npcBubbleAnchorOffset = new Vector3(2f, 1.8f, 0f);

    [Header("可选角色引用")]
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private Transform player;
    [SerializeField] private Transform npc;

    [Header("开场")]
    [SerializeField] private float dialogueDelayOnStart = 0.1f;

    private bool hasPlayed;
    private Transform runtimePlayerBubbleAnchor;
    private Transform runtimeNpcBubbleAnchor;

    private void Reset()
    {
        dialogueRunner = GetComponent<DialogueRunner>();
        playerInteractor = FindObjectOfType<PlayerInteractor2D>(includeInactive: true);
    }

    private void Awake()
    {
        dialogueRunner = GetComponent<DialogueRunner>();
        ResolveReferences();
        EnsureRuntimeAnchors();
        UpdateRuntimeAnchors();
    }

    private void LateUpdate()
    {
        UpdateRuntimeAnchors();
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
        if (hasPlayed)
        {
            return;
        }

        dialogueRunner = GetComponent<DialogueRunner>();
        ResolveReferences();
        EnsureRuntimeAnchors();
        UpdateRuntimeAnchors();

        if (!ValidateSetup())
        {
            return;
        }

        hasPlayed = true;
        StartCoroutine(PlayRoutine());
    }

    private void ResolveReferences()
    {
        if (playerInteractor == null)
        {
            playerInteractor = FindObjectOfType<PlayerInteractor2D>(includeInactive: true);
        }

        if (player == null && playerInteractor != null)
        {
            player = playerInteractor.transform;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (npc == null)
        {
            GameObject npcObject = GameObject.Find("Shen");
            if (npcObject != null)
            {
                npc = npcObject.transform;
            }
        }
    }

    private void EnsureRuntimeAnchors()
    {
        if (runtimePlayerBubbleAnchor == null)
        {
            runtimePlayerBubbleAnchor = CreateRuntimeAnchor("Chapter7PlayerBubbleAnchorRuntime");
        }

        if (runtimeNpcBubbleAnchor == null)
        {
            runtimeNpcBubbleAnchor = CreateRuntimeAnchor("Chapter7NpcBubbleAnchorRuntime");
        }
    }

    private Transform CreateRuntimeAnchor(string anchorName)
    {
        Transform existing = transform.Find(anchorName);
        if (existing != null)
        {
            return existing;
        }

        GameObject anchorObject = new GameObject(anchorName);
        anchorObject.transform.SetParent(transform, false);
        return anchorObject.transform;
    }

    private void UpdateRuntimeAnchors()
    {
        if (runtimePlayerBubbleAnchor != null)
        {
            runtimePlayerBubbleAnchor.position = ResolveAnchorPosition(playerBubbleAnchor, player, playerBubbleAnchorOffset);
        }

        if (runtimeNpcBubbleAnchor != null)
        {
            runtimeNpcBubbleAnchor.position = ResolveAnchorPosition(npcBubbleAnchor, npc, npcBubbleAnchorOffset);
        }
    }

    private Vector3 ResolveAnchorPosition(Transform explicitAnchor, Transform fallbackActor, Vector3 offset)
    {
        if (explicitAnchor != null)
        {
            return explicitAnchor.position + offset;
        }

        if (fallbackActor != null)
        {
            return fallbackActor.position + offset;
        }

        return transform.position + offset;
    }

    private bool ValidateSetup()
    {
        if (dialogueRunner == null)
        {
            Debug.LogError("Chapter7SceneController: 当前对象上未挂载 DialogueRunner。", this);
            return false;
        }

        if (dialogueBubblePrefab != null)
        {
            dialogueRunner.SetBubblePrefab(dialogueBubblePrefab);
        }

        if (runtimePlayerBubbleAnchor == null || runtimeNpcBubbleAnchor == null)
        {
            Debug.LogError("Chapter7SceneController: 运行时气泡锚点创建失败。", this);
            return false;
        }

        return true;
    }

    private IEnumerator PlayRoutine()
    {
        if (dialogueDelayOnStart > 0f)
        {
            yield return new WaitForSeconds(dialogueDelayOnStart);
        }

        IReadOnlyList<DialogueLine> lines = Chapter7.Get(dialogueId);
        if (lines == null || lines.Count == 0)
        {
            yield break;
        }

        dialogueRunner.PlayConversation(
            playerInteractor,
            runtimePlayerBubbleAnchor,
            runtimeNpcBubbleAnchor,
            (IList<DialogueLine>)lines);
    }
}
