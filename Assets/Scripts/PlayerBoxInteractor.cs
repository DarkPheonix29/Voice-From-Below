using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBoxInteractor : MonoBehaviour
{
    [Header("References")]
    public Transform cam;                       // Main Camera

    [Header("Aim / Interact")]
    public LayerMask interactMask = ~0;         // welke layers zijn "raakbaar"
    public float interactDistance = 2.5f;       // hoe ver je nog een box kunt selecteren
    public float interactRadius = 0.12f;        // "hit bubble" rondom de ray

    [Header("Drag behaviour")]
    [Tooltip("Hoeveel muisbeweging nodig is (accum.) voordat slepen start na RMB.")]
    public float dragStartDelta = 6f;           // in Mouse.delta units (werkt ook met locked cursor)

    private BoxDrag currentDrag;                // actief gesleepte box
    private BoxDrag pendingDrag;                // "gewapend", wacht op muisdrempel
    private Vector2 pendingAccumDelta;          // opgetelde mouse delta sinds arming
    private RaycastHit pendingHit;              // (niet gebruikt door BoxDrag-clean, maar houden we paraat)

    void Reset()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
    }

    void Update()
    {
        if (!cam) return;

        // --------- 1) Aim: zoek potentiële box ----------
        BoxDrag aimTarget = TryFindBoxUnderCrosshair(out RaycastHit aimHit);

        // --------- 2) RMB in -> arming ----------
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (aimTarget != null && aimTarget.enabled)
            {
                pendingDrag = aimTarget;
                pendingHit = aimHit;
                pendingAccumDelta = Vector2.zero;      // reset drempelteller
            }
            else
            {
                // niets geraakt -> veilige reset
                pendingDrag = null;
                pendingAccumDelta = Vector2.zero;
            }
        }

        // --------- 3) RMB vast -> drempel checken / starten ----------
        if (Mouse.current.rightButton.isPressed)
        {
            if (pendingDrag != null && currentDrag == null)
            {
                pendingAccumDelta += Mouse.current.delta.ReadValue();
                if (pendingAccumDelta.magnitude >= dragStartDelta)
                {
                    // start drag op de "gewapende" box
                    currentDrag = pendingDrag;
                    pendingDrag = null;
                    currentDrag.StartDrag(cam);        // clean BoxDrag gebruikt alleen de camera
                }
            }
        }

        // --------- 4) RMB los -> stoppen / annuleren ----------
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            if (currentDrag != null)
            {
                currentDrag.StopDrag();
                currentDrag = null;
            }
            pendingDrag = null;
            pendingAccumDelta = Vector2.zero;
        }

        // --------- 5) Scroll = rotate ----------
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) > 0.01f && currentDrag != null)
        {
            currentDrag.RotateOnScroll(scrollY, cam);
        }
    }

    // Snel en robuust: SphereCast voor “vriendelijke” selectie van BoxDrag
    private BoxDrag TryFindBoxUnderCrosshair(out RaycastHit hit)
    {
        Vector3 ro = cam.position;
        Vector3 rd = cam.forward;

        bool gotHit = Physics.SphereCast(
            ro, interactRadius, rd,
            out hit, interactDistance,
            interactMask, QueryTriggerInteraction.Ignore
        );
        if (!gotHit) return null;

        // Zoek component op collider of er boven
        var bd = hit.collider.GetComponentInParent<BoxDrag>();
        return bd;
    }
}
