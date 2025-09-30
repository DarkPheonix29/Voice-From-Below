using UnityEngine;
using UnityEngine.InputSystem;

public class cam : MonoBehaviour
{
    public float MouseSensitivity = 100f;
    public Transform playerbody;

    private float Xrotation = 0f;
    private InputAction lookAction;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        var map = new InputActionMap("Camera");

        lookAction = map.AddAction("Look");
        lookAction.AddBinding("<Mouse>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick").WithProcessor("stickDeadzone(min=0.2)");

        map.Enable();
    }

    void Update()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();

        float mousex = look.x * MouseSensitivity * Time.deltaTime;
        float mousey = look.y * MouseSensitivity * Time.deltaTime;

        Xrotation -= mousey;
        Xrotation = Mathf.Clamp(Xrotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(Xrotation, 0f, 0f);
        playerbody.Rotate(Vector3.up * mousex);
    }
}
