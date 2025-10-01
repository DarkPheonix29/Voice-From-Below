using UnityEngine;
using System.Collections;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ActOneDirector : MonoBehaviour
{
    [Header("Start Options")]
    public bool autoStartCall = true;
    public float autoStartDelay = 0.5f;

    // Legacy input (only used if old input is enabled)
    public KeyCode debugStartKey = KeyCode.Space; // press to start if autoStartCall=false

#if ENABLE_INPUT_SYSTEM
    [Header("Input (New System)")]
    // Default binding: Space. Change in Inspector to "<Keyboard>/e" if you prefer E.
    public InputAction startCallAction = new InputAction(
        name: "StartCall",
        type: InputActionType.Button,
        binding: "<Keyboard>/space"
    );
    void OnEnable()  { startCallAction.Enable(); }
    void OnDisable() { startCallAction.Disable(); }
#endif

    [Header("Audio (optional)")]
    public AudioSource ringSource;          // loop this before call
    public AudioSource voiceSource;         // father VO (optional)
    public AudioSource sfxCollapse;         // optional
    public AudioSource sfxWetEcho;          // optional
    public AudioSource sfxStatic;           // optional
    public AudioLowPassFilter lpFilter;     // optional
    public AudioDistortionFilter distFilter;// optional
    public AudioClip routineLine;           // optional VO clip

    [Header("Subtitles")]
    [TextArea] public string[] subRoutine = { "[Thomas] Hey, I'll be home by—" };
    [TextArea] public string[] subDistort = {
        "[Thomas] Wait… what was that—",
        "*Metal shears. Rock collapses.*",
        "*Something moves in wet tunnels.*"
    };
    public float charsPerSecond = 45f;
    public float holdAfterLine = 0.8f;

    [Header("Objectives")]
    public string objectiveStart = "Answer the phone.";
    public string objectiveGrabFlashlight = "Grab your flashlight (optional).";
    public string objectiveHeadToMine = "Head to the Jokerville Mine (on foot).";

    [Header("Fade")]
    public float fadeInDuration = 1.2f;

    [Header("HUD wait (safety)")]
    [Tooltip("Max seconds to wait for PersistentHUD to spawn before continuing anyway.")]
    public float hudWaitTimeout = 2f;

    [Header("Audio Debug Helpers")]
    [Tooltip("If true, will set the ring to 2D/volume 1/loop on start so you can always hear it.")]
    public bool force2DForRingTest = true;
    [Tooltip("Try to find a ring AudioSource in the scene if not assigned.")]
    public bool autoFindRingSource = true;

    bool callStarted;

    void Start()
    {
        // Start after HUD exists (handles bootstrapper timing)
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

        if (PersistentHUD.Instance)
        {
            PersistentHUD.Instance.FadeFromBlack(fadeInDuration);
            PersistentHUD.Instance.SetObjective(objectiveStart);
        }
        else
        {
            Debug.LogWarning("[ActOneDirector] PersistentHUD.Instance not found (timeout). Proceeding without fades/objectives.");
        }

        // Ensure audio isn't globally paused
        if (AudioListener.pause) AudioListener.pause = false;

        // Find ring source if missing (nice-to-have)
        if (!ringSource && autoFindRingSource)
        {
            ringSource = TryFindRingSource();
            if (ringSource) Debug.Log($"[ActOneDirector] Auto-assigned ringSource -> {ringSource.name}");
        }

        // Start ringing (optional)
        StartRing();

        // Reset filters if you assigned them (optional)
        if (lpFilter) lpFilter.enabled = false;
        if (distFilter) distFilter.distortionLevel = 0f;

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
        // New Input System path
        if (startCallAction.triggered)
            TriggerCall();
#else
        // Old Input System path
        if (Input.GetKeyDown(debugStartKey))
            TriggerCall();
#endif
    }

    // You can call this later from a phone interactable, button, Timeline, etc.
    public void TriggerCall()
    {
        if (callStarted) return;
        callStarted = true;

        if (ringSource) ringSource.Stop();
        StartCoroutine(CallSequence());
    }

    IEnumerator CallSequence()
    {
        // Routine line (optional audio)
        if (voiceSource && routineLine)
        {
            voiceSource.clip = routineLine;
            voiceSource.Play();
        }
        yield return StartCoroutine(ShowLines(subRoutine));

        // Distortion ramp (optional)
        if (lpFilter) { lpFilter.enabled = true; lpFilter.cutoffFrequency = 2200f; }
        if (distFilter) distFilter.distortionLevel = 0.35f;

        yield return new WaitForSeconds(0.2f);
        if (sfxCollapse) sfxCollapse.Play();
        yield return new WaitForSeconds(0.35f);
        if (sfxWetEcho) sfxWetEcho.Play();

        if (lpFilter) lpFilter.cutoffFrequency = 800f;
        if (distFilter) distFilter.distortionLevel = 0.55f;

        yield return StartCoroutine(ShowLines(subDistort));

        if (sfxStatic) sfxStatic.Play();
        if (voiceSource) voiceSource.Stop();
        PersistentHUD.Instance?.ClearSubtitle();

        // Objectives
        PersistentHUD.Instance?.SetObjective(objectiveGrabFlashlight);
        yield return new WaitForSeconds(1.0f);
        PersistentHUD.Instance?.SetObjective(objectiveHeadToMine);
    }

    IEnumerator ShowLines(string[] lines)
    {
        if (lines == null || lines.Length == 0) yield break;

        foreach (var line in lines)
        {
            // Use HUD typewriter; we wait an approximate duration
            if (PersistentHUD.Instance)
            {
                PersistentHUD.Instance.ShowSubtitle(line, holdAfterLine, true);
                float dur = Mathf.Max(0.01f, line.Length / Mathf.Max(1f, charsPerSecond));
                yield return new WaitForSeconds(dur + holdAfterLine);
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Max(0.01f, line.Length / Mathf.Max(1f, charsPerSecond)) + holdAfterLine);
            }
        }
    }

    // ---------- Ring helpers ----------

    void StartRing()
    {
        if (!ringSource)
        {
            Debug.LogWarning("[ActOneDirector] ringSource not assigned; skipping ring.");
            return;
        }

        // Force safe settings for test audibility
        ringSource.loop = true;
        ringSource.playOnAwake = false;
        ringSource.mute = false;
        ringSource.volume = 1f;

        if (force2DForRingTest) ringSource.spatialBlend = 0f; // 2D so distance doesn't matter

        ringSource.Stop();
        ringSource.Play();
        StartCoroutine(VerifyRingPlaying());
    }

    IEnumerator VerifyRingPlaying()
    {
        yield return null; // wait one frame
        string clipName = ringSource && ringSource.clip ? ringSource.clip.name : "null";
        Debug.Log($"[ActOneDirector] Ring isPlaying={ringSource.isPlaying}, clip={clipName}, vol={ringSource.volume}, spatialBlend={ringSource.spatialBlend}");
    }

    AudioSource TryFindRingSource()
    {
    // 1) Try an object literally named "RingSource"
    var go = GameObject.Find("RingSource");
    if (go)
    {
        var src = go.GetComponent<AudioSource>();
        if (src) return src;
    }

    // 2) Search all AudioSources (Unity 2023+/Unity 6 uses the new API)
    #if UNITY_2023_1_OR_NEWER
    var all = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    #else
    var all = FindObjectsOfType<AudioSource>(true); // legacy path (includes inactive)
    #endif

    // Prefer ones with "ring" in object or clip name
    foreach (var a in all)
    {
        bool nameHit = a.gameObject.name.ToLower().Contains("ring");
        bool clipHit = a.clip && a.clip.name.ToLower().Contains("ring");
        if (nameHit || clipHit) return a;
    }

    // Fallback: first found
    return all.Length > 0 ? all[0] : null;
    }
}
