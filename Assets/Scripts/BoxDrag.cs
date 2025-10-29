using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class BoxDrag : MonoBehaviour
{
    public enum BoxSize { Small, Medium, Large }

    [Header("Settings")]
    public BoxSize size = BoxSize.Medium;

    [Tooltip("Enable carry (attach to camera). ON only for Small.")]
    public bool canBeCarried = false; // via presets

    // -------- Small (carry-only) --------
    [Tooltip("Carry offset distance for Small (compat value).")]
    public float holdDistance = 2f;

    // -------- Smoothing & safety --------
    [Header("Smoothing & Safety")]
    [Tooltip("Default smoothingtime for target following AFTER the instant-lock fase.")]
    public float targetSmoothTime = 0.08f;      // korter dan eerst voor minder lag
    [Tooltip("If planar distance to target > this, stop drag.")]
    public float breakDistance = 9f;
    public float linearDragWhileDrag = 4f;
    public float angularDragWhileDrag = 2f;

    // -------- Ground drag (Medium/Large) --------
    [Header("Ground Drag (Medium/Large)")]
    public LayerMask groundMask = ~0;
    [Range(0f, 89f)] public float maxSlopeAngle = 45f;
    public float groundRayDistance = 30f;

    [Tooltip("PD gain (planar).")] public float kpGround = 12f;
    [Tooltip("PD damping (~2*sqrt(kpGround)).")] public float kdGround = 6.9f;
    [Tooltip("Max Δv per FixedUpdate (planar).")] public float maxDeltaVPerStepGround = 1.3f;
    [Tooltip("Max drag speed over ground.")] public float maxDragSpeedGround = 4.5f;

    [Tooltip("Extra stick-to-ground acceleration.")] public float groundStickForce = 30f;
    [Tooltip("If within this distance to ground, add extra down-force.")] public float groundSnapDistance = 0.3f;

    // -------- Anti-tilt & sliding --------
    [Header("Anti-tilt & Sliding (Medium/Large)")]
    public bool keepUpright = true;
    public bool hardUprightWhileDrag = false;
    public float maxTiltDeg = 8f;
    public float uprightKp = 120f;
    public float uprightKd = 18f;
    public float maxAngularSpeedDeg = 180f;
    public PhysicsMaterial lowFrictionMaterial;
    public float comLowerWhileDrag = 0.1f;

    // -------- Scroll-rotate --------
    [Header("Scroll Rotate")]
    public float scrollRotateDegreesPerNotch = 8f;
    public bool invertScroll = false;

    // -------- Low-latency instellingen --------
    [Header("Low-Latency")]
    [Tooltip("Schakel smoothing tijdelijk uit aan het begin van slepen.")]
    public bool instantLockOnStart = true;
    [Tooltip("Duur (s) van instant lock (geen smoothing).")]
    public float instantLockDuration = 0.08f;
    [Tooltip("Extra Δv multiplier tijdens de eerste frames voor soepele start.")]
    public float startBoostDvMultiplier = 1.6f;

    [Header("Presets per grootte (optioneel)")]
    public bool autoTuneBySize = true;
    [SerializeField, HideInInspector] private BoxSize _lastTunedSize;

    // --- runtime ---
    Rigidbody rb;
    Collider col;
    Transform grabber;
    bool dragging;
    bool carried;

    Vector3 originalLocalScale;
    Vector3 smoothedTargetPos, smoothedTargetVel;
    Vector3 groundNormal = Vector3.up;
    bool hasGround;

    // timing
    float dragStartTime;

    struct RBSettings
    {
        public float mass, drag, angularDrag;
        public bool useGravity, isKinematic;
        public RigidbodyInterpolation interpolation;
        public CollisionDetectionMode collisionDetection;
        public RigidbodyConstraints constraints;
        public Vector3 centerOfMass;
    }
    RBSettings saved;

    PhysicsMaterial originalMaterial;

    void Awake()
    {
        col = GetComponent<Collider>();
        rb  = GetComponent<Rigidbody>();
        originalLocalScale = transform.localScale;
        if (col) originalMaterial = col.sharedMaterial;

        if (rb != null)
        {
            SaveRBSettings();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (autoTuneBySize && _lastTunedSize != size)
        {
            ApplySizePreset();
            _lastTunedSize = size;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (autoTuneBySize && _lastTunedSize != size)
        {
            ApplySizePreset();
            _lastTunedSize = size;
        }
    }
#endif

    void ApplySizePreset()
    {
        switch (size)
        {
            case BoxSize.Small:
                canBeCarried = true; // Small = alleen carry
                // snellere feel bij scroll-rotate terwijl je draagt
                targetSmoothTime = 0.06f;
                breakDistance = 7f;
                linearDragWhileDrag = 3.2f; angularDragWhileDrag = 1.2f;
                break;

            case BoxSize.Medium:
                canBeCarried = false;
                kpGround = 14f; kdGround = 2f * Mathf.Sqrt(kpGround); // iets pittiger voor minder lag
                maxDeltaVPerStepGround = 1.8f; maxDragSpeedGround = 4.6f;
                targetSmoothTime = 0.05f; // korter = minder lag
                breakDistance = 9f;
                linearDragWhileDrag = 4f; angularDragWhileDrag = 2f;
                keepUpright = true; hardUprightWhileDrag = false;
                break;

            case BoxSize.Large:
                canBeCarried = false;
                // Langzamer dan Medium, maar nog steeds scherp op input
                kpGround = 9f; kdGround = 2f * Mathf.Sqrt(kpGround);   // ≈ 6
                maxDeltaVPerStepGround = 1.2f;
                maxDragSpeedGround = 3.3f;
                targetSmoothTime = 0.06f; // ook kort, want lag komt van smoothing
                breakDistance = 10f;
                linearDragWhileDrag = 4.8f; angularDragWhileDrag = 2.4f;
                keepUpright = true; hardUprightWhileDrag = true;
                break;
        }
    }

    void FixedUpdate()
    {
        if (!dragging || !grabber || carried) return;
        if (size != BoxSize.Small) GroundDragStep();
    }

    // ---------------- Drag ----------------
    public void StartDrag(Transform grabTransform)
    {
        // Small mag NIET met RMB gesleept worden
        if (size == BoxSize.Small) return;
        if (carried) return;

        EnsureRigidbody();
        dragging = true;
        grabber  = grabTransform;
        rb.isKinematic = false;
        dragStartTime = Time.time;

        // Demping verhogen tijdens drag
        saved.drag = GetLinearDamping(rb);
        saved.angularDrag = GetAngularDamping(rb);
        SetLinearDamping(rb, linearDragWhileDrag);
        SetAngularDamping(rb, angularDragWhileDrag);
        rb.useGravity = true;

        // Low friction
        if (!lowFrictionMaterial)
        {
            lowFrictionMaterial = new PhysicsMaterial("LowFriction_BoxDrag")
            {
                dynamicFriction = 0.05f,
                staticFriction  = 0.02f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }
        if (col) col.sharedMaterial = lowFrictionMaterial;

        // Lager zwaartepunt
        saved.centerOfMass = rb.centerOfMass;
        rb.centerOfMass = new Vector3(0f, saved.centerOfMass.y - Mathf.Abs(comLowerWhileDrag), 0f);

        if (hardUprightWhileDrag)
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // ▼ Instant lock: zet target meteen exact op de hit en reset smoothing state
        if (UpdateGroundFromAim(out Vector3 startTarget))
        {
            smoothedTargetPos = startTarget;
            smoothedTargetVel = Vector3.zero;
        }
    }

    public void StopDrag()
    {
        dragging = false;
        grabber  = null;

        if (rb != null)
        {
            SetLinearDamping(rb, saved.drag);
            SetAngularDamping(rb, saved.angularDrag);
            rb.useGravity = true;
            rb.centerOfMass = saved.centerOfMass;

            if (hardUprightWhileDrag)
                rb.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
        }

        if (col) col.sharedMaterial = originalMaterial;
    }

    // ---------------- Carry (Small) ----------------
    public void PickUp(Transform parent, Vector3 localPos, Vector3 localEuler)
    {
        if (!canBeCarried || carried || size != BoxSize.Small) return;
        StopDrag();
        StartCoroutine(DoPickup(parent, localPos, localEuler, parent));
    }

    IEnumerator DoPickup(Transform parent, Vector3 localPos, Vector3 localEuler, Transform parentRef)
    {
        carried = true;

        if (col) col.enabled = false;

        if (rb != null)
        {
            SaveRBSettings();
            Destroy(rb);
            rb = null;
            yield return null;
        }
        else
        {
            yield return new WaitForFixedUpdate();
        }

        Vector3 safeWorld = parentRef.position + parentRef.forward * 0.6f;
        transform.position = safeWorld;

        transform.SetParent(parent, false);
        transform.localPosition = localPos;
        transform.localRotation = Quaternion.Euler(localEuler);
        transform.localScale = CompensateForParentScale(originalLocalScale, parent);
    }

    public void Drop()
    {
        if (!carried) return;
        carried = false;

        transform.SetParent(null, true);
        transform.localScale = originalLocalScale;

        EnsureRigidbody(recreate: true);

        if (col) col.enabled = true;

        var cam = Camera.main ? Camera.main.transform : null;
        Vector3 fwd = cam ? cam.forward : Vector3.forward;
        Vector3 camPos = cam ? cam.position : transform.position;
        transform.position = camPos + fwd * 1.0f;

        ZeroVelocity(rb);
        AddForwardNudge(rb, fwd, 1.5f);
    }

    // ---------------- Scroll-rotate ----------------
    public void RotateOnScroll(float scrollY, Transform cam)
    {
        if (Mathf.Abs(scrollY) < 0.001f) return;

        float dir = invertScroll ? -1f : 1f;
        float degrees = dir * scrollRotateDegreesPerNotch;

        if (size == BoxSize.Small && carried)
        {
            Vector3 axis = cam ? cam.up : Vector3.up;
            transform.rotation = Quaternion.AngleAxis(degrees * scrollY, axis) * transform.rotation;
            return;
        }

        if (size != BoxSize.Small && dragging)
        {
            Vector3 axis = groundNormal.sqrMagnitude > 0.01f ? groundNormal.normalized : Vector3.up;
            Quaternion q = Quaternion.AngleAxis(degrees * scrollY, axis);

            if (rb != null) rb.MoveRotation(q * rb.rotation);
            else            transform.rotation = q * transform.rotation;
        }
    }

    // ---------------- Ground drag core (Medium/Large) ----------------
    void GroundDragStep()
    {
        if (rb == null) return;

        if (!UpdateGroundFromAim(out Vector3 rawTarget))
        {
            StopDrag();
            return;
        }

        // ■ Low-latency: tijdens eerste frames geen smoothing → direct volgen
        bool instantPhase = instantLockOnStart && (Time.time - dragStartTime) < instantLockDuration;
        Vector3 target = instantPhase ? rawTarget
                                      : Vector3.SmoothDamp(
                                            smoothedTargetPos, rawTarget,
                                            ref smoothedTargetVel, targetSmoothTime,
                                            Mathf.Infinity, Time.fixedDeltaTime
                                        );
        smoothedTargetPos = target;

        // Safety: planare afstand
        Vector3 toTarget = smoothedTargetPos - transform.position;
        Vector3 tanToTarget = Vector3.ProjectOnPlane(toTarget, groundNormal);
        if (tanToTarget.magnitude > breakDistance) { StopDrag(); return; }

#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        Vector3 vel = rb.linearVelocity;
#else
        Vector3 vel = rb.velocity;
#endif
        Vector3 tangentVel = Vector3.ProjectOnPlane(vel, groundNormal);

        // PD in vlak
        Vector3 desiredVel = Vector3.ClampMagnitude(tanToTarget * kpGround, maxDragSpeedGround);
        Vector3 velErr = desiredVel - tangentVel;

        // Startboost: tijdelijk hogere Δv
        float dvMul = (instantPhase && startBoostDvMultiplier > 1f) ? startBoostDvMultiplier : 1f;

        Vector3 deltaV_tangent = (velErr + kdGround * velErr * Time.fixedDeltaTime) * dvMul;

        float maxDv = Mathf.Max(0f, maxDeltaVPerStepGround) * dvMul;
        if (deltaV_tangent.sqrMagnitude > maxDv * maxDv)
            deltaV_tangent = deltaV_tangent.normalized * maxDv;

        // Verticale snelheid weg
        Vector3 normalVel = Vector3.Dot(vel, groundNormal) * groundNormal;
        Vector3 deltaV_normalCancel = -normalVel;

        rb.AddForce(deltaV_tangent, ForceMode.VelocityChange);
        rb.AddForce(deltaV_normalCancel, ForceMode.VelocityChange);

        // Ground stick
        rb.useGravity = true;
        rb.AddForce(-groundNormal * groundStickForce, ForceMode.Acceleration);
        SnapTowardsGroundIfClose();

        // Upright & rotatie-limit
        if (keepUpright && !hardUprightWhileDrag) ApplyUprightTorque();
        LimitAngularSpeed(maxAngularSpeedDeg);
    }

    // Upright PD
    void ApplyUprightTorque()
    {
        Vector3 up = transform.up;
        Vector3 axis = Vector3.Cross(up, groundNormal);
        float sinAngle = axis.magnitude;
        if (sinAngle < 1e-4f) return;

        axis.Normalize();
        float angle = Mathf.Asin(Mathf.Clamp(sinAngle, -1f, 1f)) * Mathf.Rad2Deg;

        float gain = uprightKp * (1f + Mathf.Clamp01((Mathf.Abs(angle) - maxTiltDeg) / Mathf.Max(1e-2f, maxTiltDeg)));
        Vector3 corrective = axis * (gain * Mathf.Deg2Rad * angle);

        Vector3 angVel = rb.angularVelocity;
        Vector3 angVelAroundAxis = Vector3.Project(angVel, axis);
        Vector3 damping = -uprightKd * angVelAroundAxis;

        rb.AddTorque(corrective + damping, ForceMode.Acceleration);
    }

    void LimitAngularSpeed(float maxDeg)
    {
        float maxRad = Mathf.Max(0.1f, maxDeg) * Mathf.Deg2Rad;
        float w = rb.angularVelocity.magnitude;
        if (w > maxRad) rb.angularVelocity = rb.angularVelocity.normalized * maxRad;
    }

    // ---------- Ground helpers ----------
    bool UpdateGroundFromAim(out Vector3 targetOnGround)
    {
        targetOnGround = Vector3.zero;

        // Ray vanaf camera-positie in kijkrichting
        Ray ray = new Ray(grabber.position, grabber.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle <= maxSlopeAngle)
            {
                groundNormal = hit.normal;
                hasGround = true;
                targetOnGround = hit.point;
                return true;
            }
        }

        // Fallback: onder de box
        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down,
            out RaycastHit under, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(under.normal, Vector3.up);
            if (angle <= maxSlopeAngle)
            {
                groundNormal = under.normal;
                hasGround = true;
                targetOnGround = under.point;
                return true;
            }
        }

        hasGround = false;
        return false;
    }

    void SnapTowardsGroundIfClose()
    {
        if (!hasGround) return;

        if (Physics.Raycast(transform.position + groundNormal * 0.5f, -groundNormal,
            out RaycastHit hit, 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            float dist = hit.distance - 0.5f;
            if (dist > 0f && dist < groundSnapDistance)
                rb.AddForce(-groundNormal * (groundStickForce * 0.5f), ForceMode.Acceleration);
        }
    }

    // ---------------- Helpers ----------------
    void SaveRBSettings()
    {
        saved.mass               = rb.mass;
        saved.drag               = GetLinearDamping(rb);
        saved.angularDrag        = GetAngularDamping(rb);
        saved.useGravity         = rb.useGravity;
        saved.isKinematic        = rb.isKinematic;
        saved.interpolation      = rb.interpolation;
        saved.collisionDetection = rb.collisionDetectionMode;
        saved.constraints        = rb.constraints;
        saved.centerOfMass       = rb.centerOfMass;
    }

    void EnsureRigidbody(bool recreate = false)
    {
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = saved.mass > 0f ? saved.mass : 1f;
            SetLinearDamping(rb, saved.drag);
            SetAngularDamping(rb, saved.angularDrag > 0f ? saved.angularDrag : 0.05f);
            rb.useGravity = saved.useGravity == false ? false : true;
            rb.isKinematic = saved.isKinematic && !recreate ? true : false;
            rb.interpolation = saved.interpolation != 0 ? saved.interpolation : RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = saved.collisionDetection != 0 ? saved.collisionDetection : CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = saved.constraints;
            rb.centerOfMass = saved.centerOfMass;
            SaveRBSettings();
        }
    }

    static float GetLinearDamping(Rigidbody r)
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        return r.linearDamping;
#else
        return r.drag;
#endif
    }
    static void SetLinearDamping(Rigidbody r, float v)
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        r.linearDamping = v;
#else
        r.drag = v;
#endif
    }
    static float GetAngularDamping(Rigidbody r)
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        return r.angularDamping;
#else
        return r.angularDrag;
