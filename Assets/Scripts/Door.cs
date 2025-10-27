using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Door : MonoBehaviour
{
    public enum DoorState { Closed, Opening, Open, Closing, Locked }

    [Header("Refs")]
    [Tooltip("If null, will search on this GameObject.")]
    public Interactable interactable;
    [Tooltip("Transform that rotates (typically the door mesh). If null, uses this transform.")]
    public Transform hinge;

    [Header("State")]
    public DoorState state = DoorState.Closed;
    [Tooltip("Start locked?")]
    public bool startLocked = false;

    [Header("Lock / Key (optional)")]
    [Tooltip("If set, the door unlocks when SaveFlags.Has(keyFlagId) is true.")]
    public string keyFlagId = "";          // e.g. "Key_HouseA"
    public bool consumeKeyOnUnlock = false;
    [Tooltip("Optional persistent door flag (e.g. \"Door_House1_Locked\"). If provided, lock state can persist via SaveFlags.")]
    public string lockPersistFlag = "";

    [Header("Motion")]
    [Tooltip("Local euler rotation for CLOSED state (captured on Awake if left as 0,0,0).")]
    public Vector3 closedLocalEuler;
    [Tooltip("Local euler rotation for OPEN state, relative to closed.")]
    public Vector3 openLocalEuler = new Vector3(0, 90, 0);
    [Tooltip("Time to complete open/close")]
    public float swingTime = 0.35f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Auto Close")]
    public bool autoClose = false;
    public float autoCloseDelay = 6f;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip sfxOpen;
    public AudioClip sfxClose;
    public AudioClip sfxLocked;

    Coroutine motionCo;

    void Awake()
    {
        if (!hinge) hinge = transform;
        if (!interactable) interactable = GetComponent<Interactable>();

        // Capture closed rotation if not set
        if (closedLocalEuler == Vector3.zero)
            closedLocalEuler = hinge.localEulerAngles;

        // Load persisted lock (optional)
        if (!string.IsNullOrEmpty(lockPersistFlag) && SaveFlags.Instance && SaveFlags.Instance.Has(lockPersistFlag))
            startLocked = true;

        state = startLocked ? DoorState.Locked : DoorState.Closed;
        UpdatePrompt();
        SetHingeToStateInstant();
    }

    void OnEnable() => UpdatePrompt();

    // Hook this up in Interactable.onInteract OR call from another script
    public void Interact()
    {
        switch (state)
        {
            case DoorState.Locked:
                TryUnlockOrBump();
                break;

            case DoorState.Closed:
                Open();
                break;

            case DoorState.Open:
                Close();
                break;

            // ignore while mid-motion
            case DoorState.Opening:
            case DoorState.Closing:
                break;
        }
    }

    // --- Public helpers you can call from other scripts if needed ---
    public void Lock()
    {
        if (motionCo != null) StopCoroutine(motionCo);
        state = DoorState.Locked;
        if (!string.IsNullOrEmpty(lockPersistFlag) && SaveFlags.Instance)
            SaveFlags.Instance.Set(lockPersistFlag);
        UpdatePrompt();
    }

    public void Unlock()
    {
        if (state == DoorState.Locked)
        {
            state = DoorState.Closed;
            UpdatePrompt();
        }
    }

    public void Open()
    {
        if (state != DoorState.Closed) return;
        if (motionCo != null) StopCoroutine(motionCo);
        motionCo = StartCoroutine(RotateDoor(closedLocalEuler, closedLocalEuler + openLocalEuler, DoorState.Opening, DoorState.Open, sfxOpen));

        if (autoClose) Invoke(nameof(Close), autoCloseDelay);
    }

    public void Close()
    {
        if (state != DoorState.Open) return;
        if (motionCo != null) StopCoroutine(motionCo);
        motionCo = StartCoroutine(RotateDoor(closedLocalEuler + openLocalEuler, closedLocalEuler, DoorState.Closing, DoorState.Closed, sfxClose));
    }

    // --- Internals ---
    void TryUnlockOrBump()
    {
        // Have a key?
        if (!string.IsNullOrEmpty(keyFlagId) && SaveFlags.Instance && SaveFlags.Instance.Has(keyFlagId))
        {
            if (consumeKeyOnUnlock)
            {
                // If you want to consume, add a Remove method to SaveFlags or keep a separate inventory.
                // For now we just unlock without consuming.
            }
            Unlock();
            Open();
            return;
        }

        // Play "locked" feedback
        if (audioSource && sfxLocked) audioSource.PlayOneShot(sfxLocked);
        ShortPrompt("Locked");
    }

    IEnumerator RotateDoor(Vector3 from, Vector3 to, DoorState phase, DoorState done, AudioClip sfx)
    {
        state = phase;
        if (audioSource && sfx) audioSource.PlayOneShot(sfx);

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

    void SetHingeToStateInstant()
    {
        if (state == DoorState.Open)
            hinge.localEulerAngles = closedLocalEuler + openLocalEuler;
        else
            hinge.localEulerAngles = closedLocalEuler;
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
        Invoke(nameof(RefreshPrompt), 0.8f);
    }

    void RefreshPrompt() => UpdatePrompt();
}
