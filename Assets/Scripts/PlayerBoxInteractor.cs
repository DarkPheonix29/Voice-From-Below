using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerBoxInteractor : MonoBehaviour
{
    [Header("References")]
    public Transform cam;                 // Main Camera
    public Transform carryPoint;          // Leeg object onder de camera

    [Header("Ray / interact settings")]
    public float interactDistance = 2.2f;     // kleiner bereik
    public float interactRadius   = 0.12f;    // smallere “hit bubble”
    public LayerMask interactMask = ~0;

    [Header("Carry placement (Small)")]
    public Vector3 smallLocalPos = new Vector3(0.35f, -0.25f, 0.7f);
    public Vector3 smallLocalEuler = new Vector3(0f, 90f, 0f);

    [Header("Place / drop behaviour")]
    public float placeDistance = 3.0f;        // ook iets korter, optioneel
    public float placeSkin = 0.02f;
    public float dropForwardDistance = 1.0f;
    public float dropPush = 1.0f;
    public float postDropCooldown = 0.25f;
    public int penetrationIters = 4;

    private BoxDrag currentDrag;
    private BoxDrag carried;
    private float pickupBlockUntil;

    void Reset()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
    }

    void Update()
    {
        if (!cam) return;

        // 1) Target zoeken met kleinere radius/afstand
        bool hitSomething = Physics.SphereCast(
            cam.position, interactRadius, cam.forward,
            out RaycastHit hit, interactDistance,
            interactMask, QueryTriggerInteraction.Ignore
        );
        BoxDrag target = hitSomething ? hit.collider.GetComponentInParent<BoxDrag>() : null;

        // 2) Slepen (RMB vasthouden) – alleen als we niets dragen, en NIET voor Small
        if (Mouse.current.rightButton.isPressed && carried == null && Time.time >= pickupBlockUntil)
        {
            if (target && target.size != BoxDrag.BoxSize.Small && currentDrag == null)
            {
                currentDrag = target;
                currentDrag.StartDrag(cam);
            }
        }
        else
        {
            if (currentDrag != null)
            {
                currentDrag.StopDrag();
                currentDrag = null;
            }
        }

        // 3) Oppakken / neerzetten (E)
        if (Keyboard.current[Key.E].wasPressedThisFrame)
        {
            if (carried != null)
            {
                Vector3? surfPoint = TryGetSurfacePoint();
                StartCoroutine(PlaceSafely(carried, surfPoint));
                carried = null;
                pickupBlockUntil = Time.time + postDropCooldown;
                return;
            }

            if (target && target.canBeCarried && target.size == BoxDrag.BoxSize.Small && Time.time >= pickupBlockUntil)
            {
                target.PickUp(carryPoint ? carryPoint : cam, smallLocalPos, smallLocalEuler);
                carried = target;

                if (currentDrag == carried)
                {
                    currentDrag.StopDrag();
                    currentDrag = null;
                }
            }
        }

        // 4) Scroll = rotate
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            if (carried != null)
            {
                carried.RotateOnScroll(scrollY, cam); // Small (carried): yaw om cam.up
            }
            else if (currentDrag != null)
            {
                currentDrag.RotateOnScroll(scrollY, cam); // Medium/Large: yaw om ground normal
            }
        }
    }

    Vector3? TryGetSurfacePoint()
    {
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, placeDistance, interactMask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return null;
    }

    IEnumerator PlaceSafely(BoxDrag box, Vector3? surfacePoint)
    {
        box.Drop();

        var col = box.GetComponent<Collider>();
        var rb  = box.GetComponent<Rigidbody>();
        if (!col || !rb) yield break;

#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic = true;

        Vector3 targetPos = surfacePoint.HasValue
            ? surfacePoint.Value + Vector3.up * 0.05f
            : cam.position + cam.forward * dropForwardDistance + Vector3.up * 0.1f;

        if (targetPos.y < 0f) targetPos.y = 0.5f;

        box.transform.position = targetPos;

        yield return new WaitForFixedUpdate();

        ResolvePenetration(col, penetrationIters);

        yield return new WaitForFixedUpdate();

        rb.isKinematic = false;
#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(cam.forward * dropPush, ForceMode.VelocityChange);
    }

    void ResolvePenetration(Collider placed, int maxIterations)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            var overlaps = Physics.OverlapBox(
                placed.bounds.center,
                placed.bounds.extents * 0.98f,
                placed.transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            bool moved = false;
            foreach (var other in overlaps)
            {
                if (other == placed) continue;
                if (other.attachedRigidbody == placed.attachedRigidbody) continue;

                if (Physics.ComputePenetration(
                    placed, placed.transform.position, placed.transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out Vector3 dir, out float dist))
                {
                    placed.transform.position += dir * (dist + Mathf.Max(0.001f, placeSkin));
                    moved = true;
                }
            }
            if (!moved) break;
        }
    }
}
