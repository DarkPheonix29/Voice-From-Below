using UnityEngine;

[RequireComponent(typeof(BoxDrag))]
public class BoxDragInteractable : Interactable
{
    private BoxDrag box;
    private Transform playerCam;
    private bool dragging;

    void Awake()
    {
        box = GetComponent<BoxDrag>();
        playerCam = Camera.main.transform; // adjust if your camera moves dynamically
    }

    public override void Interact()
    {
        if (!box) return;

        if (!dragging)
        {
            // Begin dragging
            box.StartDrag(playerCam);
            dragging = true;
        }
        else
        {
            // Stop dragging
            box.StopDrag();
            dragging = false;
        }
    }

    public override void SetHighlighted(bool on)
    {
        base.SetHighlighted(on); // keeps prompt canvas logic
    }
}
