using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class VoiceLineQueue : MonoBehaviour
{
    public static VoiceLineQueue Instance { get; private set; }

    [Header("Defaults")]
    [Tooltip("If set, used first. If destroyed on scene change, we'll fall back automatically.")]
    public AudioSource defaultVoiceSource;

    public float fallbackCharsPerSecond = 45f; // for text-only timing
    public bool useUnscaledTime = true;

    [Header("Safety")]
    [Tooltip("Create a persistent 2D AudioSource on this object if no default is available.")]
    public bool createPersistent2DSource = true;

    readonly Queue<VoiceLine> _queue = new Queue<VoiceLine>();

    public bool IsBusy { get; private set; }
    public event System.Action<bool> OnBusyChanged;

    // internal persistent fallback
    AudioSource _persistent2D;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (createPersistent2DSource)
            _persistent2D = Ensure2DSourceOnSelf();

        // If no explicit default, use our persistent 2D source
        if (!defaultVoiceSource) defaultVoiceSource = _persistent2D;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If the assigned default got destroyed with the last scene, repair it.
        if (!defaultVoiceSource)
        {
            // Prefer Main Camera's AudioSource (2D), else our persistent one
            var camSrc = GetOrMakeCamera2DSource();
            defaultVoiceSource = camSrc ? camSrc : (_persistent2D ? _persistent2D : Ensure2DSourceOnSelf());
        }
    }

    // ---------- Public API ----------
    public void Enqueue(VoiceLine line)
    {
        if (line == null) return;
        _queue.Enqueue(line);
        if (!IsBusy) StartCoroutine(Run());
    }

    public void EnqueueRange(IEnumerable<VoiceLine> lines)
    {
        if (lines == null) return;
        foreach (var l in lines) if (l != null) _queue.Enqueue(l);
        if (!IsBusy) StartCoroutine(Run());
    }

    public IEnumerator WaitUntilIdle()
    {
        while (IsBusy) yield return null;
    }

    // ---------- Core ----------
    IEnumerator Run()
    {
        SetBusy(true);
        while (_queue.Count > 0)
        {
            var v = _queue.Dequeue();
            yield return PlayOne(v);
        }
        SetBusy(false);
    }

    IEnumerator PlayOne(VoiceLine v)
    {
        if (v.preDelay > 0f) yield return Wait(v.preDelay);

        if (!string.IsNullOrEmpty(v.objectiveOverride))
            PersistentHUD.Instance?.SetObjective(v.objectiveOverride);

        // Pick a source: override > default > camera 2D > persistent 2D on self
        var src = v.overrideSource
                  ? v.overrideSource
                  : (defaultVoiceSource ? defaultVoiceSource
                     : (GetOrMakeCamera2DSource() ?? (_persistent2D ? _persistent2D : Ensure2DSourceOnSelf())));

        float audioLen = 0f;
        if (src && v.clip)
        {
            // Use PlayOneShot so we don't stomp any looping clip on that source.
            src.PlayOneShot(v.clip);
            audioLen = v.clip.length;
        }

        if (!string.IsNullOrEmpty(v.subtitle))
            PersistentHUD.Instance?.ShowSubtitle(v.subtitle, holdSeconds: 0f, typewriter: true);

        float minType = Mathf.Max(0.35f, (v.subtitle?.Length ?? 0) / Mathf.Max(1f, fallbackCharsPerSecond));
        float showFor = audioLen > 0f ? audioLen : minType;

        if (showFor > 0f) yield return Wait(showFor);
        if (v.extraHold > 0f) yield return Wait(v.extraHold);

        PersistentHUD.Instance?.ClearSubtitle();
    }

    IEnumerator Wait(float s)
    {
        if (s <= 0f) yield break;
        if (useUnscaledTime) yield return new WaitForSecondsRealtime(s);
        else yield return new WaitForSeconds(s);
    }

    void SetBusy(bool busy)
    {
        if (IsBusy == busy) return;
        IsBusy = busy;
        OnBusyChanged?.Invoke(IsBusy);
    }

    // ---------- Audio helpers ----------
    AudioSource Ensure2DSourceOnSelf()
    {
        var src = GetComponent<AudioSource>();
        if (!src) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f; // 2D
        return src;
    }

    AudioSource GetOrMakeCamera2DSource()
    {
        var cam = Camera.main;
        if (!cam) return null;

        var src = cam.GetComponent<AudioSource>();
        if (!src) src = cam.gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f; // 2D
        return src;
    }
}
