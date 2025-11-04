using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level light controller that:
///  - Flickers only lights tagged "LampLight"
///  - For Level 3: can be told (by cutscene) to kill lights permanently and remember that
///  - Plays ambience: steady hum during HOLD, flicker/buzz during FLICKER, with crossfades
/// </summary>
public class LightFlicker : MonoBehaviour
{
    public enum SceneBehavior
    {
        Disabled,           // No effect (use for Level 1 or scenes that don't need it)
        Flicker,            // Always flicker (use for Level 2)
        Level3WithShutdown  // Flicker until cutscene calls KillLevel3Lights(), then lights stay off forever
    }

    [Header("Scene Behavior")]
    public SceneBehavior behavior = SceneBehavior.Flicker;

    [Tooltip("Only lights with this tag will be controlled.")]
    public string lampTag = "LampLight";

    [Header("Flicker Cycle Settings")]
    public float flickerDuration = 0.5f;    // How long the light spends flickering
    public float holdDuration = 3.0f;       // How long it holds stable intensity before flickering again

    [Header("Flicker Effect")]
    public float flickerRate = 0.1f;        // Smaller = faster flicker steps
    [Range(0.0f, 1.0f)]
    public float minIntensityFactor = 0.5f; // % of original intensity to drop to

    // Persistence flag used for Level 3 "lights dead" state
    [Header("Persistence (Level 3 only)")]
    public string level3LightsDeadFlag = "L3_LightsDead";

    // -------- AUDIO --------
    [Header("Audio (Ambience)")]
    [Tooltip("Loop that plays during HOLD (lights stable)")]
    public AudioClip steadyLoop;
    [Tooltip("Loop that plays during FLICKER")]
    public AudioClip flickerLoop;

    [Tooltip("AudioSource for steady loop (will be auto-created if null)")]
    public AudioSource steadySource;
    [Tooltip("AudioSource for flicker loop (will be auto-created if null)")]
    public AudioSource flickerSource;

    [Range(0f, 1f)] public float steadyVolume = 0.6f;
    [Range(0f, 1f)] public float flickerVolume = 0.7f;
    [Tooltip("Seconds for crossfades between steady and flicker")]
    public float crossfadeTime = 0.25f;

    [Header("Audio Spatialization")]
    [Range(0f, 1f)]
    [Tooltip("0 = 2D (global), 1 = 3D (attenuates with distance). For ambient room tone, 0 is typical.")]
    public float spatialBlend = 0f;

    [Header("Audio Polish")]
    [Tooltip("Random pitch range applied to the FLICKER source on each step")]
    public Vector2 flickerPitchRange = new Vector2(0.96f, 1.04f);
    [Tooltip("Random volume wobble applied to flicker each step (0 = none)")]
    [Range(0f, 0.2f)] public float flickerVolumeWobble = 0.05f;

    // Internals
    private readonly List<Light> lights = new List<Light>();
    private readonly Dictionary<Light, float> originalIntensity = new Dictionary<Light, float>();
    private Coroutine cycleRoutine;
    private Coroutine crossfadeRoutine;

    void Awake()
    {
        BuildLightList();
        EnsureAudioSources();
    }

    void Start()
    {
        // If behavior is disabled, do nothing.
        if (behavior == SceneBehavior.Disabled)
        {
            StopAllAudio();
            return;
        }

        // If this is Level 3 behavior and the flag is already set, kill lights immediately and stop.
        if (behavior == SceneBehavior.Level3WithShutdown && SaveFlags.Instance && SaveFlags.Instance.Has(level3LightsDeadFlag))
        {
            SetPermanentOff();
            return;
        }

        // Otherwise, start flicker for Level 2 or Level 3 pre-cutscene
        cycleRoutine = StartCoroutine(FlickerCycleRoutine());
    }

    void OnDestroy()
    {
        if (cycleRoutine != null) StopCoroutine(cycleRoutine);
        if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
    }

    private void BuildLightList()
    {
        lights.Clear();
        originalIntensity.Clear();

        // Find all active/inactive Light components in the scene
        var all = Resources.FindObjectsOfTypeAll<Light>();
        foreach (var l in all)
        {
            // Skip editor-only, hidden, or not in this scene
            if (!l || l.gameObject.scene.name == null) continue;

            // Only include our lamp-tagged lights
            if (!string.IsNullOrEmpty(lampTag) && !l.CompareTag(lampTag)) continue;

            if (!originalIntensity.ContainsKey(l))
            {
                lights.Add(l);
                originalIntensity.Add(l, l.intensity);
            }
        }
    }

