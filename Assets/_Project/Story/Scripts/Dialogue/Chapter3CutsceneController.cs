using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第三章过场：进入场景自动开始第一句对白；之后仅鼠标左键推进；结束后 Chen 移出画面并加载下一关。
/// </summary>
public sealed class Chapter3CutsceneController : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string dialogueId = "chapter3_Chen";
    [SerializeField] private Transform chen;
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private float chenMoveSpeed = 3.5f;
    [SerializeField] private float offscreenMarginWorld = 2.5f;
    [SerializeField] private string nextSceneName = "chapter3";
    [Header("切换至下一章")]
    [SerializeField] private float fadeOutToBlackDuration = 0.75f;
    [SerializeField] private float fadeInFromBlackDuration = 0.75f;

    private void Reset()
    {
        dialogueRunner = GetComponent<DialogueRunner>();
    }

    private void Start()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (chen == null)
        {
            var go = GameObject.Find("Chen");
            if (go != null)
            {
                chen = go.transform;
            }
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindObjectOfType<PlayerInteractor2D>();
        }

        if (dialogueRunner == null || playerInteractor == null || chen == null)
        {
            Debug.LogError("Chapter3CutsceneController: 缺少 DialogueRunner / PlayerInteractor2D / Chen。", this);
            return;
        }

        var npcDialogue = chen.GetComponent<NpcDialogue>();
        if (npcDialogue != null)
        {
            npcDialogue.enabled = false;
        }

        playerInteractor.SetInteractionInputEnabled(false);
        playerInteractor.SetInteractionPromptVisible(false);

        IReadOnlyList<DialogueLine> read = DialogueScripts.Get(dialogueId);
        if (read == null || read.Count == 0)
        {
            Debug.LogError($"Chapter3CutsceneController: 对白 id「{dialogueId}」无内容。", this);
            return;
        }

        IList<DialogueLine> lines = read as IList<DialogueLine> ?? new List<DialogueLine>(read);

        dialogueRunner.PlayConversation(
            playerInteractor,
            playerInteractor.transform,
            chen,
            lines,
            OnConversationComplete);
    }

    private void OnConversationComplete()
    {
        if (playerInteractor != null && playerInteractor.Motor != null)
        {
            playerInteractor.Motor.SetMovementLocked(true);
        }

        if (chen != null)
        {
            CutsceneCoroutineUtility.StartPreferredOrFallback(this, playerInteractor, ChenExitAndLoadNextRoutine());
        }
        else
        {
            LoadNext();
        }
    }

    private IEnumerator ChenExitAndLoadNextRoutine()
    {
        var cam = Camera.main;
        while (chen != null)
        {
            chen.position += Vector3.right * (chenMoveSpeed * Time.deltaTime);
            if (cam != null && cam.orthographic)
            {
                float halfW = cam.orthographicSize * cam.aspect;
                if (chen.position.x >= cam.transform.position.x + halfW + offscreenMarginWorld)
                {
                    break;
                }
            }
            else
            {
                yield return new WaitForSeconds(2f);
                break;
            }

            yield return null;
        }

        LoadNext();
    }

    private void LoadNext()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            return;
        }

        ScreenFadeTransition.Play(nextSceneName, fadeOutToBlackDuration, fadeInFromBlackDuration);
    }
}
