using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Video;      // for VideoPlayer / VideoClip
using UnityEngine.UI;         // for RawImage (when not rendering to camera)
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System; // Added for StringComparison

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
    public string chaseStartedFlag = "L5_Chase_Started";
    public string chaseFinishedFlag = "L5_Chase_Finished";
    public string introVoDoneFlag = "L5_IntroVO_Done";

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
    public float fadeIn = 1.2f;

    [Tooltip("Optional spawn ids passed to GameFlow when loading the final 'ending scenes' (unused when endings are in Level 1).")]
    public string goodSpawnId = "FromChase_Good";
    public string badSpawnId = "FromChase_Bad";

    [Header("Credits (auto after Level 1 ending cutscene)")]
    public bool autoPlayCredits = true;
    public VideoClip creditsGood;                 // assign in Inspector
    public VideoClip creditsBad;                  // assign in Inspector
    [Tooltip("If true, render video on the Main Camera Near Plane; else use a temporary RenderTexture + RawImage overlay.")]
    public bool renderToCameraNearPlane = true;
    [Range(0f, 1f)] public float creditsVolume = 1f;
    [Tooltip("Optional: where to go after credits. Leave empty to just stop on the last frame.")]
    public string afterCreditsScene = "MainMenu";
    public float afterCreditsFadeOut = 1.0f, afterCreditsFadeIn = 1.0f;

    // flow
    string _currentNodeId;
    bool _isRunning;
    bool _lastQteSuccess; // Tracks success of the most recent QTE

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
            // Assuming VoiceLineQueue is available
            // VoiceLineQueue.Instance?.Enqueue(new VoiceLine { ... }); 
            // ... (VO handling removed for brevity/simplicity if no VoiceLineQueue script is present)

            if (SaveFlags.Instance) SaveFlags.Instance.Set(introVoDoneFlag);
        }

        // Attempt to run the current scene's anchor (if present)
        var anchors = FindObjectsOfType<SceneChaseAnchor>(true);
        var anchor = anchors.FirstOrDefault(a => a.nodeId == _currentNodeId);
        if (anchor != null) yield return RunAnchor(anchor);
        else Debug.LogWarning($"[Chase] Could not find first anchor '{_currentNodeId}' in current scene.");
    }

    IEnumerator RunAnchor(SceneChaseAnchor anchor)
    {
        // Block path if configured
        anchor.SetBarrier(true);

        // Let any VO finish before we start
        // if (VoiceLineQueue.Instance != null) yield return VoiceLineQueue.Instance.WaitUntilIdle();

        // Play this scene's Timeline segment
        if (anchor.segment)
        {
            _currentSegment = anchor.segment;
            _currentSegment.time = 0;
            _currentSegment.Play();
            while (_currentSegment.state == PlayState.Playing)
                yield return null;
            _currentSegment = null;
        }

        // Scene-specific effect (e.g., L3 lights off forever)
        if (anchor.killLightsOnComplete && anchor.level3LightFlicker)
            anchor.level3LightFlicker.KillLevel3Lights();

        // Drop barrier now that this node finished
        anchor.SetBarrier(false);

        // ---------- ENDING NODE (Level 1) ----------
        // This is ONLY for nodes marked as Final (L1_Good or L1_Bad)
        if (anchor.isFinalNode)
        {
            if (SaveFlags.Instance) SaveFlags.Instance.Set(chaseFinishedFlag);

            // Stop the chase loop so OnSceneLoaded won't try to pick anything else up.
            _isRunning = false;

            // Choose credits based on which node ran (L1_Good or L1_Bad)
            if (autoPlayCredits)
            {
                UnityEngine.Video.VideoClip endClip = null;
                // If the current node ID matches the Good Node ID, play good credits
                if (anchor.nodeId.Equals(anchor.goodEndingNodeId, StringComparison.OrdinalIgnoreCase))
                {
                     endClip = creditsGood;
                     Debug.Log($"[Chase] Final Node Reached ({anchor.nodeId}). Playing Good Credits.");
                }
                else
                {
                     endClip = creditsBad;
                     Debug.Log($"[Chase] Final Node Reached ({anchor.nodeId}). Playing Bad Credits.");
                }

                if (endClip != null)
                    StartCoroutine(PlayCreditsVideo(endClip));
            }

            yield break; // endings stop the chase flow
        }

        // ---------- PREPARE NEXT HOP (Where the decision should happen) ----------
        
        // 1. Start with the anchor's default next node ID
        string finalNodeId = anchor.nextNodeId; 

        // 2. Conditional check: Are we jumping to the final level (Level 1)?
        string nextSceneCleaned = anchor.nextScene?.Trim();
        if (nextSceneCleaned != null && nextSceneCleaned.Equals("Level 1", StringComparison.OrdinalIgnoreCase))
        {
            // Only execute decision logic if the anchor has conditional fields set
            if (!string.IsNullOrEmpty(anchor.goodEndingNodeId) && !string.IsNullOrEmpty(anchor.badEndingNodeId))
            {
                bool enoughDynamite = GetDynamiteCount() >= requiredDynamite;
                bool good = enoughDynamite && (!requireQteSuccessForGoodEnding || _lastQteSuccess);
                
                // GUARANTEED ASSIGNMENT: Force finalNodeId to the decided L1 node
                finalNodeId = good ? anchor.goodEndingNodeId : anchor.badEndingNodeId;
                
                // Log decision for debugging
                Debug.Log($"[Chase] L1 Decision: Dynamite={GetDynamiteCount()}/{requiredDynamite}, QTE Success={_lastQteSuccess}. Good Ending={good}. Next Node set to: {finalNodeId}");
            }
            // If Level 1 decision fields are missing on the anchor, we fall back to its nextNodeId, if present.
            else if (string.IsNullOrEmpty(anchor.nextNodeId))
            {
                // This captures the original error case if decision fields are also blank.
                Debug.LogWarning($"[Chase] L1 Transition failed: Decision fields or default nextNodeId are missing on anchor '{anchor.nodeId}'.");
            }
        } // End of Level 1 conditional decision

        // ----- Same-scene chaining (no scene load) -----
        if (string.IsNullOrEmpty(anchor.nextScene))
        {
            // Use the calculated/default node ID
            if (string.IsNullOrEmpty(finalNodeId))
            {
                Debug.LogWarning("[Chase] No nextScene and no nextNodeId provided — stopping.");
                _isRunning = false;
                yield break;
            }

            Debug.Log($"[Chase] Chaining to next node in SAME SCENE: {finalNodeId}");
            var nextAnchor = FindObjectsOfType<SceneChaseAnchor>(true).FirstOrDefault(a => a.nodeId == finalNodeId);
            if (nextAnchor != null)
            {
                yield return RunAnchor(nextAnchor);
            }
            else
            {
                Debug.LogWarning($"[Chase] Could not find next node '{finalNodeId}' in current scene — stopping.");
                _isRunning = false;
            }
            yield break;
        }

        // ----- Scene hop -----
        
        // Final assignment to the persistent field _currentNodeId
        if (string.IsNullOrEmpty(finalNodeId))
        {
            Debug.LogWarning($"[Chase] Scene hop node is empty for scene '{anchor.nextScene}'. Check anchor configuration.");
            _currentNodeId = ""; // This will cause the OnSceneLoaded warning if it remains empty.
        }
        else
        {
            _currentNodeId = finalNodeId;
        }

        Debug.Log($"[Chase] Loading next scene '{anchor.nextScene}', node='{_currentNodeId}'");

        System.Action before = null;
        if (!string.IsNullOrEmpty(anchor.nextSpawnId) && GameFlow.Instance != null)
            before = () => GameFlow.Instance.QueueSpawn(anchor.nextSpawnId);

        // Assuming PersistentHUD is available for fade transition
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

    // ---------- Credits playback (no extra scene objects required) ----------

    IEnumerator PlayCreditsVideo(VideoClip clip)
    {
        if (!autoPlayCredits || clip == null) yield break;

        // --- STEP 1: HIDE UI AND PLAYER ---
        // Hide Persistent HUD (subtitles, health, etc.)
        bool wasHudActive = false;
        if (PersistentHUD.Instance != null)
        {
            wasHudActive = PersistentHUD.Instance.gameObject.activeSelf;
            PersistentHUD.Instance.gameObject.SetActive(false);
            // Safety: clear any screen fade right away
            PersistentHUD.Instance.FadeFromBlack(0.1f);
        }

        // Hide Player rendering and controls (assuming a standard PlayerController structure)
        // NOTE: You may need to adjust "PlayerController" and "Camera.main"
        GameObject player = GameObject.FindGameObjectWithTag("Player"); 
        if (player != null)
        {
            // Temporarily disable player input/controls
            player.SendMessage("SetInputEnabled", false, SendMessageOptions.DontRequireReceiver);
            
            // Optionally hide the player mesh/renderer if needed (often handled by the cutscene camera)
            if (Camera.main && Camera.main.transform.parent == player.transform)
            {
                // If the camera is parented to the player (FPS/TPS), we don't disable the whole player.
            }
            else
            {
                // If the player is a separate object, disable its rendering components.
                // player.gameObject.SetActive(false); // Use this if you want to completely hide the player
            }
        }
        
        yield return null; // Wait one frame for UI state change to take effect

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
            // ... (Scaler setup remains the same)
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

        // --- STEP 2: CLEANUP & RESTORE ---
        
        // Cleanup temp objects
        if (rt) { rt.Release(); Destroy(rt); }
        if (canvas) Destroy(canvas.gameObject);
        Destroy(go);

        // Restore Player controls
        if (player != null)
        {
            player.SendMessage("SetInputEnabled", true, SendMessageOptions.DontRequireReceiver);
            // if (!player.gameObject.activeSelf) player.gameObject.SetActive(true); // Uncomment if you fully disabled the player above
        }

        // Restore Persistent HUD
        if (PersistentHUD.Instance != null && wasHudActive)
        {
            PersistentHUD.Instance.gameObject.SetActive(true);
        }

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