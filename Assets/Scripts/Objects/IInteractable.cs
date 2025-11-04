public interface IInteractable
{
    // Called when the player is looking at the object (for highlighting / showing prompts)
    void SetHighlighted(bool on);

    // Called when the player presses the Interact key
    void Interact();
}
