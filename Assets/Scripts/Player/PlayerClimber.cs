using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections;
using System.Collections.Generic;

[DefaultExecutionOrder(-50)]
public class PlayerClimber : MonoBehaviour
{
    [Header("Refs")]
    public CharacterController cc; 
    public Rigidbody rb; 
    public MonoBehaviour[] disableWhileClimbing; 

    [Header("Climb")]
    public float climbSpeed = 5f;
    public KeyCode detachKey = KeyCode.Space;
    [Tooltip("Push the player a bit off the chain axis so the capsule doesn't live inside link colliders.")]
    public float lateralOffset = 0.35f;

    [Tooltip("Min absolute input to count as intentional climb.")]
    public float inputDeadzone = 0.05f;

    [Tooltip("Stay attached even with zero input for this many seconds after a grab.")]
    public float clingGraceTime = 0.30f;

    [Header("Debug")]
    public bool debugDraw = false;

#if ENABLE_INPUT_SYSTEM
    public InputAction moveY = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/leftStick/y");
    void OnEnable() { moveY.Enable(); }
    void OnDisable() { moveY.Disable(); }
#endif

    // runtime state
    Climbable _chain;
    bool _climbing;
    Vector3 _A, _B, _ABnorm, _side; 
    float _length, _s; 
    float _clingTimer; // Time remaining until auto-detach without input

    public bool IsSnapping { get; private set; }
    public bool IsClimbing => _climbing;
    public float CurrentClimbSpeed { get; private set; }

    readonly List<Collider> _disabledChainCols = new List<Collider>();

    // cache CC settings while climbing
    float _savedStepOffset, _savedSlopeLimit;
    const float kMinStepOffset = 0.01f; // <-- strictly positive

    // snap coroutine mgmt
    Coroutine _snapCo;
    int _snapGen; 

    void Reset()
    {
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!_climbing || IsSnapping)
        {
            CurrentClimbSpeed = 0f;
            return;
        }

        // --- DETACH (manual) ---
#if ENABLE_INPUT_SYSTEM
        bool detach = Keyboard.current?.spaceKey.wasPressedThisFrame == true;
#else
        bool detach = Input.GetKeyDown(detachKey);
#endif
        if (detach)
        {
            CurrentClimbSpeed = 0f;
            EndClimb(false);
            return;
        }

        // --- INPUT ---
        float y = 0f;
#if ENABLE_INPUT_SYSTEM
        y = moveY.ReadValue<float>();
        y += (Keyboard.current?.wKey.isPressed == true ? 1f : 0f);
        y -= (Keyboard.current?.sKey.isPressed == true ? 1f : 0f);
#else
        y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
#endif

        bool hasInput = Mathf.Abs(y) >= inputDeadzone;
        float signedSpeed = 0f;

        // --- CLING / AUTO-DETACH LOGIC (NEW FIXED LOGIC) ---
        
        if (hasInput)
        {
            // If the player is actively moving, refresh the timer and set speed
            _clingTimer = clingGraceTime;
            signedSpeed = y * climbSpeed;
        }
        else
        {
            // If no input, drain the grace timer
            _clingTimer = Mathf.Max(0f, _clingTimer - Time.deltaTime);
            
            // If the grace timer runs out AND we still have no input, detach.
            if (_clingTimer <= 0f)
            {
                CurrentClimbSpeed = 0f;
                EndClimb(false);
                return;
            }
            // If we are stationary but _clingTimer > 0, signedSpeed remains 0.
        }

        // advance along chain
        _s = Mathf.Clamp(_s + signedSpeed * Time.deltaTime, 0f, _length);

        // publish absolute climb speed for cadence
        CurrentClimbSpeed = Mathf.Abs(signedSpeed);

        // auto-detach at ends
        if (_s <= 0.001f || _s >= _length - 0.001f)
        {
            CurrentClimbSpeed = 0f;
            EndClimb(false);
            return;
        }

        Vector3 target = _A + _ABnorm * _s + _side * lateralOffset;

