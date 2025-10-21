using UnityEngine;
using UnityEngine.InputSystem;

public class LookInteract : MonoBehaviour
{
    [Header("References")]
    public Transform cam;

    [Header("Settings")]
    public float interactDistance = 4f;
    public float interactRadius = 0.25f; // ietsje dikker, makkelijker mikken
    public LayerMask interactMask = ~0;
    public Key interactKey = Key.E;

    void Reset()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
    }

    void Update()
    {
        if (Keyboard.current == null || !cam) return;

        if (Keyboard.current[interactKey].wasPressedThisFrame)
        {
            // SphereCast zodat je niet pixel-perfect hoeft te mikken
            if (Physics.SphereCast(cam.position, interactRadius, cam.forward,
                out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Collide))
            {
                var pickup = hit.collider.GetComponentInParent<FlashlightPickup>();
                if (pickup != null)
                {
                    pickup.DoPickup();
                }
            }
        }
    }
}