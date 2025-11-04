using UnityEngine;
using TMPro;

[RequireComponent(typeof(Collider))]
public class PhoneInteractSimple : MonoBehaviour
{
    [Header("Links")]
    public ActOneDirector director;        // drag your ActOneDirector here
    public Canvas promptCanvas;            // world-space canvas on/above the phone
    public TextMeshProUGUI promptLabel;    // TMP text inside the canvas
    [TextArea] public string promptText = "Press E to Answer";
    public bool oneShot = true;

    private bool answered = false;

    void Reset()
    {
        // Ensure collider is not a trigger so raycasts hit it
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;
    }

    void Start()
    {
        if (promptLabel) promptLabel.text = promptText;
        if (promptCanvas) promptCanvas.enabled = false; // hidden until highlighted
    }

    // Called by PlayerInteractor when the ray is over the phone
    public void SetHighlighted(bool on)
    {
        if (answered) on = false; // don't show after it’s been answered
        if (promptCanvas) promptCanvas.enabled = on;
    }

    // Called by PlayerInteractor when the player presses Interact
    public void Interact()
    {
        if (answered) return;

        if (director) director.TriggerCall();
        if (oneShot) answered = true;

        if (promptCanvas) promptCanvas.enabled = false;
    }
}
