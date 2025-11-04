using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class Flashlight : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Spotlight of point light voor de zaklamp.")]
    public Light flashlight;
    [Tooltip("Pivot (meestal je camera) voor lichte sway/wobble.")]
    public Transform swayPivot;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F; // <-- standaard F

    [Header("Light Settings")]
    public float maxIntensity = 3.5f;
    public float maxRange = 22f;
    public float toggleFadeTime = 0.15f;
    public Texture cookie;
    public bool startOn = false;

    [Header("Flicker")]
    public float flickerAmplitude = 0.2f;
    public float flickerFrequency = 15f;

    [Header("Sway")]
    public float swayAngle = 1.2f;
    public float swayFrequency = 1.4f;
    public float swayLerpSpeed = 10f;

    private bool isOn;
    private Quaternion baseRot;
    private Vector3 lastPos;
    private float movementAmount;

#if ENABLE_INPUT_SYSTEM
    private InputAction toggleAction;
#endif

    void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        var map = new InputActionMap("Flashlight");
        string keyPath = "<Keyboard>/f";  // <-- Altijd F, ongeacht toggleKey veld
        toggleAction = map.AddAction("Toggle", binding: keyPath);
        map.Enable();
#endif

        if (!flashlight) flashlight = GetComponentInChildren<Light>(true);
        if (flashlight && flashlight.type != LightType.Spot) flashlight.type = LightType.Spot;
        if (flashlight) flashlight.cookie = cookie;

        isOn = startOn;

        if (flashlight)
        {
            if (!isOn)
            {
                flashlight.intensity = 0f;
                flashlight.range = 0f;
                flashlight.enabled = false;
            }
        }

        baseRot = transform.localRotation;
        lastPos = transform.position;
    }

    void Update()
    {
        if (WasTogglePressed())
            Toggle();

        float dt = Time.deltaTime;
        UpdateSway(dt);
        UpdateLight(dt);
    }

    bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return toggleAction != null && toggleAction.WasPressedThisFrame();
#else
        return Input.GetKeyDown(toggleKey); // ← werkt ook zonder nieuw Input System
#endif
    }

    public void Toggle()
    {
        isOn = !isOn;
        if (flashlight && isOn) flashlight.enabled = true;
    }

    void UpdateLight(float dt)
    {
        if (!flashlight) return;

        float flicker = Mathf.PerlinNoise(Time.time * flickerFrequency, 0f) * 2f - 1f;
        float flickerEffect = 1f + flicker * flickerAmplitude;

        float targetIntensity = isOn ? maxIntensity * flickerEffect : 0f;
        float targetRange = isOn ? maxRange : 0f;

        flashlight.intensity = Mathf.Lerp(flashlight.intensity, targetIntensity, dt / Mathf.Max(0.001f, toggleFadeTime));
        flashlight.range     = Mathf.Lerp(flashlight.range,     targetRange,     dt / Mathf.Max(0.001f, toggleFadeTime));

        if (!isOn && flashlight.enabled && flashlight.intensity < 0.01f) flashlight.enabled = false;
        if (isOn && !flashlight.enabled && flashlight.intensity > 0.02f) flashlight.enabled = true;
    }

    void UpdateSway(float dt)
    {
        if (!swayPivot) return;

        Vector3 delta = swayPivot.position - lastPos;
        lastPos = swayPivot.position;

        float moveSpeed = delta.magnitude / Mathf.Max(0.0001f, dt);
        movementAmount = Mathf.Lerp(movementAmount, Mathf.Clamp01(moveSpeed * 0.25f), dt * 4f);

        float t = Time.time * swayFrequency;
        float x = Mathf.PerlinNoise(t, 1.23f) * 2f - 1f;
        float y = Mathf.PerlinNoise(2.33f, t) * 2f - 1f;

        Quaternion swayRot = baseRot * Quaternion.Euler(y * swayAngle, x * swayAngle, 0f);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, swayRot, dt * swayLerpSpeed);
    }

    public bool IsOn => isOn;

    // -------------------------------
    // Pickup / Drop (alle varianten)
    // -------------------------------

    public void PickUp()
    {
        gameObject.SetActive(true);
        if (flashlight)
        {
            flashlight.enabled = isOn;
            if (!isOn)
            {
                flashlight.intensity = 0f;
                flashlight.range = 0f;
            }
        }
    }

    public void PickUp(Transform parent)
    {
        PickUp();
        if (parent)
        {
            transform.SetParent(parent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (!swayPivot) swayPivot = parent;
        }
    }

    public void PickUp(Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        PickUp(parent);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
    }

    // ✅ Overload voor jouw FlashlightPickup.cs
    public void PickUp(Transform parent, Vector3 localPosition, Vector3 localEulerAngles)
    {
        PickUp(parent);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEulerAngles);
    }

    public void Drop()
    {
        transform.SetParent(null, true);
        isOn = false;
        if (flashlight)
        {
            flashlight.intensity = 0f;
            flashlight.range = 0f;
            flashlight.enabled = false;
        }
    }
}