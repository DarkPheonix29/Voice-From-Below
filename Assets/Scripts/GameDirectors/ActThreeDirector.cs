using UnityEngine;
using System.Collections;

public class ActThreeDirector : MonoBehaviour
{
    [Header("Persistence Flags")]
    [Tooltip("Set when the intro VO has finished once.")]
    public string introVoDoneFlag = "L3_IntroVO_Done";

    [Header("Intro Voice Line")]
    public AudioSource introSource;            // preferred source for intro VO (ignored if 'introViaWalkie' + routing to walkie)
    public AudioClip introClip;                // intro VO clip
    [TextArea] public string introSubtitle;    // subtitle for intro VO
    public float subtitleExtraHold = 0.6f;

    [Header("Walkie VO Routing & Visibility")]
    [Tooltip("If true, route intro VO via the walkie (see options below).")]
    public bool introViaWalkie = false;

    [Tooltip("If true, VO marked 'via walkie' will use the walkie's AudioSource if assigned.")]
    public bool routeWalkieVOThroughWalkie = true;

    [Tooltip("Optional: assign your WalkieTalkie AudioSource to route VO when 'via walkie' is true.")]
    public AudioSource walkieSource;

    [Tooltip("Optional: reference to WalkieTalkie to force-show while VO plays.")]
    public WalkieTalkie walkieRef;

    [Tooltip("If true, force the walkie to be visible while a 'via walkie' VO plays.")]
    public bool makeWalkieVisibleOnWalkieVO = true;

    [Tooltip("Extra seconds to keep the walkie visible after the VO line ends.")]
    public float walkieVisibilityExtraHold = 0.5f;

    [Header("Optional Objective")]
    [Tooltip("If set, will be shown after the intro VO completes.")]
    public string objectiveAfterIntro;

    // internal
    AudioSource _fallback2D;

    void Start()
    {
        // Already done? Just apply objective and bail.
        if (SaveFlags.Instance && SaveFlags.Instance.Has(introVoDoneFlag))
        {
            if (!string.IsNullOrEmpty(objectiveAfterIntro))
                PersistentHUD.Instance?.SetObjective(objectiveAfterIntro);
            return;
        }

        _fallback2D = GetOrMakeCamera2DSource();

        // Queue intro line (with optional 'via walkie' routing + visibility)
        if (introClip || !string.IsNullOrEmpty(introSubtitle))
        {
            var src = PickVoSourceForWalkieFlag(introViaWalkie, introSource);

            // Force show walkie while VO plays (if enabled)
            if (introViaWalkie && makeWalkieVisibleOnWalkieVO)
            {
                float dur = EstimateClipDuration(introClip, 0.75f) + subtitleExtraHold + walkieVisibilityExtraHold;
                EnsureWalkieRef()?.ForceShow(dur);
            }

            VoiceLineQueue.Instance?.Enqueue(new VoiceLine
            {
                clip = introClip,
                subtitle = introSubtitle,
                extraHold = subtitleExtraHold,
                overrideSource = src
            });
        }

        // After VO: set objective and flag
        StartCoroutine(FinishIntro());
    }

    IEnumerator FinishIntro()
    {
        if (VoiceLineQueue.Instance != null)
            yield return VoiceLineQueue.Instance.WaitUntilIdle();

        if (!string.IsNullOrEmpty(objectiveAfterIntro))
            PersistentHUD.Instance?.SetObjective(objectiveAfterIntro);

        if (SaveFlags.Instance)
            SaveFlags.Instance.Set(introVoDoneFlag);
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

    AudioSource PickVoSourceForWalkieFlag(bool viaWalkie, AudioSource preferred)
    {
        // If line is via walkie and routing is enabled and a walkie source exists, use it
        if (viaWalkie && routeWalkieVOThroughWalkie && walkieSource)
            return walkieSource;

        // Else prefer passed-in preferred source (e.g., introSource)
        if (preferred) return preferred;

        // Else fallback to 2D camera source
        if (_fallback2D) return _fallback2D;

        // Final fallback: local source
        var src = GetComponent<AudioSource>();
        if (!src) src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        return src;
    }
}
