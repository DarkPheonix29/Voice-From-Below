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
    public GameObject loadSavesPanel; // full-screen load panel
    public GameObject windowPanel;    // window with title + slots list
    public GameObject actionPanel;    // small panel with Load/Delete/Cancel

    [Header("Action panel")]
    public TextMeshProUGUI actionTitle;

    [Header("Start level")]
    public string firstLevelSceneName = "Level 1";

    [Header("Brightness Overlay (optional)")]
    public Image brightnessOverlay;

    [Header("Continue / Load UI")]
    public Button continueButton;      // optional "Continue" button on main panel
    public Transform loadListContainer; // assign SlotsRoot here

    [Header("Load Panel UX")]
    public TextMeshProUGUI emptyLabel;
    public bool autoBootstrapSaveFlags = true;
    public bool enableEscapeBack = true;

    // internal
    private List<SaveFlags.SaveRecord> cachedSaves = new();
    private int currentSlot = -1; // index into cachedSaves (0..2)

    void Awake()
    {
        if (!SaveFlags.Instance && autoBootstrapSaveFlags)
        {
            var go = new GameObject("SaveFlags_AutoBootstrap");
            go.AddComponent<SaveFlags>();
            Debug.Log("[MainMenuUI] Auto-bootstrapped SaveFlags.");
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
            Debug.LogWarning("[MainMenuUI] SaveFlags.Instance is NULL.");
        }

        // sane initial state
        if (mainPanel) mainPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (loadSavesPanel) loadSavesPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
        if (actionPanel) actionPanel.SetActive(false);

        RefreshSavesUI();
    }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        if (enableEscapeBack && loadSavesPanel && loadSavesPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            if (actionPanel && actionPanel.activeSelf) OnActionCancelPressed();
            else CloseLoadMenu();
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

    // -------- Main menu --------

    public void StartGame()
    {
        if (string.IsNullOrEmpty(firstLevelSceneName))
        {
            Debug.LogWarning("[MainMenuUI] firstLevelSceneName is empty.");
            return;
        }
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void OpenSettings()
    {
        if (mainPanel) mainPanel.SetActive(false);
        if (loadSavesPanel) loadSavesPanel.SetActive(false);
        if (actionPanel) actionPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
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
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // -------- Continue / Load --------

    void RefreshSavesUI()
    {
        cachedSaves = SaveFlags.Instance ? SaveFlags.Instance.GetAllSaves() : new List<SaveFlags.SaveRecord>();
        bool hasAny = cachedSaves != null && cachedSaves.Count > 0;
        if (continueButton)
        {
            continueButton.gameObject.SetActive(hasAny);
            continueButton.interactable = hasAny;
        }
    }

    public void OnContinueButton()
    {
        if (!SaveFlags.Instance) { Debug.LogWarning("[MainMenuUI] No SaveFlags."); return; }
        var rec = SaveFlags.Instance.GetMostRecentSave();
        if (rec == null)
        {
            if (continueButton) continueButton.gameObject.SetActive(false);
            return;
        }
        StartFromSave(rec);
    }

    public void LoadGame() => OpenLoadMenu();

    // -------- Load panel --------

    public void OpenLoadMenu()
    {
        if (!loadListContainer)
        {
            Debug.LogWarning("[MainMenuUI] Assign loadListContainer (SlotsRoot).");
            return;
        }

        currentSlot = -1;

        if (mainPanel) mainPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);

        if (loadSavesPanel) loadSavesPanel.SetActive(true);
        if (windowPanel) windowPanel.SetActive(true);
        if (actionPanel) actionPanel.SetActive(false);

        BuildFixedSlotList(); // <- uses Slot1/2/3 under SlotsRoot
    }

    public void CloseLoadMenu()
    {
        currentSlot = -1;
        if (actionPanel) actionPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
        if (loadSavesPanel) loadSavesPanel.SetActive(false);
        if (mainPanel) mainPanel.SetActive(true);
    }

    /// <summary>
    /// Populate the three fixed slot buttons (Slot1/Slot2/Slot3) and wire clicks.
    /// </summary>
    void BuildFixedSlotList()
    {
        var saves = SaveFlags.Instance ? SaveFlags.Instance.GetAllSaves() : new List<SaveFlags.SaveRecord>();
        saves = saves?.OrderByDescending(s => s.unixTimeUtc).ToList() ?? new List<SaveFlags.SaveRecord>();
        cachedSaves = saves; // so SelectSlot and OnLoadPressed can use it

        int total = saves.Count;
        if (emptyLabel) emptyLabel.gameObject.SetActive(total == 0);

        for (int i = 0; i < 3; i++)
        {
            var slot = loadListContainer.Find($"Slot{i + 1}");
            if (!slot) continue;

            var btn = slot.GetComponent<Button>();
            var title = slot.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            var time = slot.Find("TimeText")?.GetComponent<TextMeshProUGUI>();

            // Clear previous listeners to avoid stacking
            if (btn) btn.onClick.RemoveAllListeners();

            if (i < total)
            {
                var s = saves[i];
                if (title) title.text = string.IsNullOrEmpty(s.sceneName) ? "(Unknown Scene)" : s.sceneName;
                if (time)
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(s.unixTimeUtc).ToLocalTime().DateTime;
                    time.text = dt.ToString("yyyy-MM-dd HH:mm");
                }

                if (btn)
                {
                    int captured = i;
                    btn.interactable = true;
                    btn.onClick.AddListener(() => SelectSlot(captured));
                }
            }
            else
            {
                if (title) title.text = "Empty Slot";
                if (time) time.text = "";
                if (btn) btn.interactable = false; // prevent invalid index clicks
            }
        }
    }

    public void SelectSlot(int slotIndex)
    {
        if (cachedSaves == null || slotIndex < 0 || slotIndex >= cachedSaves.Count)
        {
            Debug.LogWarning("[MainMenuUI] SelectSlot: invalid index " + slotIndex);
            return;
        }

        currentSlot = slotIndex;

        if (actionTitle)
        {
            var s = cachedSaves[slotIndex];
            actionTitle.text = $"Save slot {slotIndex + 1}: {s.sceneName}";
        }

        if (windowPanel) windowPanel.SetActive(false);
        if (actionPanel) actionPanel.SetActive(true);
    }

    // -------- Action panel buttons --------

    public void OnLoadPressed()
    {
        if (currentSlot < 0 || currentSlot >= cachedSaves.Count)
        {
            Debug.LogWarning("[MainMenuUI] OnLoadPressed: invalid slot.");
            return;
        }
        StartFromSave(cachedSaves[currentSlot]);
    }

    public void OnDeletePressed()
    {
        // Not implemented: you can add a delete method on SaveFlags if needed.
        OnActionCancelPressed();
        BuildFixedSlotList(); // refresh labels/buttons
    }

    public void OnActionCancelPressed()
    {
        currentSlot = -1;
        if (actionPanel) actionPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
    }

    // -------- Load record --------

    void StartFromSave(SaveFlags.SaveRecord rec)
    {
        if (rec == null) return;
        if (!SaveFlags.Instance)
        {
            Debug.LogWarning("[MainMenuUI] StartFromSave: SaveFlags missing.");
            return;
        }

        SaveFlags.Instance.LoadFromRecord(rec, replaceSaved: true);

        if (!string.IsNullOrEmpty(rec.sceneName))
            SceneManager.LoadScene(rec.sceneName);
        else
            Debug.LogWarning("[MainMenuUI] Save record has no sceneName.");
    }
}
