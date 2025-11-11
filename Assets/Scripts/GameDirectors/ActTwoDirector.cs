using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ActTwoDirector : MonoBehaviour
{
    public static ActTwoDirector Instance { get; private set; }

    [Header("Flags (SaveFlags)")]
    public string walkieFoundFlag = "Act2_WalkieFound";
    public string leverInstalledFlag = "Act2_LeverInstalled";

    [Header("Walkie Talkie")]
    [Tooltip("AudioSource used to play the periodic walkie ping (PlayOneShot). "
           + "If null, we'll fall back to a 2D persistent/camera source so pings and VO keep working.")]
    public AudioSource walkieSource;
    public AudioClip walkiePingClip;
    public float walkiePingInterval = 8f;
    public float walkiePingJitter = 0.75f;

    [Tooltip("Optional: link to the WalkieTalkie component so we can force show it during VO.")]
    public WalkieTalkie walkieRef;

    [Header("Walkie VO Routing & Visibility")]
    public bool routeWalkieVOThroughWalkie = true;
    public bool makeWalkieVisibleOnWalkieVO = true;
    public float walkieVisibilityExtraHold = 0.5f;

    [Header("Voice Lines")]
    public AudioClip voWalkieFound;
    [TextArea] public string subWalkieFound = "[Radio] *You found the walkie.*";
    public bool voWalkieFoundViaWalkie = true;

    public AudioClip voLeverMissing;
    [TextArea] public string subLeverMissing = "[You] I need a lever handle for this.";
    public bool voLeverMissingViaWalkie = false;

    public AudioClip voLeverInstalled;
    [TextArea] public string subLeverInstalled = "[You] That should do it.";
    public bool voLeverInstalledViaWalkie = false;

    [Header("Elevator")]
    public AudioSource elevatorSource;
    public AudioClip elevatorMoveClip;

    [Header("Subtitle pacing")]
    public float extraHold = 0.6f;

    [Header("Scene gating for walkie ping")]
    [Tooltip("If true, the periodic walkie ping only plays in the scenes listed below.")]
    public bool limitWalkiePingToSpecificScenes = true;

    [Tooltip("Scene names where the walkie ping is allowed. Example: \"Level2\".")]
    public string[] pingEnabledScenes = new[] { "Level2" };

    // internal
    Coroutine pingLoop;
    AudioSource _fallback2D;           // camera-based 2D source (scene)
    AudioSource _persistentWalkie2D;   // DDOL 2D source (always valid)
    bool _walkieFoundAnnounced = false;

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _persistentWalkie2D = gameObject.AddComponent<AudioSource>();
        _persistentWalkie2D.playOnAwake = false;
        _persistentWalkie2D.spatialBlend = 0f; // 2D
    }

    void Start()
    {
        RefreshAudioBindings();
        MaybeStartOrStopPingBasedOnState();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshAudioBindings();
        // Start/stop the ping depending on the new scene and flag state
        MaybeStartOrStopPingBasedOnState();
    }

    // ---------- helpers ----------
    void RefreshAudioBindings()
    {
        _fallback2D = GetOrMakeCamera2DSource();

        if (!walkieRef)
            walkieRef = FindObjectOfType<WalkieTalkie>(includeInactive: true);

        if (!walkieSource || (walkieSource && walkieSource.gameObject == null))
        {
            if (walkieRef)
            {
                var src = walkieRef.GetComponent<AudioSource>();
                if (!src) src = walkieRef.gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f; // 3D on the prop
                walkieSource = src;
            }
            else
            {
                walkieSource = null;
            }
        }

        if (VoiceLineQueue.Instance)
        {
            VoiceLineQueue.Instance.defaultVoiceSource =
                walkieSource ? walkieSource :
                (_persistentWalkie2D ? _persistentWalkie2D : _fallback2D);
        }
    }

    AudioSource GetOrMakeCamera2DSource()
    {
        var cam = Camera.main;
        if (!cam) return null;

        var src = cam.GetComponent<AudioSource>();
        if (!src) src = cam.gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;  // 2D
        src.playOnAwake = false;
        return src;
    }

    AudioSource PickVoSourceForWalkieFlag(bool viaWalkie)
    {
        if (viaWalkie && routeWalkieVOThroughWalkie)
        {
            if (walkieSource) return walkieSource;               // 3D on prop
            if (_persistentWalkie2D) return _persistentWalkie2D; // DDOL 2D
        }

        if (_fallback2D) return _fallback2D;                     // camera 2D
        if (!_persistentWalkie2D)
        {
            _persistentWalkie2D = gameObject.AddComponent<AudioSource>();
            _persistentWalkie2D.playOnAwake = false;
            _persistentWalkie2D.spatialBlend = 0f;
        }
        return _persistentWalkie2D;
    }

    float EstimateClipDuration(AudioClip clip, float minimum = 0.5f)
        => Mathf.Max(minimum, clip ? clip.length : 0f);

    bool IsPingAllowedInCurrentScene()
    {
        if (!limitWalkiePingToSpecificScenes) return true;

        var sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName) || pingEnabledScenes == null || pingEnabledScenes.Length == 0)
            return false;

        // Case-insensitive compare
        return pingEnabledScenes.Any(s => !string.IsNullOrEmpty(s) &&
                                          string.Equals(s, sceneName, System.StringComparison.OrdinalIgnoreCase));
    }

    void MaybeStartOrStopPingBasedOnState()
    {
        bool alreadyFound = SaveFlags.Instance && SaveFlags.Instance.Has(walkieFoundFlag);

        if (!alreadyFound && IsPingAllowedInCurrentScene())
            StartPingLoop();
        else
            StopPingLoop(); // stops immediately on leaving Level 2
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
        // Keep coroutine alive; only fire pings when allowed & until walkie is found.
        while (!(SaveFlags.Instance && SaveFlags.Instance.Has(walkieFoundFlag)))
        {
            if (IsPingAllowedInCurrentScene())
            {
                var pingSrc = walkieSource ?? _persistentWalkie2D ?? _fallback2D;
                if (pingSrc && walkiePingClip)
                    pingSrc.PlayOneShot(walkiePingClip);

                float jitter = Random.Range(-walkiePingJitter, walkiePingJitter);
                float wait = Mathf.Max(0.25f, walkiePingInterval + jitter);
                yield return new WaitForSeconds(wait);
            }
            else
            {
                // Outside allowed scenes: do nothing until we re-enter (e.g., Level 2)
                yield return null;
            }
        }

        // found: ensure loop is cleared
        StopPingLoop();
    }

    // --- PUBLIC HOOKS ---------------------------------------------------------
    void EnqueueVO(AudioClip clip, string subtitle, bool viaWalkie)
    {
        if (string.IsNullOrEmpty(subtitle) && clip == null)
        {
            PersistentHUD.Instance?.ClearSubtitle();
            return;
        }

        var src = PickVoSourceForWalkieFlag(viaWalkie);

        if (viaWalkie && makeWalkieVisibleOnWalkieVO)
        {
            var visDur = EstimateClipDuration(clip, 0.75f) + extraHold + walkieVisibilityExtraHold;
            if (!walkieRef) walkieRef = FindObjectOfType<WalkieTalkie>(includeInactive: true);
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
        if ((SaveFlags.Instance && SaveFlags.Instance.Has(walkieFoundFlag)) || _walkieFoundAnnounced)
            return;

        _walkieFoundAnnounced = true;
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
        EnqueueVO(voLeverInstalled, subLeverInstalled, viaWalkie: voLeverInstalledViaWalkie);

        if (VoiceLineQueue.Instance != null)
            yield return VoiceLineQueue.Instance.WaitUntilIdle();
        else
            PersistentHUD.Instance?.ClearSubtitle();

        var src = elevatorSource ? elevatorSource : PickVoSourceForWalkieFlag(false);
        if (src && elevatorMoveClip) src.PlayOneShot(elevatorMoveClip);

        yield return new WaitForSeconds(0.2f);

        if (lever != null)
            lever.PullAndTransfer();
    }
}
