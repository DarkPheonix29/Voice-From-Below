// Interactable.cs
using UnityEngine;
using UnityEngine.Events;
using TMPro;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [Header("Prompt (optional)")]
    public Canvas promptCanvas;                 // world-space canvas near/above the object
    public TextMeshProUGUI promptLabel;         // TMP text inside the canvas
    [TextArea] public string promptText = "Press E to interact";

    [Header("Behavior")]
    public bool oneShot = false;                // disable after first use
    public UnityEvent onInteract;               // wire actions here in Inspector

    bool _enabled = true;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;         // make sure raycasts hit
    }

    void Start()
    {
        if (promptLabel) promptLabel.text = promptText;
        if (promptCanvas) promptCanvas.enabled = false;
    }

    public virtual void SetHighlighted(bool on)
    {
        if (!_enabled) on = false;
        if (promptCanvas) promptCanvas.enabled = on;
    }

    public virtual void Interact()
    {
        if (!_enabled) return;
        Debug.Log($"[Interactable] Interact on {name}");
        onInteract?.Invoke();
        if (oneShot) _enabled = false;
        if (promptCanvas) promptCanvas.enabled = false;
    }

    // Add this to your existing Interactable.cs
    public void SetPromptText(string text)
    {
        promptText = text;
        if (promptLabel) promptLabel.text = text;
    }
}
