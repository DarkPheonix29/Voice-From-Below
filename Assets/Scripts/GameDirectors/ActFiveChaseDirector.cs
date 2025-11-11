using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Video;      // for VideoPlayer / VideoClip
using UnityEngine.UI;         // for RawImage (when not rendering to camera)
using System.Collections;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Persistent chase brain for L5->L4->L3->L2->L1 continuous sequence.
/// - Lives in a persistent Systems scene (DontDestroyOnLoad)
/// - Knows which "SceneChaseAnchor" node to run in each scene
/// - Handles scene transfers, optional intro VO, QTE pausing, endings (inside Level 1),
///   and auto-playing credits video without extra scene objects.
/// </summary>
public class ActFiveChaseDirector : MonoBehaviour
{
    public static ActFiveChaseDirector Instance { get; private set; }

    // runtime state
    PlayableDirector _currentSegment;
    HashSet<string> _consumedPiles = new HashSet<string>();

    [Header("Flags")]
    public string chaseStartedFlag  = "L5_Chase_Started";
    public string chaseFinishedFlag = "L5_Chase_Finished";
    public string introVoDoneFlag   = "L5_IntroVO_Done";

    [Header("Intro VO (Level 5 entry - optional)")]
    public AudioSource introSource;
    public AudioClip introClip;
    [TextArea] public string introSubtitle;
    public float introExtraHold = 0.6f;

    [Header("Endings (decision)")]
    [Tooltip("How many dynamite pickups are needed for the good ending.")]
    public int requiredDynamite = 3;
    [Tooltip("If true, good ending requires BOTH dynamite >= threshold AND last QTE success.")]
    public bool requireQteSuccessForGoodEnding = true;

    [Header("Scene Transfer (between chase scenes)")]
    public float fadeOut = 0.8f;
    public float fadeIn  = 1.2f;

    [Tooltip("Optional spawn ids passed to GameFlow when loading the final 'ending scenes' (unused when endings are in Level 1).")]
    public string goodSpawnId = "FromChase_Good";
    public string badSpawnId  = "FromChase_Bad";

    [Header("Credits (auto after Level 1 ending cutscene)")]
    public bool autoPlayCredits = true;
    public VideoClip creditsGood;                 // assign in Inspector
    public VideoClip creditsBad;                  // assign in Inspector
    [Tooltip("If true, render video on the Main Camera Near Plane; else use a temporary RenderTexture + RawImage overlay.")]
    public bool renderToCameraNearPlane = true;
    [Range(0f,1f)] public float creditsVolume = 1f;
    [Tooltip("Optional: where to go after credits. Leave empty to just stop on the last frame.")]
    public string afterCreditsScene = "MainMenu";
    public float afterCreditsFadeOut = 1.0f, afterCreditsFadeIn = 1.0f;

    // flow
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

        // In all chase scenes (including Level 1), run the node we set before loading
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

    /// <summary>Optional: called by a Timeline bridge if you keep that pattern.</summary>
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

