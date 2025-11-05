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

    [Header("Voice Lines")]
    public AudioClip voWalkieFound;
    [TextArea] public string subWalkieFound = "[Radio] *You found the walkie.*";

    public AudioClip voLeverMissing;
    [TextArea] public string subLeverMissing = "[You] I need a lever handle for this.";

    public AudioClip voLeverInstalled;
    [TextArea] public string subLeverInstalled = "[You] That should do it.";

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

    AudioSource PickVoSource(AudioSource preferred)
    {
        // Use preferred if assigned; else fallback to camera; else last resort create one on this GO
        if (preferred) return preferred;
        if (_fallback2D) return _fallback2D;

        var src = GetComponent<AudioSource>();
        if (!src) src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        return src;
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

    public void OnWalkieFound()
    {
        if (SaveFlags.Instance) SaveFlags.Instance.Set(walkieFoundFlag);
        StopPingLoop();

        if (voWalkieFound || !string.IsNullOrEmpty(subWalkieFound))
        {
            VoiceLineQueue.Instance?.Enqueue(new VoiceLine
            {
                clip = voWalkieFound,
                subtitle = subWalkieFound,
                extraHold = extraHold,
                overrideSource = PickVoSource(walkieSource) // use walkie if set, else fallback
            });
        }
        else
        {
            // nothing to play—clear any stale subtitle just in case
            PersistentHUD.Instance?.ClearSubtitle();
        }
    }

    public void OnLeverMissingAttempt()
    {
        if (voLeverMissing || !string.IsNullOrEmpty(subLeverMissing))
        {
            VoiceLineQueue.Instance?.Enqueue(new VoiceLine
            {
                clip = voLeverMissing,
                subtitle = subLeverMissing,
                extraHold = extraHold,
                overrideSource = PickVoSource(null) // use default/fallback
            });
        }
        else
        {
            PersistentHUD.Instance?.ClearSubtitle();
        }
    }

    public void OnLeverInstalledThenTransfer(LeverBase lever)
    {
        if (SaveFlags.Instance) SaveFlags.Instance.Set(leverInstalledFlag);
        StartCoroutine(LeverInstalledFlow(lever));
    }

    IEnumerator LeverInstalledFlow(LeverBase lever)
    {
        // 1) VO: Lever installed
        if (voLeverInstalled || !string.IsNullOrEmpty(subLeverInstalled))
        {
            VoiceLineQueue.Instance?.Enqueue(new VoiceLine
            {
                clip = voLeverInstalled,
                subtitle = subLeverInstalled,
                extraHold = extraHold,
                overrideSource = PickVoSource(null)
            });
        }

        // Wait until all queued lines finish (this also ensures subtitles clear at the end)
        if (VoiceLineQueue.Instance != null)
            yield return VoiceLineQueue.Instance.WaitUntilIdle();
        else
            PersistentHUD.Instance?.ClearSubtitle(); // safety

        // 2) Elevator moving SFX
        var src = elevatorSource ? elevatorSource : PickVoSource(null);
        if (src && elevatorMoveClip) src.PlayOneShot(elevatorMoveClip);

        // tiny delay to let rumble start (optional)
        yield return new WaitForSeconds(0.2f);

        // 3) Transfer
        if (lever != null)
            lever.PullAndTransfer();
    }
}
