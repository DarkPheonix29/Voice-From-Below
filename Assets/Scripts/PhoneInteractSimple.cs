using UnityEngine;
using TMPro;

public class PhoneInteractSimple : MonoBehaviour
{
    [Header("Links")]
    public ActOneDirector director;               // drag your ActOneDirector here
    public Transform player;                      // leave empty to auto-use Main Camera
    public Canvas promptCanvas;                   // the world-space canvas on the phone
    public TextMeshProUGUI promptLabel;           // the TMP text on that canvas

    [Header("Settings")]
    public float interactDistance = 2.0f;
    public KeyCode interactKey = KeyCode.E;
    public string promptText = "Press E to Answer";
    public bool oneShot = true;

    bool answered = false;

    void Start()
    {
        if (!player && Camera.main) player = Camera.main.transform;
        if (promptCanvas) promptCanvas.enabled = false;
        if (promptLabel) promptLabel.text = promptText;
    }

    void Update()
    {
        if (answered) return;

        // late-assign camera if scene started without one
        if (!player && Camera.main) player = Camera.main.transform;
        if (!player) return;

        float d = Vector3.Distance(player.position, transform.position);
        bool canInteract = d <= interactDistance;

        if (promptCanvas) promptCanvas.enabled = canInteract;

        if (canInteract && Input.GetKeyDown(interactKey))
        {
            if (director) director.TriggerCall();
            answered = oneShot;
            if (promptCanvas) promptCanvas.enabled = false;
        }
    }
}

