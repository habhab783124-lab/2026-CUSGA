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

    [Header("Player during intro")]
    [SerializeField] private bool hideInteractionPromptDuringIntro = true;
    [SerializeField] private bool disableInteractionInputDuringIntro = true;

    [Header("Walk")]
    [SerializeField] private Vector2 stopOffsetFromPlayer = new Vector2(-1.25f, 0f);
    [SerializeField] private float extraRightBeyondCamera = 0.75f;
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private bool flipSpriteWhenWalkingLeft = true;
    [SerializeField] private float arriveEpsilon = 0.04f;

    [Header("Auto dialogue after intro")]
    [SerializeField] private bool playDialogueWhenNpcArrives = true;
    [SerializeField] private string arrivedDialogueId = "chapter4_Chen";
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private Transform npcBubbleAnchor;
    [SerializeField] private Transform playerBubbleAnchor;

    [Header("After intro")]
    [SerializeField] private bool releaseCutsceneHoldWhenNpcArrives = true;
    [SerializeField] private bool unlockPlayerWhenNpcArrives = true;
    [SerializeField] private bool restoreInteractionPromptAfter = true;
    [SerializeField] private bool restoreInteractionInputAfter = true;
    [SerializeField] private UnityEvent onNpcArrived;

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

        ApplyFacing(target.x - npc.position.x);

        while (Vector3.Distance(
                   new Vector3(npc.position.x, npc.position.y, 0f),
                   new Vector3(target.x, target.y, 0f)) > arriveEpsilon)
        {
            npc.position = Vector3.MoveTowards(npc.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        npc.position = new Vector3(target.x, target.y, npc.position.z);
        ApplyFacing(ppos.x - npc.position.x);

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

    private void PlayArrivedDialogue()
    {
        if (dialogueRunner == null || dialogueRunner.IsPlaying || string.IsNullOrWhiteSpace(arrivedDialogueId))
        {
            return;
        }

        IReadOnlyList<DialogueLine> dialogue = DialogueScripts.Get(arrivedDialogueId);
        if (dialogue == null || dialogue.Count == 0)
        {
            Debug.LogWarning($"StoryNpcWalkIntro2D: dialogue id '{arrivedDialogueId}' returned no lines.", this);
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
            OnArrivedDialogueEnded);
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
}