    private IEnumerator FlickerCycleRoutine()
    {
        while (true)
        {
            // HOLD: restore original intensity, play steady ambience
            foreach (var l in lights)
            {
                if (!l) continue;
                if (originalIntensity.TryGetValue(l, out var orig))
                    l.intensity = orig;
            }
            PlaySteady();
            yield return new WaitForSeconds(holdDuration);

            // FLICKER: randomize intensity for a short burst, play flicker ambience
            PlayFlicker();
            float t0 = Time.time;
            while (Time.time < t0 + flickerDuration)
            {
                foreach (var l in lights)
                {
                    if (!l || !l.enabled) continue;

                    float maxI = originalIntensity.TryGetValue(l, out var orig) ? orig : l.intensity;
                    float minI = maxI * minIntensityFactor;
                    l.intensity = Random.Range(minI, maxI);
                }

                // Small audio liveliness during flicker
                if (flickerSource != null)
                {
                    flickerSource.pitch = Random.Range(flickerPitchRange.x, flickerPitchRange.y);
                    var targetVol = Mathf.Clamp01(flickerVolume + Random.Range(-flickerVolumeWobble, flickerVolumeWobble));
                    SetVolumeInstant(flickerSource, targetVol);
                }

                yield return new WaitForSeconds(flickerRate);
            }
        }
    }

    /// <summary>
    /// Call this from your Level 3 cutscene when it finishes.
    /// It permanently kills the lights and saves a flag so they remain off when revisiting.
    /// </summary>
    public void KillLevel3Lights()
    {
        if (behavior != SceneBehavior.Level3WithShutdown) return;

        if (SaveFlags.Instance)
            SaveFlags.Instance.Set(level3LightsDeadFlag);

        SetPermanentOff();
    }

    private void SetPermanentOff()
    {
        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }

        foreach (var l in lights)
        {
            if (!l) continue;
            l.intensity = 0f;
            l.enabled = false; // ensure totally off
        }

        StopAllAudio();

        // No further control needed—disable this component.
        enabled = false;
    }

    // ---------- AUDIO HELPERS ----------

    private void EnsureAudioSources()
    {
        if (steadySource == null)
        {
            steadySource = gameObject.AddComponent<AudioSource>();
            steadySource.playOnAwake = false;
            steadySource.loop = true;
        }
        if (flickerSource == null)
        {
            flickerSource = gameObject.AddComponent<AudioSource>();
            flickerSource.playOnAwake = false;
            flickerSource.loop = true;
        }

        steadySource.clip = steadyLoop;
        steadySource.spatialBlend = spatialBlend;
        steadySource.volume = 0f;

        flickerSource.clip = flickerLoop;
        flickerSource.spatialBlend = spatialBlend;
        flickerSource.volume = 0f;
    }

    private void PlaySteady()
    {
        if (!steadySource) return;

        if (steadySource.clip && !steadySource.isPlaying) steadySource.Play();
        if (flickerSource && flickerSource.isPlaying && crossfadeTime > 0f)
            StartCrossfade(flickerSource, 0f, steadySource, steadyVolume, crossfadeTime);
        else
        {
            SetVolumeInstant(flickerSource, 0f);
            SetVolumeInstant(steadySource, steadyVolume);
        }
    }

    private void PlayFlicker()
    {
        if (!flickerSource) return;

        if (flickerSource.clip && !flickerSource.isPlaying) flickerSource.Play();
        if (steadySource && steadySource.isPlaying && crossfadeTime > 0f)
            StartCrossfade(steadySource, 0f, flickerSource, flickerVolume, crossfadeTime);
        else
        {
            SetVolumeInstant(steadySource, 0f);
            SetVolumeInstant(flickerSource, flickerVolume);
        }
    }

    private void StopAllAudio()
    {
        if (crossfadeRoutine != null)
        {
            StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = null;
        }
        if (steadySource)
        {
            steadySource.volume = 0f;
            steadySource.Stop();
        }
        if (flickerSource)
        {
            flickerSource.volume = 0f;
            flickerSource.Stop();
        }
    }

    private void SetVolumeInstant(AudioSource src, float vol)
    {
        if (!src) return;
        src.volume = Mathf.Clamp01(vol);
        if (src.volume > 0f && !src.isPlaying && src.clip) src.Play();
        if (src.volume == 0f && src.isPlaying && crossfadeTime <= 0f) src.Stop();
    }

    private void StartCrossfade(AudioSource from, float fromTarget, AudioSource to, float toTarget, float time)
    {
        if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
        crossfadeRoutine = StartCoroutine(CrossfadeCo(from, fromTarget, to, toTarget, time));
    }

    private IEnumerator CrossfadeCo(AudioSource from, float fromTarget, AudioSource to, float toTarget, float time)
    {
        float t = 0f;
        float startFrom = from ? from.volume : 0f;
        float startTo = to ? to.volume : 0f;

        if (to && to.clip && !to.isPlaying) to.Play();

        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            if (from) from.volume = Mathf.Lerp(startFrom, Mathf.Clamp01(fromTarget), k);
            if (to)   to.volume   = Mathf.Lerp(startTo,   Mathf.Clamp01(toTarget),   k);
            yield return null;
        }

        if (from)
        {
            from.volume = Mathf.Clamp01(fromTarget);
            if (from.volume <= 0f) from.Stop();
        }
        if (to) to.volume = Mathf.Clamp01(toTarget);

        crossfadeRoutine = null;
    }
}
