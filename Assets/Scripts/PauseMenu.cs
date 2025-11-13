using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject pausePanel;        // hoofd pauzemenu (inactive bij start)
    public GameObject settingsPanel;     // settings (inactive bij start)
    public GameObject saveConfirmPanel;  // bevestiging (inactive bij start)

    [Header("Scene Namen")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Input (alleen gebruikt bij Old Input)")]
    public KeyCode legacyToggleKey = KeyCode.Escape;

    [Header("Gameplay UI verbergen")]
    [Tooltip("Naam van je speler-UI root. Wordt automatisch gezocht en onzichtbaar gemaakt tijdens pauze.")]
    public string playerUiObjectName = "UI - Player interact";

    [Tooltip("Extra objecten die mee verborgen moeten worden (optioneel).")]
    public GameObject[] hideWhilePaused; // extra’s, optioneel

    [Header("Optioneel: scripts/inputs uitschakelen tijdens pauze")]
    public MonoBehaviour[] disableWhilePaused;

#if ENABLE_INPUT_SYSTEM
    [Header("New Input System (optioneel)")]
    public UnityEngine.InputSystem.PlayerInput playerInput;
    public string gameplayActionMap = "Gameplay";
    public string uiActionMap = "UI";
#endif

    public static bool IsPaused { get; private set; }

    float prevTimeScale = 1f;
    bool prevAudioPaused = false;
    CursorLockMode prevLockMode;
    bool prevCursorVisible;

    readonly List<VideoPlayer> pausedVideos = new();
    GameObject cachedPlayerUi; // ← hier cachen we "UI - Player interact"

    void Awake()
    {
        if (pausePanel) pausePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        IsPaused = false;

        // probeer meteen te vinden
        cachedPlayerUi = FindPlayerUI();
    }

    GameObject FindPlayerUI()
    {
        // 1) directe naam-zoek
        if (!string.IsNullOrWhiteSpace(playerUiObjectName))
        {
            var go = GameObject.Find(playerUiObjectName);
            if (go) return go;
        }
        // 2) fallback: zoek een object met vergelijkbare naam
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (!t) continue;
            var n = t.name.ToLowerInvariant();
            if (n.Contains("ui") && n.Contains("player") && n.Contains("interact"))
                return t.gameObject;
        }
        return null;
    }

    void Update()
    {
        if (PressedPauseThisFrame())
        {
            if (IsPaused)
            {
                if (saveConfirmPanel && saveConfirmPanel.activeSelf) { OnSaveConfirmCancel(); return; }
                if (settingsPanel && settingsPanel.activeSelf) { CloseSettings(); return; }
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    bool PressedPauseThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gpStart = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        bool gpSelect = Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
        return esc || gpStart || gpSelect;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(legacyToggleKey);
#else
        return false;
#endif
    }

    // ===================== Pauze / Resume =====================
    public void PauseGame()
    {
        if (IsPaused) return;
        IsPaused = true;

        prevTimeScale = Time.timeScale;
        prevAudioPaused = AudioListener.pause;
        prevLockMode = Cursor.lockState;
        prevCursorVisible = Cursor.visible;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        pausedVideos.Clear();
        var videos = FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var vp in videos)
        {
            if (vp && vp.isActiveAndEnabled && vp.isPlaying)
            {
                vp.Pause();
                pausedVideos.Add(vp);
            }
        }

        DisableGameplayInput();
        SetGameplayUIVisible(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pausePanel) pausePanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;
        IsPaused = false;

        if (pausePanel) pausePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);

        foreach (var vp in pausedVideos) if (vp) vp.Play();
        pausedVideos.Clear();

        Time.timeScale = prevTimeScale == 0f ? 1f : prevTimeScale;
        AudioListener.pause = prevAudioPaused;

        EnableGameplayInput();
        SetGameplayUIVisible(true);

        Cursor.lockState = prevLockMode;
        Cursor.visible = prevCursorVisible;
    }

    // ===================== UI-knoppen =====================
    public void OnResumeButton() => ResumeGame();

    public void OnOpenSettingsButton()
    {
        if (!IsPaused) PauseGame();
        if (pausePanel) pausePanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void OnCloseSettingsButton() => CloseSettings();
    void CloseSettings()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(true);
    }

    public void OnBackToMainMenuButton()
    {
        if (!IsPaused) PauseGame();
        if (pausePanel) pausePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(true);
    }

    public void OnQuitButton()
    {
        RestoreRealtime();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnSaveConfirmYes() { RestoreRealtime(); SaveGame(); LoadMainMenu(); }
    public void OnSaveConfirmNo() { RestoreRealtime(); LoadMainMenu(); }
    public void OnSaveConfirmCancel()
    {
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(true);
    }

    // ===================== Helpers =====================
    void RestoreRealtime()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        pausedVideos.Clear();
        IsPaused = false;

        EnableGameplayInput();
        SetGameplayUIVisible(true);
    }

    void LoadMainMenu()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Debug.LogWarning("PauseMenu: mainMenuSceneName is niet ingesteld.");
    }

    void SaveGame()
    {
        // Persist all current session flags
        SaveFlags.Instance?.Commit();

        // Persist dynamic scene objects (e.g., boxes) for the current level
        var active = SceneManager.GetActiveScene();
        if (active.IsValid())
        {
            SaveFlags.Instance?.SaveSceneDynamicState(active.name);
            // Also ensure there is an autosave slot for this level (created once)
            SaveFlags.Instance?.RecordLevelEntryAndSave(active.name);
        }

        Debug.Log("PauseMenu: SaveGame() complete.");
    }

    // ====== Gameplay UI zichtbaar/onzichtbaar ======
    void SetGameplayUIVisible(bool visible)
    {
        // hoofd UI op naam
        if (!cachedPlayerUi || !cachedPlayerUi.scene.IsValid())
            cachedPlayerUi = FindPlayerUI();

        if (cachedPlayerUi) cachedPlayerUi.SetActive(visible);

        // eventuele extra’s
        if (hideWhilePaused != null)
        {
            foreach (var go in hideWhilePaused)
                if (go) go.SetActive(visible);
        }
    }

    // ====== besturing tijdelijk uit/aan (optioneel) ======
    void DisableGameplayInput()
    {
        if (disableWhilePaused != null)
        {
            foreach (var mb in disableWhilePaused)
                if (mb && mb.enabled) mb.enabled = false;
        }

#if ENABLE_INPUT_SYSTEM
        if (playerInput)
        {
            var actions = playerInput.actions;
            if (!string.IsNullOrEmpty(uiActionMap) && actions != null && actions.FindActionMap(uiActionMap) != null)
                playerInput.SwitchCurrentActionMap(uiActionMap);
            else
            {
                playerInput.currentActionMap?.Disable();
                if (actions != null) foreach (var map in actions.actionMaps) map.Disable();
            }
        }
#endif
    }

    void EnableGameplayInput()
    {
        if (disableWhilePaused != null)
        {
            foreach (var mb in disableWhilePaused)
                if (mb && !mb.enabled) mb.enabled = true;
        }

#if ENABLE_INPUT_SYSTEM
        if (playerInput)
        {
            var actions = playerInput.actions;
            if (!string.IsNullOrEmpty(gameplayActionMap) && actions != null && actions.FindActionMap(gameplayActionMap) != null)
                playerInput.SwitchCurrentActionMap(gameplayActionMap);
            else
            {
                if (actions != null) foreach (var map in actions.actionMaps) map.Enable();
            }
        }
#endif
    }
}
