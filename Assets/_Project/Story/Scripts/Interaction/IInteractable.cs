public interface IInteractable
{
    string Prompt { get; }
    bool CanInteract { get; }
    void Interact(PlayerInteractor2D interactor);
}

