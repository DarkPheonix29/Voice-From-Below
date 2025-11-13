using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Hoofdpanelen")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject loadSavesPanel; // full-screen panel for save loading
    public GameObject windowPanel;    // window in LoadSavesPanel (title + slots + list)
    public GameObject actionPanel;    // small panel with Load/Delete/Cancel

    [Header("Action panel")]
    public TextMeshProUGUI actionTitle;

    [Header("Start level")]
    public string firstLevelSceneName = "Level 1";

    [Header("Brightness Overlay (optional)")]
    public Image brightnessOverlay;

    [Header("Continue / Load UI")]
    public Button continueButton;            // Hide if no saves
    public Transform loadListContainer;      // Parent for instantiated rows
    public GameObject loadItemPrefab;        // Prefab with two TMP texts + a Button

    [Header("Load Panel UX")]
    [Tooltip("Optional: label that says 'No saves found' inside the load panel.")]
    public TextMeshProUGUI emptyLabel;
    [Tooltip("If true, will create a SaveFlags object at runtime when missing.")]
    public bool autoBootstrapSaveFlags = true;

    [Tooltip("Enable pressing Escape to go back from the Load panel.")]
    public bool enableEscapeBack = true;

    // Internal
    private List<SaveFlags.SaveRecord> cachedSaves = new();
    private int currentSlot = -1; // index in cachedSaves for action panel

    void Awake()
    {
        // Ensure SaveFlags exists in the Main Menu scene (singleton destroys dupes later).
        if (!SaveFlags.Instance && autoBootstrapSaveFlags)
        {
            var go = new GameObject("SaveFlags_AutoBootstrap");
            go.AddComponent<SaveFlags>();
            Debug.Log("[MainMenuUI] Auto-bootstrapped SaveFlags in main menu scene.");
        }
    }

    void Start()
    {
        if (brightnessOverlay && GameSettingsManager.Instance)
        {
            float v01 = GameSettingsManager.Instance.Brightness01;
            ApplyOverlay(v01);
        }

        if (SaveFlags.Instance != null)
        {
            var count = SaveFlags.Instance.GetAllSaves()?.Count ?? 0;
            Debug.Log($"[MainMenuUI] SaveFlags alive. Found {count} autosave slots.");
        }
        else
        {
            Debug.LogWarning("[MainMenuUI] SaveFlags.Instance is NULL. Add SaveFlags to main menu scene or enable autoBootstrapSaveFlags.");
        }

        RefreshSavesUI();

        // Make sure panels start in a sane state
        if (mainPanel) mainPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (loadSavesPanel) loadSavesPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
        if (actionPanel) actionPanel.SetActive(false);
    }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        if (enableEscapeBack && loadSavesPanel && loadSavesPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            // If action panel is up, cancel that first; else close the whole load menu.
            if (actionPanel && actionPanel.activeSelf)
                OnActionCancelPressed();
            else
                CloseLoadMenu();
        }
