using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject settingsPanel;
    public GameObject saveConfirmPanel;
    [Tooltip("Panel where you pick Slot 1 / Slot 2.")]
    public GameObject saveSlotPanel;

    [Header("Scene Namen")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Player for saving position")]
    [Tooltip("Player object whose position/rotation is stored in saves. If left empty, will try tag 'Player'.")]
    public Transform playerTransform;

    [Header("Input (alleen gebruikt bij Old Input)")]
    public KeyCode legacyToggleKey = KeyCode.Escape;

    [Header("Gameplay UI verbergen")]
    [Tooltip("Naam van je speler-UI root. Wordt automatisch gezocht en onzichtbaar gemaakt tijdens pauze.")]
    public string playerUiObjectName = "UI - Player interact";

    [Tooltip("Extra objecten die mee verborgen moeten worden (optioneel).")]
    public GameObject[] hideWhilePaused;

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
    GameObject cachedPlayerUi;

    void Awake()
    {
        if (pausePanel)      pausePanel.SetActive(false);
        if (settingsPanel)   settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (saveSlotPanel)   saveSlotPanel.SetActive(false);
        IsPaused = false;

        cachedPlayerUi = FindPlayerUI();
    }

    GameObject FindPlayerUI()
    {
        if (!string.IsNullOrWhiteSpace(playerUiObjectName))
        {
            var go = GameObject.Find(playerUiObjectName);
            if (go) return go;
        }

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
                if (saveSlotPanel && saveSlotPanel.activeSelf)       { OnSaveSlotCancel(); return; }
                if (saveConfirmPanel && saveConfirmPanel.activeSelf) { OnSaveConfirmCancel(); return; }
                if (settingsPanel && settingsPanel.activeSelf)       { CloseSettings(); return; }
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

        // Ensure there's an EventSystem so UI buttons work in every scene
        if (EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif
            Debug.Log("PauseMenu: Spawned EventSystem in this scene.");
        }

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

        if (pausePanel)      pausePanel.SetActive(true);
        if (settingsPanel)   settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (saveSlotPanel)   saveSlotPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;
        IsPaused = false;

        if (pausePanel)      pausePanel.SetActive(false);
        if (settingsPanel)   settingsPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (saveSlotPanel)   saveSlotPanel.SetActive(false);

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
        if (pausePanel)      pausePanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (saveSlotPanel)   saveSlotPanel.SetActive(false);
        if (settingsPanel)   settingsPanel.SetActive(true);
    }

    public void OnCloseSettingsButton() => CloseSettings();
    void CloseSettings()
    {
        if (settingsPanel)   settingsPanel.SetActive(false);
        if (pausePanel)      pausePanel.SetActive(true);
    }

    public void OnBackToMainMenuButton()
    {
        if (!IsPaused) PauseGame();
        if (pausePanel)      pausePanel.SetActive(false);
        if (settingsPanel)   settingsPanel.SetActive(false);
        if (saveSlotPanel)   saveSlotPanel.SetActive(false);
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

    // ---- First confirm panel (Save? / Don't save / Cancel) ----

    // "Save and back to main menu"
    public void OnSaveConfirmYes()
    {
        if (saveSlotPanel)
        {
            if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
            saveSlotPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("PauseMenu: saveSlotPanel not assigned; doing autosave only.");
            SaveAutoForCurrentScene();
            RestoreRealtime();
            LoadMainMenu();
        }
    }

    // "Don't save, just go"
    public void OnSaveConfirmNo()
    {
        RestoreRealtime();
        LoadMainMenu();
    }

    // "Cancel" on first confirm
    public void OnSaveConfirmCancel()
    {
        if (saveConfirmPanel) saveConfirmPanel.SetActive(false);
        if (pausePanel)       pausePanel.SetActive(true);
    }

    // ---- Slot picker panel ----

    public void OnChooseSaveSlot1() => SaveAndReturnUsingManualSlot(0);
    public void OnChooseSaveSlot2() => SaveAndReturnUsingManualSlot(1);

    public void OnSaveSlotCancel()
    {
        if (saveSlotPanel)    saveSlotPanel.SetActive(false);
        if (saveConfirmPanel) saveConfirmPanel.SetActive(true);
    }

    // ===================== Saving helpers =====================

    // Grab current player pose (with fallback to tag "Player")
    void GetPlayerPose(out Vector3 pos, out Quaternion rot)
    {
        Transform t = playerTransform;
        if (!t)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) t = go.transform;
        }

        if (t)
        {
            pos = t.position;
            rot = t.rotation;
        }
        else
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
        }
    }

    void SaveAutoForCurrentScene()
    {
        if (!SaveFlags.Instance) return;

        SaveFlags.Instance.Commit();

        var active = SceneManager.GetActiveScene();
        if (!active.IsValid()) return;

        SaveFlags.Instance.SaveSceneDynamicState(active.name);

        GetPlayerPose(out var pos, out var rot);
        SaveFlags.Instance.UpsertAutoSaveForScene(active.name, pos, rot);
    }

    // Called when choosing Slot 1/2 and then going to main menu
    void SaveAndReturnUsingManualSlot(int slotIndex)
    {
        if (SaveFlags.Instance)
        {
            SaveFlags.Instance.Commit();

            var active = SceneManager.GetActiveScene();
            if (active.IsValid())
            {
                SaveFlags.Instance.SaveSceneDynamicState(active.name);

                GetPlayerPose(out var pos, out var rot);

                SaveFlags.Instance.SaveManualToSlot(slotIndex, active.name, pos, rot);
                SaveFlags.Instance.UpsertAutoSaveForScene(active.name, pos, rot);
            }
        }

        RestoreRealtime();
        LoadMainMenu();
    }

    // ===================== Misc helpers =====================
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
        // Make sure gameplay UI (including stamina bar) is hidden when going to main menu.
        SetGameplayUIVisible(false);

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Debug.LogWarning("PauseMenu: mainMenuSceneName is niet ingesteld.");
    }

    void SetGameplayUIVisible(bool visible)
    {
        if (!cachedPlayerUi || !cachedPlayerUi.scene.IsValid())
            cachedPlayerUi = FindPlayerUI();

        if (cachedPlayerUi) cachedPlayerUi.SetActive(visible);

        if (hideWhilePaused != null)
        {
            foreach (var go in hideWhilePaused)
                if (go) go.SetActive(visible);
        }
    }

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
            var uiMap = (!string.IsNullOrEmpty(uiActionMap) && actions != null)
                        ? actions.FindActionMap(uiActionMap) : null;

            if (uiMap != null)
            {
                playerInput.SwitchCurrentActionMap(uiActionMap);
                Debug.Log($"PauseMenu: switched to UI map '{uiActionMap}'.");
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
            var gameMap = (!string.IsNullOrEmpty(gameplayActionMap) && actions != null)
                          ? actions.FindActionMap(gameplayActionMap) : null;

            if (gameMap != null)
            {
                playerInput.SwitchCurrentActionMap(gameplayActionMap);
            }
            else
            {
                if (actions != null) foreach (var map in actions.actionMaps) map.Enable();
            }
        }
#endif
    }
}
