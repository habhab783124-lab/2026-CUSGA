using UnityEngine;

/// <summary>
/// Unity scene component entry for relay nodes.
/// This file intentionally keeps the script asset identity stable for Scene references.
/// The actual phase-two runtime behavior lives in the partial companion file.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public partial class RelayTower : MonoBehaviour
{
}

/// <summary>
/// Centralizes the "towers must stay on top" rule for this prototype.
///
/// We keep the rule in one helper instead of scattering magic numbers across
/// multiple tower scripts, because the project now has four final tower types
/// and they all need to obey the same visual contract:
/// - placed towers stay above normal battlefield content
/// - preview towers stay slightly above placed towers
/// - child markers and feedback keep their local offsets instead of collapsing
///   onto one flat order
/// </summary>
public static class TowerRenderSorting
{
    public const string TopmostTowerSortingLayerName = "Default";
    public const int PlacedTowerBaseSortingOrder = 200;
    public const int PlacementPreviewBaseSortingOrder = 220;
    public const int PlacementPreviewRingRelativeSortingOffset = -1;

    /// <summary>
    /// Rebases every sprite under one placed tower so the full tower hierarchy
    /// moves upward together while preserving the prefab's local ordering.
    /// </summary>
    public static void ApplyPlacedTowerTopmostSorting(Transform towerRoot, SpriteRenderer primaryRenderer)
    {
        ApplyHierarchyTopmostSorting(towerRoot, primaryRenderer, PlacedTowerBaseSortingOrder);
    }

    /// <summary>
    /// Placement previews need the same treatment, but they sit one layer
    /// higher than placed towers so the drag ghost stays easy to read.
    /// </summary>
    public static void ApplyPlacementPreviewTopmostSorting(Transform previewRoot, SpriteRenderer primaryRenderer)
    {
        ApplyHierarchyTopmostSorting(previewRoot, primaryRenderer, PlacementPreviewBaseSortingOrder);
    }

    /// <summary>
    /// Applies a stable relative offset for a child renderer that belongs to a
    /// placed tower, such as a type signature or level marker.
    /// </summary>
    public static void ApplyPlacedTowerAdornmentSorting(SpriteRenderer renderer, SpriteRenderer primaryRenderer, int relativeOffset)
    {
        ApplyRendererSorting(renderer, GetReferenceSortingOrder(primaryRenderer, PlacedTowerBaseSortingOrder) + relativeOffset);
    }

    /// <summary>
    /// Applies a stable relative offset for a preview-only child renderer, such
    /// as the placement ring.
    /// </summary>
    public static void ApplyPlacementPreviewAdornmentSorting(SpriteRenderer renderer, SpriteRenderer primaryRenderer, int relativeOffset)
    {
        ApplyRendererSorting(renderer, GetReferenceSortingOrder(primaryRenderer, PlacementPreviewBaseSortingOrder) + relativeOffset);
    }

    /// <summary>
    /// One-off runtime feedback objects are not part of the author-authored
    /// sprite stack, so they opt into tower-topmost sorting explicitly.
    /// </summary>
    public static void ApplyPlacedTowerEffectSorting(SpriteRenderer renderer, SpriteRenderer primaryRenderer, int relativeOffset)
    {
        ApplyRendererSorting(renderer, GetReferenceSortingOrder(primaryRenderer, PlacedTowerBaseSortingOrder) + relativeOffset);
    }

    /// <summary>
    /// Prefab-based feedback can contain multiple renderers.
    /// We preserve the prefab's internal layering, then move the whole effect
    /// above the tower root by the requested offset.
    /// </summary>
    public static void ApplyPlacedTowerEffectSorting(SpriteRenderer[] renderers, SpriteRenderer primaryRenderer, int relativeOffset)
    {
        ApplyRendererSetSorting(
            renderers,
            GetReferenceSortingOrder(primaryRenderer, PlacedTowerBaseSortingOrder) + relativeOffset);
    }

    private static void ApplyHierarchyTopmostSorting(Transform root, SpriteRenderer primaryRenderer, int desiredBaseOrder)
    {
        if (root == null)
        {
            return;
        }

        SpriteRenderer referenceRenderer = primaryRenderer != null
            ? primaryRenderer
            : root.GetComponentInChildren<SpriteRenderer>(true);
        if (referenceRenderer == null)
        {
            return;
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        int referenceOriginalOrder = referenceRenderer.sortingOrder;

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];
            if (renderer == null)
            {
                continue;
            }

            int relativeOffset = renderer.sortingOrder - referenceOriginalOrder;
            ApplyRendererSorting(renderer, desiredBaseOrder + relativeOffset);
        }
    }

    private static void ApplyRendererSetSorting(SpriteRenderer[] renderers, int desiredBaseOrder)
    {
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        SpriteRenderer referenceRenderer = null;
        for (int index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] != null)
            {
                referenceRenderer = renderers[index];
                break;
            }
        }

        if (referenceRenderer == null)
        {
            return;
        }

        int referenceOriginalOrder = referenceRenderer.sortingOrder;
        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];
            if (renderer == null)
            {
                continue;
            }

            int relativeOffset = renderer.sortingOrder - referenceOriginalOrder;
            ApplyRendererSorting(renderer, desiredBaseOrder + relativeOffset);
        }
    }

    private static void ApplyRendererSorting(SpriteRenderer renderer, int sortingOrder)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sortingLayerName = TopmostTowerSortingLayerName;
        renderer.sortingOrder = sortingOrder;
    }

    private static int GetReferenceSortingOrder(SpriteRenderer primaryRenderer, int fallbackOrder)
    {
        return primaryRenderer != null ? primaryRenderer.sortingOrder : fallbackOrder;
    }
}
