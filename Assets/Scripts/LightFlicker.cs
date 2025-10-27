using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level light controller that:
///  - Flickers only lights tagged "LampLight"
///  - For Level 3: can be told (by cutscene) to kill lights permanently and remember that
/// </summary>
public class LightFlickerManager : MonoBehaviour
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

    // Internals
    private readonly List<Light> lights = new List<Light>();
    private readonly Dictionary<Light, float> originalIntensity = new Dictionary<Light, float>();
    private Coroutine cycleRoutine;

    void Awake()
    {
        BuildLightList();
    }

    void Start()
    {
        // If behavior is disabled, do nothing.
        if (behavior == SceneBehavior.Disabled) return;

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
            // HOLD: restore original intensity
            foreach (var l in lights)
            {
                if (!l) continue;
                if (originalIntensity.TryGetValue(l, out var orig))
                    l.intensity = orig;
            }
            yield return new WaitForSeconds(holdDuration);

            // FLICKER: randomize intensity for a short burst
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

        // No further control needed—disable this component.
        enabled = false;
    }
}
