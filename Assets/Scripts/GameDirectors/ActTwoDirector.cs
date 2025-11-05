using System.Collections;
using UnityEngine;

public class ActTwoDirector : MonoBehaviour
{
    public static ActTwoDirector Instance { get; private set; }

    [Header("Flags (SaveFlags)")]
    public string walkieFoundFlag = "Act2_WalkieFound";
    public string leverInstalledFlag = "Act2_LeverInstalled";

    [Header("Walkie Talkie")]
    [Tooltip("AudioSource used to play the periodic walkie ping (PlayOneShot). "
           + "If null, we'll fall back to a camera 2D source for VO, but pings will be skipped.")]
    public AudioSource walkieSource;
    public AudioClip walkiePingClip;
    public float walkiePingInterval = 8f;
    public float walkiePingJitter = 0.75f;

    // NEW: reference so we can force visibility while VO plays
    [Tooltip("Optional: link to the WalkieTalkie component so we can force show it during VO.")]
    public WalkieTalkie walkieRef;

    // NEW: control VO routing + visibility behavior
    [Header("Walkie VO Routing & Visibility")]
    [Tooltip("Default route for VO when a line is marked 'via Walkie'. If true, use walkieSource; else fallback 2D.")]
    public bool routeWalkieVOThroughWalkie = true;

    [Tooltip("If true, force the walkie to be visible while a VO line marked 'via Walkie' is playing.")]
    public bool makeWalkieVisibleOnWalkieVO = true;

    [Tooltip("Extra seconds to keep the walkie visible after a VO line ends.")]
    public float walkieVisibilityExtraHold = 0.5f;

    [Header("Voice Lines")]
    public AudioClip voWalkieFound;
    [TextArea] public string subWalkieFound = "[Radio] *You found the walkie.*";
    // NEW: per-line toggle: is this line spoken over the walkie?
    public bool voWalkieFoundViaWalkie = true;

    public AudioClip voLeverMissing;
    [TextArea] public string subLeverMissing = "[You] I need a lever handle for this.";
    public bool voLeverMissingViaWalkie = false; // probably the player speaking, not radio

    public AudioClip voLeverInstalled;
    [TextArea] public string subLeverInstalled = "[You] That should do it.";
    public bool voLeverInstalledViaWalkie = false;

    [Header("Elevator")]
    public AudioSource elevatorSource;
    public AudioClip elevatorMoveClip;

    [Header("Subtitle pacing")]
    public float extraHold = 0.6f;

    // internal
    Coroutine pingLoop;
    AudioSource _fallback2D;   // camera-based 2D source used for VO if needed

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Ensure we always have a valid VO source and set it for the queue
        _fallback2D = GetOrMakeCamera2DSource();

        if (VoiceLineQueue.Instance)
        {
            // Prefer walkieSource for default if assigned, else camera fallback
            VoiceLineQueue.Instance.defaultVoiceSource = walkieSource ? walkieSource : _fallback2D;
        }

        // Start/stop ping loop depending on whether walkie already found (saved or in-session)
        bool alreadyFound = SaveFlags.Instance && SaveFlags.Instance.Has(walkieFoundFlag);
        if (!alreadyFound) StartPingLoop();
    }

    // ---------- helpers ----------
    AudioSource GetOrMakeCamera2DSource()
    {
        var cam = Camera.main;
        if (!cam) return null;

        var src = cam.GetComponent<AudioSource>();
        if (!src) src = cam.gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;  // 2D so it follows player
        src.playOnAwake = false;
        return src;
    }

    AudioSource PickVoSourceForWalkieFlag(bool viaWalkie)
    {
        if (viaWalkie && routeWalkieVOThroughWalkie && walkieSource)
            return walkieSource;

        // otherwise use fallback / queue default
        if (_fallback2D) return _fallback2D;

        var src = GetComponent<AudioSource>();
        if (!src) src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        return src;
    }

    float EstimateClipDuration(AudioClip clip, float minimum = 0.5f)
    {
        if (!clip) return minimum;
        return Mathf.Max(minimum, clip.length);
    }

    // --- PING LOOP ------------------------------------------------------------
    public void StartPingLoop()
    {
        if (pingLoop != null) StopCoroutine(pingLoop);
        pingLoop = StartCoroutine(PingRoutine());
    }

    public void StopPingLoop()
    {
        if (pingLoop != null)
        {
            StopCoroutine(pingLoop);
            pingLoop = null;
        }
    }

    IEnumerator PingRoutine()
    {
        // Keep pinging until the walkie is found
        while (!(SaveFlags.Instance && SaveFlags.Instance.Has(walkieFoundFlag)))
        {
            if (walkieSource && walkiePingClip)
                walkieSource.PlayOneShot(walkiePingClip);

            float jitter = Random.Range(-walkiePingJitter, walkiePingJitter);
            float wait = Mathf.Max(0.25f, walkiePingInterval + jitter);
            yield return new WaitForSeconds(wait);
        }
    }

    // --- PUBLIC HOOKS ---------------------------------------------------------

    // CENTRAL helper to enqueue VO (with optional "via walkie" behavior)
    void EnqueueVO(AudioClip clip, string subtitle, bool viaWalkie)
    {
        if (string.IsNullOrEmpty(subtitle) && clip == null)
        {
            PersistentHUD.Instance?.ClearSubtitle();
            return;
        }

        var src = PickVoSourceForWalkieFlag(viaWalkie);

        // Make the physical walkie visible while the VO plays (if requested)
        if (viaWalkie && makeWalkieVisibleOnWalkieVO)
        {
            var visDur = EstimateClipDuration(clip, 0.75f) + extraHold + walkieVisibilityExtraHold;
            if (!walkieRef)
            {
                // Try to find a walkie in the scene if not assigned
                walkieRef = FindObjectOfType<WalkieTalkie>(includeInactive: true);
            }
            if (walkieRef) walkieRef.ForceShow(visDur);
        }

        VoiceLineQueue.Instance?.Enqueue(new VoiceLine
        {
            clip = clip,
            subtitle = subtitle,
            extraHold = extraHold,
            overrideSource = src
        });
    }

    public void OnWalkieFound()
    {
        if (SaveFlags.Instance) SaveFlags.Instance.Set(walkieFoundFlag);
        StopPingLoop();

        EnqueueVO(voWalkieFound, subWalkieFound, viaWalkie: voWalkieFoundViaWalkie);
    }

    public void OnLeverMissingAttempt()
    {
        EnqueueVO(voLeverMissing, subLeverMissing, viaWalkie: voLeverMissingViaWalkie);
    }

    public void OnLeverInstalledThenTransfer(LeverBase lever)
    {
        if (SaveFlags.Instance) SaveFlags.Instance.Set(leverInstalledFlag);
        StartCoroutine(LeverInstalledFlow(lever));
    }

    IEnumerator LeverInstalledFlow(LeverBase lever)
    {
        // 1) VO
        EnqueueVO(voLeverInstalled, subLeverInstalled, viaWalkie: voLeverInstalledViaWalkie);

        // 2) Wait until VO queue is empty (and ensures subtitles clear)
        if (VoiceLineQueue.Instance != null)
            yield return VoiceLineQueue.Instance.WaitUntilIdle();
        else
            PersistentHUD.Instance?.ClearSubtitle();

        // 3) Elevator SFX
        var src = elevatorSource ? elevatorSource : PickVoSourceForWalkieFlag(false);
        if (src && elevatorMoveClip) src.PlayOneShot(elevatorMoveClip);

        yield return new WaitForSeconds(0.2f);

        // 4) Transfer
        if (lever != null)
            lever.PullAndTransfer();
    }
}
