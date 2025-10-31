using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Door : MonoBehaviour
{
    public enum DoorState { Closed, Opening, Open, Closing, Locked }

    [Header("References")]
    public Interactable interactable;             // auto-found if null
    public Transform hinge;                       // the rotating part (defaults to self)
    public SFXRouterSimple sfxRouter;             // universal sound router

    [Header("SFX IDs")]
    public string sfxOpen = "Open";
    public string sfxClose = "Close";
    public string sfxLocked = "Locked";

    [Header("Door State")]
    public DoorState state = DoorState.Closed;
    public bool startLocked = false;

    [Header("Rotation Settings")]
    public Vector3 closedLocalEuler = Vector3.zero;
    public Vector3 openLocalEuler = new Vector3(0, 90, 0);
    public float swingTime = 0.35f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Auto Close")]
    public bool autoClose = false;
    public float autoCloseDelay = 5f;

    Coroutine motionCo;

    void Awake()
    {
        if (!hinge) hinge = transform;
        if (!interactable) interactable = GetComponent<Interactable>();
        if (!sfxRouter) sfxRouter = GetComponent<SFXRouterSimple>();

        // capture starting rotation
        if (closedLocalEuler == Vector3.zero)
            closedLocalEuler = hinge.localEulerAngles;

        state = startLocked ? DoorState.Locked : DoorState.Closed;
        SetHingeInstant();
        UpdatePrompt();

        // auto-link Interactable to Door.Interact
        if (interactable)
        {
            interactable.onInteract.RemoveListener(Interact);
            interactable.onInteract.AddListener(Interact);
        }
    }

    void OnDisable()
    {
        if (interactable)
            interactable.onInteract.RemoveListener(Interact);
    }

    public void Interact()
    {
        switch (state)
        {
            case DoorState.Locked:
                PlaySFX(sfxLocked);
                ShortPrompt("Locked");
                break;

            case DoorState.Closed:
                Open();
                break;

            case DoorState.Open:
                Close();
                break;

            default:
                break;
        }
    }

    public void Open()
    {
        if (state != DoorState.Closed) return;

        PlaySFX(sfxOpen);

        if (motionCo != null) StopCoroutine(motionCo);
        motionCo = StartCoroutine(RotateDoor(
            closedLocalEuler,
            closedLocalEuler + openLocalEuler,
            DoorState.Opening,
            DoorState.Open
        ));

        if (autoClose) Invoke(nameof(Close), autoCloseDelay);
    }

    public void Close()
    {
        if (state != DoorState.Open) return;

        PlaySFX(sfxClose);

        if (motionCo != null) StopCoroutine(motionCo);
        motionCo = StartCoroutine(RotateDoor(
            closedLocalEuler + openLocalEuler,
            closedLocalEuler,
            DoorState.Closing,
            DoorState.Closed
        ));
    }

    IEnumerator RotateDoor(Vector3 from, Vector3 to, DoorState phase, DoorState done)
    {
        state = phase;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, swingTime);
            hinge.localEulerAngles = Vector3.LerpUnclamped(from, to, ease.Evaluate(t));
            yield return null;
        }

        hinge.localEulerAngles = to;
        state = done;
        UpdatePrompt();
    }

    void SetHingeInstant()
    {
        hinge.localEulerAngles = (state == DoorState.Open)
            ? closedLocalEuler + openLocalEuler
            : closedLocalEuler;
    }

    void PlaySFX(string id)
    {
        if (sfxRouter && !string.IsNullOrEmpty(id))
            sfxRouter.PlaySFX(id);
    }

    void UpdatePrompt()
    {
        if (!interactable) return;

        switch (state)
        {
            case DoorState.Locked: interactable.SetPromptText("Locked"); break;
            case DoorState.Closed: interactable.SetPromptText("Press E to open"); break;
            case DoorState.Open:   interactable.SetPromptText("Press E to close"); break;
            default:               interactable.SetPromptText("Press E"); break;
        }
    }

    void ShortPrompt(string text)
    {
        if (!interactable) return;
        interactable.SetPromptText(text);
        CancelInvoke(nameof(RefreshPrompt));
        Invoke(nameof(RefreshPrompt), 1f);
    }

    void RefreshPrompt() => UpdatePrompt();
}
