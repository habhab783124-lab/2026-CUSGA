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
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Animator shenAnimator;
    [SerializeField] private SpriteRenderer shenSpriteRenderer;
    [SerializeField] private Sprite[] shenWalkLeftFrames = new Sprite[0];
    [SerializeField] private Sprite[] shenWalkRightFrames = new Sprite[0];
    [SerializeField] private Sprite shenIdleSprite;
    [SerializeField] private float shenWalkFramesPerSecond = 8f;

    [Header("Final Positions")]
    [SerializeField] private Transform playerStopPoint;
    [SerializeField] private Transform shenStopPoint;

    [Header("Entrance Movement")]
    [SerializeField] private float playerMoveSpeed = 3.2f;
    [SerializeField] private float shenMoveSpeed = 3.2f;
    [SerializeField] private float extraOffscreenDistance = 1.5f;
    [SerializeField] private float arriveEpsilon = 0.02f;
    [SerializeField] private float dialogueDelayAfterArrive = 0.15f;

    [Header("Bus Interlude")]
    [SerializeField] private Sprite busSprite;
    [SerializeField] private float busMoveSpeed = 5f;
    [SerializeField] private float busArriveEpsilon = 0.02f;
    [SerializeField] private float busStartExtraLeftBeyondCamera = 8f;
    [SerializeField] private float busStopRightScreenMargin = 0.1f;
    [SerializeField] private float busYPosition = 0.5f;
    [SerializeField] private int busSortingOrder = 0;
    [SerializeField] private Vector3 busScale = new(1.4f, 1.4f, 1f);

    [Header("Transition")]
    [SerializeField] private string nextSceneName = "chapter7";
    [SerializeField] private float fadeOutToBlackDuration = 1f;
    [SerializeField] private float fadeInFromBlackDuration = 1f;

    private const string Chapter6IntroDialogueId = "chapter6_intro";
    private const int Chapter6BusInsertAfterLineIndex = 25;
    private Coroutine sequenceRoutine;
    private bool hasPlayed;
    private Vector3 playerTargetPosition;
    private Vector3 shenTargetPosition;
    private bool hasPlayerTarget;
    private bool hasShenTarget;
    private bool targetPositionsInitialized;
    private string currentPlayerAnimationState;
    private string currentShenAnimationState;
    private Transform runtimeBus;
    private SpriteRenderer runtimeBusRenderer;
    private List<DialogueLine> pendingDialogueLinesAfterBus;
    private Coroutine shenSpriteAnimationRoutine;
    private bool shenAnimatorDisabledForSpriteAnimation;
    private bool transitionQueued;

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
        if (HasAnyShenSpriteAnimationFrames())
        {
            SetShenAnimatorEnabled(false);
        }

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

        if (player != null && playerAnimator == null)
        {
            playerAnimator = player.GetComponentInChildren<Animator>(true);
        }

        if (shen != null && shenAnimator == null)
        {
            shenAnimator = shen.GetComponentInChildren<Animator>(true);
        }

        if (shen != null && shenSpriteRenderer == null)
        {
            shenSpriteRenderer = shen.GetComponentInChildren<SpriteRenderer>(true);
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
            UpdateMovementAnimations();
            yield return null;
        }

        SnapToTargets();
        PlayPlayerAnimation("LingIdle");
        PlayShenIdleAnimation();

        if (dialogueDelayAfterArrive > 0f)
        {
            yield return new WaitForSeconds(dialogueDelayAfterArrive);
        }

        IReadOnlyList<DialogueLine> lines = Chapter6.Get(dialogueId);
        if (lines == null || lines.Count == 0)
        {
            CompleteScene();
            sequenceRoutine = null;
            yield break;
        }

        IList<DialogueLine> resolvedLines = lines as IList<DialogueLine> ?? new List<DialogueLine>(lines);
        if (TrySplitDialogueForBusInterlude(resolvedLines, out List<DialogueLine> firstSegment, out List<DialogueLine> secondSegment))
        {
            pendingDialogueLinesAfterBus = secondSegment;
            PlayDialogueSegment(firstSegment, OnFirstDialogueSegmentEnded);
        }
        else
        {
            PlayDialogueSegment(resolvedLines, CompleteScene);
        }

        sequenceRoutine = null;
    }

    private void PlayDialogueSegment(IList<DialogueLine> dialogueLines, System.Action onEnded)
    {
        dialogueRunner.PlayConversation(
            playerInteractor,
            playerBubbleAnchor,
            shenBubbleAnchor,
            dialogueLines,
            onEnded: onEnded);
    }

    private bool TrySplitDialogueForBusInterlude(
        IList<DialogueLine> resolvedLines,
        out List<DialogueLine> firstSegment,
        out List<DialogueLine> secondSegment)
    {
        firstSegment = null;
        secondSegment = null;

        if (!string.Equals(dialogueId, Chapter6IntroDialogueId, System.StringComparison.Ordinal)
            || resolvedLines == null
            || resolvedLines.Count <= Chapter6BusInsertAfterLineIndex + 1)
        {
            return false;
        }

        firstSegment = new List<DialogueLine>(Chapter6BusInsertAfterLineIndex + 1);
        secondSegment = new List<DialogueLine>(resolvedLines.Count - Chapter6BusInsertAfterLineIndex - 1);

        for (int i = 0; i < resolvedLines.Count; i++)
        {
            if (i <= Chapter6BusInsertAfterLineIndex)
            {
                firstSegment.Add(resolvedLines[i]);
            }
            else
            {
                secondSegment.Add(resolvedLines[i]);
            }
        }

        return secondSegment.Count > 0;
    }

    private void OnFirstDialogueSegmentEnded()
    {
        SetPlayerCutsceneLock(playerInteractor, true);

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
        }

        sequenceRoutine = StartCoroutine(PlayBusInterludeThenResumeDialogueRoutine());
    }

    private IEnumerator PlayBusInterludeThenResumeDialogueRoutine()
    {
        yield return PlayBusEntranceRoutine();

        if (pendingDialogueLinesAfterBus != null && pendingDialogueLinesAfterBus.Count > 0)
        {
            List<DialogueLine> remainingLines = pendingDialogueLinesAfterBus;
            pendingDialogueLinesAfterBus = null;
            PlayDialogueSegment(remainingLines, CompleteScene);
        }
        else
        {
            CompleteScene();
        }

        sequenceRoutine = null;
    }

    private IEnumerator PlayBusEntranceRoutine()
    {
        if (!EnsureRuntimeBus())
        {
            yield break;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            yield break;
        }

        float halfCameraWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : 10f;
        float halfBusWidth = GetBusHalfWidthWorld();
        float targetX = cam.transform.position.x + halfCameraWidth - halfBusWidth - busStopRightScreenMargin;
        float startX = cam.transform.position.x - halfCameraWidth - busStartExtraLeftBeyondCamera;

        runtimeBus.position = new Vector3(startX, busYPosition, 0f);
        runtimeBus.gameObject.SetActive(true);

        Vector3 targetPosition = new Vector3(targetX, busYPosition, 0f);
        while (runtimeBus != null && Vector3.Distance(runtimeBus.position, targetPosition) > busArriveEpsilon)
        {
            runtimeBus.position = Vector3.MoveTowards(runtimeBus.position, targetPosition, busMoveSpeed * Time.deltaTime);
            yield return null;
        }

        if (runtimeBus != null)
        {
            runtimeBus.position = targetPosition;
        }
    }

    private bool EnsureRuntimeBus()
    {
        if (busSprite == null)
        {
            return false;
        }

        if (runtimeBus == null)
        {
            GameObject busObject = new GameObject("Chapter6RuntimeBus");
            runtimeBus = busObject.transform;
            runtimeBusRenderer = busObject.AddComponent<SpriteRenderer>();
        }

        if (runtimeBusRenderer == null)
        {
            runtimeBusRenderer = runtimeBus.GetComponent<SpriteRenderer>();
        }

        runtimeBusRenderer.sprite = busSprite;
        runtimeBusRenderer.sortingOrder = busSortingOrder;
        runtimeBus.localScale = busScale;
        runtimeBus.gameObject.SetActive(false);
        return true;
    }

    private float GetBusHalfWidthWorld()
    {
        if (busSprite == null)
        {
            return 0f;
        }

        return busSprite.bounds.extents.x * Mathf.Abs(busScale.x);
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

    private void UpdateMovementAnimations()
    {
        UpdateActorAnimation(player, playerTargetPosition, playerAnimator, "LingWalkRight", "LingIdle", ref currentPlayerAnimationState);
        UpdateShenMovementAnimation();
    }

    private static void UpdateActorAnimation(
        Transform actor,
        Vector3 destination,
        Animator animator,
        string moveStateName,
        string idleStateName,
        ref string currentState)
    {
        if (actor == null || animator == null)
        {
            return;
        }

        string targetState = Vector3.Distance(actor.position, destination) > 0.02f ? moveStateName : idleStateName;
        if (currentState == targetState || string.IsNullOrWhiteSpace(targetState))
        {
            return;
        }

        animator.Play(targetState, 0, 0f);
        currentState = targetState;
    }

    private void PlayPlayerAnimation(string stateName)
    {
        if (playerAnimator == null || string.IsNullOrWhiteSpace(stateName) || currentPlayerAnimationState == stateName)
        {
            return;
        }

        playerAnimator.Play(stateName, 0, 0f);
        currentPlayerAnimationState = stateName;
    }

    private void PlayShenAnimation(string stateName)
    {
        if (shenAnimator == null || string.IsNullOrWhiteSpace(stateName) || currentShenAnimationState == stateName)
        {
            return;
        }

        SetShenAnimatorEnabled(true);
        shenAnimator.Play(stateName, 0, 0f);
        currentShenAnimationState = stateName;
    }

    private void ReleaseMovementLock()
    {
        SetPlayerCutsceneLock(playerInteractor, false);
    }

    private void CompleteScene()
    {
        ReleaseMovementLock();
        QueueSceneTransition();
    }

    private void QueueSceneTransition()
    {
        if (transitionQueued || string.IsNullOrWhiteSpace(nextSceneName))
        {
            return;
        }

        transitionQueued = true;
        ScreenFadeTransition.Play(nextSceneName, fadeOutToBlackDuration, fadeInFromBlackDuration, startOpaque: false);
    }

    private void OnDestroy()
    {
        StopShenSpriteAnimation();
    }

    private void UpdateShenMovementAnimation()
    {
        if (shen == null)
        {
            StopShenSpriteAnimation();
            return;
        }

        if (Vector3.Distance(shen.position, shenTargetPosition) <= 0.02f)
        {
            PlayShenIdleAnimation();
            return;
        }

        float deltaX = shenTargetPosition.x - shen.position.x;
        PlayShenWalkAnimation(deltaX);
    }

    private void PlayShenWalkAnimation(float deltaXWorld)
    {
        Sprite[] frames = deltaXWorld < -0.01f ? shenWalkLeftFrames : shenWalkRightFrames;
        if (frames != null && frames.Length > 0)
        {
            SetShenAnimatorEnabled(false);
            PlayShenSpriteAnimation(frames);
            return;
        }

        string stateName = deltaXWorld < -0.01f ? "ShenWalkLeft" : "ShenWalkRight";
        PlayShenAnimation(stateName);
    }

    private void PlayShenIdleAnimation()
    {
        StopShenSpriteAnimation();
        if (shenSpriteRenderer != null && shenIdleSprite != null)
        {
            SetShenAnimatorEnabled(false);
            shenSpriteRenderer.sprite = shenIdleSprite;
            return;
        }

        PlayShenAnimation("ShenIdle");
    }

    private void PlayShenSpriteAnimation(Sprite[] frames)
    {
        if (shenSpriteRenderer == null || frames == null || frames.Length == 0)
        {
            return;
        }

        if (shenSpriteAnimationRoutine != null)
        {
            return;
        }

        shenSpriteAnimationRoutine = StartCoroutine(PlayShenSpriteAnimationRoutine(frames));
    }

    private IEnumerator PlayShenSpriteAnimationRoutine(Sprite[] frames)
    {
        float frameDelay = 1f / Mathf.Max(1f, shenWalkFramesPerSecond);
        int index = 0;

        while (true)
        {
            if (shenSpriteRenderer != null)
            {
                shenSpriteRenderer.sprite = frames[index];
            }

            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(frameDelay);
        }
    }

    private void StopShenSpriteAnimation()
    {
        if (shenSpriteAnimationRoutine != null)
        {
            StopCoroutine(shenSpriteAnimationRoutine);
            shenSpriteAnimationRoutine = null;
        }
    }

    private bool HasAnyShenSpriteAnimationFrames()
    {
        return (shenWalkLeftFrames != null && shenWalkLeftFrames.Length > 0)
            || (shenWalkRightFrames != null && shenWalkRightFrames.Length > 0)
            || shenIdleSprite != null;
    }

    private void SetShenAnimatorEnabled(bool enabled)
    {
        if (shenAnimator == null)
        {
            return;
        }

        if (enabled)
        {
            if (shenAnimatorDisabledForSpriteAnimation)
            {
                shenAnimator.enabled = true;
                shenAnimatorDisabledForSpriteAnimation = false;
                currentShenAnimationState = null;
            }

            return;
        }

        if (shenAnimator.enabled)
        {
            shenAnimator.enabled = false;
            shenAnimatorDisabledForSpriteAnimation = true;
            currentShenAnimationState = null;
        }
    }
}
