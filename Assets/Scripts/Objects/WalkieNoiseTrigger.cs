using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WalkieNoiseTrigger : MonoBehaviour
{
    [Header("Instellingen per trigger")]
    [Tooltip("Welke WalkieTalkie moet reageren.")]
    public WalkieTalkie targetWalkie;

    [Tooltip("Hoe lang de zichtbaarheid/ruis duurt (in seconden).")]
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

        // Altijd zichtbaar maken (ongeacht pickup-status)
        targetWalkie.ForceShow(duration);

        // 🔇 Alleen geluid afspelen ALS de walkie is opgepakt
        if (IsPickedUp(targetWalkie))
        {
            if (clip || volume > 0f)
                targetWalkie.PlayTriggerNoise(duration, volume, clip);
        }
    }

    // Deze functie kijkt of de walkie al opgepakt is
    private bool IsPickedUp(WalkieTalkie walkie)
    {
        // we controleren of de walkie aan iets vastzit (zoals de camera/player)
        return walkie.transform.parent != null;
    }

    // Wanneer de walkie zelf de trigger raakt (bijv. via OnTriggerEnter in WalkieTalkie)
    public void OnWalkieEnter(WalkieTalkie walkie)
    {
        if (walkie == targetWalkie)
        {
            walkie.ForceShow(duration);

            if (IsPickedUp(walkie))
            {
                if (clip || volume > 0f)
                    walkie.PlayTriggerNoise(duration, volume, clip);
            }
        }
    }
}
