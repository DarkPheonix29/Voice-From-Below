using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// SaveFlags: manages story flags, autosave/manual save slots,
/// and per-scene dynamic state (e.g., box transforms).
/// Also stores a simple player position/rotation per save.
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

    // -------------------- Save Slots (Auto + Manual) --------------------

    [Serializable]
    public class SaveRecord
    {
        public string sceneName;
        public long unixTimeUtc;           // when recorded
        public List<string> flags;         // snapshot of all saved+session flags
        public bool isAuto = true;         // true = autosave, false = manual
        public int manualSlotIndex = -1;   // 0,1,... for manual slots; -1 = none

        // simple player pose
        public Vector3 playerPosition;
        public Quaternion playerRotation;
    }

    [Serializable]
    private class SaveRecordListWrapper
    {
        public List<SaveRecord> records = new List<SaveRecord>();
    }

    // NOTE: name kept for backwards compatibility; now stores auto+manual.
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

    // -------------------- Flags API --------------------

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

    // -------------------- AUTOSAVE SLOTS --------------------

    /// <summary>
    /// First-time level entry autosave: create an autosave record ONLY if none exists yet for the scene.
    /// NOTE: this does NOT store a player pose, it's only for the "first time entered" snapshot.
    /// </summary>
    public void RecordLevelEntryAndSave(string sceneName)
    {
        if (!enablePersistence || string.IsNullOrEmpty(sceneName)) return;

        var list = LoadSaveRecords();

        bool alreadyHasAuto = list.records.Any(r =>
            r.isAuto &&
            string.Equals(r.sceneName, sceneName, StringComparison.OrdinalIgnoreCase));

        if (alreadyHasAuto)
            return; // already have an autosave for this scene

        var snapshotFlags = MakeFlagsSnapshot();

        var rec = new SaveRecord
        {
            sceneName       = sceneName,
            unixTimeUtc     = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            flags           = snapshotFlags,
            isAuto          = true,
            manualSlotIndex = -1,
            playerPosition  = Vector3.zero,
            playerRotation  = Quaternion.identity
        };
        list.records.Add(rec);
        SaveSaveRecords(list);
    }

    /// <summary>
    /// Upsert autosave for this scene (create OR update, always latest flags + player pose).
    /// Use this when doing "Save and return to main menu".
    /// </summary>
    public void UpsertAutoSaveForScene(string sceneName, Vector3 playerPos, Quaternion playerRot)
    {
        if (!enablePersistence || string.IsNullOrEmpty(sceneName)) return;

        var list = LoadSaveRecords();
        var rec = list.records.FirstOrDefault(r =>
            r.isAuto &&
            string.Equals(r.sceneName, sceneName, StringComparison.OrdinalIgnoreCase));

        var snapshot = MakeFlagsSnapshot();

        if (rec == null)
        {
            rec = new SaveRecord
            {
                sceneName       = sceneName,
                isAuto          = true,
                manualSlotIndex = -1
            };
            list.records.Add(rec);
        }

        rec.flags          = snapshot;
        rec.unixTimeUtc    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        rec.playerPosition = playerPos;
        rec.playerRotation = playerRot;

        SaveSaveRecords(list);
    }

    public SaveRecord GetLatestAutoSave()
    {
        var list = LoadSaveRecords();
        return list.records
            .Where(r => r.isAuto)
            .OrderByDescending(r => r.unixTimeUtc)
            .FirstOrDefault();
    }

    /// <summary>Returns the most recent save (auto OR manual), or null if none exist.</summary>
    public SaveRecord GetMostRecentSave()
    {
        var list = LoadSaveRecords();
        if (list.records.Count == 0) return null;
        return list.records.OrderByDescending(r => r.unixTimeUtc).FirstOrDefault();
    }

    /// <summary>Returns all saves (auto + manual).</summary>
    public List<SaveRecord> GetAllSaves()
    {
        var list = LoadSaveRecords();
        return new List<SaveRecord>(list.records);
    }

    // ------------- MANUAL SLOTS -------------

    /// <summary>
    /// Save into a *manual* slot (0,1,2...). Overwrites that slot, including player pose.
    /// </summary>
    public void SaveManualToSlot(int slotIndex, string sceneName, Vector3 playerPos, Quaternion playerRot)
    {
        if (!enablePersistence || slotIndex < 0 || string.IsNullOrEmpty(sceneName)) return;

        var list = LoadSaveRecords();

        var rec = list.records.FirstOrDefault(r => !r.isAuto && r.manualSlotIndex == slotIndex);

        var snapshot = MakeFlagsSnapshot();

        if (rec == null)
        {
            rec = new SaveRecord
            {
                sceneName       = sceneName,
                isAuto          = false,
                manualSlotIndex = slotIndex
            };
            list.records.Add(rec);
        }

        rec.flags          = snapshot;
        rec.unixTimeUtc    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        rec.playerPosition = playerPos;
        rec.playerRotation = playerRot;

        SaveSaveRecords(list);
    }

    /// <summary>Get the manual save for a specific slot (0,1,2...).</summary>
    public SaveRecord GetManualSave(int slotIndex)
    {
        var list = LoadSaveRecords();
        return list.records
            .Where(r => !r.isAuto && r.manualSlotIndex == slotIndex)
            .OrderByDescending(r => r.unixTimeUtc)
            .FirstOrDefault();
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
                id   = id,
                pos  = go.transform.position,
                rot  = go.transform.rotation,
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

    private List<string> MakeFlagsSnapshot()
    {
        var snapshot = new HashSet<string>(savedFlags);
        foreach (var f in sessionFlags) snapshot.Add(f);
        return snapshot.ToList();
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

    private string SceneBoxesKey(string sceneName) => $"scene_state_boxes_v1_{sceneName}";

    private List<GameObject> FindBoxesInScene()
    {
        var list = new List<GameObject>();
        if (string.IsNullOrWhiteSpace(boxTag)) return list;

        try
        {
            var tagged = GameObject.FindGameObjectsWithTag(boxTag);
            if (tagged != null) list.AddRange(tagged);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"SaveFlags: Tag '{boxTag}' is not defined. Either add it in Project Settings → Tags and Layers or clear boxTag.");
        }

        return list;
    }

    private string TryGetStableId(GameObject go)
    {
        var saveId = go.GetComponent<SaveId>();
        if (saveId && !string.IsNullOrEmpty(saveId.Id))
            return saveId.Id;

        return GetHierarchyPath(go.transform);
    }

    private string GetHierarchyPath(Transform t)
    {
        var stack = new Stack<string>();
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }

    // ==== APPLY / LOAD A SAVE ====
    public void ApplyFlagsSnapshot(IEnumerable<string> flags, bool replaceSaved = true, bool writeToPlayerPrefs = true)
    {
        if (flags == null) return;

        if (replaceSaved)
            savedFlags.Clear();

        foreach (var f in flags)
            if (!string.IsNullOrEmpty(f))
                savedFlags.Add(f);

        if (writeToPlayerPrefs && enablePersistence)
        {
            foreach (var id in savedFlags)
                PlayerPrefs.SetInt(keyPrefix + id, 1);
            PlayerPrefs.Save();
        }

        sessionFlags.Clear();
    }

    /// <summary>Apply the given save record (flags snapshot) to become the active progress.</summary>
    public void LoadFromRecord(SaveRecord rec, bool replaceSaved = true)
    {
        if (rec == null) return;
        ApplyFlagsSnapshot(rec.flags, replaceSaved, writeToPlayerPrefs: true);
    }

    // ================= DELETE HELPERS =================

    /// <summary>Delete a single save record (auto or manual).</summary>
    public void DeleteSaveRecord(SaveRecord rec)
    {
        if (rec == null) return;

        var list = LoadSaveRecords();

        // Try reference remove first
        bool removed = list.records.Remove(rec);

        // Fallback remove by matching fields in case instances differ
        if (!removed)
        {
            int count = list.records.RemoveAll(r =>
                r != null &&
                r.isAuto == rec.isAuto &&
                r.manualSlotIndex == rec.manualSlotIndex &&
                r.sceneName == rec.sceneName &&
                r.unixTimeUtc == rec.unixTimeUtc);
            removed = count > 0;
        }

        if (removed)
        {
            SaveSaveRecords(list);
            Debug.Log("SaveFlags: deleted save record.");
        }
        else
        {
            Debug.LogWarning("SaveFlags: DeleteSaveRecord found no matching record to delete.");
        }
    }

    /// <summary>Delete all save records (does NOT touch story flags).</summary>
    public void DeleteAllSaveRecords()
    {
        var wrapper = new SaveRecordListWrapper(); // empty
        SaveSaveRecords(wrapper);
        Debug.Log("SaveFlags: all save records deleted.");
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
