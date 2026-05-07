using UnityEngine;

/// <summary>
/// Simple 2D camera follow: keeps target at camera center.
/// Attach to Main Camera.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Offsets")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Header("Axes")]
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;

    [Header("Framing (Orthographic)")]
    [Tooltip("If enabled and camera is Orthographic, keeps target at the desired viewport Y.\n0=bottom, 0.5=center, 1=top.")]
    [SerializeField] private bool useViewportYFraming = true;
    [SerializeField] [Range(0.05f, 0.95f)] private float targetViewportY = 0.3333333f;

    [Header("Smoothing (optional)")]
    [SerializeField] private bool smooth = false;
    [SerializeField] [Min(0.001f)] private float smoothTime = 0.08f;

    [Header("Level bounds (optional)")]
    [Tooltip("Clamp camera so the view stays inside the background. Uses world Bounds of the Renderer (e.g. SpriteRenderer on your backdrop).")]
    [SerializeField] private bool clampCameraToBackground = false;
    [SerializeField] private Renderer backgroundForBounds;
    [SerializeField] private bool clampHorizontal = true;
    [SerializeField] private bool clampVertical = false;

    private Vector3 velocity;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (target == null)
        {
            // Common fallbacks: tagged Player, or any PlayerMotor2D in scene.
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                target = go.transform;
            }
            else
            {
                var motor = FindObjectOfType<PlayerMotor2D>(includeInactive: true);
                if (motor != null)
                {
                    target = motor.transform;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 p = transform.position;
        Vector3 t = target.position;

        float x = followX ? t.x + offset.x : p.x;
        float y = followY ? t.y + offset.y : p.y;

        // Requirement: player centered horizontally, but at lower third vertically.
        // For orthographic camera: camY = targetY + orthoSize * (0.5 - viewportY) * 2
        // Simplifies to camY = targetY + orthoSize * (1 - 2*viewportY)
        if (followY && useViewportYFraming && cam != null && cam.orthographic)
        {
            float os = Mathf.Max(0.0001f, cam.orthographicSize);
            float framingOffsetY = os * (1f - 2f * targetViewportY);
            y = t.y + framingOffsetY + offset.y;
        }

        Vector3 desired = new Vector3(x, y, p.z);
        desired = ClampToBackground(desired);

        if (!smooth)
        {
            transform.position = desired;
            return;
        }

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        transform.position = ClampToBackground(transform.position);
    }

    private Vector3 ClampToBackground(Vector3 worldPos)
    {
        if (!clampCameraToBackground || backgroundForBounds == null || cam == null || !cam.orthographic)
        {
            return worldPos;
        }

        Bounds b = backgroundForBounds.bounds;
        float halfH = Mathf.Max(0.0001f, cam.orthographicSize);
        float halfW = halfH * Mathf.Max(0.0001f, cam.aspect);

        float cx = worldPos.x;
        float cy = worldPos.y;

        if (clampHorizontal)
        {
            float minCamX = b.min.x + halfW;
            float maxCamX = b.max.x - halfW;
            if (minCamX > maxCamX)
            {
                cx = (b.min.x + b.max.x) * 0.5f;
            }
            else
            {
                cx = Mathf.Clamp(cx, minCamX, maxCamX);
            }
        }

        if (clampVertical)
        {
            float minCamY = b.min.y + halfH;
            float maxCamY = b.max.y - halfH;
            if (minCamY > maxCamY)
            {
                cy = (b.min.y + b.max.y) * 0.5f;
            }
            else
            {
                cy = Mathf.Clamp(cy, minCamY, maxCamY);
            }
        }

        worldPos.x = cx;
        worldPos.y = cy;
        return worldPos;
    }
}

