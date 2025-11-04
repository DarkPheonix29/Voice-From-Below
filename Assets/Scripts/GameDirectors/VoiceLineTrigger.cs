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

    bool _fired;

    void Awake()
    {
        // If we’ve already fired previously (session or saved), consume now.
        if (!string.IsNullOrEmpty(triggerId) && SaveFlags.Instance && SaveFlags.Instance.Has(triggerId))
        {
            _fired = true;
            // If we want the barrier gone for already-completed content, ensure it's off
            if (barrierPlane) barrierPlane.SetActive(false);

            if (disableAfterFire)
            {
                if (deactivateObjectAfterFire) { gameObject.SetActive(false); return; }
                if (disableComponentAfterFire) { enabled = false; return; }
            }
        }
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

        // Turn on barrier if requested (or leave it on if it’s already active)
        if (barrierPlane && activateBarrierOnEnter && !barrierPlane.activeSelf)
            barrierPlane.SetActive(true);

        // Optionally: wait for any currently playing VO before starting ours
        if (VoiceLineQueue.Instance) yield return VoiceLineQueue.Instance.WaitUntilIdle();

        if (lines != null && lines.Length > 0)
            VoiceLineQueue.Instance?.EnqueueRange(lines);

        // Wait until all queued lines finish
        if (VoiceLineQueue.Instance) yield return VoiceLineQueue.Instance.WaitUntilIdle();

        // Now let player pass by disabling the barrier
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
}
