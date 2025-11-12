using UnityEngine;
using UnityEngine.InputSystem;

public class cam : MonoBehaviour
{
    public Transform playerbody;
    public Transform pitchTarget;
    public Transform bobTarget;

    [Range(0.1f, 10f)] public float sensitivity = 1f;

    public float bobAmplitude = 0.05f;
    public float bobFrequency = 8f;
    public float swayAmplitude = 0.02f;
    public float returnSpeed = 10f;
    public float moveThreshold = 0.02f;
    public float sprintBobMultiplier = 1.8f;
    public float crouchBobMultiplier = 0.5f;

    float xRot;
    InputAction look;

    Vector3 restLocalPos;
    Vector3 lastBodyPos;
    float bobPhase;
    CharacterController cc;
    FPPlayer player;

    void Awake()
    {
        if (!pitchTarget) pitchTarget = transform;
        if (!bobTarget) bobTarget = pitchTarget;

        Cursor.lockState = CursorLockMode.Locked;

        var map = new InputActionMap("Camera");
        look = map.AddAction("Look");
        look.AddBinding("<Mouse>/delta");
        map.Enable();

        cc = playerbody ? playerbody.GetComponent<CharacterController>() : null;
        player = playerbody ? playerbody.GetComponent<FPPlayer>() : null;

        restLocalPos = bobTarget.localPosition;
        lastBodyPos = playerbody ? playerbody.position : Vector3.zero;
    }

    void LateUpdate()
    {
        if (PauseMenu.IsPaused) return;

        Vector2 delta = look.ReadValue<Vector2>();

        // 🟢 Nieuw: haal gevoeligheid uit GameSettingsManager
        float uiSens = GameSettingsManager.Instance ? GameSettingsManager.Instance.MouseSensitivity : 1f;
        float effSens = sensitivity * uiSens;

        // Gebruik de effectieve gevoeligheid
        float mouseX = delta.x * 0.075f * effSens;
        float mouseY = delta.y * 0.075f * effSens;

        xRot -= mouseY;
        xRot = Mathf.Clamp(xRot, -90f, 90f);
        if (pitchTarget) pitchTarget.localRotation = Quaternion.Euler(xRot, 0, 0);
        if (playerbody) playerbody.Rotate(Vector3.up * mouseX);

        HeadWobble();
    }

    void HeadWobble()
    {
        if (!bobTarget) return;
        if (playerbody == null) return;

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