#endif
    }
    static void SetAngularDamping(Rigidbody r, float v)
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        r.angularDamping = v;
#else
        r.angularDrag = v;
#endif
    }
    static void ZeroVelocity(Rigidbody r)
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2022_1_OR_NEWER
        r.linearVelocity = Vector3.zero;
#else
        r.velocity = Vector3.zero;
#endif
        r.angularVelocity = Vector3.zero;
    }
    static void AddForwardNudge(Rigidbody r, Vector3 fwd, float push)
    {
        r.AddForce(fwd * push, ForceMode.VelocityChange);
    }

    Vector3 CompensateForParentScale(Vector3 desiredWorldScale, Transform parent)
    {
        if (parent == null) return desiredWorldScale;
        Vector3 p = parent.lossyScale;
        return new Vector3(
            p.x != 0f ? desiredWorldScale.x / p.x : desiredWorldScale.x,
            p.y != 0f ? desiredWorldScale.y / p.y : desiredWorldScale.y,
            p.z != 0f ? desiredWorldScale.z / p.z : desiredWorldScale.z
        );
    }

    public bool IsDragged => dragging;
    public bool IsCarried => carried;

    // --- util for RB values across versions ---
    static float GetLinearDampingInternal(Rigidbody r) => GetLinearDamping(r);
    static float GetAngularDampingInternal(Rigidbody r) => GetAngularDamping(r);
    static void SetLinearDampingInternal(Rigidbody r, float v) => SetLinearDamping(r, v);
    static void SetAngularDampingInternal(Rigidbody r, float v) => SetAngularDamping(r, v);
}