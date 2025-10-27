using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections.Generic;

public class PlayerClimber : MonoBehaviour
{
    [Header("Refs")]
<<<<<<< Updated upstream
    public CharacterController cc;                   // REQUIRED (do not scale the CC GameObject)
    public Rigidbody rb;                             // optional; set kinematic during climb to avoid physics fights
    public MonoBehaviour[] disableWhileClimbing;     // e.g. FP Player (Script), headbob, camera sway

    [Header("Climb")]
    public float climbSpeed = 5f;
    public KeyCode detachKey = KeyCode.Space;
    [Tooltip("Push the player a bit off the chain axis so the capsule doesn't live inside link colliders.")]
    public float lateralOffset = 0.35f;

    [Header("Debug")]
    public bool debugDraw = false;                   // turn gizmos/logs on/off

#if ENABLE_INPUT_SYSTEM
    public InputAction moveY = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/leftStick/y");
    void OnEnable() { moveY.Enable(); }
    void OnDisable() { moveY.Disable(); }
#endif

    // runtime state
    Climbable _chain;
    bool _climbing;
    bool _skipOneFrame;              // avoid a big delta right after BeginClimb
    Vector3 _A, _B, _ABnorm, _side;  // chain axis (world space) + a stable sideways direction
    float _length, _s;               // length of chain; param s in [0..length]

    // if you temporarily want to disable the chain's colliders to avoid snagging
    readonly List<Collider> _disabledChainCols = new List<Collider>();
=======
    public Transform cameraRoot;                 // optional: face the chain on grab
    public CharacterController cc;               // if present, we use CC path exclusively
    public Rigidbody rb;                         // used only if no CC present
    public Collider playerCollider;              // optional (for ignore-collision with chain)

    [Header("Climb Settings")]
    public float climbSpeed = 4.5f;
    public float detachJumpForce = 4.5f;
    public KeyCode detachKey = KeyCode.Space;

    [Header("Stability")]
    public float snapSmoothTime = 0.04f;         // 0 = hard lock
    public MonoBehaviour[] disableWhileClimbing; // add your FP Player, PlayerMovement, etc.

    [Header("Collision Safety")]
    public LayerMask worldMask = ~0;             // used for CC and RB sweeps
    public float skin = 0.02f;
    public float maxStepPerFrame = 0.5f;         // lower helps avoid tunneling
    public QueryTriggerInteraction triggerQuery = QueryTriggerInteraction.Ignore;

#if ENABLE_INPUT_SYSTEM
    public InputAction moveY = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/leftStick/y");
    void OnEnable(){ moveY.Enable(); }
    void OnDisable(){ moveY.Disable(); }
#endif

    // runtime
    Climbable _current;
    bool _climbing;
    float _s;                        // scalar along chain [0..Length]
    Vector3 _a, _b, _abNorm;
    float _length;
    Vector3 _snapVel;
    Vector3 _pendingTarget;

    // CC cache
    float _origStepOffset;
    float _origSlopeLimit;

    // RB cache
    RigidbodyInterpolation _origInterpolation;
    CollisionDetectionMode _origCollisionMode;

    // ignore-chain bookkeeping
    readonly List<Collider> _ignoredChainCols = new List<Collider>();
>>>>>>> Stashed changes

    void Reset()
    {
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
<<<<<<< Updated upstream
    }

    public bool IsClimbing => _climbing;
=======
        if (!playerCollider) playerCollider = GetComponentInChildren<CapsuleCollider>();
        if (!playerCollider) playerCollider = GetComponentInChildren<Collider>();
    }

    void Awake()
    {
        if (cc)
        {
            _origStepOffset = cc.stepOffset;
            _origSlopeLimit = cc.slopeLimit;
        }
        if (rb)
        {
            _origInterpolation = rb.interpolation;
            _origCollisionMode = rb.collisionDetectionMode;
        }
    }
