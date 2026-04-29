using TMPro;
using UnityEngine;

/// <summary>
/// World-space interaction prompt that follows a target Transform.
/// </summary>
public sealed class InteractionPromptView : MonoBehaviour
{
    [Header("Refs (bound in prefab)")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private TextMeshProUGUI promptText;

    [Header("Follow")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0.8f, 1.2f, 0f);

    [Header("Display")]
    [SerializeField] private bool showPromptString = false;

    private Transform follow;
    private bool useAnchorWorld;
    private Vector3 anchorWorld;

    public void SetFollow(Transform t)
    {
        follow = t;
        useAnchorWorld = false;
        if (enabled && follow != null)
        {
            UpdateFollow();
        }
    }

    public void SetAnchorWorldPosition(Vector3 worldPos)
    {
        useAnchorWorld = true;
        anchorWorld = worldPos;
        if (enabled)
        {
            UpdateFollow();
        }
    }

    public void SetWorldOffset(Vector3 offset)
    {
        worldOffset = offset;
        if (!enabled)
        {
            return;
        }

        // Only update immediately when we have something to follow.
        if (useAnchorWorld || follow != null)
        {
            UpdateFollow();
        }
    }

    public void SetText(string keyLabel, string prompt)
    {
        if (promptText == null)
        {
            return;
        }

        if (showPromptString && !string.IsNullOrWhiteSpace(prompt))
        {
            promptText.text = $"[{keyLabel}] {prompt}";
        }
        else
        {
            promptText.text = $"[{keyLabel}]";
        }
    }

    public void Show(bool visible)
    {
        if (rootCanvas != null)
        {
            rootCanvas.gameObject.SetActive(visible);
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    private void LateUpdate()
    {
        if (!useAnchorWorld && follow == null)
        {
            return;
        }

        UpdateFollow();
    }

    private void UpdateFollow()
    {
        if (!useAnchorWorld && follow == null)
        {
            return;
        }

        Vector3 basePos = useAnchorWorld ? anchorWorld : follow.position;
        transform.position = basePos + worldOffset;
        transform.rotation = Quaternion.identity;
    }
}

