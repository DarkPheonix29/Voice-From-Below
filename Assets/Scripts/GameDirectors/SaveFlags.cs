using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// SaveFlags: now also manages lightweight autosave slots (first time entering a level)
/// and per-scene dynamic state (e.g., box transforms) across level transfers.
/// </summary>
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

    [Header("Dynamic State")]
    [Tooltip("Tag used to find box objects whose transforms should be persisted across transfers.")]
    public string boxTag = "Box";

    // -------------------- Save Slots (Autosave on first entry) --------------------

    [Serializable]
    public class SaveRecord
    {
        public string sceneName;
        public long unixTimeUtc;       // when recorded (first entry time)
        public List<string> flags;     // snapshot of all saved+session flags at that time (optional/helpful)
    }

    [Serializable]
    private class SaveRecordListWrapper
    {
        public List<SaveRecord> records = new List<SaveRecord>();
    }

    private const string SaveSlotsKey = "autosave_slots_v1";

    // -------------------- Per-Scene Dynamic State (Boxes) --------------------

    [Serializable]
    public class BoxState
    {
        public string id;   // stable object id (hierarchy path or custom SaveId)
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
    }

    [Serializable]
    private class BoxStateListWrapper
    {
        public List<BoxState> boxes = new List<BoxState>();
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSavedFromPlayerPrefs();
    }

    // -------------------- Flags API (unchanged behavior) --------------------

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
            PlayerPrefs.DeleteAll();
        }
    }

    void LoadSavedFromPlayerPrefs()
    {
        // Kept intentionally empty; see LoadKnownIds for opt-in hydration of flags.
        // Slots/dynamic state load on demand from JSON.
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

    // -------------------- AUTOSAVE SLOTS (First-time level entry) --------------------

    /// <summary>
    /// Call this when entering a level. If it's the first time ever entering this scene,
    /// create a new autosave slot (one slot per level, created only once).
    /// </summary>
    public void RecordLevelEntryAndSave(string sceneName)
    {
        if (!enablePersistence || string.IsNullOrEmpty(sceneName)) return;

        var list = LoadSaveRecords();
        bool alreadyHasSlotForScene = list.records.Any(r => string.Equals(r.sceneName, sceneName, StringComparison.OrdinalIgnoreCase));
        if (alreadyHasSlotForScene)
            return; // Only the first time you ever enter this scene becomes a slot.

        // Merge both saved + current session flags into the snapshot.
        var snapshotFlags = new HashSet<string>(savedFlags);
        foreach (var f in sessionFlags) snapshotFlags.Add(f);

        var rec = new SaveRecord
        {
            sceneName = sceneName,
            unixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            flags = snapshotFlags.ToList()
        };
        list.records.Add(rec);

        SaveSaveRecords(list);
    }

    /// <summary>Returns the most recent autosave slot (by time), or null if none exist.</summary>
    public SaveRecord GetMostRecentSave()
    {
        var list = LoadSaveRecords();
        if (list.records.Count == 0) return null;
        return list.records.OrderByDescending(r => r.unixTimeUtc).FirstOrDefault();
    }

    /// <summary>Returns all autosave slots (read-only list).</summary>
    public List<SaveRecord> GetAllSaves()
    {
        var list = LoadSaveRecords();
        return new List<SaveRecord>(list.records);
    }

    private SaveRecordListWrapper LoadSaveRecords()
    {
        var json = PlayerPrefs.GetString(SaveSlotsKey, "");
        if (string.IsNullOrEmpty(json)) return new SaveRecordListWrapper();
        try
        {
            return JsonUtility.FromJson<SaveRecordListWrapper>(json) ?? new SaveRecordListWrapper();
        }
        catch
        {
            return new SaveRecordListWrapper();
        }
    }

    private void SaveSaveRecords(SaveRecordListWrapper wrapper)
    {
        var json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(SaveSlotsKey, json);
        PlayerPrefs.Save();
    }

    // -------------------- PER-SCENE DYNAMIC STATE: BOX TRANSFORMS --------------------

    /// <summary>
    /// Call this BEFORE unloading the current scene to persist all box transforms in that scene.
    /// Boxes are found via tag (boxTag). You can also add a custom SaveId component and use its Id.
    /// </summary>
    public void SaveSceneDynamicState(string sceneName)
    {
        if (!enablePersistence || string.IsNullOrEmpty(sceneName)) return;

        var boxes = FindBoxesInScene();
        var states = new BoxStateListWrapper { boxes = new List<BoxState>(boxes.Count) };

        foreach (var go in boxes)
        {
            var id = TryGetStableId(go);
            states.boxes.Add(new BoxState
            {
                id = id,
                pos = go.transform.position,
                rot = go.transform.rotation,
                scale = go.transform.localScale
            });
        }

        var json = JsonUtility.ToJson(states);
        PlayerPrefs.SetString(SceneBoxesKey(sceneName), json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Call this AFTER loading a scene to get the last known box transforms for that scene.
    /// You must apply the transforms yourself (match by id).
    /// </summary>
    public Dictionary<string, BoxState> LoadSceneDynamicState(string sceneName)
    {
        var dict = new Dictionary<string, BoxState>();
        if (string.IsNullOrEmpty(sceneName)) return dict;

        var json = PlayerPrefs.GetString(SceneBoxesKey(sceneName), "");
        if (string.IsNullOrEmpty(json)) return dict;

        try
        {
            var wrapper = JsonUtility.FromJson<BoxStateListWrapper>(json);
            if (wrapper?.boxes != null)
            {
                foreach (var b in wrapper.boxes)
                {
                    if (!string.IsNullOrEmpty(b.id))
                        dict[b.id] = b;
                }
            }
        }
        catch { /* ignore parse error */ }

        return dict;
    }

    /// <summary>
    /// Utility to apply loaded box states to the current scene. Call after you spawned/loaded boxes.
    /// Matches by stable id (hierarchy path or custom SaveId).
    /// </summary>
    public void ApplyBoxStatesToScene(string sceneName)
    {
        var map = LoadSceneDynamicState(sceneName);
        if (map.Count == 0) return;

        var boxes = FindBoxesInScene();
        foreach (var go in boxes)
        {
            var id = TryGetStableId(go);
            if (map.TryGetValue(id, out var st))
            {
                go.transform.SetPositionAndRotation(st.pos, st.rot);
                go.transform.localScale = st.scale;
            }
        }
    }

    // -------------------- Helpers --------------------

    private string SceneBoxesKey(string sceneName) => $"scene_state_boxes_v1_{sceneName}";

    private List<GameObject> FindBoxesInScene()
    {
        // Tag-based. If you prefer a component filter, replace this with FindObjectsOfType<YourBoxComponent>().
        var tagged = GameObject.FindGameObjectsWithTag(boxTag);
        return new List<GameObject>(tagged);
    }

    /// <summary>
    /// Try to obtain a stable id for an object. If a SaveId component exists, use that;
    /// else fall back to a hierarchy path which is stable as long as the structure doesn't change.
    /// </summary>
    private string TryGetStableId(GameObject go)
    {
        var saveId = go.GetComponent<SaveId>();
        if (saveId && !string.IsNullOrEmpty(saveId.Id))
            return saveId.Id;

        return GetHierarchyPath(go.transform);
    }

    private string GetHierarchyPath(Transform t)
    {
        // root/child/grandchild — stable enough for static scene hierarchies
        var stack = new Stack<string>();
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }
}

/// <summary>
/// Optional helper you can slap on any object to control the persisted id,
/// making it resilient to hierarchy/name changes.
/// </summary>
public class SaveId : MonoBehaviour
{
    [SerializeField] private string id;
    public string Id => id;

#if UNITY_EDITOR
    [ContextMenu("Generate GUID")]
    void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
