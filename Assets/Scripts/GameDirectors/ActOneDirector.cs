using UnityEngine;
using System.Collections;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ActOneDirector : MonoBehaviour
{
    [Header("Persistence")]
    [Tooltip("Unique flag for the opening phone call having completed.")]
    public string callCompletedFlag = "Act1_OpeningCallDone";

    [Header("Start Options")]
    public bool autoStartCall = true;
    public float autoStartDelay = 0.5f;

    // Legacy input (only used if old input is enabled)
    public KeyCode debugStartKey = KeyCode.Space; // use only if autoStartCall=false

#if ENABLE_INPUT_SYSTEM
    [Header("Input (New System)")]
    // Change binding in Inspector if you prefer "<Keyboard>/e"
    public InputAction startCallAction = new InputAction(
        name: "StartCall",
        type: InputActionType.Button,
        binding: "<Keyboard>/space"
    );
    void OnEnable()  { startCallAction.Enable(); }
    void OnDisable() { startCallAction.Disable(); }
#endif

    [Header("Phone Call")]
    public AudioSource voiceSource;        // where the phone VO plays from
    public AudioSource ringSource;         // optional: ringing before pickup
    public AudioClip callClip;             // the phone call VO line
    [TextArea] public string callSubtitle; // subtitle for the call
    public float subtitleHoldAfterLine = 0.6f; // small extra hold

    [Header("Objectives")]
    public string objectiveAtStart = "Answer the phone.";
    public string objectiveAfterCall = "Grab your flashlight (optional).";

    [Header("HUD wait (safety)")]
    public float hudWaitTimeout = 2f;

    bool callStarted;

    void Start()
    {
        StartCoroutine(BeginAfterHUD());
    }

    IEnumerator BeginAfterHUD()
    {
        float waited = 0f;
        while (PersistentHUD.Instance == null && waited < hudWaitTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        bool callAlreadyDone = SaveFlags.Instance && SaveFlags.Instance.Has(callCompletedFlag);

        if (PersistentHUD.Instance)
        {
            PersistentHUD.Instance.FadeFromBlack();

            if (callAlreadyDone)
            {
                // Skip ringing; restore post-call objective.
                PersistentHUD.Instance.SetObjective(objectiveAfterCall);
            }
            else
            {
                PersistentHUD.Instance.SetObjective(objectiveAtStart);
            }
        }
        else
        {
            Debug.LogWarning("[ActOneDirector] HUD not found (timeout). Proceeding.");
        }

        if (callAlreadyDone)
        {
            if (ringSource) ringSource.Stop();
            yield break;
        }

        // optional ring
        if (ringSource)
        {
            ringSource.loop = true;
            if (!ringSource.isPlaying) ringSource.Play();
        }

        // configure queue defaults
        if (VoiceLineQueue.Instance)
            VoiceLineQueue.Instance.defaultVoiceSource = voiceSource;

        if (autoStartCall) StartCoroutine(AutoStart());
    }

    IEnumerator AutoStart()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, autoStartDelay));
        TriggerCall();
    }

    void Update()
    {
        if (callStarted || autoStartCall) return;

#if ENABLE_INPUT_SYSTEM
        if (startCallAction.triggered) TriggerCall();
#else
        if (Input.GetKeyDown(debugStartKey)) TriggerCall();
#endif
    }

    // Call from interactable/button/Timeline/etc.
    public void TriggerCall()
    {
        // Ignore if already started or already completed (session or saved).
        if (callStarted) return;
        if (SaveFlags.Instance && SaveFlags.Instance.Has(callCompletedFlag)) return;

        callStarted = true;

        if (ringSource) ringSource.Stop();
        StartCoroutine(CallFlow());
    }

    IEnumerator CallFlow()
    {
        // Build a single voice line (clip + subtitle)
        var line = new VoiceLine
        {
            clip = callClip,
            subtitle = callSubtitle,
            extraHold = subtitleHoldAfterLine,
            overrideSource = voiceSource
        };

        // Enqueue for playback (subtitles will auto-clear after the line)
        VoiceLineQueue.Instance?.Enqueue(line);

        // Change objective after the line finishes
        if (callClip) yield return new WaitForSeconds(callClip.length + subtitleHoldAfterLine);

        // Mark complete IN SESSION (won't persist until SaveFlags.Commit())
        if (SaveFlags.Instance) SaveFlags.Instance.Set(callCompletedFlag);

        PersistentHUD.Instance?.SetObjective(objectiveAfterCall);
    }
}
