using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMotor2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private bool useBoundary = true;
    [SerializeField] private float minX = -8f;
    [SerializeField] private float maxX = 8f;
    [SerializeField] private bool fallbackToKeysIfAxisMissing = true;
    [SerializeField] private KeyCode leftKey1 = KeyCode.A;
    [SerializeField] private KeyCode leftKey2 = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightKey1 = KeyCode.D;
    [SerializeField] private KeyCode rightKey2 = KeyCode.RightArrow;

    [Header("Debug")]
    [SerializeField] private bool debugOverlay = true;

    private Rigidbody2D body;
    private float moveInput;
    private bool movementLocked;
    private bool cutsceneMovementHold;
    private float lastAxis;

    public bool MovementLocked => movementLocked;
    public bool CutsceneMovementHold => cutsceneMovementHold;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        if (!cutsceneMovementHold)
        {
            movementLocked = false;
        }
    }

    private void OnEnable()
    {
        // Safety: ensure we don't stay locked due to an interrupted dialogue/session.
        if (!cutsceneMovementHold)
        {
            movementLocked = false;
        }
    }

    /// <summary>
    /// 开场/过场：为 true 时 <see cref="PlayerInteractor2D"/> 不会在「无对话」时自动解锁移动。
    /// </summary>
    public void SetCutsceneMovementHold(bool hold)
    {
        cutsceneMovementHold = hold;
    }

    private void Update()
    {
        if (movementLocked)
        {
            moveInput = 0f;
            return;
        }

        lastAxis = Input.GetAxisRaw("Horizontal");
        moveInput = lastAxis;

        if (fallbackToKeysIfAxisMissing && Mathf.Approximately(moveInput, 0f))
        {
            bool left = Input.GetKey(leftKey1) || Input.GetKey(leftKey2);
            bool right = Input.GetKey(rightKey1) || Input.GetKey(rightKey2);
            if (left && !right)
            {
                moveInput = -1f;
            }
            else if (right && !left)
            {
                moveInput = 1f;
            }
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log(
                $"[PlayerMotor2D] locked={movementLocked}, axis={lastAxis}, moveInput={moveInput}, pos={body.position}, simulated={body.simulated}",
                this);
        }
    }

    private void OnGUI()
    {
        if (!debugOverlay)
        {
            return;
        }

        if (body == null)
        {
            return;
        }

        string s =
            $"PlayerMotor2D | locked={movementLocked} | axis={lastAxis:0.00} | moveInput={moveInput:0.00} | pos=({body.position.x:0.00},{body.position.y:0.00}) | sim={body.simulated}";
        GUI.Label(new Rect(10, 10, 1200, 24), s);
    }

    private void FixedUpdate()
    {
        Vector2 next = body.position;
        next.x += moveInput * moveSpeed * Time.fixedDeltaTime;

        if (useBoundary)
        {
            next.x = Mathf.Clamp(next.x, minX, maxX);
        }

        body.MovePosition(next);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
    }
}

