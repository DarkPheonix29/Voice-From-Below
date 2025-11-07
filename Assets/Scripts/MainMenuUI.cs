using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;

    [Header("Scene Settings")]
    [Tooltip("Naam van de eerste scene die wordt geladen bij Start Game.")]
    public string firstLevelSceneName = "Level 1";

    [Header("Brightness Overlay")]
    [Tooltip("Wijs hier je 'Darkoverlay' Image toe (boven de video).")]
    public Image brightnessOverlay;

    void Start()
    {
        // Zorg dat de overlay direct de juiste helderheid heeft bij opstarten
        if (brightnessOverlay && GameSettingsManager.Instance)
        {
            float v01 = GameSettingsManager.Instance.Brightness01;
            ApplyOverlay(v01);
        }
    }

    /// <summary>
    /// Past de alpha van de overlay aan op basis van helderheid (0=donker,1=helder)
    /// </summary>
    void ApplyOverlay(float v01)
    {
        float darkness = Mathf.Lerp(0.6f, 0f, v01);
        var c = brightnessOverlay.color;
        c.a = darkness;
        brightnessOverlay.color = c;
    }

    public void StartGame()
    {
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void LoadGame()
    {
        Debug.Log("Load Game clicked!");
    }

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);

        // Werk de overlay direct bij na het sluiten van settings
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
}
