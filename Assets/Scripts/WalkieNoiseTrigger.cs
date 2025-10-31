using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WalkieNoiseTrigger : MonoBehaviour
{
    [Header("Instellingen per trigger")]
    [Tooltip("Welke WalkieTalkie moet ruis afspelen.")]
    public WalkieTalkie targetWalkie;   // <-- sleep hier je WalkieTalkie object in!

    [Tooltip("Hoe lang de ruis duurt (in seconden).")]
    public float duration = 3f;

    [Tooltip("Hoe hard de ruis is (0 = stil, 1 = max).")]
    [Range(0f, 1f)] 
    public float volume = 0.8f;

    [Tooltip("Het geluidsbestand dat wordt afgespeeld (optioneel).")]
    public AudioClip clip;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!targetWalkie)
        {
            Debug.LogWarning($"{name}: Geen WalkieTalkie gekoppeld aan trigger!", this);
            return;
        }

        // alleen ruis afspelen als de walkie actief is in de scene
        targetWalkie.PlayTriggerNoise(duration, volume, clip);
    }
    public void OnWalkieEnter(WalkieTalkie walkie)
    {
        if (walkie == targetWalkie)
        {
            walkie.PlayTriggerNoise(duration, volume, clip);
        }
    }
}
