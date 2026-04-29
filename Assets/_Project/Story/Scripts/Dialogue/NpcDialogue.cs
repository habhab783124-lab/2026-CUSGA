using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class NpcDialogue : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string prompt = "Press E";
    [SerializeField] private bool canInteract = true;

    [Header("Dialogue")]
    [SerializeField] private Transform npcBubbleAnchor;
    [SerializeField] private Transform playerBubbleAnchor;

    [Tooltip("Dialogue script id resolved from DialogueScripts.Get(id).")]
    [SerializeField] private string dialogueId = "Demo_Ling_To_Command";

    [Tooltip("Legacy: kept for compatibility; prefer dialogueId in scripts.")]
    [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();

    public string Prompt => prompt;
    public bool CanInteract => canInteract;

    private void Reset()
    {
        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        npcBubbleAnchor = transform;
    }

    public void Interact(PlayerInteractor2D interactor)
    {
        if (!canInteract || interactor == null)
        {
            return;
        }

        DialogueRunner runner = interactor.DialogueRunner;
        if (runner == null || runner.IsPlaying)
        {
            return;
        }

        Transform resolvedNpcAnchor = npcBubbleAnchor != null ? npcBubbleAnchor : transform;
        Transform resolvedPlayerAnchor = playerBubbleAnchor != null
            ? playerBubbleAnchor
            : interactor.transform;

        IReadOnlyList<DialogueLine> resolved = !string.IsNullOrWhiteSpace(dialogueId) ? DialogueScripts.Get(dialogueId) : null;
        IList<DialogueLine> toPlay = resolved != null && resolved.Count > 0 ? (IList<DialogueLine>)resolved : lines;
        runner.PlayConversation(interactor, resolvedPlayerAnchor, resolvedNpcAnchor, toPlay, OnDialogueEnded);
    }

    private void OnDialogueEnded()
    {
        canInteract = false;
    }
}

