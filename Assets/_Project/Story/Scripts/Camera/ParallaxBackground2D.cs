using UnityEngine;

/// <summary>
/// Simple 2D parallax background. Move this object based on camera movement.
/// Attach to a background (SpriteRenderer) GameObject.
/// </summary>
[DisallowMultipleComponent]
public sealed class ParallaxBackground2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Parallax (0 = fixed, 1 = same as camera)")]
    [SerializeField] [Range(-1f, 2f)] private float parallaxX = 0.4f;
    [SerializeField] [Range(-1f, 2f)] private float parallaxY = 0f;

    [Header("Options")]
    [Tooltip("Keeps initial Z (useful for 2D sorting by Z).")]
    [SerializeField] private bool keepZ = true;

    private Vector3 startPos;
    private Vector3 camStartPos;
    private float startZ;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        startPos = transform.position;
        startZ = startPos.z;
        camStartPos = cameraTransform != null ? cameraTransform.position : Vector3.zero;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 camDelta = cameraTransform.position - camStartPos;
        Vector3 desired = startPos + new Vector3(camDelta.x * parallaxX, camDelta.y * parallaxY, 0f);
        if (keepZ)
        {
            desired.z = startZ;
        }

        transform.position = desired;
    }
}

