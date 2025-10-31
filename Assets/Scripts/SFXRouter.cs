using UnityEngine;
using System;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class SFXRouterSimple : MonoBehaviour
{
    [Serializable]
    public class SFX
    {
        public string id = "Default";
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public Vector2 pitchRange = new Vector2(1f, 1f);
    }

    [Header("Sound Library")]
    public List<SFX> sounds = new();

    [Header("Audio Source Settings")]
    [Range(0, 1)] public float spatialBlend = 0f;
    public float minDistance = 1f;
    public float maxDistance = 30f;
    public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

    [Header("Audio Source Location (optional)")]
    public Transform sourceLocation;

    AudioSource src;
    Dictionary<string, SFX> lookup;

    void Awake()
    {
        // --- AudioSource setup ---
        Transform target = sourceLocation ? sourceLocation : transform;
        src = target.GetComponent<AudioSource>();
        if (!src)
        {
            src = target.gameObject.AddComponent<AudioSource>();
            Debug.Log($"[SFXRouterSimple] Created AudioSource on '{target.name}'", target);
        }

        src.playOnAwake = false;
        ApplySourceSettings();

        // --- Build lookup ---
        lookup = new Dictionary<string, SFX>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var s in sounds)
        {
            if (s != null && !lookup.ContainsKey(s.id))
                lookup[s.id] = s;
        }

        Debug.Log($"[SFXRouterSimple] Awake complete. Found {lookup.Count} sound(s): {string.Join(", ", lookup.Keys)}", this);
    }

    void ApplySourceSettings()
    {
        src.spatialBlend = spatialBlend;
        src.minDistance = Mathf.Max(0.01f, minDistance);
        src.maxDistance = Mathf.Max(src.minDistance, maxDistance);
        src.rolloffMode = rolloff;
        src.mute = false;
        src.volume = 1f;
    }

    // --- Automatically fix bad pitch ranges in inspector ---
    void OnValidate()
    {
        if (sounds == null) return;
        foreach (var s in sounds)
        {
            if (s == null) continue;
            // if both ends are 0, reset to (1,1)
            if (s.pitchRange.x < 0.05f && s.pitchRange.y < 0.05f)
                s.pitchRange = new Vector2(1f, 1f);
        }
    }

    // --- Main play method ---
    public void PlaySFX(string id)
    {
        Debug.Log($"[SFXRouterSimple] PlaySFX('{id}') called on {name}", this);

        if (lookup == null || lookup.Count == 0)
        {
            Debug.LogWarning($"[SFXRouterSimple] No sounds configured on {name}", this);
            return;
        }

        if (!lookup.TryGetValue(id, out var sfx))
        {
            Debug.LogWarning($"[SFXRouterSimple] Cue '{id}' not found. Available: {string.Join(", ", lookup.Keys)}", this);
            return;
        }

        if (!sfx.clip)
        {
            Debug.LogWarning($"[SFXRouterSimple] Clip for '{id}' is missing!", this);
            return;
        }

        // --- Safe random pitch ---
        float pMin = Mathf.Min(sfx.pitchRange.x, sfx.pitchRange.y);
        float pMax = Mathf.Max(sfx.pitchRange.x, sfx.pitchRange.y);
        float p = UnityEngine.Random.Range(pMin, pMax);
        if (p < 0.05f || float.IsNaN(p) || float.IsInfinity(p))
            p = 1f;

        // --- Apply and play ---
        src.pitch = p;
        src.volume = Mathf.Clamp01(sfx.volume);
        src.spatialBlend = spatialBlend;
        src.loop = false;

        Debug.Log($"[SFXRouterSimple] ▶ Playing '{id}' | clip='{sfx.clip.name}' | vol={src.volume:F2} | pitch={src.pitch:F2} | source={src.gameObject.name}", src);
        src.PlayOneShot(sfx.clip, src.volume);
    }
}
