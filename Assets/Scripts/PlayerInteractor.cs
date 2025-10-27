// PlayerInteractor.cs
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInteractor : MonoBehaviour
{
    [Header("View")]
    public Camera cam;

    [Header("Settings")]
    public float interactDistance = 3f;
    public float interactRadius = 0.2f;         // small sphere to make aiming friendlier
    public LayerMask interactMask = ~0;

#if ENABLE_INPUT_SYSTEM
    public InputAction interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
    void OnEnable(){ interactAction.Enable(); }
    void OnDisable(){ interactAction.Disable(); }
#else
    public KeyCode interactKey = KeyCode.E;
#endif

    Interactable current;

    void Start()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (!cam) return;

        bool hitSomething = Physics.SphereCast(
            cam.transform.position,
            interactRadius,
            cam.transform.forward,
            out RaycastHit hit,
            interactDistance,
            interactMask,
            QueryTriggerInteraction.Ignore
        );

        Interactable target = hitSomething ? hit.collider.GetComponentInParent<Interactable>() : null;

        if (current != target)
        {
            current?.SetHighlighted(false);
            current = target;
            current?.SetHighlighted(true);
        }

#if ENABLE_INPUT_SYSTEM
        bool pressed = interactAction.triggered;
#else
        bool pressed = Input.GetKeyDown(interactKey);
#endif
        if (pressed && current != null)
            current.Interact();
    }
}
