using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class BoxDrag : MonoBehaviour
{
    public enum BoxSize { Small, Medium, Large }

    [Header("General Settings")]
    public BoxSize size = BoxSize.Medium;
    public bool canBeCarried = false;

    [Header("Carry Settings (Small Only)")]
    public float holdDistance = 2f;

    [Header("Drag Limits")]
    public float breakDistance = 9f;
    public float maxPlayerDragRadius = 3f;
    public float minPlayerDragRadius = 0f;

    [Header("Ground Drag Settings")]
    public LayerMask groundMask = ~0;
    [Range(0f, 89f)] public float maxSlopeAngle = 45f;
    public float groundRayDistance = 12f;
    public float kpGround = 12f;
    public float kdGround = 6.9f;
    public float maxPlanarAcceleration = 25f;
    public float maxDragSpeedGround = 4.5f;
    public float groundStickForce = 24f;
    public float groundSnapDistance = 0.25f;

    [Header("Upright Stabilization")]
    public bool keepUpright = true;
    public bool hardUprightWhileDrag = false;
    public float maxTiltDeg = 8f;
    public float uprightKp = 120f;
    public float uprightKd = 18f;
    public float maxAngularSpeedDeg = 180f;
    public float comLowerWhileDrag = 0.1f;

    [Header("Friction & Damping")]
    public PhysicsMaterial lowFrictionMaterial;
    public float linearDragWhileDrag = 4f;
    public float angularDragWhileDrag = 2f;

    [Header("Scroll Rotate")]
    public float scrollRotateDegreesPerNotch = 8f;
    public bool invertScroll = false;

    Rigidbody rb;
    Collider col;
    Transform grabber;
    bool dragging;
    bool carried;

    Vector3 groundNormal = Vector3.up;
    Vector3 lastGroundNormal = Vector3.up;
    int keepStillTicks;

    PhysicsMaterial originalMaterial;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        if (col) originalMaterial = col.sharedMaterial;
    }

    void FixedUpdate()
    {
        if (dragging && grabber && size != BoxSize.Small)
            GroundDragStep();
    }

    // -------------------------------------------------------------
    // Dragging
    // -------------------------------------------------------------
    public void StartDrag(Transform grabTransform)
    {
        if (size == BoxSize.Small || carried) return;

        grabber = grabTransform;
        dragging = true;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearDamping = linearDragWhileDrag;
        rb.angularDamping = angularDragWhileDrag;

        if (!lowFrictionMaterial)
        {
            lowFrictionMaterial = new PhysicsMaterial("LowFriction")
            {
                dynamicFriction = 0.02f,
                staticFriction = 0.02f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
        }
        if (col) col.sharedMaterial = lowFrictionMaterial;

        rb.centerOfMass -= new Vector3(0, comLowerWhileDrag, 0);
        if (hardUprightWhileDrag)
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        keepStillTicks = 1;
    }

    public void StopDrag()
    {
        dragging = false;
        grabber = null;

        if (col) col.sharedMaterial = originalMaterial;
        if (hardUprightWhileDrag)
            rb.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
    }

    // -------------------------------------------------------------
    // Ground Drag Logic
    // -------------------------------------------------------------
    void GroundDragStep()
    {
        if (!grabber) return;

        Vector3 camPos = grabber.position;
        Vector3 camFwd = grabber.forward;

        // 1) Vind grondpunt onder de crosshair (negeer eigen collider)
        Vector3 targetPos;
        if (RayToGroundIgnoringSelf(camPos, camFwd, col, out Vector3 hitPoint, out Vector3 hitNormal))
        {
            targetPos = hitPoint;
            groundNormal = hitNormal;
        }
        else
        {
            // Fallback: op vlak voor speler
            Vector3 flatFwd = Vector3.ProjectOnPlane(camFwd, Vector3.up).normalized;
            targetPos = camPos + flatFwd * 2f;
            groundNormal = Vector3.up;
        }

        // 2) Clamp sleepradius rond speler
        Vector3 playerGround = GetPlayerGroundPoint();
        Vector3 fromPlayerPlanar = Vector3.ProjectOnPlane(targetPos - playerGround, groundNormal);
        float dist = fromPlayerPlanar.magnitude;
        if (dist > maxPlayerDragRadius)
            targetPos = playerGround + fromPlayerPlanar.normalized * maxPlayerDragRadius;
        else if (dist < minPlayerDragRadius)
            targetPos = playerGround + fromPlayerPlanar.normalized * minPlayerDragRadius;

        // 3) PD-kracht in vlak
#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        Vector3 vel = rb.linearVelocity;
#else
        Vector3 vel = rb.velocity;
#endif
        Vector3 planarErr = Vector3.ProjectOnPlane(targetPos - transform.position, groundNormal);
        Vector3 planarVel = Vector3.ProjectOnPlane(vel, groundNormal);
        Vector3 desiredVel = Vector3.ClampMagnitude(planarErr * kpGround, maxDragSpeedGround);
        Vector3 accel = (desiredVel - planarVel) * kdGround;
        accel = Vector3.ClampMagnitude(accel, maxPlanarAcceleration);

        if (keepStillTicks > 0) { keepStillTicks--; return; }
        rb.AddForce(accel, ForceMode.Acceleration);

        // 4) Verticale correctie + "plak" aan grond
        Vector3 normalVel = Vector3.Dot(vel, groundNormal) * groundNormal;
        rb.AddForce(-normalVel / Mathf.Max(Time.fixedDeltaTime, 0.01f), ForceMode.Acceleration);
        rb.AddForce(-groundNormal * groundStickForce, ForceMode.Acceleration);

        if (keepUpright) ApplyUprightTorque();
        LimitAngularSpeed();
    }

    // -------------------------------------------------------------
    // Ground & Helpers
    // -------------------------------------------------------------
    bool RayToGroundIgnoringSelf(Vector3 origin, Vector3 dir, Collider self, out Vector3 point, out Vector3 normal)
    {
        origin += dir.normalized * 0.3f;
        var hits = Physics.RaycastAll(origin, dir, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == self || h.collider.transform.IsChildOf(self.transform)) continue;
            if (Vector3.Angle(h.normal, Vector3.up) <= maxSlopeAngle)
            {
                point = h.point;
                normal = h.normal;
                return true;
            }
        }
        point = origin + dir * 2f;
        normal = Vector3.up;
        return false;
    }

    Vector3 GetPlayerGroundPoint()
    {
        if (Physics.Raycast(grabber.position + Vector3.up, Vector3.down, out RaycastHit hit, 5f, groundMask))
            return hit.point;
        return grabber.position;
    }

    void ApplyUprightTorque()
    {
        Vector3 up = transform.up;
        Vector3 axis = Vector3.Cross(up, groundNormal);
        if (axis.sqrMagnitude < 1e-6f) return;

        axis.Normalize();
        float angle = Mathf.Asin(Mathf.Clamp(axis.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 corrective = axis * (uprightKp * Mathf.Deg2Rad * angle);
        Vector3 damping = -uprightKd * Vector3.Project(rb.angularVelocity, axis);

        rb.AddTorque(corrective + damping, ForceMode.Acceleration);
    }

    void LimitAngularSpeed()
    {
        float maxRad = maxAngularSpeedDeg * Mathf.Deg2Rad;
        if (rb.angularVelocity.magnitude > maxRad)
            rb.angularVelocity = rb.angularVelocity.normalized * maxRad;
    }

    // -------------------------------------------------------------
    // Scroll-rotate
    // -------------------------------------------------------------
    public void RotateOnScroll(float scrollY, Transform cam)
    {
        if (Mathf.Abs(scrollY) < 0.001f) return;
        float dir = invertScroll ? -1f : 1f;
        float degrees = dir * scrollRotateDegreesPerNotch * scrollY;

        if (size == BoxSize.Small && carried)
        {
            transform.rotation = Quaternion.AngleAxis(degrees, cam.up) * transform.rotation;
        }
        else if (size != BoxSize.Small && dragging)
        {
            Quaternion q = Quaternion.AngleAxis(degrees, groundNormal);
            rb.MoveRotation(q * rb.rotation);
        }
    }
}
