using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections.Generic;

public class PlayerClimber : MonoBehaviour
{
    [Header("Refs")]
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
    void OnEnable(){ moveY.Enable(); }
    void OnDisable(){ moveY.Disable(); }
#endif

    // runtime state
    Climbable _chain;
    bool _climbing;
    bool _skipOneFrame;              // avoid a big delta right after BeginClimb
    Vector3 _A, _B, _ABnorm, _side;  // chain axis (world space) + a stable sideways direction
    float _length, _s;               // length of chain; param s in [0..length]

    // if you temporarily want to disable the chain's colliders to avoid snagging
    readonly List<Collider> _disabledChainCols = new List<Collider>();

    void Reset()
    {
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
    }

    public bool IsClimbing => _climbing;

    void Update()
    {
        if (!_climbing) return;

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
    }

    public void EndClimb(bool jumpOff)
    {
        if (!_climbing) return;

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
}
