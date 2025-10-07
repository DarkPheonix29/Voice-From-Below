using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LightFlicker : MonoBehaviour
{
    // === Flicker Cycle Settings ===
    [Header("Cycle Control")]
    public float flickerDuration = 0.5f;    // How long the light spends flickering
    public float holdDuration = 3.0f;      // How long the light holds its stable intensity before flickering again

    [Header("Flicker Effect")]
    public float flickerRate = 0.1f;         // The speed of the individual intensity changes during the flicker (smaller value = faster flicker)
    [Range(0.0f, 1.0f)]
    public float minIntensityFactor = 0.5f; // Percentage (0.0 to 1.0) of the original intensity to drop to.

    // Storage
    private List<Light> allLights = new List<Light>();
    private Dictionary<Light, float> originalIntensities = new Dictionary<Light, float>();

    void Start()
    {
        // Find all lights and store their original intensity
        Light[] lightsInScene = FindObjectsOfType<Light>();
        allLights.AddRange(lightsInScene);

        foreach (Light lightComponent in allLights)
        {
            if (lightComponent != null)
            {
                originalIntensities.Add(lightComponent, lightComponent.intensity);
            }
        }
        
        StartCoroutine(FlickerCycleRoutine());
    }

    // New routine to manage the ON/FLICKER cycle
    IEnumerator FlickerCycleRoutine()
    {
        while (true)
        {
            // 1. HOLD PHASE (Every few seconds)
            // Restore all lights to their full, original intensity
            foreach (Light lightComponent in allLights)
            {
                if (lightComponent != null && originalIntensities.ContainsKey(lightComponent))
                {
                    lightComponent.intensity = originalIntensities[lightComponent];
                }
            }
            // Wait for the duration you set (e.g., 3.0 seconds)
            yield return new WaitForSeconds(holdDuration);

            // 2. FLICKER PHASE
            float startTime = Time.time;
            while (Time.time < startTime + flickerDuration)
            {
                // Iterate through every light and apply the quick flicker
                foreach (Light lightComponent in allLights)
                {
                    if (lightComponent != null && lightComponent.enabled && originalIntensities.ContainsKey(lightComponent)) 
                    {
                        float maxIntensity = originalIntensities[lightComponent];
                        float minIntensity = maxIntensity * minIntensityFactor;
                        
                        lightComponent.intensity = Random.Range(minIntensity, maxIntensity);
                    }
                }
                
                // Wait for the quick rate (e.g., 0.1 seconds)
                yield return new WaitForSeconds(flickerRate);
            }
        }
    }
}