using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("左右移动速度")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("是否限制 X 轴范围")]
    [SerializeField] private bool useBoundary = true;

    [Tooltip("最小 X 边界")]
    [SerializeField] private float minX = -8f;

    [Tooltip("最大 X 边界")]
    [SerializeField] private float maxX = 8f;

    [Header("Movement Input")]
    [Tooltip("移动输入轴名称，默认 Horizontal")]
    [SerializeField] private string horizontalAxis = "Horizontal";

    [Tooltip("输入死区，过滤键盘抖动")]
    [SerializeField] private float inputDeadZone = 0.01f;

    [Tooltip("移动平滑系数，值越大越快响应")]
    [SerializeField] private float moveSmoothing = 20f;

    [Header("Animator")]
    [Tooltip("可选，留空自动从物体上查找 Animator")]
    [SerializeField] private Animator animator;

    [Tooltip("行走状态 Bool 参数名")]
    [SerializeField] private string walkingBoolParameter = "isWalking";

    [Tooltip("可选，横向速度 Float 参数名（BlendTree 可用）")]
    [SerializeField] private string moveXFloatParameter = "";

    [Header("Runtime State")]
    [Tooltip("当前是否冻结输入与位移（对话、过场时设 true）")]
    [SerializeField] private bool isFrozen = false;

    private Rigidbody2D playerRigidBody;
    private SpriteRenderer spriteRenderer;

    // 当前水平输入，范围 [-1, 1]
    private float moveInput;

    // 当前是否正在移动
    private bool isWalking;

    // 是否朝右
    private bool isFacingRight = true;

    private int walkingParamHash = -1;
    private int moveXParamHash = -1;

    public bool IsFrozen => isFrozen;

    private void Awake()
    {
        // ========= 组件初始化 =========
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        playerRigidBody = GetComponent<Rigidbody2D>();

        // 强制设置连续检测，减少触发漏判
        if (playerRigidBody != null)
        {
            playerRigidBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerRigidBody.freezeRotation = true;
        }

        walkingParamHash = string.IsNullOrWhiteSpace(walkingBoolParameter)
            ? -1
            : Animator.StringToHash(walkingBoolParameter);

        moveXParamHash = string.IsNullOrWhiteSpace(moveXFloatParameter)
            ? -1
            : Animator.StringToHash(moveXFloatParameter);
    }

    private void Update()
    {
        // ========= 输入采集 =========
        // 锁定状态下不接收移动输入，保证对话期间不偏移
        if (isFrozen)
        {
            moveInput = 0f;
            isWalking = false;
            UpdateAnimatorState();
            return;
        }

        moveInput = Input.GetAxisRaw(horizontalAxis);

        // ========= 朝向翻转 =========
        if (moveInput > inputDeadZone)
        {
            isFacingRight = true;
            SetFlip(false);
        }
        else if (moveInput < -inputDeadZone)
        {
            isFacingRight = false;
            SetFlip(true);
        }

        isWalking = Mathf.Abs(moveInput) > inputDeadZone;
        UpdateAnimatorState();
    }

    private void FixedUpdate()
    {
        // ========= 物理位移 =========
        if (playerRigidBody == null)
        {
            return;
        }

        // 冻结时只保留竖直速度，让角色可被外力推/受重力影响（如有需求）
        if (isFrozen)
        {
            playerRigidBody.velocity = new Vector2(0f, playerRigidBody.velocity.y);
            return;
        }

        float targetVelocityX = moveInput * moveSpeed;
        float currentVelocityX = playerRigidBody.velocity.x;
        float smoothedVelocityX = Mathf.Lerp(currentVelocityX, targetVelocityX, moveSmoothing * Time.fixedDeltaTime);

        playerRigidBody.velocity = new Vector2(smoothedVelocityX, playerRigidBody.velocity.y);

        // ========= X 轴边界约束（防止跑出场景） =========
        if (useBoundary)
        {
            Vector2 pos = playerRigidBody.position;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            playerRigidBody.position = pos;
        }
    }

    /// <summary>
    /// 对外统一入口：对话、剧情、UI 阶段可调用该函数锁定角色移动。
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;

        if (playerRigidBody != null && isFrozen)
        {
            playerRigidBody.velocity = Vector2.zero;
        }
    }

    private void UpdateAnimatorState()
    {
        if (animator == null)
        {
            return;
        }

        // ========= Bool 动画参数 =========
        if (walkingParamHash != -1 && HasAnimatorParameter(walkingParamHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(walkingParamHash, isWalking);
        }

        // ========= Float 动画参数 =========
        if (moveXParamHash != -1 && HasAnimatorParameter(moveXParamHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(moveXParamHash, moveInput);
        }
    }

    private bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
    {
        // ========= 兼容旧版 Animator 参数检查 =========
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

    private void SetFlip(bool flipX)
    {
        // ========= 使用 SpriteRenderer 优先翻转，若不存在则通过缩放翻转 =========
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = flipX;
            return;
        }

        Vector3 scale = transform.localScale;
        scale.x = flipX ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        transform.localScale = scale;
    }
}
