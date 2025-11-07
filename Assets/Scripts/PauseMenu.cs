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

    [Header("Te disablen components (optioneel, handmatig)")]
    [Tooltip("Sleep hier je look/move scripts in (bv. FirstPersonController, CameraLook). "
           + "Ze worden uit gezet bij pauze, aan bij resume.")]
    public MonoBehaviour[] disableWhilePaused;

#if ENABLE_INPUT_SYSTEM
    [Header("New Input System (optioneel)")]
    [Tooltip("PlayerInput van je speler. Bij pauze schakelen we naar UI of disablen we alle maps.")]
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

    void Awake()
    {
        if (pausePanel)       pausePanel.SetActive(false);
        if (settingsPanel)    settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        IsPaused = false;
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
        bool esc      = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gpStart  = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        bool gpSelect = Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
        return esc || gpStart || gpSelect;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
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

        // Lopende VideoPlayers pauzeren
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

        // Gameplay-input blokkeren
        DisableGameplayInput_Aggressive();

        // Cursor vrij voor UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pausePanel)       pausePanel.SetActive(true);
        if (settingsPanel)    settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;
        IsPaused = false;

        if (pausePanel)       pausePanel.SetActive(false);
        if (settingsPanel)    settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);

        foreach (var vp in pausedVideos) if (vp) vp.Play();
        pausedVideos.Clear();

        Time.timeScale = prevTimeScale == 0f ? 1f : prevTimeScale;
        AudioListener.pause = prevAudioPaused;

        EnableGameplayInput_Aggressive();

        Cursor.lockState = prevLockMode;
        Cursor.visible = prevCursorVisible;
    }

    // ===================== UI-knoppen =====================
    public void OnResumeButton() => ResumeGame();

    public void OnOpenSettingsButton()
    {
        if (!IsPaused) PauseGame();
        if (pausePanel)       pausePanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (settingsPanel)    settingsPanel.SetActive(true);
    }

    public void OnCloseSettingsButton() => CloseSettings();
    void CloseSettings()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (pausePanel)    pausePanel.SetActive(true);
    }

    public void OnBackToMainMenuButton()
    {
        if (!IsPaused) PauseGame();
        if (pausePanel)       pausePanel.SetActive(false);
        if (settingsPanel)    settingsPanel.SetActive(false);
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
    public void OnSaveConfirmNo()  { RestoreRealtime(); LoadMainMenu(); }
    public void OnSaveConfirmCancel()
    {
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (pausePanel)       pausePanel.SetActive(true);
    }

    // ===================== Helpers =====================
    void RestoreRealtime()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        pausedVideos.Clear();
        IsPaused = false;
        EnableGameplayInput_Aggressive();
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
        // TODO: vervang door je eigen save-systeem
        Debug.Log("PauseMenu: SaveGame() aangeroepen (hier jouw save-logica).");
    }

    // ====== Agressief (auto) uitschakelen van look/move ======
    void DisableGameplayInput_Aggressive()
    {
        // 1) Handmatig opgegeven componenten uit
        if (disableWhilePaused != null)
        {
            foreach (var mb in disableWhilePaused)
                if (mb && mb.enabled) mb.enabled = false;
        }

        // 2) Bekende look/move-controllers automatisch uitschakelen op naam
        AutoToggleByTypeName(false, new[]
        {
            "FirstPersonController",
            "ThirdPersonController",
            "StarterAssetsInputs",
            "PlayerController",
            "CharacterControllerMover",
            "CameraLook",
            "MouseLook",
            "CinemachineInputProvider",
            "FreeLookCam",
            "FPSController",
            "TPPController"
        });

#if ENABLE_INPUT_SYSTEM
        // 3) PlayerInput -> UI map of alles disablen
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

    void EnableGameplayInput_Aggressive()
    {
        // 1) Handmatig opgegeven componenten aan
        if (disableWhilePaused != null)
        {
            foreach (var mb in disableWhilePaused)
                if (mb && !mb.enabled) mb.enabled = true;
        }

        // 2) Bekende look/move-controllers weer aan
        AutoToggleByTypeName(true, new[]
        {
            "FirstPersonController",
            "ThirdPersonController",
            "StarterAssetsInputs",
            "PlayerController",
            "CharacterControllerMover",
            "CameraLook",
            "MouseLook",
            "CinemachineInputProvider",
            "FreeLookCam",
            "FPSController",
            "TPPController"
        });

#if ENABLE_INPUT_SYSTEM
        // 3) PlayerInput -> gameplay map of alles enablen
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

    // Helper: zet componenten aan/uit op basis van type-naam (zonder hard dependency)
    void AutoToggleByTypeName(bool enable, string[] typeNames)
    {
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mb in behaviours)
        {
            if (!mb) continue;
            var n = mb.GetType().Name;
            for (int i = 0; i < typeNames.Length; i++)
            {
                if (n == typeNames[i])
                {
                    mb.enabled = enable;
                    break;
                }
            }
        }
    }
}