        // Attempt to run the current scene's anchor (if present)
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
            _currentSegment = anchor.segment;
            _currentSegment.time = 0;
            _currentSegment.Play();
            while (_currentSegment.state == PlayState.Playing)
                yield return null;
            _currentSegment = null;
        }

        // Scene-specific effect (e.g., Level 3 lights off forever)
        if (anchor.killLightsOnComplete && anchor.level3LightFlicker)
            anchor.level3LightFlicker.KillLevel3Lights();

        anchor.SetBarrier(false);

        // If this is an ending node inside Level 1, mark finished and roll credits in-place
        if (anchor.isFinalNode)
        {
            if (SaveFlags.Instance) SaveFlags.Instance.Set(chaseFinishedFlag);

            // Choose credits clip based on which ending node ran
            VideoClip endClip = null;
            if (autoPlayCredits)
            {
                // Prefer node id to choose, fallback to computed decision if needed
                if (anchor.nodeId == "L1_Good") endClip = creditsGood;
                else if (anchor.nodeId == "L1_Bad") endClip = creditsBad;
                else
                {
                    // Fallback decision (in case you renamed nodes)
                    bool enoughDynamite = GetDynamiteCount() >= requiredDynamite;
                    bool good = enoughDynamite && (!requireQteSuccessForGoodEnding || _lastQteSuccess);
                    endClip = good ? creditsGood : creditsBad;
                }
                if (endClip != null)
                    StartCoroutine(PlayCreditsVideo(endClip));
            }

            yield break; // endings stop the chase flow
        }

        // ---------- Prepare next hop ----------
        string nextNode = anchor.nextNodeId;

        // If hopping to Level 1 without a specific node, auto-select L1_Good / L1_Bad
        if (anchor.nextScene == "Level1" && (string.IsNullOrEmpty(nextNode) || nextNode == "AUTO"))
        {
            bool enoughDynamite = GetDynamiteCount() >= requiredDynamite;
            bool good = enoughDynamite && (!requireQteSuccessForGoodEnding || _lastQteSuccess);
            nextNode = good ? "L1_Good" : "L1_Bad";
        }

        _currentNodeId = nextNode;

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

    int GetDynamiteCount()
    {
        // Swap to your Inventory if you track counts there
        return DynamiteTracker.SessionCount;
    }

    public void BeginQteAtPile(string pileId, KeyCode key, float duration)
    {
        if (_consumedPiles.Contains(pileId)) return;
        _consumedPiles.Add(pileId);

        PauseSegment();

        QTEManager.Instance?.Begin(key, duration, (success) =>
        {
            _lastQteSuccess = success;
            if (success) DynamiteTracker.Add(1);
            ResumeSegment();
        });
    }

    void PauseSegment()
    {
        if (_currentSegment)
            _currentSegment.playableGraph.GetRootPlayable(0).SetSpeed(0);
    }

    void ResumeSegment()
    {
        if (_currentSegment)
            _currentSegment.playableGraph.GetRootPlayable(0).SetSpeed(1);
    }

    // ---------- Credits playback (no extra scene objects required) ----------

    IEnumerator PlayCreditsVideo(VideoClip clip)
    {
        if (!autoPlayCredits || clip == null) yield break;

        // Safety: clear any screen fade right away
        PersistentHUD.Instance?.FadeFromBlack(0.1f);
        yield return null;

        var go = new GameObject("RuntimeCreditsVideo");
        DontDestroyOnLoad(go);

        var vp = go.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.clip = clip;
        vp.isLooping = false;
        vp.waitForFirstFrame = true;
        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = false;
        audio.spatialBlend = 0f;
        audio.volume = Mathf.Clamp01(creditsVolume);
        vp.SetTargetAudioSource(0, audio);

        Canvas canvas = null;
        RenderTexture rt = null;

        if (renderToCameraNearPlane && Camera.main != null)
        {
            vp.renderMode = VideoRenderMode.CameraNearPlane;
            vp.targetCamera = Camera.main;
            vp.targetCameraAlpha = 1f;
        }
        else
        {
            // Build a temporary fullscreen RawImage on top of UI
            var canvasGO = new GameObject("CreditsCanvas");
            canvasGO.layer = LayerMask.NameToLayer("UI");
            DontDestroyOnLoad(canvasGO);

            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // above HUD

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var imgGO = new GameObject("CreditsImage");
            imgGO.transform.SetParent(canvasGO.transform, false);
            var img = imgGO.AddComponent<RawImage>();

            rt = new RenderTexture(Screen.width, Screen.height, 0);
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            img.texture = rt;

            var r = img.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        // Prepare & play
        vp.Prepare();
        while (!vp.isPrepared) yield return null;

        audio.Play();
        vp.Play();
        while (vp.isPlaying || audio.isPlaying) yield return null;

        // Cleanup temp objects
        if (rt) { rt.Release(); Destroy(rt); }
        if (canvas) Destroy(canvas.gameObject);
        Destroy(go);

        // After credits: optional fade & go to menu
        if (!string.IsNullOrEmpty(afterCreditsScene))
        {
            if (PersistentHUD.Instance != null)
                PersistentHUD.Instance.LoadSceneWithFade(afterCreditsScene, afterCreditsFadeOut, afterCreditsFadeIn, null);
            else
                SceneManager.LoadScene(afterCreditsScene);
        }
    }
}
