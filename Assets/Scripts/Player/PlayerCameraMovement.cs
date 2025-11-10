using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class cam : MonoBehaviour
{
    public Transform playerbody;    // player root for yaw
    public Transform pitchTarget;   // usually CameraPivot (this object)
    public Transform bobTarget;     // NEW: the HeadBob child

    // Dit blijft je "basis"-multiplier (kan je per wapen/scene tunen)
    [Range(0.1f, 10f)] public float sensitivity = 1f;

    public float bobAmplitude = 0.05f;
    public float bobFrequency = 8f;
    public float swayAmplitude = 0.02f;
    public float returnSpeed = 10f;
    public float moveThreshold = 0.02f;
    public float sprintBobMultiplier = 1.8f;
    public float crouchBobMultiplier = 0.5f;
    float effectiveSens = 1f;


    float xRot;
    InputAction look;

    Vector3 restLocalPos;
    Vector3 lastBodyPos;
    float bobPhase;
    CharacterController cc;
    FPPlayer player;

    bool _pausedByCutscene;

    public void PauseForCutscene(bool pause) { _pausedByCutscene = pause; if (pause) bobPhase = 0f; }

    void Awake()
    {
        // Initial setup for references and input actions
        if (!pitchTarget) pitchTarget = transform;
        if (!bobTarget) bobTarget = pitchTarget; 

        Cursor.lockState = CursorLockMode.Locked;

        var map = new InputActionMap("Camera");
        look = map.AddAction("Look");
        look.AddBinding("<Mouse>/delta");
        map.Enable();
        
        // Get components early
        cc = playerbody.GetComponent<CharacterController>();
        player = playerbody.GetComponent<FPPlayer>();

        // Temporarily set restLocalPos to zero/initial value.
        // The *correct* value will be captured in the coroutine.
        restLocalPos = bobTarget.localPosition; 
    }

    void Start()
    {
        // Lees elke frame de waarde uit de Settings (werkt live tijdens slepen)
   float uiSens = GameSettingsManager.Instance ? GameSettingsManager.Instance.MouseSensitivity : 1f;
float effectiveSens = sensitivity * uiSens;

        // Start the coroutine to wait until the player has landed.
        StartCoroutine(InitializeCameraPositionAfterLanding());
    }

    System.Collections.IEnumerator InitializeCameraPositionAfterLanding()
    {
        // 1. Wait for one frame to let all Start() methods and initial physics run.
        yield return null; 

        // 2. Wait until the Character Controller is grounded. This handles the fall.
        if (cc != null)
        {
            while (!cc.isGrounded)
            {
                yield return null;
            }
        }

        // 3. Now the player has landed and the position is finalized.
        // CAPTURE THE TRUE RESTING POSITION HERE.
        restLocalPos = bobTarget.localPosition;
        lastBodyPos = playerbody.position;
    }

    void LateUpdate()
    {
        if (_pausedByCutscene) return;

        // Note: We skip input and rotation for the first few frames until restLocalPos is set.
        // This is generally fine since the player won't be moving much yet.
        
        Vector2 delta = look.ReadValue<Vector2>();
        float mouseX = delta.x * 0.075f * effectiveSens;
        float mouseY = delta.y * 0.075f * effectiveSens;

        xRot -= mouseY;
        xRot = Mathf.Clamp(xRot, -90f, 90f);
        if (pitchTarget) pitchTarget.localRotation = Quaternion.Euler(xRot, 0, 0);

        if (playerbody) playerbody.Rotate(Vector3.up * mouseX);

        HeadWobble();
    } 

    void HeadWobble()
    {
        // Only run bob/sway logic if the rest position has been successfully captured.
        if (restLocalPos == Vector3.zero && bobTarget.localPosition != Vector3.zero) return;
        if (!bobTarget) return;

        Vector3 bodyDelta = playerbody.position - lastBodyPos;
        lastBodyPos = playerbody.position;

        float horizSpeed = new Vector3(bodyDelta.x, 0f, bodyDelta.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        bool grounded = cc ? cc.isGrounded : true;
        bool moving = grounded && horizSpeed > moveThreshold;

        float amp = bobAmplitude;
        float freq = bobFrequency;

        if (player)
        {
            if (player.IsCrouching) { amp *= crouchBobMultiplier; freq *= crouchBobMultiplier; }
            else if (player.IsSprinting) { amp *= sprintBobMultiplier; freq *= sprintBobMultiplier; }
        }

        if (moving)
        {
            bobPhase += Time.deltaTime * freq;
            float bobY = Mathf.Sin(bobPhase) * amp;
            float bobX = Mathf.Sin(bobPhase * 0.5f) * swayAmplitude;
            Vector3 target = restLocalPos + new Vector3(bobX, bobY, 0f);
            bobTarget.localPosition = Vector3.Lerp(bobTarget.localPosition, target, Time.deltaTime * returnSpeed);
        }
        else
        {
            bobPhase = 0f;
            bobTarget.localPosition = Vector3.Lerp(bobTarget.localPosition, restLocalPos, Time.deltaTime * returnSpeed);
        }
    }
}
