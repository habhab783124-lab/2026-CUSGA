using UnityEngine;

/// <summary>
/// Attach this component to the root of a tower prefab to define where the
/// visual bottom of the tower sits.
///
/// Create an empty child GameObject, position it at the tower's visual bottom,
/// and drag it into the <see cref="anchorPoint"/> field.
///
/// During drag and placement the mouse / grid position aligns with this anchor
/// instead of the transform pivot.
///
/// If a tower prefab does not have this component the system falls back to
/// Tight-mesh sprite bounds or the CircleCollider2D offset.
/// </summary>
public sealed class TowerPlacementAnchor : MonoBehaviour
{
    [Tooltip("The child Transform that marks the visual bottom of this tower.")]
    [SerializeField] private Transform anchorPoint;

    public Transform AnchorPoint => anchorPoint;
}
