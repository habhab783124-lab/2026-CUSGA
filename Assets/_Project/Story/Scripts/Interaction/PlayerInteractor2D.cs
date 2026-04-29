using UnityEngine;

[RequireComponent(typeof(PlayerMotor2D))]
public sealed class PlayerInteractor2D : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float radius = 1.1f;
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField] private LayerMask interactableLayers = ~0;

    [Header("Interaction Prompt (World Space)")]
    [SerializeField] private InteractionPromptView promptPrefab;
    [Tooltip("Additional offset applied after the auto bounds anchor (if enabled).")]
    [SerializeField] private Vector3 promptWorldOffset = Vector3.zero;
    [SerializeField] private bool promptAnchorToTargetBounds = true;
    [Tooltip("Prompt X = bounds.extents.x + padding (to the right of target).")]
    [SerializeField] private float promptBoundsXPadding = 0.12f;
    [Tooltip("Prompt Y = bounds.center.y + extents.y * factor. 0 = mid-height, 1 = top.")]
    [SerializeField] [Range(-1f, 1f)]
    private float promptBoundsYFactor = 0f;

    [Header("Optional")]
    [SerializeField] private DialogueRunner dialogueRunner;

    private PlayerMotor2D motor;
    private readonly Collider2D[] buffer = new Collider2D[12];
    private IInteractable currentTarget;
    private Transform currentTargetTransform;
    private IInteractable lastTarget;
    private InteractionPromptView promptInstance;

    public PlayerMotor2D Motor => motor;
    public DialogueRunner DialogueRunner => dialogueRunner;
    public IInteractable CurrentTarget => currentTarget;
    public Transform CurrentTargetTransform => currentTargetTransform;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor2D>();

        if (dialogueRunner == null)
        {
            dialogueRunner = FindObjectOfType<DialogueRunner>(includeInactive: true);
        }

        EnsurePrompt();
    }

    private void Update()
    {
        if (motor != null && motor.MovementLocked)
        {
            // Safety: if something left the motor locked but dialogue isn't actually playing, unlock.
            if (dialogueRunner == null || !dialogueRunner.IsPlaying)
            {
                motor.SetMovementLocked(false);
            }

            currentTarget = null;
            currentTargetTransform = null;
            UpdatePromptVisibility();
            return;
        }

        // Keep an updated target for UI prompts (e.g., show "E" icon when nearby).
        currentTarget = FindNearestInteractable();
        currentTargetTransform = (currentTarget as Component) != null ? ((Component)currentTarget).transform : null;
        UpdatePromptVisibility();

        if (!Input.GetKeyDown(interactKey))
        {
            return;
        }

        if (currentTarget == null || !currentTarget.CanInteract)
        {
            return;
        }

        currentTarget.Interact(this);
        UpdatePromptVisibility(forceHide: true);
    }

    private void EnsurePrompt()
    {
        if (promptInstance != null)
        {
            return;
        }

        if (promptPrefab == null)
        {
            // Optional fallback: allow placing the prefab under Resources/InteractionPrompt.prefab.
            promptPrefab = Resources.Load<InteractionPromptView>("InteractionPrompt");
        }

        if (promptPrefab == null)
        {
            return;
        }

        promptInstance = Instantiate(promptPrefab);
        promptInstance.name = "InteractionPrompt (Runtime)";
        promptInstance.Show(false);
    }

    private void UpdatePromptVisibility(bool forceHide = false)
    {
        EnsurePrompt();
        if (promptInstance == null)
        {
            return;
        }

        promptInstance.SetWorldOffset(promptWorldOffset);

        if (forceHide || currentTarget == null || currentTargetTransform == null || !currentTarget.CanInteract)
        {
            lastTarget = null;
            promptInstance.Show(false);
            return;
        }

        if (promptAnchorToTargetBounds)
        {
            var bounds = GetTargetBounds(currentTargetTransform);
            var anchor = bounds.center + new Vector3(bounds.extents.x + promptBoundsXPadding, bounds.extents.y * promptBoundsYFactor, 0f);
            promptInstance.SetAnchorWorldPosition(anchor);
        }
        else
        {
            promptInstance.SetFollow(currentTargetTransform);
        }

        if (!ReferenceEquals(lastTarget, currentTarget))
        {
            lastTarget = currentTarget;
            promptInstance.SetText(interactKey.ToString(), currentTarget.Prompt);
        }

        promptInstance.Show(true);
    }

    private static Bounds GetTargetBounds(Transform t)
    {
        // Prefer 2D collider bounds (interaction range uses colliders).
        var c2d = t.GetComponentInChildren<Collider2D>();
        if (c2d != null)
        {
            return c2d.bounds;
        }

        // Fallback: any renderer bounds (SpriteRenderer etc).
        var r = t.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            return r.bounds;
        }

        return new Bounds(t.position, Vector3.one * 0.25f);
    }

    private IInteractable FindNearestInteractable()
    {
        int count = Physics2D.OverlapCircleNonAlloc((Vector2)transform.position + offset, radius, buffer, interactableLayers);
        float nearest = float.MaxValue;
        IInteractable best = null;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = buffer[i];
            if (hit == null)
            {
                continue;
            }

            var interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract)
            {
                continue;
            }

            float dist = Vector2.Distance(transform.position, hit.bounds.center);
            if (dist < nearest)
            {
                nearest = dist;
                best = interactable;
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.75f);
        Gizmos.DrawWireSphere((Vector2)transform.position + offset, radius);
    }
}

