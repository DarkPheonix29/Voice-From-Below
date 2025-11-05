using UnityEngine;
using UnityEngine.Playables;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class Level3CutsceneTrigger : MonoBehaviour
{
    [Header("Persistence")]
    [Tooltip("Unique ID for this cutscene trigger. Once run, it won't run again this session or after save.")]
    public string triggerId = "L3_Cutscene_Main";

    [Header("Cutscene")]
    public PlayableDirector cutscene;          // your Timeline
    [Tooltip("If true, we wait for any currently playing VO before starting the cutscene.")]
    public bool waitForVoiceQueueBeforeStart = true;

    [Header("Lights")]
    [Tooltip("Scene LightFlicker set to Level3WithShutdown behavior.")]
    public LightFlicker lightFlicker;
    [Tooltip("Flag used by LightFlicker for 'lights dead'. Keep in sync with LightFlicker.level3LightsDeadFlag.")]
    public string lightsDeadFlag = "L3_LightsDead";

    [Header("Barrier (optional)")]
    [Tooltip("Assign a plane/wall object that blocks the path until the cutscene finishes.")]
    public GameObject barrierPlane;
    [Tooltip("If barrier starts disabled, enable it when the trigger fires.")]
    public bool activateBarrierOnEnter = false;

    [Header("One-shot cleanup")]
    public bool oneShot = true;
    public bool disableComponentAfterFire = true;
    public bool deactivateObjectAfterFire = false;

    bool _fired;

    void Awake()
    {
        // If this was already done (session or saved), consume immediately and make sure barrier is off.
        if ((SaveFlags.Instance && SaveFlags.Instance.Has(triggerId)) ||
            (SaveFlags.Instance && SaveFlags.Instance.Has(lightsDeadFlag)))
        {
            _fired = true;
            if (barrierPlane) barrierPlane.SetActive(false);
            if (deactivateObjectAfterFire) { gameObject.SetActive(false); return; }
            if (disableComponentAfterFire) { enabled = false; return; }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_fired && oneShot) return;

        StartCoroutine(RunCutsceneFlow());
    }

    IEnumerator RunCutsceneFlow()
    {
        _fired = true;

        // Bring up barrier if requested
        if (barrierPlane && activateBarrierOnEnter && !barrierPlane.activeSelf)
            barrierPlane.SetActive(true);

        // Optionally wait for any ongoing VO to finish
        if (waitForVoiceQueueBeforeStart && VoiceLineQueue.Instance != null)
            yield return VoiceLineQueue.Instance.WaitUntilIdle();

        // Play the Timeline
        if (cutscene)
        {
            cutscene.Play();
            // Wait until Timeline finishes
            while (cutscene.state == PlayState.Playing)
                yield return null;
        }

        // Kill Level 3 lights (and remember it)
        if (lightFlicker)
            lightFlicker.KillLevel3Lights();
        if (SaveFlags.Instance)
            SaveFlags.Instance.Set(lightsDeadFlag);

        // Drop barrier so player can proceed
        if (barrierPlane) barrierPlane.SetActive(false);

        // Mark this trigger as completed
        if (SaveFlags.Instance && !string.IsNullOrEmpty(triggerId))
            SaveFlags.Instance.Set(triggerId);

        if (oneShot)
        {
            if (deactivateObjectAfterFire) { gameObject.SetActive(false); yield break; }
            if (disableComponentAfterFire) { enabled = false; yield break; }
        }
    }
}
