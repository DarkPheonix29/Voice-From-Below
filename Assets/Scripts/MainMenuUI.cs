using System;
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
    private SaveFlags.SaveRecord[] slotRecords = new SaveFlags.SaveRecord[3]; // 0=auto,1=manual0,2=manual1
    private int currentSlot = -1; // index into slotRecords

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
            Debug.Log($"[MainMenuUI] SaveFlags alive. Found {count} save slots.");
        }
        else
        {
            Debug.LogWarning("[MainMenuUI] SaveFlags.Instance is NULL.");
        }

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
        bool hasAny = SaveFlags.Instance && SaveFlags.Instance.GetMostRecentSave() != null;
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

        BuildFixedSlotList();
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
    /// Populate Slot1/Slot2/Slot3:
    ///   Slot1 = latest autosave
    ///   Slot2 = manual slot 0
    ///   Slot3 = manual slot 1
    /// </summary>
    void BuildFixedSlotList()
    {
        if (!SaveFlags.Instance)
        {
            Debug.LogWarning("[MainMenuUI] No SaveFlags.");
            return;
        }

        slotRecords = new SaveFlags.SaveRecord[3];

        // map: 0 = auto, 1 = manual slot 0, 2 = manual slot 1
        slotRecords[0] = SaveFlags.Instance.GetLatestAutoSave();
        slotRecords[1] = SaveFlags.Instance.GetManualSave(0);
        slotRecords[2] = SaveFlags.Instance.GetManualSave(1);

        bool any = slotRecords[0] != null || slotRecords[1] != null || slotRecords[2] != null;
        if (emptyLabel) emptyLabel.gameObject.SetActive(!any);

        for (int i = 0; i < 3; i++)
        {
            var rec = slotRecords[i];
            var slot = loadListContainer.Find($"Slot{i + 1}");
            if (!slot) continue;

            var btn = slot.GetComponent<Button>();
            var title = slot.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            var time  = slot.Find("TimeText")?.GetComponent<TextMeshProUGUI>();

            if (btn) btn.onClick.RemoveAllListeners();

            if (rec != null)
            {
                string labelPrefix = (i == 0) ? "Autosave" : $"Manual {i}";
                if (title) title.text = $"{labelPrefix} - {rec.sceneName}";
                if (time)
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(rec.unixTimeUtc).ToLocalTime().DateTime;
                    time.text = dt.ToString("yyyy-MM-dd HH:mm");
                }

                if (btn)
                {
                    int capturedIndex = i;
                    btn.interactable = true;
                    btn.onClick.AddListener(() => SelectSlot(capturedIndex));
                }
            }
            else
            {
                if (title)
                    title.text = (i == 0) ? "Autosave (empty)" : $"Manual {i} (empty)";
                if (time) time.text = "";
                if (btn) btn.interactable = false;
            }
        }
    }

    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotRecords.Length || slotRecords[slotIndex] == null)
        {
            Debug.LogWarning("[MainMenuUI] SelectSlot: invalid or empty slot " + slotIndex);
            return;
        }

        currentSlot = slotIndex;
        var rec = slotRecords[slotIndex];

        if (actionTitle)
        {
            string label = (slotIndex == 0) ? "Autosave" : $"Manual {slotIndex}";
            actionTitle.text = $"{label}: {rec.sceneName}";
        }

        if (windowPanel) windowPanel.SetActive(false);
        if (actionPanel) actionPanel.SetActive(true);
    }

    // -------- Action panel buttons --------

    public void OnLoadPressed()
    {
        if (currentSlot < 0 || currentSlot >= slotRecords.Length || slotRecords[currentSlot] == null)
        {
            Debug.LogWarning("[MainMenuUI] OnLoadPressed: invalid slot.");
            return;
        }
        StartFromSave(slotRecords[currentSlot]);
    }

    public void OnDeletePressed()
    {
        if (!SaveFlags.Instance)
        {
            Debug.LogWarning("[MainMenuUI] OnDeletePressed: SaveFlags missing.");
            return;
        }

        if (currentSlot < 0 || currentSlot >= slotRecords.Length)
        {
            Debug.LogWarning("[MainMenuUI] OnDeletePressed: invalid slot index.");
            return;
        }

        var rec = slotRecords[currentSlot];
        if (rec == null)
        {
            Debug.LogWarning("[MainMenuUI] OnDeletePressed: slot is empty.");
            return;
        }

        // delete this save record
        SaveFlags.Instance.DeleteSaveRecord(rec);

        // reset selection + UI
        currentSlot = -1;
        if (actionPanel) actionPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);

        // refresh list & continue button
        BuildFixedSlotList();
        RefreshSavesUI();
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
