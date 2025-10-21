using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class FPPlayer : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    public float crouchSpeed = 3f;
    public float jumpHeight = 1f;
    public float gravity = -25f;

    [Header("References")]
    [SerializeField] Transform body;
    [SerializeField] Transform cameraTransform;  
    [SerializeField] Slider staminaBar;
    [SerializeField] Image staminaFill;
    [SerializeField] CanvasGroup staminaGroup;

    [Header("Crouch (CTRL vasthouden)")]
    [SerializeField] float bodyCrouchScaleY = 0.6f;
    [SerializeField] float scaleLerpSpeed = 12f;
    [SerializeField] float camCrouchOffset = -0.4f;
    [SerializeField] float camLerpSpeed = 12f;

    [SerializeField] float controllerCrouchMultiplier = 0.6f;
    [SerializeField] float controllerLerpSpeed = 14f;
    [SerializeField] LayerMask ceilingMask = ~0;

    [Header("Stamina")]
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float staminaDrainRate = 25f;
    [SerializeField] float staminaRegenRate = 20f;
    [SerializeField] float minStaminaToSprint = 10f;
    [SerializeField] float staminaUISpeed = 10f;

    [Header("Colliders (code-only fix)")]
    [SerializeField] bool autoDisableChildColliders = true;

    [Header("Anti-glitch")]
    [SerializeField] int standClearFramesRequired = 2;
    [SerializeField] float standCheckBuffer = 0.02f;

    CharacterController controller;
    Vector3 velocity;
    float stamina;
    bool isGrounded;
    bool isCrouching;
    bool isSprinting;

    InputAction move, jump, crouch, sprint;

    Vector3 bodyScaleStart;
    float camBaseLocalY;

    float standHeight;
    Vector3 standCenter;
    float crouchHeight;
    Vector3 crouchCenter;

    Collider[] childCols;
    int standClearFrames;
    bool initialized;

    // camera-pivot (nieuw)
    Transform camPivot;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = maxStamina;

        // Input
        var map = new InputActionMap("Player");
        move = map.AddAction("Move");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        jump   = map.AddAction("Jump", binding: "<Keyboard>/space");
        crouch = map.AddAction("Crouch", binding: "<Keyboard>/leftCtrl");
        sprint = map.AddAction("Sprint", binding: "<Keyboard>/leftShift");
        map.Enable();

        // UI
        if (staminaBar) { staminaBar.minValue = 0f; staminaBar.maxValue = maxStamina; staminaBar.value = maxStamina; }
        if (body) bodyScaleStart = body.localScale;

        // --- Camera pivot setup ---
        if (cameraTransform == null)
        {
            var mainCam = Camera.main;
            if (mainCam) cameraTransform = mainCam.transform;
        }
        if (cameraTransform != null)
        {
            camBaseLocalY = cameraTransform.localPosition.y;
            EnsureCameraPivot(); // maakt/zet pivot en herparent de camera
        }

        if (staminaGroup) staminaGroup.alpha = 0f;

        // Capsule-cache
        standHeight = controller.height;
        standCenter = controller.center;
        crouchHeight = Mathf.Max(controller.radius * 2f + 0.05f, standHeight * controllerCrouchMultiplier);
        float deltaH = standHeight - crouchHeight;
        crouchCenter = standCenter + new Vector3(0f, -deltaH * 0.5f, 0f);

        // child colliders uit
        if (autoDisableChildColliders)
        {
            childCols = GetComponentsInChildren<Collider>(true);
            foreach (var col in childCols)
            {
                if (!col) continue;
                if (col.gameObject == gameObject) continue;
                if (!col.isTrigger) col.enabled = false;
            }
        }

        // eigen layer uit mask
        ceilingMask &= ~(1 << gameObject.layer);
    }

    void Start() { StartCoroutine(DelayedInit()); }
    IEnumerator DelayedInit() { yield return null; initialized = true; }

    void Update()
    {
        if (!initialized) return;

        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f) velocity.y = -2f;

        Vector2 input = move.ReadValue<Vector2>();
        Vector3 moveDir = transform.right * input.x + transform.forward * input.y;

        HandleCrouch();
        HandleSprint(input);

        float currentSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
        controller.Move(moveDir * currentSpeed * Time.deltaTime);

        if (isGrounded && jump.WasPressedThisFrame() && !isCrouching)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        UpdateStaminaUI();
        UpdateBodyAndCamera();
        UpdateControllerCapsule();
    }

    void HandleCrouch()
    {
        bool wantsToCrouch = crouch.IsPressed();

        if (!wantsToCrouch)
        {
            if (StandSpaceBlocked()) { wantsToCrouch = true; standClearFrames = 0; }
            else { standClearFrames++; if (standClearFrames < standClearFramesRequired) wantsToCrouch = true; }
        }
        else standClearFrames = 0;

        isCrouching = wantsToCrouch;
    }

    bool StandSpaceBlocked()
    {
        float r = Mathf.Max(0.0f, controller.radius - 0.005f);
        float h = standHeight + standCheckBuffer;
        Vector3 worldCenter = transform.position + transform.rotation * standCenter;
        float half = h * 0.5f - r;
        Vector3 bottom = worldCenter + Vector3.up * (-half);
        Vector3 top    = worldCenter + Vector3.up * ( half);
        return Physics.CheckCapsule(bottom, top, r, ceilingMask, QueryTriggerInteraction.Ignore);
    }

    void UpdateBodyAndCamera()
    {
        // Visual body scale
        if (body)
        {
            float targetY = isCrouching ? bodyCrouchScaleY : 1f;
            Vector3 s = body.localScale; s.y = Mathf.Lerp(s.y, targetY, Time.deltaTime * scaleLerpSpeed);
            body.localScale = s;
        }

        // Camera: verplaats de PIVOT in Y; camera zelf doet head-bob daarbovenop
        if (camPivot != null)
        {
            float goal = (isCrouching ? camCrouchOffset : 0f);
            Vector3 p = camPivot.localPosition;
            p.y = Mathf.Lerp(p.y, goal, Time.deltaTime * camLerpSpeed);
            camPivot.localPosition = p;
        }
        else if (cameraTransform != null)
        {
            // fallback (mocht pivot niet bestaan)
            Vector3 p = cameraTransform.localPosition;
            float goalY = camBaseLocalY + (isCrouching ? camCrouchOffset : 0f);
            p.y = Mathf.Lerp(p.y, goalY, Time.deltaTime * camLerpSpeed);
            cameraTransform.localPosition = p;
        }
    }

    void UpdateControllerCapsule()
    {
        bool forceCrouchTargets = isCrouching || StandSpaceBlocked();
        float targetHeight = forceCrouchTargets ? crouchHeight : standHeight;
        Vector3 targetCenter = forceCrouchTargets ? crouchCenter : standCenter;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * controllerLerpSpeed);
        controller.center = Vector3.Lerp(controller.center, targetCenter, Time.deltaTime * controllerLerpSpeed);
    }

    void HandleSprint(Vector2 input)
    {
        bool moving = input.sqrMagnitude > 0.01f;
        bool canSprint = stamina > minStaminaToSprint && moving && !isCrouching;
        bool wants = sprint.IsPressed();

        isSprinting = (stamina > 0f) && canSprint && wants;

        if (isSprinting) stamina = Mathf.Max(0f, stamina - staminaDrainRate * Time.deltaTime);
        else stamina = Mathf.Min(maxStamina, stamina + staminaRegenRate * Time.deltaTime);
    }

    void UpdateStaminaUI()
    {
        if (!staminaBar) return;
        float target = Mathf.Clamp(stamina, 0f, maxStamina);
        staminaBar.value = Mathf.MoveTowards(staminaBar.value, target, Time.deltaTime * staminaUISpeed * maxStamina);
        if (staminaFill) staminaFill.enabled = staminaBar.value > 0.001f;
        if (staminaGroup)
        {
            bool show = stamina < maxStamina - 0.01f;
            float goal = show ? 1f : 0f;
            staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, goal, Time.deltaTime * 8f);
        }
    }

    public bool IsCrouching => isCrouching;
    public bool IsSprinting => isSprinting;

    // ---------- helpers ----------
    void EnsureCameraPivot()
    {
        // Als camera al een parent heeft dat niet de Player is, laat zo.
        // Anders maken we een pivot onder de Player en hangen daar de camera in.
        if (cameraTransform == null) return;

        if (cameraTransform.parent == null || cameraTransform.parent == transform)
        {
            // Maak/zoek pivot als direct child "CameraPivot"
            var existing = transform.Find("CameraPivot");
            camPivot = existing != null ? existing : new GameObject("CameraPivot").transform;
            camPivot.SetParent(transform, false);
            camPivot.localPosition = Vector3.zero;
            camPivot.localRotation = Quaternion.identity;

            // Herparent camera onder pivot en behoud lokale offset
            cameraTransform.SetParent(camPivot, true);
        }
        else
        {
            camPivot = cameraTransform.parent;
        }
    }
}
