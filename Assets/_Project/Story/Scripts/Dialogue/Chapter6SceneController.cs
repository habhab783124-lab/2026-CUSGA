using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueRunner))]
public sealed class Chapter6SceneController : StoryCutsceneControllerBase
{
    [Header("Dialogue")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string dialogueId = "chapter6_intro";
    [SerializeField] private bool playOnStart = true;

    [Header("Dialogue Bubble")]
    [SerializeField] private DialogueBubbleView dialogueBubblePrefab;
    [SerializeField] private Transform playerBubbleAnchor;
    [SerializeField] private Transform shenBubbleAnchor;

    [Header("Actors")]
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private Transform player;
    [SerializeField] private Transform shen;

    [Header("Final Positions")]
    [SerializeField] private Transform playerStopPoint;
    [SerializeField] private Transform shenStopPoint;

    [Header("Entrance Movement")]
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

    protected override void Reset()
    {
        base.Reset();
        dialogueRunner = GetComponent<DialogueRunner>();
        playerInteractor = ResolvePlayerInteractor(playerInteractor);
    }

    protected override void Awake()
    {
        base.Awake();
        dialogueRunner = ResolveDialogueRunner(dialogueRunner);
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

        dialogueRunner = ResolveDialogueRunner(dialogueRunner);
        ResolveReferences();
        EnsureTargetPositions();

        if (!ValidateSetup())
        {
            return;
        }

        hasPlayed = true;
        SetPlayerCutsceneLock(playerInteractor, true);

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
        }

        sequenceRoutine = StartCoroutine(PlaySequenceRoutine());
    }

    private void ResolveReferences()
    {
        playerInteractor = ResolvePlayerInteractor(playerInteractor);
        player = ResolvePlayerTransform(player);
        shen = ResolveActor(shen, "shen", "Shen");
        playerBubbleAnchor = ResolveDialogueAnchor(playerBubbleAnchor, "player", player, "Player");
        shenBubbleAnchor = ResolveDialogueAnchor(shenBubbleAnchor, "shen", shen, "Shen");
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
        if (!TryConfigureDialogueRunner(ref dialogueRunner, dialogueBubblePrefab, nameof(Chapter6SceneController)))
        {
            ReleaseMovementLock();
            return false;
        }

        if (player == null)
        {
            Debug.LogError("Chapter6SceneController: Missing Player reference.", this);
            ReleaseMovementLock();
            return false;
        }

        if (shen == null)
        {
            Debug.LogError("Chapter6SceneController: Missing Shen reference.", this);
            ReleaseMovementLock();
            return false;
        }

        if (!hasPlayerTarget)
        {
            Debug.LogError("Chapter6SceneController: Missing Player final stop position.", this);
            ReleaseMovementLock();
            return false;
        }

        if (!hasShenTarget)
        {
            Debug.LogError("Chapter6SceneController: Missing Shen final stop position.", this);
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

        IList<DialogueLine> resolvedLines = lines as IList<DialogueLine> ?? new List<DialogueLine>(lines);
        dialogueRunner.PlayConversation(
            playerInteractor,
            playerBubbleAnchor,
            shenBubbleAnchor,
            resolvedLines,
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
        SetPlayerCutsceneLock(playerInteractor, false);
    }
}
