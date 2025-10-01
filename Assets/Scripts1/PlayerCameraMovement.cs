using UnityEngine;
using UnityEngine.InputSystem;

public class cam : MonoBehaviour
{
    public Transform playerbody;

    [Header("Look Settings")]
    [Range(0.1f, 10f)]
    public float sensitivity = 1f; // later instelbaar via SettingsManager

    private float xRot = 0f;
    private InputAction look;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        var map = new InputActionMap("Camera");
        look = map.AddAction("Look");
        look.AddBinding("<Mouse>/delta");
        map.Enable();
    }

    void Update()
    {
        Vector2 delta = look.ReadValue<Vector2>();

        // Schaal pixel-delta → ongeveer gelijk aan oude Input.GetAxis
        float mouseX = delta.x * 0.075f * sensitivity;
        float mouseY = delta.y * 0.075f * sensitivity;

        xRot -= mouseY;
        xRot = Mathf.Clamp(xRot, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRot, 0, 0);
        playerbody.Rotate(Vector3.up * mouseX);
    }

    // Kan later vanuit je SettingsManager aangeroepen worden
    public void SetSensitivity(float value)
    {
        sensitivity = value;
    }
}
