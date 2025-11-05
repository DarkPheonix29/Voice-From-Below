using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

/// <summary>
/// Persistent chase brain for L5->L4->L3->L2->L1 continuous sequence.
/// - Lives in a persistent Systems scene (DontDestroyOnLoad)
/// - Knows which "SceneChaseAnchor" node to run in each scene
/// - Handles scene transfers, optional intro VO, optional QTE success feed, and endings
/// </summary>
public class ActFiveChaseDirector : MonoBehaviour
{
    public static ActFiveChaseDirector Instance { get; private set; }

    [Header("Flags")]
    public string chaseStartedFlag = "L5_Chase_Started";
    public string chaseFinishedFlag = "L5_Chase_Finished";
    public string introVoDoneFlag   = "L5_IntroVO_Done";

    [Header("Intro VO (Level 5 entry - optional)")]
    public AudioSource introSource;
    public AudioClip introClip;
    [TextArea] public string introSubtitle;
    public float introExtraHold = 0.6f;

    [Header("Endings")]
    [Tooltip("How many dynamite pickups are needed for the good ending.")]
    public int requiredDynamite = 3;
    [Tooltip("If true, good ending requires BOTH dynamite >= threshold AND last QTE success.")]
    public bool requireQteSuccessForGoodEnding = true;

    [Header("Ending Scenes")]
    public string goodEndingScene = "Level1_EndGood";
    public string badEndingScene  = "Level1_EndBad";
    public float fadeOut = 0.8f;
    public float fadeIn  = 1.2f;
    public string goodSpawnId = "FromChase_Good"; // optional
    public string badSpawnId  = "FromChase_Bad";  // optional

    // runtime
    string _currentNodeId;
    bool   _isRunning;
    bool   _lastQteSuccess;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (!_isRunning || string.IsNullOrEmpty(_currentNodeId)) return;

        var anchors = FindObjectsOfType<SceneChaseAnchor>(true);
        var anchor = anchors.FirstOrDefault(a => a.nodeId == _currentNodeId);
        if (anchor != null) StartCoroutine(RunAnchor(anchor));
        else Debug.LogWarning($"[Chase] Scene '{s.name}' missing anchor '{_currentNodeId}'.");
    }

    // ----- Public API -----

    /// <summary>Call from Level 5 start trigger.</summary>
    public void StartChaseFromLevel5(string firstNodeId)
    {
        if (_isRunning) return;
        if (SaveFlags.Instance && SaveFlags.Instance.Has(chaseFinishedFlag)) return; // already done
        StartCoroutine(StartFlow(firstNodeId));
    }

    /// <summary>Call from a QTE Timeline clip via the bridge when a QTE ends.</summary>
    public void OnQteSequenceEnd(bool success) { _lastQteSuccess = success; }

    // ----- Flow -----

    IEnumerator StartFlow(string firstNodeId)
    {
        _isRunning = true;
        _currentNodeId = firstNodeId;

        if (SaveFlags.Instance) SaveFlags.Instance.Set(chaseStartedFlag);

        // Optional intro VO (only once per playthrough)
        bool introAlreadyDone = SaveFlags.Instance && SaveFlags.Instance.Has(introVoDoneFlag);
        if (!introAlreadyDone && (introClip || !string.IsNullOrEmpty(introSubtitle)))
        {
            VoiceLineQueue.Instance?.Enqueue(new VoiceLine {
                clip = introClip,
                subtitle = introSubtitle,
                extraHold = introExtraHold,
                overrideSource = introSource
            });
            if (VoiceLineQueue.Instance != null)
                yield return VoiceLineQueue.Instance.WaitUntilIdle();
            if (SaveFlags.Instance) SaveFlags.Instance.Set(introVoDoneFlag);
        }

        // Run current scene's anchor (if present)
        var anchors = FindObjectsOfType<SceneChaseAnchor>(true);
        var anchor = anchors.FirstOrDefault(a => a.nodeId == _currentNodeId);
        if (anchor != null) yield return RunAnchor(anchor);
        else Debug.LogWarning($"[Chase] Could not find first anchor '{_currentNodeId}' in current scene.");

        _isRunning = false;
    }

    IEnumerator RunAnchor(SceneChaseAnchor anchor)
    {
        anchor.SetBarrier(true);

        if (VoiceLineQueue.Instance != null)
            yield return VoiceLineQueue.Instance.WaitUntilIdle();

        if (anchor.segment)
        {
            anchor.segment.time = 0;
            anchor.segment.Play();
            while (anchor.segment.state == PlayState.Playing)
                yield return null;
        }

        // Scene-specific effect (e.g., Level 3 lights off forever)
        if (anchor.killLightsOnComplete && anchor.level3LightFlicker)
            anchor.level3LightFlicker.KillLevel3Lights();

        anchor.SetBarrier(false);

        if (anchor.isFinalNode)
        {
            bool enoughDynamite = GetDynamiteCount() >= requiredDynamite;
            bool good = enoughDynamite && (!requireQteSuccessForGoodEnding || _lastQteSuccess);

            if (SaveFlags.Instance) SaveFlags.Instance.Set(chaseFinishedFlag);
            TransferToEnding(good);
            yield break;
        }

        // Prepare next hop
        _currentNodeId = anchor.nextNodeId;

        System.Action before = null;
        if (!string.IsNullOrEmpty(anchor.nextSpawnId) && GameFlow.Instance != null)
            before = () => GameFlow.Instance.QueueSpawn(anchor.nextSpawnId);

        if (PersistentHUD.Instance != null)
            PersistentHUD.Instance.LoadSceneWithFade(anchor.nextScene, fadeOut, fadeIn, before);
        else
        {
            before?.Invoke();
            SceneManager.LoadScene(anchor.nextScene);
        }
        // Next anchor will be picked up in OnSceneLoaded.
    }

    void TransferToEnding(bool good)
    {
        System.Action before = null;
        if (GameFlow.Instance != null)
        {
            var spawn = good ? goodSpawnId : badSpawnId;
            if (!string.IsNullOrEmpty(spawn))
                before = () => GameFlow.Instance.QueueSpawn(spawn);
        }

        var scene = good ? goodEndingScene : badEndingScene;

        if (PersistentHUD.Instance != null)
            PersistentHUD.Instance.LoadSceneWithFade(scene, fadeOut, fadeIn, before);
        else
        {
            before?.Invoke();
            SceneManager.LoadScene(scene);
        }
    }

    int GetDynamiteCount()
    {
        // Swap to your Inventory if you track counts there
        return DynamiteTracker.SessionCount;
    }
}