>>>>>>> Stashed changes

    void Update()
    {
        if (!_climbing) return;

<<<<<<< Updated upstream
        // --- DETACH ---
#if ENABLE_INPUT_SYSTEM
        bool detach = Keyboard.current?.spaceKey.wasPressedThisFrame == true;
#else
        bool detach = Input.GetKeyDown(detachKey);
#endif
        if (detach) { EndClimb(false); return; }

        // --- MOVE ---
        if (_skipOneFrame) { _skipOneFrame = false; return; } // prevent a huge first delta

        float y = 0f;
#if ENABLE_INPUT_SYSTEM
        y = moveY.ReadValue<float>();
        y += (Keyboard.current?.wKey.isPressed == true ? 1f : 0f);
        y -= (Keyboard.current?.sKey.isPressed == true ? 1f : 0f);
#else
        y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
#endif

        _s = Mathf.Clamp(_s + y * climbSpeed * Time.deltaTime, 0f, _length);

        // auto-detach at ends (nice UX)
        if (_s <= 0.001f || _s >= _length - 0.001f)
        {
            EndClimb(false);
            return;
        }

        Vector3 target = _A + _ABnorm * _s + _side * lateralOffset;

        // move controller directly to target; CC resolves collisions
        Vector3 delta = target - transform.position;
        cc.Move(delta);

        if (debugDraw)
        {
            if (delta.magnitude > 0.25f)
                Debug.LogWarning($"[Climber] Large delta move ({delta.magnitude:F3}) at s={_s:F2}");
            Debug.DrawLine(transform.position, transform.position + delta, Color.cyan, 0.1f);
            Debug.DrawRay(transform.position, Vector3.up * 0.5f, Color.yellow, 0.1f);
        }
    }

    // Called by Climbable.Grab()
    public void BeginClimb(Climbable c)
    {
        if (!cc) { Debug.LogWarning("PlayerClimber: needs CharacterController on the same object."); return; }
        if (_climbing) return;

        Vector3 A = c.bottom.position;
        Vector3 B = c.top.position;
        Vector3 AB = B - A;
        float len = AB.magnitude;
        if (len < 0.01f) { Debug.LogWarning($"[Climber] Chain '{c.name}' too short (len={len})."); return; }

        _chain = c;
        _A = A;
        _B = B;
        _length = len;
        _ABnorm = AB / len;

        _side = Vector3.Cross(_ABnorm, Vector3.up);
        if (_side.sqrMagnitude < 1e-4f) _side = Vector3.Cross(_ABnorm, Vector3.right);
        _side.Normalize();

        SetEnabled(disableWhileClimbing, false);

        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        _s = Mathf.Clamp(Vector3.Dot(transform.position - _A, _ABnorm), 0f, _length);
        Vector3 snap = _A + _ABnorm * _s + _side * lateralOffset;

        cc.enabled = false;
        transform.position = snap;
        cc.enabled = true;

        // 🩹 FIX: lift slightly so CC isn’t grounded
        cc.Move(Vector3.up * 0.1f);

        Vector3 face = _side; face.y = 0f;
        if (face.sqrMagnitude > 0.001f) transform.forward = face.normalized;

        ToggleChainColliders(false);

        _climbing = true;
        _skipOneFrame = true;

        if (debugDraw)
            Debug.Log($"[Climber] Begin climb on '{c.name}' len={_length:F2} start s={_s:F2}");
=======
        // input
        float inputY = 0f;
#if ENABLE_INPUT_SYSTEM
        inputY = moveY.ReadValue<float>();
        inputY += (Keyboard.current?.wKey.isPressed == true ? 1f : 0f);
        inputY -= (Keyboard.current?.sKey.isPressed == true ? 1f : 0f);
#else
        inputY = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
#endif

        // advance along chain (scalar)
        _s = Mathf.Clamp(_s + inputY * climbSpeed * Time.deltaTime, 0f, _length);
        _pendingTarget = _a + _abNorm * _s;

        // CC path: move now (with sweep)
        if (cc) MoveCC(_pendingTarget);

        // detach
        bool detach;
#if ENABLE_INPUT_SYSTEM
        detach = Keyboard.current?.spaceKey.wasPressedThisFrame == true;
#else
        detach = Input.GetKeyDown(detachKey);
#endif
        if (detach) EndClimb(jumpOff:true);
    }

    void FixedUpdate()
    {
        // RB path: apply in physics step
        if (_climbing && !cc && rb) MoveRBWithSweep(_pendingTarget);
    }

    // ----------------- CC path (with sweep) -----------------
    void MoveCC(Vector3 target)
    {
        Vector3 desired = (snapSmoothTime <= 0f)
            ? target
            : Vector3.SmoothDamp(transform.position, target, ref _snapVel, snapSmoothTime);

        Vector3 from = transform.position;
        Vector3 to = desired;
        Vector3 delta = to - from;
        float dist = delta.magnitude;

        // clamp big teleports
        float maxStep = Mathf.Max(0.01f, maxStepPerFrame);
        if (dist > maxStep)
        {
            to = from + delta.normalized * maxStep;
            delta = to - from;
            dist = maxStep;
        }

        // sweep a capsule matching CC (or fallback) to avoid tunneling
        Vector3 p1, p2; float radius;
        GetCapsule(from, out p1, out p2, out radius);

        if (dist > 1e-4f && Physics.CapsuleCast(p1, p2, radius, delta.normalized, out RaycastHit hit, dist, worldMask, triggerQuery))
        {
            to = from + delta.normalized * Mathf.Max(hit.distance - skin, 0f);
            to += hit.normal * skin; // tiny push out
            delta = to - from;
        }

        cc.Move(delta); // let CC resolve residual micro-contacts
    }

    // ----------------- RB path (sweep) -----------------
    void MoveRBWithSweep(Vector3 target)
    {
        Vector3 desired = (snapSmoothTime <= 0f)
            ? target
            : Vector3.SmoothDamp(rb.position, target, ref _snapVel, snapSmoothTime);

        Vector3 from = rb.position;
        Vector3 to = desired;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist > maxStepPerFrame)
        {
            to = from + delta.normalized * maxStepPerFrame;
            dist = maxStepPerFrame;
        }

        Vector3 p1, p2; float radius;
        GetCapsule(from, out p1, out p2, out radius);

        if (dist > 1e-4f && Physics.CapsuleCast(p1, p2, radius, delta.normalized, out RaycastHit hit, dist, worldMask, triggerQuery))
        {
            to = from + delta.normalized * Mathf.Max(hit.distance - skin, 0f);
            to += hit.normal * skin;
        }

        if (!rb.isKinematic) rb.isKinematic = true;
        rb.MovePosition(to);
    }

    void GetCapsule(Vector3 center, out Vector3 p1, out Vector3 p2, out float radius)
    {
        // Prefer CapsuleCollider
        var cap = playerCollider as CapsuleCollider;
        if (cap)
        {
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float scaleX = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            radius = cap.radius * scaleX;
            float height = Mathf.Max(cap.height * scaleY, radius * 2f);
            Vector3 up = transform.up;
            Vector3 worldCenter = transform.TransformPoint(cap.center);
            float half = Mathf.Max(0f, (height * 0.5f) - radius);
            p1 = worldCenter + up * half;
            p2 = worldCenter - up * half;
            return;
        }

        // fallback to CC dims
        if (cc)
        {
            radius = cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float height = Mathf.Max(cc.height * Mathf.Abs(transform.lossyScale.y), radius * 2f);
            Vector3 up = transform.up;
            Vector3 worldCenter = transform.TransformPoint(cc.center);
            float half = Mathf.Max(0f, (height * 0.5f) - radius);
            p1 = worldCenter + up * half;
            p2 = worldCenter - up * half;
            return;
        }

        // generic
        radius = 0.4f;
        Vector3 up2 = Vector3.up;
        p1 = center + up2 * 0.9f;
        p2 = center - up2 * 0.9f;
    }

    // --------------- lifecycle ----------------
    public void BeginClimb(Climbable target)
    {
        if (!target) return;

        _current = target;
        _climbing = true;

        // cache chain
        _a = _current.bottom.position;
        _b = _current.top.position;
        Vector3 ab = _b - _a;
        _length = Mathf.Max(0f, ab.magnitude);
        _abNorm = (_length > 0.0001f) ? ab / _length : Vector3.up;

        // disable your normal mover/gravity etc.
        SetClimbDependencies(false);

        // CharacterController path
        if (cc)
        {
            _origStepOffset = cc.stepOffset;
            _origSlopeLimit = cc.slopeLimit;
            cc.stepOffset = 0f;
            cc.slopeLimit = 90f;
            // Do NOT touch the Rigidbody here; CC is the authority.
        }
        else if (rb) // Rigidbody path
        {
            // zero velocities BEFORE switching to kinematic
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.useGravity = false;
            rb.isKinematic = true;
            _origInterpolation = rb.interpolation;
            _origCollisionMode = rb.collisionDetectionMode;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        // snap to closest point on the axis
        _s = ClosestSOnSegment(transform.position);
        _s = Mathf.Clamp(_s, 0f, _length);
        _pendingTarget = _a + _abNorm * _s;

        if (cc) MoveCC(_pendingTarget);
        else if (rb) rb.position = _pendingTarget;
        else transform.position = _pendingTarget;

        // face the chain once
        Vector3 faceDir = Vector3.Cross(_abNorm, Vector3.up);
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.001f) transform.forward = faceDir.normalized;

        ToggleChainCollisions(true);
>>>>>>> Stashed changes
    }

    public void EndClimb(bool jumpOff)
    {
        if (!_climbing) return;
<<<<<<< Updated upstream

        // restore scripts/physics
        SetEnabled(disableWhileClimbing, true);
        if (rb) rb.isKinematic = false;

        // re-enable chain colliders
        ToggleChainColliders(true);

        if (debugDraw)
            Debug.Log($"[Climber] End climb at s={_s:F2} pos={transform.position}");

        _climbing = false;
        _chain = null;
    }

    // ------- helpers -------
    void SetEnabled(MonoBehaviour[] arr, bool on)
    {
        if (arr == null) return;
        foreach (var m in arr) if (m) m.enabled = on;
    }

    void ToggleChainColliders(bool enable)
    {
        if (!_chain) return;

        if (enable)
        {
            foreach (var col in _disabledChainCols)
                if (col) col.enabled = true;
            _disabledChainCols.Clear();
            return;
        }

        _disabledChainCols.Clear();
        foreach (var col in _chain.GetComponentsInChildren<Collider>())
        {
            if (!col || !col.enabled) continue;
            col.enabled = false;
            _disabledChainCols.Add(col);
        }
    }

    void OnDrawGizmos()
    {
        if (!_climbing || !debugDraw) return;

        Gizmos.color = Color.green; Gizmos.DrawLine(_A, _B);
        Gizmos.color = Color.yellow; Gizmos.DrawSphere(transform.position, 0.05f);
        Vector3 target = _A + _ABnorm * _s + _side * lateralOffset;
        Gizmos.color = Color.cyan; Gizmos.DrawSphere(target, 0.05f);
        Gizmos.color = Color.magenta; Gizmos.DrawLine(transform.position, transform.position + _side * 0.5f);
    }
