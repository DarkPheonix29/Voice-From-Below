using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInteractor : MonoBehaviour
{
    public Camera cam;
    public float interactDistance = 3f;
    public LayerMask interactMask = ~0;

#if ENABLE_INPUT_SYSTEM
    public InputAction interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
    void OnEnable(){ interactAction.Enable(); }
    void OnDisable(){ interactAction.Disable(); }
#else
    public KeyCode interactKey = KeyCode.E;
#endif

    private PhoneInteractSimple current;

    void Start(){ if (!cam) cam = Camera.main; }

    void Update()
    {
        if (!cam) return;

        Ray r = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(r, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            var pi = hit.collider.GetComponentInParent<PhoneInteractSimple>();
            if (pi != null)
            {
                if (current != pi)
                {
                    current?.SetHighlighted(false);
                    current = pi;
                    current.SetHighlighted(true);
                }

#if ENABLE_INPUT_SYSTEM
                if (interactAction.triggered) current.Interact();
#else
                if (Input.GetKeyDown(interactKey)) current.Interact();
#endif
                return;
            }
        }

        if (current != null) { current.SetHighlighted(false); current = null; }
    }
}
