using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// One anchor per scene for the L5→L1 chase.
/// - Holds THIS scene's Timeline "segment"
/// - Optional: kills Level 3 lights when done
/// - Knows where to jump next (scene/node/spawn)
/// - Exposes helper methods you can call from Timeline Signals:
///     PauseTimeline / ResumeTimeline / SetBarrier / StartTimelineQTE / FadeOut / FadeIn
/// </summary>
public class SceneChaseAnchor : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique id used by ActFiveChaseDirector to find this anchor in the scene.")]
    public string nodeId = "L5_Start";

    [Header("Segment")]
    [Tooltip("Timeline for THIS scene's portion of the chase.")]
    public PlayableDirector segment;

    [Header("Optional Lights (for Level 3)")]
    public bool killLightsOnComplete = false;
    public LightFlicker level3LightFlicker;

    [Header("Next Jump (used by ActFiveChaseDirector after this segment finishes)")]
    public string nextScene;      // e.g. "Level4"
    public string nextNodeId;     // e.g. "L4_Start"
    public string nextSpawnId;    // optional spawn where next level should start

    [Header("Final Node? (outside/cave exit)")]
    public bool isFinalNode = false;

    [Header("Barrier (optional)")]
    public GameObject barrierPlane;
    [Tooltip("If true, SetBarrier(true) will enable the plane on play; SetBarrier(false) disables it on stop.")]
    public bool activateBarrierOnPlay = true;

    // --------- Timeline helpers (optional) ---------
    [Header("Timeline QTE (optional convenience)")]
    [Tooltip("If you want to trigger a QTE directly from Timeline via a Signal, use these defaults.")]
    public string timelinePileId = "TIMELINE_QTE";
    public KeyCode timelineQteKey = KeyCode.E;
    [Range(0.5f, 5f)] public float timelineQteWindow = 1.5f;

    [Header("Fade (optional convenience)")]
    public float fadeOutDur = 0.3f;
    public float fadeInDur  = 0.6f;

    /// <summary>Enable/disable the barrier. You can call this from Signals.</summary>
    public void SetBarrier(bool on)
    {
        if (!barrierPlane) return;
        if (on && activateBarrierOnPlay) barrierPlane.SetActive(true);
        if (!on) barrierPlane.SetActive(false);
    }

    /// <summary>Pause just this anchor's Timeline segment (useful when starting a QTE from Timeline).</summary>
    public void PauseTimeline()
    {
        if (!segment) return;
        var root = segment.playableGraph.GetRootPlayable(0);
        if (root.IsValid()) root.SetSpeed(0);
    }

    /// <summary>Resume this anchor's Timeline segment.</summary>
    public void ResumeTimeline()
    {
        if (!segment) return;
        var root = segment.playableGraph.GetRootPlayable(0);
        if (root.IsValid()) root.SetSpeed(1);
    }

    /// <summary>Start a QTE using the default Timeline QTE settings.</summary>
    public void StartTimelineQTE()
    {
        if (ActFiveChaseDirector.Instance != null)
            ActFiveChaseDirector.Instance.BeginQteAtPile(timelinePileId, timelineQteKey, timelineQteWindow);
    }

    /// <summary>Start a QTE with explicit parameters (callable from Signal Receiver with arguments via UnityEvents).</summary>
    public void StartQTE(string pileId, string keyName, float windowSeconds)
    {
        if (!System.Enum.TryParse<KeyCode>(keyName, out var key))
            key = timelineQteKey;

        if (ActFiveChaseDirector.Instance != null)
            ActFiveChaseDirector.Instance.BeginQteAtPile(pileId, key, windowSeconds);
    }

    /// <summary>Convenience fades for use from Signals.</summary>
    public void FadeOut() => PersistentHUD.Instance?.FadeToBlack(fadeOutDur);
    public void FadeIn()  => PersistentHUD.Instance?.FadeFromBlack(fadeInDur);
}
