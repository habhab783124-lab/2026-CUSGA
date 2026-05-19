using UnityEngine;

/// <summary>
/// 挂在 Chen 身上，用于在 Inspector 中直接配置第三章进场后的停止位置。
/// </summary>
public sealed class Chapter3ChenStopConfig : MonoBehaviour
{
    [Header("第三章停止位置")]
    [SerializeField] private Vector2 stopPosition = Vector2.zero;
    [SerializeField] private bool useCurrentPositionAsDefault = true;

    public Vector2 StopPosition => stopPosition;

    private void Reset()
    {
        if (useCurrentPositionAsDefault)
        {
            stopPosition = transform.position;
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && useCurrentPositionAsDefault && stopPosition == Vector2.zero)
        {
            stopPosition = transform.position;
        }
    }
}