=======
        _climbing = false;

        if (cc)
        {
            cc.stepOffset = _origStepOffset;
            cc.slopeLimit = _origSlopeLimit;
        }
        else if (rb)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = _origInterpolation;
            rb.collisionDetectionMode = _origCollisionMode;

            if (jumpOff)
            {
                Vector3 impulse = (transform.forward + Vector3.up).normalized * detachJumpForce;
                rb.AddForce(impulse, ForceMode.VelocityChange);
            }
        }

        SetClimbDependencies(true);
        ToggleChainCollisions(false);
        _current = null;
    }

    public bool IsClimbing => _climbing;

    // --------------- helpers ----------------
    float ClosestSOnSegment(Vector3 p)
    {
        Vector3 ap = p - _a;
        return Mathf.Clamp(Vector3.Dot(ap, _abNorm), 0f, _length);
    }

    void SetClimbDependencies(bool enabledState)
    {
        if (disableWhileClimbing == null) return;
        foreach (var m in disableWhileClimbing)
            if (m) m.enabled = enabledState;
    }

    void ToggleChainCollisions(bool ignore)
    {
        if (!playerCollider || _current == null) return;
        if (ignore) _ignoredChainCols.Clear();

        foreach (var col in _current.GetComponentsInChildren<Collider>())
        {
            if (!col || !col.enabled) continue;
            Physics.IgnoreCollision(playerCollider, col, ignore);
            if (ignore) _ignoredChainCols.Add(col);
        }

        if (!ignore)
        {
            foreach (var col in _ignoredChainCols)
                if (col) Physics.IgnoreCollision(playerCollider, col, false);
            _ignoredChainCols.Clear();
        }
    }
>>>>>>> Stashed changes
}
