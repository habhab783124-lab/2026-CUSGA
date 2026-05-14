using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueRunner))]
public sealed class Chapter6SceneController : MonoBehaviour
{
    [Header("对白")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string dialogueId = "chapter6_intro";
    [SerializeField] private bool playOnStart = true;

    [Header("对话气泡")]
    [SerializeField] private DialogueBubbleView dialogueBubblePrefab;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform shenBubbleAnchor;

    [Header("角色引用")]
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private Transform player;
    [SerializeField] private Transform shen;

    [Header("角色最终位置")]
    [SerializeField] private Transform playerStopPoint;
    [SerializeField] private Transform shenStopPoint;

    [Header("入场移动")]
    [SerializeField] private float playerMoveSpeed = 3.2f;
    [SerializeField] private float shenMoveSpeed = 3.2f;
    [SerializeField] private float extraOffscreenDistance = 1.5f;
    [SerializeField] private float arriveEpsilon = 0.02f;
    [SerializeField] private float dialogueDelayAfterArrive = 0.15f;

    private Coroutine sequenceRoutine;
    private bool hasPlayed;
    private Vector3 playerTargetPosition;
    private Vector3 shenTargetPosition;
    private bool hasPlayerTarget;
    private bool hasShenTarget;
    private bool targetPositionsInitialized;

    private void Reset()
    {
        dialogueRunner = GetComponent<DialogueRunner>();
        playerInteractor = FindObjectOfType<PlayerInteractor2D>(includeInactive: true);
    }

    private void Awake()
    {
        dialogueRunner = GetComponent<DialogueRunner>();
        ResolveReferences();
        CaptureInitialTargetPositionsIfNeeded();
        PlaceCharactersAtStartPositions();
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
        EnsureTargetPositions();

        if (!ValidateSetup())
        {
            return;
        }

        hasPlayed = true;

        if (playerInteractor != null && playerInteractor.Motor != null)
        {
            playerInteractor.Motor.SetCutsceneMovementHold(true);
            playerInteractor.Motor.SetMovementLocked(true);
        }

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
        }

        sequenceRoutine = StartCoroutine(PlaySequenceRoutine());
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

        if (shen == null)
        {
            GameObject shenObject = GameObject.Find("Shen");
            if (shenObject != null)
            {
                shen = shenObject.transform;
            }
        }

        if (playerBubbleAnchor == null)
        {
            playerBubbleAnchor = player;
        }

        if (shenBubbleAnchor == null)
        {
            shenBubbleAnchor = shen;
        }
    }

    private void CaptureInitialTargetPositionsIfNeeded()
    {
        if (targetPositionsInitialized)
        {
            return;
        }

        if (playerStopPoint != null)
        {
            playerTargetPosition = playerStopPoint.position;
            hasPlayerTarget = true;
        }
        else if (player != null)
        {
            playerTargetPosition = player.position;
            hasPlayerTarget = true;
        }

        if (shenStopPoint != null)
        {
            shenTargetPosition = shenStopPoint.position;
            hasShenTarget = true;
        }
        else if (shen != null)
        {
            shenTargetPosition = shen.position;
            hasShenTarget = true;
        }

        targetPositionsInitialized = hasPlayerTarget || hasShenTarget;
    }

    private void EnsureTargetPositions()
    {
        if (playerStopPoint != null)
        {
            playerTargetPosition = playerStopPoint.position;
            hasPlayerTarget = true;
        }
        else if (!hasPlayerTarget && player != null)
        {
            playerTargetPosition = player.position;
            hasPlayerTarget = true;
        }

        if (shenStopPoint != null)
        {
            shenTargetPosition = shenStopPoint.position;
            hasShenTarget = true;
        }
        else if (!hasShenTarget && shen != null)
        {
            shenTargetPosition = shen.position;
            hasShenTarget = true;
        }
    }

    private void PlaceCharactersAtStartPositions()
    {
        EnsureTargetPositions();

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        float halfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : 5f;
        float leftStartX = cam.transform.position.x - halfWidth - extraOffscreenDistance;
        float rightStartX = cam.transform.position.x + halfWidth + extraOffscreenDistance;

        if (player != null && hasPlayerTarget)
        {
            player.position = new Vector3(leftStartX, playerTargetPosition.y, playerTargetPosition.z);
        }

        if (shen != null && hasShenTarget)
        {
            shen.position = new Vector3(rightStartX, shenTargetPosition.y, shenTargetPosition.z);
        }
    }

    private bool ValidateSetup()
    {
        if (dialogueRunner == null)
        {
            Debug.LogError("Chapter6SceneController: 当前对象上未挂载 DialogueRunner。", this);
            ReleaseMovementLock();
            return false;
        }

        if (dialogueBubblePrefab != null)
        {
            dialogueRunner.SetBubblePrefab(dialogueBubblePrefab);
        }

        if (player == null)
        {
            Debug.LogError("Chapter6SceneController: 未找到 Player。", this);
            ReleaseMovementLock();
            return false;
        }

        if (shen == null)
        {
            Debug.LogError("Chapter6SceneController: 未找到 Shen。", this);
            ReleaseMovementLock();
            return false;
        }

        if (!hasPlayerTarget)
        {
            Debug.LogError("Chapter6SceneController: 未找到 Player 的最终停靠位置。", this);
            ReleaseMovementLock();
            return false;
        }

        if (!hasShenTarget)
        {
            Debug.LogError("Chapter6SceneController: 未找到 Shen 的最终停靠位置。", this);
            ReleaseMovementLock();
            return false;
        }

        return true;
    }

    private IEnumerator PlaySequenceRoutine()
    {
        while (!BothArrived())
        {
            MoveTowards(player, playerTargetPosition, playerMoveSpeed);
            MoveTowards(shen, shenTargetPosition, shenMoveSpeed);
            yield return null;
        }

        SnapToTargets();

        if (dialogueDelayAfterArrive > 0f)
        {
            yield return new WaitForSeconds(dialogueDelayAfterArrive);
        }

        IReadOnlyList<DialogueLine> lines = Chapter6.Get(dialogueId);
        if (lines == null || lines.Count == 0)
        {
            ReleaseMovementLock();
            sequenceRoutine = null;
            yield break;
        }

        dialogueRunner.PlayConversation(
            playerInteractor,
            playerBubbleAnchor,
            shenBubbleAnchor,
            (IList<DialogueLine>)lines,
            onEnded: ReleaseMovementLock);

        sequenceRoutine = null;
    }

    private bool BothArrived()
    {
        return IsArrived(player, playerTargetPosition) && IsArrived(shen, shenTargetPosition);
    }

    private bool IsArrived(Transform target, Vector3 destination)
    {
        if (target == null)
        {
            return true;
        }

        return Vector3.Distance(target.position, destination) <= arriveEpsilon;
    }

    private void MoveTowards(Transform target, Vector3 destination, float speed)
    {
        if (target == null)
        {
            return;
        }

        target.position = Vector3.MoveTowards(target.position, destination, speed * Time.deltaTime);
    }

    private void SnapToTargets()
    {
        if (player != null)
        {
            player.position = playerTargetPosition;
        }

        if (shen != null)
        {
            shen.position = shenTargetPosition;
        }
    }

    private void ReleaseMovementLock()
    {
        if (playerInteractor != null && playerInteractor.Motor != null)
        {
            playerInteractor.Motor.SetCutsceneMovementHold(false);
            playerInteractor.Motor.SetMovementLocked(false);
        }
    }
}
