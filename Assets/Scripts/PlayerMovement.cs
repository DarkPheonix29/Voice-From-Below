using UnityEngine;
using UnityEngine.InputSystem;

public class FPPlayer : MonoBehaviour
{
    public CharacterController controller;
    public float Speed = 6f;
    public float Jump = 3f;
    public float Gravity = -9.81f;

    public float GroundDistance = 0.4f; 
    public LayerMask GroundMask;      
    
    private bool isGrounded;
    private Vector3 velocity;

    private InputAction moveAction;
    private InputAction jumpAction;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        var map = new InputActionMap("Player");
        moveAction = map.AddAction("Move");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        jumpAction = map.AddAction("Jump");
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
        map.Enable();
    }

    void Update()
    {
        PhysicsCheck();
        PlayerMove();
    }

    void PhysicsCheck()
    {

        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;
    }

    void PlayerMove()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        controller.Move(move * Speed * Time.deltaTime);

        if (isGrounded && jumpAction.WasPressedThisFrame())
            velocity.y = Mathf.Sqrt(Jump * -2f * Gravity);

        velocity.y += Gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
