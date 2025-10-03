using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FPPlayer : MonoBehaviour
{
    [SerializeField] float walkSpeed = 6f;
    [SerializeField] float jumpHeight = 1f;
    [SerializeField] float gravity = -25f;

    [SerializeField] float crouchSpeedMultiplier = 0.5f;
    [SerializeField] float crouchJumpMultiplier = 0.6f;
    [SerializeField] Transform body;
    [SerializeField] float crouchScaleY = 0.6f;
    [SerializeField] float scaleLerpSpeed = 12f;
    [SerializeField] Transform playerCamera;
    [SerializeField] float cameraDropAtFull = 0.5f;

    [SerializeField] float sprintSpeedMultiplier = 1.5f;
    [SerializeField] float sprintJumpMultiplier = 1.2f;

    CharacterController controller;
    Vector3 velocity;

    InputAction move, jump, crouch, sprint;

    Renderer bodyRenderer;
    float camStartLocalY;

    public bool IsCrouching => crouch.IsPressed();
    public bool IsSprinting => sprint.IsPressed() && !IsCrouching;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        var map = new InputActionMap("Player");
        move = map.AddAction("Move");
        move.AddCompositeBinding("2DVector")
            .With("Up","<Keyboard>/w").With("Down","<Keyboard>/s")
            .With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
        jump = map.AddAction("Jump");
        jump.AddBinding("<Keyboard>/space");
        crouch = map.AddAction("Crouch");
        crouch.AddBinding("<Keyboard>/leftCtrl");
        sprint = map.AddAction("Sprint");
        sprint.AddBinding("<Keyboard>/leftShift");
        map.Enable();

        if (body) bodyRenderer = body.GetComponentInChildren<Renderer>();
        if (playerCamera) camStartLocalY = playerCamera.localPosition.y;
    }

    void Update()
    {
        bool grounded = controller.isGrounded;
        if (grounded && velocity.y < 0f) velocity.y = -2f;

        float curSpeed = walkSpeed;
        float curJump = jumpHeight;

        if (IsCrouching)
        {
            curSpeed *= crouchSpeedMultiplier;
            curJump *= crouchJumpMultiplier;
        }
        else if (IsSprinting)
        {
            curSpeed *= sprintSpeedMultiplier;
            curJump *= sprintJumpMultiplier;
        }

        Vector2 in2 = move.ReadValue<Vector2>();
        Vector3 dir = transform.right * in2.x + transform.forward * in2.y;
        controller.Move(dir * curSpeed * Time.deltaTime);

        if (grounded && jump.WasPressedThisFrame())
            velocity.y = Mathf.Sqrt(curJump * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (!bodyRenderer) return;

        float targetY = IsCrouching ? crouchScaleY : 1f;
        Vector3 s = body.localScale;
        s.y = Mathf.Lerp(s.y, targetY, Time.deltaTime * scaleLerpSpeed);
        body.localScale = s;

        float ccBottomY = transform.position.y + controller.center.y - controller.height * 0.5f;
        float epsilon = Mathf.Max(controller.skinWidth, 0.01f);
        float desiredBottomY = ccBottomY + epsilon;

        float currentBottomY = bodyRenderer.bounds.min.y;
        float delta = desiredBottomY - currentBottomY;
        if (Mathf.Abs(delta) > 0.0001f) body.position += Vector3.up * delta;

        if (!playerCamera) return;

        float crouchPercent = 1f - s.y;
        float targetCamY = camStartLocalY - (crouchPercent / (1f - crouchScaleY)) * cameraDropAtFull;
        Vector3 p = playerCamera.localPosition;
        p.y = Mathf.Lerp(p.y, targetCamY, Time.deltaTime * scaleLerpSpeed);
        playerCamera.localPosition = p;
    }
}