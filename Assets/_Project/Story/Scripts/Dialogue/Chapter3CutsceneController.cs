using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 第三章过场：Chen 从场外向左走入时播放左走帧动画；到位后显示指定 Idle；
/// 对话结束后 Chen 向右离场播放右走帧动画并消失，随后 player 也向右离场；最后黑场切到 chapter3.5。
/// </summary>
public sealed class Chapter3CutsceneController : MonoBehaviour
{
    [Header("对白")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string dialogueId = "chapter3_Chen";

    [Header("Chen")]
    [SerializeField] private Transform chen;
    [SerializeField] private Transform chenStopPoint;
    [SerializeField] private Chapter3ChenStopConfig chenStopConfig;
    [SerializeField] private SpriteRenderer chenSpriteRenderer;
    [SerializeField] private Animator chenAnimator;
    [SerializeField] private Vector2 chenStopOffsetFromPlayer = new(-1.25f, 0f);
    [SerializeField] private float chenMoveSpeed = 3.5f;
    [SerializeField] private float chenArriveEpsilon = 0.04f;
    [SerializeField] private float chenStartExtraRightBeyondCamera = 0.75f;
    [SerializeField] private float offscreenMarginWorld = 2.5f;

    [Header("Chen 帧动画")]
    [SerializeField] private Sprite[] chenWalkLeftFrames = new Sprite[0];
    [SerializeField] private Sprite[] chenWalkRightFrames = new Sprite[0];
    [SerializeField] private Sprite chenIdleSprite;
    [SerializeField] private float chenWalkFramesPerSecond = 8f;

    [Header("玩家")]
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private float playerExitMoveSpeed = 3.5f;
    [SerializeField] private float playerOffscreenMarginWorld = 2.5f;

    [Header("切换至下一章")]
    [SerializeField] private bool forceNextSceneToChapter35 = true;
    [SerializeField] private string nextSceneName = "chapter3.5";
    [SerializeField] private float fadeOutToBlackDuration = 0.75f;
    [SerializeField] private float fadeInFromBlackDuration = 0.75f;

    private const string Chapter35SceneName = "chapter3.5";
    private Coroutine chenSpriteAnimationRoutine;

    private void Reset()
    {
        dialogueRunner = GetComponent<DialogueRunner>();
        nextSceneName = Chapter35SceneName;
    }

    private void Awake()
    {
        ResolveReferences();
        ApplySceneDefaults();
    }

    private void OnValidate()
    {
        ApplySceneDefaults();
    }

    private void Start()
    {
        ResolveReferences();
        ApplySceneDefaults();

        if (dialogueRunner == null || playerInteractor == null || chen == null)
        {
            Debug.LogError("Chapter3CutsceneController: 缺少 DialogueRunner / PlayerInteractor2D / Chen。", this);
            return;
        }

        NpcDialogue npcDialogue = chen.GetComponent<NpcDialogue>();
        if (npcDialogue != null)
        {
            npcDialogue.enabled = false;
        }

        if (chenAnimator != null)
        {
            chenAnimator.enabled = false;
        }

        playerInteractor.SetInteractionInputEnabled(false);
        playerInteractor.SetInteractionPromptVisible(false);
        if (playerInteractor.Motor != null)
        {
            playerInteractor.Motor.SetMovementLocked(true);
        }

        StartCoroutine(EnterThenDialogueRoutine());
    }

    private void ApplySceneDefaults()
    {
        if (forceNextSceneToChapter35)
        {
            nextSceneName = Chapter35SceneName;
        }
    }

    private void ResolveReferences()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (chen == null)
        {
            GameObject go = GameObject.Find("Chen");
            if (go != null)
            {
                chen = go.transform;
            }
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindObjectOfType<PlayerInteractor2D>();
        }

        if (chen != null && chenStopConfig == null)
        {
            chenStopConfig = chen.GetComponent<Chapter3ChenStopConfig>();
        }

        if (chen != null && chenSpriteRenderer == null)
        {
            chenSpriteRenderer = chen.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (chen != null && chenAnimator == null)
        {
            chenAnimator = chen.GetComponentInChildren<Animator>(true);
        }

        if ((chenWalkLeftFrames == null || chenWalkLeftFrames.Length == 0) && !Application.isPlaying)
        {
            chenWalkLeftFrames = LoadSpritesAtPath("Assets/_Project/Story/Sprites/Character/chen_left.png");
        }

        if ((chenWalkRightFrames == null || chenWalkRightFrames.Length == 0) && !Application.isPlaying)
        {
            chenWalkRightFrames = LoadSpritesAtPath("Assets/_Project/Story/Sprites/Character/chen_right.png");
        }

        if (chenIdleSprite == null && !Application.isPlaying)
        {
            chenIdleSprite = LoadSpriteAtPath("Assets/_Project/Story/Sprites/Character/Chen.png");
        }
    }

    private IEnumerator EnterThenDialogueRoutine()
    {
        yield return ChenEnterRoutine();

        IReadOnlyList<DialogueLine> read = DialogueScripts.Get(dialogueId);
        if (read == null || read.Count == 0)
        {
            Debug.LogError($"Chapter3CutsceneController: 对白 id「{dialogueId}」无内容。", this);
            yield break;
        }

        IList<DialogueLine> lines = read as IList<DialogueLine> ?? new List<DialogueLine>(read);
        ShowChenIdleSprite();
        dialogueRunner.PlayConversation(
            playerInteractor,
            playerInteractor.transform,
            chen,
            lines,
            OnConversationComplete);
    }

    private IEnumerator ChenEnterRoutine()
    {
        if (chen == null)
        {
            yield break;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            yield break;
        }

        Vector3 playerPosition = playerInteractor != null ? playerInteractor.transform.position : Vector3.zero;
        Vector3 target = ResolveChenStopTarget(playerPosition);

        float halfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : 5f;
        float startX = cam.transform.position.x + halfWidth + chenStartExtraRightBeyondCamera;
        chen.position = new Vector3(startX, target.y, chen.position.z);

        PlayChenSpriteAnimation(chenWalkLeftFrames);

        while (Vector3.Distance(
                   new Vector3(chen.position.x, chen.position.y, 0f),
                   new Vector3(target.x, target.y, 0f)) > chenArriveEpsilon)
        {
            chen.position = Vector3.MoveTowards(chen.position, target, chenMoveSpeed * Time.deltaTime);
            yield return null;
        }

        chen.position = new Vector3(target.x, target.y, chen.position.z);
        ShowChenIdleSprite();
    }

    private Vector3 ResolveChenStopTarget(Vector3 playerPosition)
    {
        if (chenStopConfig != null)
        {
            Vector2 stopPosition = chenStopConfig.StopPosition;
            return new Vector3(stopPosition.x, stopPosition.y, chen.position.z);
        }

        if (chenStopPoint != null)
        {
            return new Vector3(chenStopPoint.position.x, chenStopPoint.position.y, chen.position.z);
        }

        return new Vector3(
            playerPosition.x + chenStopOffsetFromPlayer.x,
            playerPosition.y + chenStopOffsetFromPlayer.y,
            chen.position.z);
    }

    private void PlayChenSpriteAnimation(Sprite[] frames)
    {
        StopChenSpriteAnimation();
        if (chenSpriteRenderer == null || frames == null || frames.Length == 0)
        {
            return;
        }

        chenSpriteAnimationRoutine = StartCoroutine(PlayChenSpriteAnimationRoutine(frames));
    }

    private IEnumerator PlayChenSpriteAnimationRoutine(Sprite[] frames)
    {
        float frameDelay = 1f / Mathf.Max(1f, chenWalkFramesPerSecond);
        int index = 0;

        while (true)
        {
            if (chenSpriteRenderer != null)
            {
                chenSpriteRenderer.sprite = frames[index];
            }

            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(frameDelay);
        }
    }

    private void StopChenSpriteAnimation()
    {
        if (chenSpriteAnimationRoutine != null)
        {
            StopCoroutine(chenSpriteAnimationRoutine);
            chenSpriteAnimationRoutine = null;
        }
    }

    private void ShowChenIdleSprite()
    {
        StopChenSpriteAnimation();
        if (chenSpriteRenderer != null && chenIdleSprite != null)
        {
            chenSpriteRenderer.sprite = chenIdleSprite;
        }
    }

    private void OnConversationComplete()
    {
        if (playerInteractor != null && playerInteractor.Motor != null)
        {
            playerInteractor.Motor.SetMovementLocked(true);
        }

        CutsceneCoroutineUtility.StartPreferredOrFallback(this, playerInteractor, ExitSequenceAndLoadNextRoutine());
    }

    private IEnumerator ExitSequenceAndLoadNextRoutine()
    {
        yield return ChenExitRoutine();
        yield return PlayerExitRoutine();
        LoadNext();
    }

    private IEnumerator ChenExitRoutine()
    {
        if (chen == null)
        {
            yield break;
        }

        PlayChenSpriteAnimation(chenWalkRightFrames);

        Camera cam = Camera.main;
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

        StopChenSpriteAnimation();
        if (chen != null)
        {
            chen.gameObject.SetActive(false);
        }
    }

    private IEnumerator PlayerExitRoutine()
    {
        if (playerInteractor == null)
        {
            yield break;
        }

        Transform playerTransform = playerInteractor.transform;
        if (playerTransform == null)
        {
            yield break;
        }

        Camera cam = Camera.main;
        while (playerTransform != null)
        {
            playerTransform.position += Vector3.right * (playerExitMoveSpeed * Time.deltaTime);
            if (cam != null && cam.orthographic)
            {
                float halfW = cam.orthographicSize * cam.aspect;
                if (playerTransform.position.x >= cam.transform.position.x + halfW + playerOffscreenMarginWorld)
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
    }

    private void LoadNext()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            return;
        }

        ScreenFadeTransition.Play(nextSceneName, fadeOutToBlackDuration, fadeInFromBlackDuration, startOpaque: false);
    }

#if UNITY_EDITOR
    private static Sprite[] LoadSpritesAtPath(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        List<Sprite> sprites = new List<Sprite>();
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
        return sprites.ToArray();
    }

    private static Sprite LoadSpriteAtPath(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
#endif
}