#endif
    }

    void ApplyOverlay(float v01)
    {
        if (!brightnessOverlay) return;
        float darkness = Mathf.Lerp(0.6f, 0f, v01);
        var c = brightnessOverlay.color;
        c.a = darkness;
        brightnessOverlay.color = c;
    }

    // ------------------ Main Menu ------------------

    public void StartGame()
    {
        if (string.IsNullOrEmpty(firstLevelSceneName))
        {
            Debug.LogWarning("[MainMenuUI] firstLevelSceneName is empty.");
            return;
        }
        Debug.Log("[MainMenuUI] StartGame");
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void OpenSettings()
    {
        Debug.Log("[MainMenuUI] OpenSettings");
        if (mainPanel) mainPanel.SetActive(false);
        if (loadSavesPanel) loadSavesPanel.SetActive(false);
        if (actionPanel) actionPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        Debug.Log("[MainMenuUI] CloseSettings");
        if (settingsPanel) settingsPanel.SetActive(false);
        if (mainPanel) mainPanel.SetActive(true);

        if (brightnessOverlay && GameSettingsManager.Instance)
        {
            float v01 = GameSettingsManager.Instance.Brightness01;
            ApplyOverlay(v01);
        }
    }

    public void QuitGame()
    {
        Debug.Log("[MainMenuUI] QuitGame");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------ Continue / Load ------------------

    void RefreshSavesUI()
    {
        cachedSaves = SaveFlags.Instance ? SaveFlags.Instance.GetAllSaves()
                                         : new List<SaveFlags.SaveRecord>();

        bool hasAny = cachedSaves != null && cachedSaves.Count > 0;

        if (continueButton)
        {
            continueButton.gameObject.SetActive(hasAny);
            continueButton.interactable = hasAny;
        }
    }

    public void OnContinueButton()
    {
        if (!SaveFlags.Instance) { Debug.LogWarning("[MainMenuUI] No SaveFlags in scene."); return; }
        var rec = SaveFlags.Instance.GetMostRecentSave();
        if (rec == null)
        {
            Debug.Log("[MainMenuUI] No recent save found. Hiding Continue.");
            if (continueButton) continueButton.gameObject.SetActive(false);
            return;
        }
        StartFromSave(rec);
    }

    // Entry point from your "Load Game" button on the main panel
    public void LoadGame()
    {
        OpenLoadMenu();
    }

    // ------------------ Load Saves panel ------------------

    public void OpenLoadMenu()
    {
        Debug.Log("[MainMenuUI] OpenLoadMenu");
        currentSlot = -1;

        if (mainPanel) mainPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);

        if (loadSavesPanel) loadSavesPanel.SetActive(true);
        if (windowPanel) windowPanel.SetActive(true);
        if (actionPanel) actionPanel.SetActive(false);

        BuildLoadList();
    }

    public void CloseLoadMenu()
    {
        Debug.Log("[MainMenuUI] CloseLoadMenu");
        currentSlot = -1;

        if (actionPanel) actionPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
        if (loadSavesPanel) loadSavesPanel.SetActive(false);
        if (mainPanel) mainPanel.SetActive(true);
    }

    /// <summary>
    /// Populates the list inside the window panel. Each row loads directly,
    /// but you can switch it to select + action panel if you want.
    /// </summary>
    void BuildLoadList()
    {
        if (!loadListContainer) { Debug.LogWarning("[MainMenuUI] loadListContainer not set."); return; }
        if (!loadItemPrefab) { Debug.LogWarning("[MainMenuUI] loadItemPrefab not set."); return; }

        // clear previous
        for (int i = loadListContainer.childCount - 1; i >= 0; i--)
            Destroy(loadListContainer.GetChild(i).gameObject);

        var saves = SaveFlags.Instance ? SaveFlags.Instance.GetAllSaves() : new List<SaveFlags.SaveRecord>();
        int count = saves?.Count ?? 0;
        Debug.Log($"[MainMenuUI] Building load list. Saves found = {count}");

        if (emptyLabel) emptyLabel.gameObject.SetActive(count == 0);

        if (count == 0) return;

        // newest first
        saves = saves.OrderByDescending(s => s.unixTimeUtc).ToList();

        // keep a cached copy for action-panel selection
        cachedSaves = saves;

        for (int i = 0; i < saves.Count; i++)
        {
            var s = saves[i];

            var go = Instantiate(loadItemPrefab, loadListContainer);
            go.name = $"SaveItem_{s.sceneName}_{s.unixTimeUtc}";

            // Expect prefab has:
            // - child TMP: "TitleText"
            // - child TMP: "TimeText"
            // - Button on root (or "LoadButton") to select
            var title = go.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            var time  = go.transform.Find("TimeText")?.GetComponent<TextMeshProUGUI>();
            var btn   = go.GetComponent<Button>() ?? go.transform.Find("LoadButton")?.GetComponent<Button>();

            if (title) title.text = string.IsNullOrEmpty(s.sceneName) ? "(Unknown Scene)" : s.sceneName;
            if (time)
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(s.unixTimeUtc).ToLocalTime().DateTime;
                time.text = dt.ToString("yyyy-MM-dd HH:mm");
            }

            if (btn)
            {
                btn.onClick.RemoveAllListeners();

                // Choose behavior:
                // A) Load immediately:
                //btn.onClick.AddListener(() => StartFromSave(s));

                // B) Or show the action panel for this slot:
                int capturedIndex = i;
                btn.onClick.AddListener(() => SelectSlot(capturedIndex));
            }
            else
            {
                Debug.LogWarning($"{go.name}: No Button found. Add a Button to the root or a child named 'LoadButton'.");
            }
        }
    }

    // Select a row to open the small action panel (Load/Delete/Cancel)
    public void SelectSlot(int slotIndex)
    {
        if (cachedSaves == null || slotIndex < 0 || slotIndex >= cachedSaves.Count)
        {
            Debug.LogWarning($"[MainMenuUI] SelectSlot: invalid index {slotIndex}.");
            return;
        }

        Debug.Log("[MainMenuUI] SelectSlot geklikt, slot = " + slotIndex);
        currentSlot = slotIndex;

        if (actionTitle)
            actionTitle.text = $"Save slot {slotIndex + 1}: {cachedSaves[slotIndex].sceneName}";

        if (windowPanel) windowPanel.SetActive(false);
        if (actionPanel) actionPanel.SetActive(true);
    }

    // ------------------ ActionPanel knoppen ------------------

    public void OnLoadPressed()
    {
        Debug.Log("[MainMenuUI] OnLoadPressed, currentSlot = " + currentSlot);
        if (currentSlot < 0 || currentSlot >= cachedSaves.Count)
        {
            Debug.LogWarning("[MainMenuUI] OnLoadPressed: invalid slot.");
            return;
        }

        var rec = cachedSaves[currentSlot];
        StartFromSave(rec);
    }

    public void OnDeletePressed()
    {
        Debug.Log("[MainMenuUI] OnDeletePressed, currentSlot = " + currentSlot);
        // TODO: implement SaveFlags delete if desired (not included in SaveFlags yet).
        // For now, just go back:
        OnActionCancelPressed();
        BuildLoadList(); // refresh list after potential deletion in future
    }

    public void OnActionCancelPressed()
    {
        Debug.Log("[MainMenuUI] OnActionCancelPressed");
        currentSlot = -1;
        if (actionPanel) actionPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
    }

    // ------------------ Load a record ------------------

    void StartFromSave(SaveFlags.SaveRecord rec)
    {
        if (rec == null) return;

        if (!SaveFlags.Instance)
        {
            Debug.LogWarning("[MainMenuUI] StartFromSave: SaveFlags not present; cannot apply flags.");
            return;
        }

        // Make these flags the active profile
        SaveFlags.Instance.LoadFromRecord(rec, replaceSaved: true);

        // Load target scene; SceneSaveBootstrap will re-apply dynamic state on Start()
        if (!string.IsNullOrEmpty(rec.sceneName))
            SceneManager.LoadScene(rec.sceneName);
        else
            Debug.LogWarning("[MainMenuUI] Save record has no sceneName.");
    }
}
