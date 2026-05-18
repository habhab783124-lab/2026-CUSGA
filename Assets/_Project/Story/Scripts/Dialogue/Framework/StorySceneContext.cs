using UnityEngine;

[DisallowMultipleComponent]
public sealed class StorySceneContext : MonoBehaviour
{
    [SerializeField] private StoryActorRegistry actorRegistry;
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private DialogueBubbleView defaultDialogueBubblePrefab;
    [SerializeField] private PlayerInteractor2D playerInteractor;

    private void Awake()
    {
        CacheOptionalReferences();
    }

    private void Reset()
    {
        CacheOptionalReferences();
    }

    public DialogueRunner ResolveDialogueRunner(Component owner = null)
    {
        if (dialogueRunner == null && owner != null)
        {
            dialogueRunner = owner.GetComponent<DialogueRunner>();
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = FindObjectOfType<DialogueRunner>(includeInactive: true);
        }

        return dialogueRunner;
    }

    public void ConfigureDialogueRunner(DialogueRunner runner, DialogueBubbleView bubblePrefabOverride = null)
    {
        if (runner == null)
        {
            return;
        }

        DialogueBubbleView bubblePrefab = bubblePrefabOverride != null
            ? bubblePrefabOverride
            : defaultDialogueBubblePrefab;
        if (bubblePrefab != null)
        {
            runner.SetBubblePrefab(bubblePrefab);
        }
    }

    public PlayerInteractor2D ResolvePlayerInteractor()
    {
        if (playerInteractor != null)
        {
            return playerInteractor;
        }

        if (actorRegistry != null)
        {
            playerInteractor = actorRegistry.ResolvePlayerInteractor();
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindObjectOfType<PlayerInteractor2D>(includeInactive: true);
        }

        return playerInteractor;
    }

    public Transform ResolvePlayerTransform()
    {
        if (actorRegistry != null)
        {
            Transform registeredPlayer = actorRegistry.ResolvePlayer();
            if (registeredPlayer != null)
            {
                return registeredPlayer;
            }
        }

        PlayerInteractor2D resolvedInteractor = ResolvePlayerInteractor();
        if (resolvedInteractor != null)
        {
            return resolvedInteractor.transform;
        }

        GameObject playerObject = GameObject.Find("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    public Transform ResolveActor(string actorId, string fallbackSceneObjectName = null)
    {
        if (actorRegistry != null)
        {
            Transform registeredActor = actorRegistry.ResolveActor(actorId, fallbackSceneObjectName);
            if (registeredActor != null)
            {
                return registeredActor;
            }
        }

        if (string.Equals(actorId, "player", System.StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePlayerTransform();
        }

        if (string.IsNullOrWhiteSpace(fallbackSceneObjectName))
        {
            return null;
        }

        GameObject sceneObject = GameObject.Find(fallbackSceneObjectName);
        return sceneObject != null ? sceneObject.transform : null;
    }

    public Transform ResolveDialogueAnchor(string actorId, Transform fallbackActor = null, string fallbackSceneObjectName = null)
    {
        if (actorRegistry != null)
        {
            Transform registeredAnchor = actorRegistry.ResolveDialogueAnchor(actorId, fallbackActor, fallbackSceneObjectName);
            if (registeredAnchor != null)
            {
                return registeredAnchor;
            }
        }

        return fallbackActor != null
            ? fallbackActor
            : ResolveActor(actorId, fallbackSceneObjectName);
    }

    public void SetPlayerCutsceneLock(bool locked)
    {
        PlayerInteractor2D resolvedInteractor = ResolvePlayerInteractor();
        if (resolvedInteractor == null || resolvedInteractor.Motor == null)
        {
            return;
        }

        resolvedInteractor.Motor.SetCutsceneMovementHold(locked);
        resolvedInteractor.Motor.SetMovementLocked(locked);
    }

    public void SetPlayerInteractionState(bool inputEnabled, bool promptVisible)
    {
        PlayerInteractor2D resolvedInteractor = ResolvePlayerInteractor();
        if (resolvedInteractor == null)
        {
            return;
        }

        resolvedInteractor.SetInteractionInputEnabled(inputEnabled);
        resolvedInteractor.SetInteractionPromptVisible(promptVisible);
    }

    private void CacheOptionalReferences()
    {
        if (actorRegistry == null)
        {
            actorRegistry = GetComponent<StoryActorRegistry>();
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (playerInteractor == null && actorRegistry != null)
        {
            playerInteractor = actorRegistry.ResolvePlayerInteractor();
        }
    }
}
