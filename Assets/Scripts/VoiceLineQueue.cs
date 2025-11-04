using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VoiceLineQueue : MonoBehaviour
{
    public static VoiceLineQueue Instance { get; private set; }

    [Header("Defaults")]
    public AudioSource defaultVoiceSource;
    public float fallbackCharsPerSecond = 45f; // for text-only timing
    public bool useUnscaledTime = true;

    readonly Queue<VoiceLine> _queue = new Queue<VoiceLine>();

    public bool IsBusy { get; private set; }   // NEW
    public event System.Action<bool> OnBusyChanged; // optional observers

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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

        var src = v.overrideSource ? v.overrideSource : defaultVoiceSource;

        float audioLen = 0f;
        if (src && v.clip)
        {
            src.clip = v.clip;
            src.Play();
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

    // NEW: triggers can yield until the queue is done
    public IEnumerator WaitUntilIdle()
    {
        while (IsBusy) yield return null;
    }
}
