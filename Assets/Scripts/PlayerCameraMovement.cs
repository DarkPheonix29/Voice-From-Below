using UnityEngine;
using UnityEngine.InputSystem;

public class cam : MonoBehaviour
{
    public Transform playerbody;

    [Range(0.1f, 10f)]
    public float sensitivity = 1f;

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

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        var map = new InputActionMap("Camera");
        look = map.AddAction("Look");
        look.AddBinding("<Mouse>/delta");
        map.Enable();

        restLocalPos = transform.localPosition;
        lastBodyPos = playerbody.position;
        cc = playerbody.GetComponent<CharacterController>();
        player = playerbody.GetComponent<FPPlayer>();
    }

    void Update()
    {
        Vector2 delta = look.ReadValue<Vector2>();
        float mouseX = delta.x * 0.075f * sensitivity;
        float mouseY = delta.y * 0.075f * sensitivity;

        xRot -= mouseY;
        xRot = Mathf.Clamp(xRot, -90f, 90f);
        transform.localRotation = Quaternion.Euler(xRot, 0, 0);
        playerbody.Rotate(Vector3.up * mouseX);

        HeadWobble();
    }

    void HeadWobble()
    {
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
            transform.localPosition = Vector3.Lerp(transform.localPosition, target, Time.deltaTime * returnSpeed);
        }
        else
        {
            bobPhase = 0f;
            transform.localPosition = Vector3.Lerp(transform.localPosition, restLocalPos, Time.deltaTime * returnSpeed);
        }
    }

    public void SetSensitivity(float value) => sensitivity = value;
}