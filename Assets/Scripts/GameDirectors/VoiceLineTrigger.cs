using UnityEngine;

[RequireComponent(typeof(Collider))]
public class VoiceLineTrigger : MonoBehaviour
{
    [Header("Persistence")]
    [Tooltip("Unique ID for this trigger. If set, it will only fire once per session or after save, and stay deactivated after a saved game restart.")]
    public string triggerId;

    [Header("Playback")]
    public VoiceLine[] lines;           // voice lines to play (in order)

    [Header("Behavior")]
    public bool oneShot = true;

    [Header("Objective")]
    [Tooltip("Optional: set/update objective immediately when the trigger is entered.")]
    public string objectiveOnTrigger;

    [Header("Barrier Plane (no movement code touched)")]
    [Tooltip("Assign a plane / wall / blocker GameObject to keep the player from passing. It will be disabled after the voice lines finish.")]
    public GameObject barrierPlane;
    [Tooltip("If true, the barrierPlane will be activated as soon as the trigger fires (useful if it starts disabled). If false, we assume it is already active in the scene.")]
    public bool activateBarrierOnEnter = false;
    [Tooltip("If true, we disable this component or object after firing (as configured below).")]
    public bool disableAfterFire = true;

    [Header("Post-fire Handling")]
    [Tooltip("Disable this component after it fires.")]
    public bool disableComponentAfterFire = true;
    [Tooltip("Deactivate the entire GameObject after it fires.")]
    public bool deactivateObjectAfterFire = false;

    [Header("Walkie VO Routing & Visibility")]
    [Tooltip("If true, ALL lines in this trigger will be played 'via walkie'.")]
    public bool routeAllLinesViaWalkie = false;

    [Tooltip("If true, 'via walkie' lines will use this AudioSource (walkie). If null, we fallback to camera 2D or default.")]
    public AudioSource walkieSource;

    [Tooltip("If true, make the walkie visible while 'via walkie' lines play.")]
    public bool makeWalkieVisibleOnWalkieVO = true;

    [Tooltip("Optional reference to the WalkieTalkie component to force visibility on.")]
    public WalkieTalkie walkieRef;

    [Tooltip("Extra seconds to keep the walkie visible after all lines finish.")]
    public float walkieVisibilityExtraHold = 0.5f;

    [Tooltip("If true, route 'via walkie' lines through walkieSource (when assigned). If false, we use the default VoiceLineQueue source instead.")]
    public bool routeWalkieVOThroughWalkie = true;

    bool _fired;
    AudioSource _fallback2D;

    void Awake()
    {
        // If we've already fired previously (session or saved), consume now.
        if (!string.IsNullOrEmpty(triggerId) && SaveFlags.Instance && SaveFlags.Instance.Has(triggerId))
        {
            _fired = true;
            if (barrierPlane) barrierPlane.SetActive(false);

            if (disableAfterFire)
            {
                if (deactivateObjectAfterFire) { gameObject.SetActive(false); return; }
                if (disableComponentAfterFire) { enabled = false; return; }
            }
        }
    }

    void Start()
    {
        _fallback2D = GetOrMakeCamera2DSource();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_fired && oneShot) return;

        StartCoroutine(HandleTrigger());
    }

    System.Collections.IEnumerator HandleTrigger()
    {
        if (!string.IsNullOrEmpty(objectiveOnTrigger))
            PersistentHUD.Instance?.SetObjective(objectiveOnTrigger);

        if (barrierPlane && activateBarrierOnEnter && !barrierPlane.activeSelf)
            barrierPlane.SetActive(true);

        // Wait for current VO to finish, if any
        if (VoiceLineQueue.Instance) yield return VoiceLineQueue.Instance.WaitUntilIdle();

        // Prepare lines (clone and inject override sources if routing via walkie)
        VoiceLine[] toPlay = lines;
        if (lines != null && lines.Length > 0 && routeAllLinesViaWalkie)
        {
            var src = PickVoSourceForWalkieFlag(true);
            toPlay = new VoiceLine[lines.Length];
            float totalDur = 0f;

            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                // clone
                var cloned = new VoiceLine
                {
                    clip = l.clip,
                    subtitle = l.subtitle,
                    extraHold = l.extraHold,
                    overrideSource = routeWalkieVOThroughWalkie ? src : l.overrideSource
                };
                toPlay[i] = cloned;

                // estimate duration for visibility (clip + extra hold; fallback min)
                totalDur += EstimateClipDuration(l.clip, 0.5f) + l.extraHold;
            }

            if (makeWalkieVisibleOnWalkieVO)
            {
                EnsureWalkieRef()?.ForceShow(totalDur + walkieVisibilityExtraHold);
            }
        }

        if (toPlay != null && toPlay.Length > 0)
            VoiceLineQueue.Instance?.EnqueueRange(toPlay);

        // Wait for all queued lines to finish
        if (VoiceLineQueue.Instance) yield return VoiceLineQueue.Instance.WaitUntilIdle();

        // Now let player pass
        if (barrierPlane) barrierPlane.SetActive(false);

        _fired = true;
        if (!string.IsNullOrEmpty(triggerId) && SaveFlags.Instance)
            SaveFlags.Instance.Set(triggerId);

        if (oneShot && disableAfterFire)
        {
            if (deactivateObjectAfterFire) { gameObject.SetActive(false); yield break; }
            if (disableComponentAfterFire) { enabled = false; yield break; }
        }
    }

    // ---------- helpers ----------
    AudioSource GetOrMakeCamera2DSource()
    {
        var cam = Camera.main;
        if (!cam) return null;
        var src = cam.GetComponent<AudioSource>();
        if (!src) src = cam.gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        return src;
    }

    WalkieTalkie EnsureWalkieRef()
    {
        if (walkieRef) return walkieRef;
        walkieRef = FindObjectOfType<WalkieTalkie>(includeInactive: true);
        return walkieRef;
    }

    float EstimateClipDuration(AudioClip clip, float minimum = 0.5f)
    {
        if (!clip) return minimum;
        return Mathf.Max(minimum, clip.length);
    }

    AudioSource PickVoSourceForWalkieFlag(bool viaWalkie)
    {
        if (viaWalkie && routeWalkieVOThroughWalkie && walkieSource)
            return walkieSource;

        if (_fallback2D) return _fallback2D;

        var src = GetComponent<AudioSource>();
        if (!src) src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        return src;
    }
}
