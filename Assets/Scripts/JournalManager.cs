using System;
using System.Collections.Generic;
using UnityEngine;

public class JournalManager : MonoBehaviour
{
    public static JournalManager Instance { get; private set; }
    public event Action<JournalPageData> OnPageCollected;

    // Keeps the order you found them; also allows lookup by id
    readonly Dictionary<string, JournalPageData> _collected = new Dictionary<string, JournalPageData>();
    public IReadOnlyCollection<JournalPageData> Collected => _collected.Values;

    void Awake()
    {
        if (Instance && Instance != this){ Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool Has(string pageId) => _collected.ContainsKey(pageId);

    public void Collect(JournalPageData data)
    {
        if (string.IsNullOrWhiteSpace(data.id)) { Debug.LogWarning("Journal page without id"); return; }
        if (_collected.ContainsKey(data.id)) return;

        _collected.Add(data.id, data);
        OnPageCollected?.Invoke(data);
        Debug.Log($"[Journal] Collected page: {data.title} ({data.id})");
    }
}

[Serializable]
public struct JournalPageData
{
    public string id;      // unique key, e.g. "mine_01"
    public string title;   // short title
    [TextArea(4,12)] public string body; // lore text
    public Sprite icon;    // optional for UI
}
