using UnityEngine;

public class DynamiteTracker : MonoBehaviour
{
    public static int SessionCount { get; private set; }
    public static string SaveKeyPrefix = "Dyn_";

    public static void AddOne(string pickupId)
    {
        SessionCount++;
        if (SaveFlags.Instance) SaveFlags.Instance.Set(SaveKeyPrefix + pickupId);
    }

    public static void BootstrapFromSave(string[] knownPickupIds)
    {
        SessionCount = 0;
        if (SaveFlags.Instance == null || knownPickupIds == null) return;
        foreach (var id in knownPickupIds)
            if (SaveFlags.Instance.Has(SaveKeyPrefix + id))
                SessionCount++;
    }
}
