public interface IInteractable
{
    string InteractionPrompt { get; }
    bool IsInteractable { get; }
    void Interact(PlayerController player);
}
