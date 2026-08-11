using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "Press E to interact.";
    [SerializeField] private bool interactable = true;

    [Header("Optional Dialogue")]
    [SerializeField] private bool useDialogue = false;
    [TextArea(3, 12)]
    [SerializeField] private List<string> dialogueLines = new List<string>();
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private float typingSpeed = 0.05f;
    [SerializeField] private bool hidePanelWhenFinish = true;

    [Header("Optional Unity Event")]
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    public string InteractionPrompt => interactionPrompt;
    public bool IsInteractable => interactable;

    public void Interact(PlayerController player)
    {
        if (!interactable)
        {
            return;
        }

        if (useDialogue && dialogueLines != null && dialogueLines.Count > 0)
        {
            var manager = DialogueManager.Instance;
            if (manager == null)
            {
                var autoManager = new GameObject("GlobalDialogueManager");
                manager = autoManager.AddComponent<DialogueManager>();
            }

            manager.ShowDialogue(
                dialogueLines,
                dialogueText,
                dialoguePanel,
                typingSpeed,
                true,
                hidePanelWhenFinish
            );
        }

        onInteract?.Invoke();
    }
}
