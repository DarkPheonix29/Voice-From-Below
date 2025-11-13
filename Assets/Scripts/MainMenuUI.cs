using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject loadPanel;             // panel that lists saves

    [Header("Scene Settings")]
    [Tooltip("Naam van de eerste scene die wordt geladen bij Start Game.")]
    public string firstLevelSceneName = "Level 1";

    [Header("Brightness Overlay")]
    [Tooltip("Wijs hier je 'Darkoverlay' Image toe (boven de video).")]
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

    // optional: let Esc go back from Load panel
    [Tooltip("Enable pressing Escape to go back from the Load panel.")]
    public bool enableEscapeBack = true;

    List<SaveFlags.SaveRecord> cachedSaves = new();

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
    }

    void Update()
    {
        if (enableEscapeBack && loadPanel && loadPanel.activeSelf)
        {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseLoadPanel();
#endif
        }
    }

    void ApplyOverlay(float v01)
    {
        if (!brightnessOverlay) return;
        float darkness = Mathf.Lerp(0.6f, 0f, v01);
        var c = brightnessOverlay.color;
        c.a = darkness;
        brightnessOverlay.color = c;
    }

    // ================= MAIN MENU BUTTONS =================

    public void StartGame()
    {
        if (string.IsNullOrEmpty(firstLevelSceneName))
        {
            Debug.LogWarning("[MainMenuUI] firstLevelSceneName is empty.");
            return;
        }
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void LoadGame() // open the load list panel
    {
        BuildLoadList();

        if (!mainPanel) Debug.LogWarning("[MainMenuUI] mainPanel not assigned.");
        if (!settingsPanel) Debug.LogWarning("[MainMenuUI] settingsPanel not assigned.");
        if (!loadPanel) { Debug.LogWarning("[MainMenuUI] loadPanel not assigned."); return; }

        if (mainPanel) mainPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        loadPanel.SetActive(true);
    }

    public void CloseLoadPanel()
    {
        if (!loadPanel || !mainPanel)
        {
            Debug.LogWarning("[MainMenuUI] CloseLoadPanel(): loadPanel or mainPanel not assigned.");
            return;
        }

        loadPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void OpenSettings()
    {
        if (mainPanel) mainPanel.SetActive(false);
        if (loadPanel) loadPanel.SetActive(false);
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
        Debug.Log("Quit Game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ================= CONTINUE / LOAD =================

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

        foreach (var s in saves)
        {
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
                var captured = s;
                btn.onClick.AddListener(() => StartFromSave(captured));
            }
            else
            {
                Debug.LogWarning($"{go.name}: No Button found. Add a Button to the root or a child named 'LoadButton'.");
            }
        }
    }

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
