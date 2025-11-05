using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Place one in each scene of the chase. It references:
/// - this scene's Timeline "segment"
/// - optional light kill (Level 3)
/// - next scene/id/spawn to continue the chain
/// </summary>
public class SceneChaseAnchor : MonoBehaviour
{
    [Header("Identity")]
    public string nodeId = "L5_Start";       // must match what the director expects

    [Header("Segment")]
    public PlayableDirector segment;         // Timeline for THIS scene's portion

    [Header("Optional Lights (for Level 3)")]
    public bool killLightsOnComplete = false;
    public LightFlicker level3LightFlicker;

    [Header("Next Jump")]
    public string nextScene;                 // e.g., "Level4"
    public string nextNodeId;                // e.g., "L4_Middle"
    public string nextSpawnId;               // optional spawn where next level should start

    [Header("Final Node? (outside/cave exit)")]
    public bool isFinalNode = false;

    [Header("Barrier (optional)")]
    public GameObject barrierPlane;
    public bool activateBarrierOnPlay = true;

    public void SetBarrier(bool on)
    {
        if (!barrierPlane) return;
        if (on && activateBarrierOnPlay) barrierPlane.SetActive(true);
        if (!on) barrierPlane.SetActive(false);
    }
}
