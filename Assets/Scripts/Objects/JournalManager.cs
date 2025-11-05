using System.Collections.Generic;
using UnityEngine;

public class JournalManager : MonoBehaviour
{
    public static JournalManager Instance { get; private set; }

    // Collected pages for this session
    private readonly HashSet<string> collected = new HashSet<string>();

    [Header("Persistence")]
    [Tooltip("If true, collected pages are marked in SaveFlags on collect; persist with SaveFlags.Commit().")]
    public bool mirrorToSaveFlags = true;
    public string saveFlagPrefix = "Journal_";

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool Has(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (collected.Contains(id)) return true;

        // Also honor saved flags if they exist (checkpoint model)
        if (SaveFlags.Instance && SaveFlags.Instance.Has(saveFlagPrefix + id))
        {
            collected.Add(id);
            return true;
        }
        return false;
    }

    public void Collect(JournalPageData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return;
        if (!collected.Add(data.id)) return;

        if (mirrorToSaveFlags && SaveFlags.Instance)
            SaveFlags.Instance.Set(saveFlagPrefix + data.id);

        // TODO: Open your Journal UI, add to list, etc.
        Debug.Log($"[Journal] Collected: {data.title} ({data.id})");
    }
}
