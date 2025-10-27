using System.Collections.Generic;
using UnityEngine;

public class SaveFlags : MonoBehaviour
{
    public static SaveFlags Instance { get; private set; }

    // In-memory flags for this play session
    private readonly HashSet<string> flags = new HashSet<string>();

    [Header("Persistence")]
    [Tooltip("If true, also mirror flags to PlayerPrefs so they persist across game restarts.")]
    public bool persistToPlayerPrefs = false;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool Has(string id) => !string.IsNullOrEmpty(id) && flags.Contains(id);

    public void Set(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (flags.Add(id) && persistToPlayerPrefs)
            PlayerPrefs.SetInt($"flag_{id}", 1);
    }

    public void LoadFromPlayerPrefs(string[] knownIds)
    {
        if (!persistToPlayerPrefs || knownIds == null) return;
        foreach (var id in knownIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (PlayerPrefs.GetInt($"flag_{id}", 0) == 1) flags.Add(id);
        }
    }

    // Optional helpers
    public void ClearAll(bool clearPlayerPrefs = false)
    {
        flags.Clear();
        if (clearPlayerPrefs)
        {
            // If you want to clear only known flags, keep a list and iterate them
            PlayerPrefs.DeleteAll();
        }
    }
}
