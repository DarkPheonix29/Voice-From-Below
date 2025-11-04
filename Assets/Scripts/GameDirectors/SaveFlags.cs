using System.Collections.Generic;
using UnityEngine;

public class SaveFlags : MonoBehaviour
{
    public static SaveFlags Instance { get; private set; }

    // Runtime progress (cleared on app restart)
    private readonly HashSet<string> sessionFlags = new HashSet<string>();

    // Persisted progress loaded from PlayerPrefs on boot
    private readonly HashSet<string> savedFlags = new HashSet<string>();

    [Header("Persistence")]
    [Tooltip("If true, Commit() writes to PlayerPrefs; if false, Commit() is a no-op.")]
    public bool enablePersistence = true;

    [Tooltip("PlayerPrefs key prefix. Change if you want multiple profiles.")]
    public string keyPrefix = "flag_";

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSavedFromPlayerPrefs();
    }

    /// <summary>True if the flag is completed either in this session or from a previous save.</summary>
    public bool Has(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return sessionFlags.Contains(id) || savedFlags.Contains(id);
    }

    /// <summary>Mark a flag in-memory for this session only. Not persisted until Commit().</summary>
    public void Set(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        sessionFlags.Add(id);
    }

    /// <summary>
    /// Persist ALL current session flags to PlayerPrefs (i.e., checkpoint/save).
    /// Also mirrors them into savedFlags so subsequent Has() checks in this session see them as saved.
    /// </summary>
    public void Commit()
    {
        if (!enablePersistence) return;

        foreach (var id in sessionFlags)
        {
            if (string.IsNullOrEmpty(id)) continue;
            savedFlags.Add(id);
            PlayerPrefs.SetInt(keyPrefix + id, 1);
        }
        PlayerPrefs.Save();
    }

    /// <summary>Clear ONLY the in-memory session flags (does not touch saved/progress).</summary>
    public void ClearSession()
    {
        sessionFlags.Clear();
    }

    /// <summary>Wipes both in-memory and saved flags (danger: full reset).</summary>
    public void ClearAll(bool clearPlayerPrefs = false)
    {
        sessionFlags.Clear();
        savedFlags.Clear();
        if (clearPlayerPrefs)
        {
            // If you want targeted clear, keep your own list of keys.
            PlayerPrefs.DeleteAll();
        }
    }

    void LoadSavedFromPlayerPrefs()
    {
        // We don't know all IDs up front, so we lazily hydrate on Has() is not feasible without scanning all keys.
        // Instead, you may keep a registry of known IDs; here we do a simple pattern-less load:
        // If you want exact control, provide a list and check each one.
        // For now, we do nothing here and rely on an optional API for known IDs.
    }

    /// <summary>
    /// Optional: preload a known list of IDs from PlayerPrefs.
    /// Call this once (e.g., in your bootstrap) if you maintain a registry of story trigger IDs.
    /// </summary>
    public void LoadKnownIds(string[] knownIds)
    {
        if (knownIds == null) return;
        foreach (var id in knownIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (PlayerPrefs.GetInt(keyPrefix + id, 0) == 1)
                savedFlags.Add(id);
        }
    }
}
