using UnityEngine;

public abstract class StoryCutsceneControllerBase : MonoBehaviour
{
    [Header("Shared")]
    [SerializeField] private StorySceneContext storyContext;

    protected StorySceneContext StoryContext => storyContext;

    protected virtual void Awake()
    {
        CacheStoryContext();
    }

    protected virtual void Reset()
    {
        CacheStoryContext();
    }

    protected DialogueRunner ResolveDialogueRunner(DialogueRunner current)
    {
        if (current != null)
        {
            return current;
        }

        if (storyContext != null)
        {
            DialogueRunner resolvedRunner = storyContext.ResolveDialogueRunner(this);
            if (resolvedRunner != null)
            {
                return resolvedRunner;
            }
        }

        return GetComponent<DialogueRunner>();
    }

    protected PlayerInteractor2D ResolvePlayerInteractor(PlayerInteractor2D current)
    {
        if (current != null)
        {
            return current;
        }

        if (storyContext != null)
        {
            PlayerInteractor2D resolvedInteractor = storyContext.ResolvePlayerInteractor();
            if (resolvedInteractor != null)
            {
                return resolvedInteractor;
            }
        }

        return FindObjectOfType<PlayerInteractor2D>(includeInactive: true);
    }

    protected Transform ResolvePlayerTransform(Transform current)
    {
        if (current != null)
        {
            return current;
        }

        if (storyContext != null)
        {
            Transform resolvedPlayer = storyContext.ResolvePlayerTransform();
            if (resolvedPlayer != null)
            {
                return resolvedPlayer;
            }
        }

        PlayerInteractor2D resolvedInteractor = ResolvePlayerInteractor(null);
        if (resolvedInteractor != null)
        {
            return resolvedInteractor.transform;
        }

        GameObject playerObject = GameObject.Find("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    protected Transform ResolveActor(Transform current, string actorId, string fallbackSceneObjectName)
    {
        if (current != null)
        {
            return current;
        }

        if (storyContext != null)
        {
            Transform resolvedActor = storyContext.ResolveActor(actorId, fallbackSceneObjectName);
            if (resolvedActor != null)
            {
                return resolvedActor;
            }
        }

        if (string.IsNullOrWhiteSpace(fallbackSceneObjectName))
        {
            return null;
        }

        GameObject sceneObject = GameObject.Find(fallbackSceneObjectName);
        return sceneObject != null ? sceneObject.transform : null;
    }

    protected Transform ResolveDialogueAnchor(
        Transform explicitAnchor,
        string actorId,
        Transform fallbackActor,
        string fallbackSceneObjectName = null)
    {
        if (explicitAnchor != null)
        {
            return explicitAnchor;
        }

        if (storyContext != null)
        {
            Transform resolvedAnchor = storyContext.ResolveDialogueAnchor(actorId, fallbackActor, fallbackSceneObjectName);
            if (resolvedAnchor != null)
            {
                return resolvedAnchor;
            }
        }

        return fallbackActor != null
            ? fallbackActor
            : ResolveActor(null, actorId, fallbackSceneObjectName);
    }

    protected Transform EnsureRuntimeAnchor(ref Transform runtimeAnchor, string anchorName)
    {
        if (runtimeAnchor != null)
        {
            return runtimeAnchor;
        }

        Transform existing = transform.Find(anchorName);
        if (existing != null)
        {
            runtimeAnchor = existing;
            return runtimeAnchor;
        }

        GameObject anchorObject = new GameObject(anchorName);
        anchorObject.transform.SetParent(transform, false);
        runtimeAnchor = anchorObject.transform;
        return runtimeAnchor;
    }

    protected void UpdateRuntimeAnchor(
        Transform runtimeAnchor,
        Transform explicitAnchor,
        Transform fallbackActor,
        Vector3 offset,
        string actorId = null,
        string fallbackSceneObjectName = null)
    {
        if (runtimeAnchor == null)
        {
            return;
        }

        runtimeAnchor.position = ResolveAnchorPosition(
            explicitAnchor,
            fallbackActor,
            offset,
            actorId,
            fallbackSceneObjectName);
    }

    protected Vector3 ResolveAnchorPosition(
        Transform explicitAnchor,
        Transform fallbackActor,
        Vector3 offset,
        string actorId = null,
        string fallbackSceneObjectName = null)
    {
        Transform resolvedAnchor = ResolveDialogueAnchor(
            explicitAnchor,
            actorId,
            fallbackActor,
            fallbackSceneObjectName);
        if (resolvedAnchor != null)
        {
            return resolvedAnchor.position + offset;
        }

        return transform.position + offset;
    }

    protected void SetPlayerCutsceneLock(PlayerInteractor2D interactor, bool locked)
    {
        if (interactor != null && interactor.Motor != null)
        {
            interactor.Motor.SetCutsceneMovementHold(locked);
            interactor.Motor.SetMovementLocked(locked);
            return;
        }

        if (storyContext != null)
        {
            storyContext.SetPlayerCutsceneLock(locked);
        }
    }

    protected bool TryConfigureDialogueRunner(
        ref DialogueRunner runner,
        DialogueBubbleView bubblePrefab,
        string ownerName)
    {
        runner = ResolveDialogueRunner(runner);
        if (runner == null)
        {
            Debug.LogError($"{ownerName}: 当前对象上未挂载 DialogueRunner。", this);
            return false;
        }

        if (storyContext != null)
        {
            storyContext.ConfigureDialogueRunner(runner, bubblePrefab);
        }
        else if (bubblePrefab != null)
        {
            runner.SetBubblePrefab(bubblePrefab);
        }

        return true;
    }

    private void CacheStoryContext()
    {
        if (storyContext == null)
        {
            storyContext = GetComponent<StorySceneContext>();
        }
    }
}
