using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DynamitePickup : MonoBehaviour
{
    public string pickupId = "L5_Dyn_01"; // unique per stick
    public AudioSource sfx;

    void Awake()
    {
        if (SaveFlags.Instance && SaveFlags.Instance.Has(DynamiteTracker.SaveKeyPrefix + pickupId))
            gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        DynamiteTracker.AddOne(pickupId);
        if (sfx) sfx.Play();
        gameObject.SetActive(false);
    }
}
