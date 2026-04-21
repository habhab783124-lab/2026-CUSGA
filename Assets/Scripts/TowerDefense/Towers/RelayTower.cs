using UnityEngine;

/// <summary>
/// Unity scene component entry for relay nodes.
/// This file intentionally keeps the script asset identity stable for Scene references.
/// The actual phase-two runtime behavior lives in the partial companion file.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(PlacedTower))]
public partial class RelayTower : MonoBehaviour
{
}
