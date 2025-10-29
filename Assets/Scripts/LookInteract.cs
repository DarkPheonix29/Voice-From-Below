using UnityEngine;
using UnityEngine.InputSystem;

public class LookInteract : MonoBehaviour
{
    [Header("References")]
    public Transform cam;

    [Header("Settings")]
    public float interactDistance = 4f;
    public float interactRadius = 0.25f;
    public LayerMask interactMask = ~0;
    public Key interactKey = Key.E;

    // keep track of the currently highlighted phone so we can unhighlight when we look away
    private PhoneInteractSimple currentPhone;

    void Reset()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
    }

    void Update()
    {
        if (Keyboard.current == null || !cam) return;

        // 1) SphereCast to find what we’re looking at
        bool hitSomething = Physics.SphereCast(
            cam.position,
            interactRadius,
            cam.forward,
            out RaycastHit hit,
            interactDistance,
            interactMask,
            QueryTriggerInteraction.Collide
        );

        // 2) Handle highlight for phone prompts
        PhoneInteractSimple phone = null;
        if (hitSomething)
        {
            // find either on the collider or a parent
            phone = hit.collider.GetComponentInParent<PhoneInteractSimple>();
        }

        // turn off highlight if we were highlighting a different phone
        if (currentPhone && currentPhone != phone)
            currentPhone.SetHighlighted(false);

        // turn on highlight for the phone we’re looking at
        if (phone)
        {
            phone.SetHighlighted(true);
            currentPhone = phone;
        }
        else
        {
            currentPhone = null;
        }

        // 3) On key press, interact with what we hit
        if (Keyboard.current[interactKey].wasPressedThisFrame && hitSomething)
        {
            // flashlight?
            var flashlightPickup = hit.collider.GetComponentInParent<FlashlightPickup>();
            if (flashlightPickup != null)
            {
                flashlightPickup.DoPickup();
                return;
            }

            // walkie talkie?
            var walkiePickup = hit.collider.GetComponentInParent<WalkieTalkiePickup>();
            if (walkiePickup != null)
            {
                walkiePickup.DoPickup();
                return;
            }

            // phone?
            if (phone != null)
            {
                phone.Interact();
                return;
            }

            // add other interactables here as needed
        }
    }
}
