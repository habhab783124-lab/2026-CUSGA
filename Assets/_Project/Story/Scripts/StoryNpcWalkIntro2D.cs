using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Chapter intro: lock player, hide [E], NPC walks from right edge to beside player.
[DefaultExecutionOrder(-1000)]
public class StoryNpcWalkIntro2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerMotor2D playerMotor;
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private Transform npc;
    [SerializeField] private SpriteRenderer npcSprite;
    [SerializeField] private Animator npcAnimator;
    [SerializeField] private Sprite[] npcWalkLeftFrames = new Sprite[0];
    [SerializeField] private Sprite[] npcWalkRightFrames = new Sprite[0];
    [SerializeField] private Sprite npcIdleSprite;
    [SerializeField] private float npcWalkFramesPerSecond = 8f;

    [Header("Scene start audio")]
    [SerializeField] private bool playAlarmOnSceneStart = true;
    [SerializeField] private string alarmResourcePath = "StoryAudio/BGM/alarm";
    [SerializeField] [Range(0f, 1f)] private float alarmVolume = 0.8f;
    [SerializeField] private bool loopAlarm = true;

    [Header("Walk audio")]
    [SerializeField] private bool playFootstepWhileWalking = true;
    [SerializeField] private string footstepResourcePath = "StoryAudio/BGM/iron_step";
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.7f;
    [SerializeField] private bool loopFootstep = true;

    [Header("Player during intro")]
    [SerializeField] private bool hideInteractionPromptDuringIntro = true;
    [SerializeField] private bool disableInteractionInputDuringIntro = true;

    [Header("Walk")]
    [SerializeField] private Vector2 stopOffsetFromPlayer = new Vector2(-1.25f, 0f);
    [SerializeField] private float extraRightBeyondCamera = 0.75f;
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private bool flipSpriteWhenWalkingLeft = true;
    [SerializeField] private float arriveEpsilon = 0.04f;
    [SerializeField] private string npcIdleStateName = "ChenIdle";
    [SerializeField] private string npcWalkLeftStateName = "ChenWalkLeft";
    [SerializeField] private string npcWalkRightStateName = "ChenWalkRight";

    [Header("Auto dialogue after intro")]
    [SerializeField] private bool playDialogueWhenNpcArrives = true;
    [SerializeField] private bool autoPlayFirstLineWhenNpcArrives = true;
    [SerializeField] private string arrivedDialogueId = "chapter4_Chen";
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private Transform npcBubbleAnchor;
    [SerializeField] private Transform playerBubbleAnchor;

    [Header("After intro")]
    [SerializeField] private bool releaseCutsceneHoldWhenNpcArrives = true;
    [SerializeField] private bool unlockPlayerWhenNpcArrives = true;
    [SerializeField] private bool restoreInteractionPromptAfter = true;
    [SerializeField] private bool restoreInteractionInputAfter = true;
    [SerializeField] private bool autoLoadNextSceneAfterDialogue = false;
    [SerializeField] private string nextSceneName = string.Empty;
    [SerializeField] private float fadeOutToBlackDuration = 0.75f;
    [SerializeField] private float fadeInFromBlackDuration = 0.75f;
    [SerializeField] private UnityEvent onNpcArrived;

    private AudioSource alarmAudioSource;
    private AudioSource footstepAudioSource;
    private string currentNpcAnimationState;
    private Coroutine npcSpriteAnimationRoutine;
    private bool npcAnimatorDisabledForSpriteAnimation;
    private bool transitionQueued;

    private void Awake()
    {
        if (playerMotor == null)
        {
            playerMotor = UnityEngine.Object.FindObjectOfType<PlayerMotor2D>();
        }

        if (playerInteractor == null)
        {
            playerInteractor = UnityEngine.Object.FindObjectOfType<PlayerInteractor2D>();
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = UnityEngine.Object.FindObjectOfType<DialogueRunner>();
        }

        if (npc != null && npcSprite == null)
        {
            npcSprite = npc.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (npc != null && npcAnimator == null)
        {
            npcAnimator = npc.GetComponentInChildren<Animator>(true);
        }

        if (playAlarmOnSceneStart)
        {
            PlaySceneStartAlarm();
        }

        if (HasAnyNpcSpriteAnimationFrames())
        {
            SetNpcAnimatorEnabled(false);
        }

        if (playerMotor != null)
        {
            playerMotor.SetCutsceneMovementHold(true);
            playerMotor.SetMovementLocked(true);
        }

        if (playerInteractor != null)
        {
            if (hideInteractionPromptDuringIntro)
            {
                playerInteractor.SetInteractionPromptVisible(false);
            }

            if (disableInteractionInputDuringIntro)
            {
                playerInteractor.SetInteractionInputEnabled(false);
            }
        }
    }

    private void OnDestroy()
    {
        StopAudioSource(alarmAudioSource);
        StopAudioSource(footstepAudioSource);
    }

    private void Start()
    {
        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        if (npc == null || playerMotor == null)
        {
            Debug.LogWarning("StoryNpcWalkIntro2D: assign NPC and ensure PlayerMotor2D exists.", this);
            yield break;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("StoryNpcWalkIntro2D: need a camera tagged MainCamera.", this);
            yield break;
        }

        Vector3 ppos = playerMotor.transform.position;
        Vector3 target = new Vector3(
            ppos.x + stopOffsetFromPlayer.x,
            ppos.y + stopOffsetFromPlayer.y,
            npc.position.z);

        float halfW = cam.orthographic ? cam.orthographicSize * cam.aspect : 5f;
        float startX = cam.transform.position.x + halfW + extraRightBeyondCamera;
        npc.position = new Vector3(startX, target.y, npc.position.z);

        float walkDeltaX = target.x - npc.position.x;
        ApplyFacing(walkDeltaX);
        PlayNpcWalkAnimation(walkDeltaX);
        StartWalkingFootstep();

        while (Vector3.Distance(
                   new Vector3(npc.position.x, npc.position.y, 0f),
                   new Vector3(target.x, target.y, 0f)) > arriveEpsilon)
        {
            npc.position = Vector3.MoveTowards(npc.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        StopWalkingFootstep();
        npc.position = new Vector3(target.x, target.y, npc.position.z);
        ApplyFacing(ppos.x - npc.position.x);
        PlayNpcIdleAnimation();

        if (releaseCutsceneHoldWhenNpcArrives && playerMotor != null)
        {
            playerMotor.SetCutsceneMovementHold(false);
        }

        if (unlockPlayerWhenNpcArrives && playerMotor != null)
        {
            playerMotor.SetMovementLocked(false);
        }

        if (playerInteractor != null && !playDialogueWhenNpcArrives)
        {
            if (restoreInteractionPromptAfter)
            {
                playerInteractor.SetInteractionPromptVisible(true);
            }

            if (restoreInteractionInputAfter)
            {
                playerInteractor.SetInteractionInputEnabled(true);
            }
        }

        onNpcArrived?.Invoke();

        if (playDialogueWhenNpcArrives)
        {
            PlayArrivedDialogue();
        }
    }

    private void PlaySceneStartAlarm()
    {
        AudioClip clip = Resources.Load<AudioClip>(alarmResourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"StoryNpcWalkIntro2D: failed to load alarm clip at Resources path '{alarmResourcePath}'.", this);
            return;
        }

        if (alarmAudioSource == null)
        {
            alarmAudioSource = GetOrCreateAudioSource("Alarm Audio");
        }

        ConfigureAndPlayAudioSource(alarmAudioSource, clip, Mathf.Clamp01(alarmVolume), loopAlarm);
    }

    private void StartWalkingFootstep()
    {
        if (!playFootstepWhileWalking)
        {
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(footstepResourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"StoryNpcWalkIntro2D: failed to load footstep clip at Resources path '{footstepResourcePath}'.", this);
            return;
        }

        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetOrCreateAudioSource("Footstep Audio");
        }

        ConfigureAndPlayAudioSource(footstepAudioSource, clip, Mathf.Clamp01(footstepVolume), loopFootstep);
    }

    private void StopWalkingFootstep()
    {
        StopAudioSource(footstepAudioSource);
    }

    private AudioSource GetOrCreateAudioSource(string childName)
    {
        Transform child = transform.Find(childName);
        GameObject target = child != null ? child.gameObject : new GameObject(childName);
        if (child == null)
        {
            target.transform.SetParent(transform, false);
        }

        AudioSource source = target.GetComponent<AudioSource>();
        if (source == null)
        {
            source = target.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private static void ConfigureAndPlayAudioSource(AudioSource source, AudioClip clip, float volume, bool loop)
    {
        if (source == null || clip == null)
        {
            return;
        }

        source.loop = loop;
        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    private static void StopAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
    }

    private void PlayArrivedDialogue()
    {
        if (dialogueRunner == null || dialogueRunner.IsPlaying || string.IsNullOrWhiteSpace(arrivedDialogueId))
        {
            QueueSceneTransition();
            return;
        }

        IReadOnlyList<DialogueLine> dialogue = DialogueScripts.Get(arrivedDialogueId);
        if (dialogue == null || dialogue.Count == 0)
        {
            Debug.LogWarning($"StoryNpcWalkIntro2D: dialogue id '{arrivedDialogueId}' returned no lines.", this);
            QueueSceneTransition();
            return;
        }

        Transform resolvedPlayerAnchor = playerBubbleAnchor != null
            ? playerBubbleAnchor
            : playerInteractor != null ? playerInteractor.transform : playerMotor.transform;
        Transform resolvedNpcAnchor = npcBubbleAnchor != null ? npcBubbleAnchor : npc;

        dialogueRunner.PlayConversation(
            playerInteractor,
            resolvedPlayerAnchor,
            resolvedNpcAnchor,
            new List<DialogueLine>(dialogue),
            OnArrivedDialogueEnded,
            deferFirstLineUntilExternal: !autoPlayFirstLineWhenNpcArrives);

        if (autoPlayFirstLineWhenNpcArrives)
        {
            dialogueRunner.PlayDeferredFirstLine();
        }
    }

    private void OnArrivedDialogueEnded()
    {
        if (playerInteractor != null)
        {
            if (restoreInteractionPromptAfter)
            {
                playerInteractor.SetInteractionPromptVisible(true);
            }

            if (restoreInteractionInputAfter)
            {
                playerInteractor.SetInteractionInputEnabled(true);
            }
        }

        QueueSceneTransition();
    }

    /// <summary>
    /// `StoryNpcWalkIntro2D` 负责 chapter4 / chapter8 里“NPC 入场 + 自动播完到站对话”的整段流程。
    /// 所以这里直接在对话结束回调里切场景，最贴近真实的剧情完成时机。
    /// </summary>
    private void QueueSceneTransition()
    {
        if (!autoLoadNextSceneAfterDialogue || transitionQueued || string.IsNullOrWhiteSpace(nextSceneName))
        {
            return;
        }

        transitionQueued = true;
        if (!CampaignFlowController.AdvanceToNextStepOrLoadFallback(
                nextSceneName,
                fadeOutToBlackDuration,
                fadeInFromBlackDuration,
                startOpaque: false))
        {
            Debug.LogWarning("StoryNpcWalkIntro2D 无法推进到下一场景：既没有活动战役流程，也没有有效的 nextSceneName。", this);
            transitionQueued = false;
        }
    }

    private void ApplyFacing(float deltaXWorld)
    {
        if (!flipSpriteWhenWalkingLeft || npcSprite == null)
        {
            return;
        }

        Vector3 s = npcSprite.transform.localScale;
        float ax = Mathf.Abs(s.x) > 0.0001f ? Mathf.Abs(s.x) : 1f;
        if (deltaXWorld < -0.01f)
        {
            s.x = -ax;
        }
        else if (deltaXWorld > 0.01f)
        {
            s.x = ax;
        }

        npcSprite.transform.localScale = s;
    }

    private void PlayNpcWalkAnimation(float deltaXWorld)
    {
        Sprite[] frames = deltaXWorld < -0.01f ? npcWalkLeftFrames : npcWalkRightFrames;
        if (frames != null && frames.Length > 0)
        {
            SetNpcAnimatorEnabled(false);
            PlayNpcSpriteAnimation(frames);
            return;
        }

        if (npcAnimator == null)
        {
            return;
        }

        SetNpcAnimatorEnabled(true);
        string targetState = deltaXWorld < -0.01f ? npcWalkLeftStateName : npcWalkRightStateName;
        PlayNpcAnimationState(targetState);
    }

    private void PlayNpcIdleAnimation()
    {
        StopNpcSpriteAnimation();
        if (npcSprite != null && npcIdleSprite != null)
        {
            SetNpcAnimatorEnabled(false);
            npcSprite.sprite = npcIdleSprite;
            return;
        }

        SetNpcAnimatorEnabled(true);
        PlayNpcAnimationState(npcIdleStateName);
    }

    private void PlayNpcAnimationState(string stateName)
    {
        if (npcAnimator == null || string.IsNullOrWhiteSpace(stateName) || currentNpcAnimationState == stateName)
        {
            return;
        }

        npcAnimator.Play(stateName, 0, 0f);
        currentNpcAnimationState = stateName;
    }

    private void PlayNpcSpriteAnimation(Sprite[] frames)
    {
        StopNpcSpriteAnimation();
        if (npcSprite == null || frames == null || frames.Length == 0)
        {
            return;
        }

        npcSpriteAnimationRoutine = StartCoroutine(PlayNpcSpriteAnimationRoutine(frames));
    }

    private IEnumerator PlayNpcSpriteAnimationRoutine(Sprite[] frames)
    {
        float frameDelay = 1f / Mathf.Max(1f, npcWalkFramesPerSecond);
        int index = 0;

        while (true)
        {
            if (npcSprite != null)
            {
                npcSprite.sprite = frames[index];
            }

            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(frameDelay);
        }
    }

    private void StopNpcSpriteAnimation()
    {
        if (npcSpriteAnimationRoutine != null)
        {
            StopCoroutine(npcSpriteAnimationRoutine);
            npcSpriteAnimationRoutine = null;
        }
    }

    private bool HasAnyNpcSpriteAnimationFrames()
    {
        return (npcWalkLeftFrames != null && npcWalkLeftFrames.Length > 0)
            || (npcWalkRightFrames != null && npcWalkRightFrames.Length > 0)
            || npcIdleSprite != null;
    }

    private void SetNpcAnimatorEnabled(bool enabled)
    {
        if (npcAnimator == null)
        {
            return;
        }

        if (enabled)
        {
            if (npcAnimatorDisabledForSpriteAnimation)
            {
                npcAnimator.enabled = true;
                npcAnimatorDisabledForSpriteAnimation = false;
                currentNpcAnimationState = null;
            }

            return;
        }

        if (npcAnimator.enabled)
        {
            npcAnimator.enabled = false;
            npcAnimatorDisabledForSpriteAnimation = true;
            currentNpcAnimationState = null;
        }
    }
}
