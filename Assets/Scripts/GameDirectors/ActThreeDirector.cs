using UnityEngine;
using System.Collections;

public class ActThreeDirector : MonoBehaviour
{
    [Header("Persistence Flags")]
    [Tooltip("Set when the intro VO has finished once.")]
    public string introVoDoneFlag = "L3_IntroVO_Done";

    [Header("Intro Voice Line")]
    public AudioSource introSource;            // where the intro VO should come from
    public AudioClip introClip;                // intro VO clip
    [TextArea] public string introSubtitle;    // subtitle for intro VO
    public float subtitleExtraHold = 0.6f;

    [Header("Optional Objective")]
    [Tooltip("If set, will be shown after the intro VO completes.")]
    public string objectiveAfterIntro;

    void Start()
    {
        // Only run once per session/saved progress
        if (SaveFlags.Instance && SaveFlags.Instance.Has(introVoDoneFlag))
        {
            if (!string.IsNullOrEmpty(objectiveAfterIntro))
                PersistentHUD.Instance?.SetObjective(objectiveAfterIntro);
            return;
        }

        // Queue intro line
        if (introClip || !string.IsNullOrEmpty(introSubtitle))
        {
            VoiceLineQueue.Instance?.Enqueue(new VoiceLine
            {
                clip = introClip,
                subtitle = introSubtitle,
                extraHold = subtitleExtraHold,
                overrideSource = introSource
            });
        }

        // After VO: set objective and flag
        StartCoroutine(FinishIntro());
    }

    IEnumerator FinishIntro()
    {
        // Wait for any VO (including the intro we just enqueued) to finish
        if (VoiceLineQueue.Instance != null)
            yield return VoiceLineQueue.Instance.WaitUntilIdle();

        if (!string.IsNullOrEmpty(objectiveAfterIntro))
            PersistentHUD.Instance?.SetObjective(objectiveAfterIntro);

        if (SaveFlags.Instance)
            SaveFlags.Instance.Set(introVoDoneFlag);
    }
}
