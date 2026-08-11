using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Horizontal move speed")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("Clamp position by x range")]
    [SerializeField] private bool useBoundary = true;
    [Tooltip("Boundary min X")]
    [SerializeField] private float minX = -8f;
    [Tooltip("Boundary max X")]
    [SerializeField] private float maxX = 8f;

    [Header("Animator")]
    [Tooltip("Optional animator, auto get from self if empty")]
    [SerializeField] private Animator animator;
    [Tooltip("Bool parameter name for walk animation")]
    [SerializeField] private string walkingBoolParameter = "isWalking";
    [Tooltip("Optional float parameter name for move X")]
    [SerializeField] private string moveXFloatParameter = "";

    [Header("Interaction")]
    [Tooltip("Use raycast to find nearby NPC/interactable")]
    [SerializeField] private bool useRaycastDetection = true;
    [Tooltip("Local offset of ray/overlap origin")]
    [SerializeField] private Vector2 interactionOffset = Vector2.zero;
    [Tooltip("Ray length")]
    [SerializeField] private float interactDistance = 1.2f;
    [Tooltip("Overlap radius when trigger mode is enabled")]
    [SerializeField] private float triggerRadius = 0.9f;
    [Tooltip("Only detect objects in these layers")]
    [SerializeField] private LayerMask interactableLayer = ~0;
    [Tooltip("Optional tag filter, empty means no filter")]
    [SerializeField] private string interactableTag = "";
    [Tooltip("Interact key")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private static readonly float InputDeadZone = 0.001f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rigidbody2D;

    private float moveInput;
    private bool isFacingRight = true;
    private bool isWalking;
    private IInteractable currentInteractable;
    private readonly Collider2D[] overlapBuffer = new Collider2D[8];

    private int walkingParamHash = -1;
    private int moveXParamHash = -1;

    public IInteractable CurrentInteractable => currentInteractable;
    public string CurrentInteractPrompt =>
        currentInteractable != null && !string.IsNullOrWhiteSpace(currentInteractable.InteractionPrompt)
            ? currentInteractable.InteractionPrompt
            : string.Empty;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidbody2D = GetComponent<Rigidbody2D>();

        walkingParamHash = string.IsNullOrWhiteSpace(walkingBoolParameter)
            ? -1
            : Animator.StringToHash(walkingBoolParameter);

        moveXParamHash = string.IsNullOrWhiteSpace(moveXFloatParameter)
            ? -1
            : Animator.StringToHash(moveXFloatParameter);
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (moveInput > InputDeadZone)
        {
            isFacingRight = true;
            SetFlip(false);
        }
        else if (moveInput < -InputDeadZone)
        {
            isFacingRight = false;
            SetFlip(true);
        }

        isWalking = Mathf.Abs(moveInput) > InputDeadZone;
        UpdateAnimatorState();
        FindInteractable();

        if (Input.GetKeyDown(interactKey) && currentInteractable != null && currentInteractable.IsInteractable)
        {
            currentInteractable.Interact(this);
        }
    }

    private void FixedUpdate()
    {
        Vector2 nextPosition = transform.position;

        if (Mathf.Abs(moveInput) > InputDeadZone)
        {
            nextPosition.x += moveInput * moveSpeed * Time.fixedDeltaTime;
        }

        if (useBoundary)
        {
            nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        }

        if (rigidbody2D != null)
        {
            rigidbody2D.MovePosition(nextPosition);
        }
        else
        {
            transform.position = new Vector3(nextPosition.x, transform.position.y, transform.position.z);
        }
    }

    private void UpdateAnimatorState()
    {
        if (animator == null)
        {
            return;
        }

        if (walkingParamHash != -1 && HasAnimatorParameter(walkingParamHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(walkingParamHash, isWalking);
        }

        if (moveXParamHash != -1 && HasAnimatorParameter(moveXParamHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(moveXParamHash, moveInput);
        }
    }

    private void FindInteractable()
    {
        IInteractable next = null;

        if (useRaycastDetection)
        {
            Vector2 origin = (Vector2)transform.position + interactionOffset;
            Vector2 dir = isFacingRight ? Vector2.right : Vector2.left;
            RaycastHit2D hit = Physics2D.Raycast(origin, dir, interactDistance, interactableLayer);
            if (hit.collider != null)
            {
                next = ResolveInteractable(hit.collider);
            }
        }
        else
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                (Vector2)transform.position + interactionOffset,
                triggerRadius,
                overlapBuffer,
                interactableLayer
            );

            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider2D target = overlapBuffer[i];
                if (target == null || target.gameObject == gameObject)
                {
                    continue;
                }

                var interactable = ResolveInteractable(target);
                if (interactable == null || !interactable.IsInteractable)
                {
                    continue;
                }

                float distance = Vector2.Distance(transform.position, target.bounds.center);
                if (distance < nearest)
                {
                    nearest = distance;
                    next = interactable;
                }
            }
        }

        currentInteractable = next;
    }

    private IInteractable ResolveInteractable(Collider2D collider)
    {
        if (collider == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(interactableTag))
        {
            if (!collider.CompareTag(interactableTag) && !collider.transform.root.CompareTag(interactableTag))
            {
                return null;
            }
        }

        return collider.GetComponentInParent<IInteractable>();
    }

    private void SetFlip(bool flipX)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = flipX;
            return;
        }

        Vector3 scale = transform.localScale;
        scale.x = flipX ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    private bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.nameHash == hash)
            {
                return param.type == type;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (useRaycastDetection)
        {
            Gizmos.color = Color.yellow;
            Vector2 dir = isFacingRight ? Vector2.right : Vector2.left;
            Vector2 origin = (Vector2)transform.position + interactionOffset;
            Gizmos.DrawLine(origin, origin + dir * interactDistance);
        }
        else
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere((Vector2)transform.position + interactionOffset, triggerRadius);
        }
    }
}