        // move controller directly to target; CC resolves collisions
        Vector3 delta = target - transform.position;
        if (CCReady()) cc.Move(delta);

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

        // compute axis data
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

        CacheCCSettings();
        ClimbCCSettings(); 

        // Mark climbing & snapping FIRST so FPPlayer can skip its Move this frame
        _climbing = true;
        IsSnapping = true;
        CurrentClimbSpeed = 0f;
        _clingTimer = clingGraceTime; // CRITICAL: Timer set high on start

        // cancel any prior snap
        if (_snapCo != null) StopCoroutine(_snapCo);
        _snapGen++;
        _snapCo = StartCoroutine(SnapIntoClimbPosition(_snapGen));
    }

    IEnumerator SnapIntoClimbPosition(int gen)
    {
        // 1. Wait for the absolute end of the frame. 
        yield return new WaitForEndOfFrame(); 
        if (gen != _snapGen) yield break;

        // 2. Calculate the total delta required for the snap.
        
        // --- CRITICAL FIX: Add a buffer to prevent immediate end-of-chain detach. ---
        float buffer = 0.5f; // Place 0.5 units away from the actual ends
        float minS = Mathf.Min(buffer, _length / 2f);
        float maxS = Mathf.Max(_length - buffer, _length / 2f);
        
        // Calculate the raw 's' value based on projection
        float projectedS = Vector3.Dot(transform.position - _A, _ABnorm);
        
        // Clamp 's' to prevent it from sitting too close to 0 or _length
        _s = Mathf.Clamp(projectedS, minS, maxS);
        // -----------------------------------------------------------------------------

        Vector3 snapTarget = _A + _ABnorm * _s + _side * lateralOffset;
        Vector3 snapDelta = snapTarget - transform.position;
        
        Vector3 halfDelta = snapDelta / 2f;

        if (CCReady()) cc.Move(halfDelta); // First half of the snap move
        yield return null;
        if (gen != _snapGen) yield break;

        if (CCReady()) cc.Move(halfDelta); // Second half of the snap move

        // face sideways (towards _side)
        Vector3 face = _side; face.y = 0f;
        if (face.sqrMagnitude > 0.001f) transform.forward = face.normalized;

        ToggleChainColliders(false);

        // safety wait
        yield return null;
        if (gen != _snapGen) yield break;

        IsSnapping = false;

        if (debugDraw)
            Debug.Log($"[Climber] Begin climb on '{_chain.name}' len={_length:F2} start s={_s:F2}");
    }

    public void EndClimb(bool jumpOff)
    {
        CurrentClimbSpeed = 0f;
        if (!_climbing) return;

        // stop snap coroutine if it's still running
        if (_snapCo != null) StopCoroutine(_snapCo);
        _snapGen++;
        _snapCo = null;
        IsSnapping = false;

        SetEnabled(disableWhileClimbing, true);
        if (rb) rb.isKinematic = false;

        ToggleChainColliders(true);

        RestoreCCSettings();

        if (debugDraw)
            Debug.Log($"[Climber] End climb at s={_s:F2} pos={transform.position}");

        _climbing = false;
        _chain = null;
    }

    // ------- helpers -------
    bool CCReady()
    {
        return cc && cc.enabled && cc.gameObject.activeInHierarchy;
    }
    
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

    void CacheCCSettings()
    {
        if (!cc) return;
        _savedStepOffset = Mathf.Max(kMinStepOffset, cc.stepOffset);
        _savedSlopeLimit = cc.slopeLimit;
    }

    void ClimbCCSettings()
    {
        if (!cc) return;
        // IMPORTANT: Set stepOffset first and keep it strictly positive
        cc.stepOffset = kMinStepOffset; 
        cc.slopeLimit = 90f;
    }

    void RestoreCCSettings()
    {
        if (!cc) return;
        // Restore slope first, then step offset
        cc.slopeLimit = _savedSlopeLimit;
        cc.stepOffset = Mathf.Max(kMinStepOffset, _savedStepOffset);
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