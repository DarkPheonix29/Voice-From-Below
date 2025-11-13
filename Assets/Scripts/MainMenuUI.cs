using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Hoofdpanelen")]
    public GameObject mainPanel;      
    public GameObject settingsPanel;  
    public GameObject loadSavesPanel; 
    public GameObject windowPanel;    // window in LoadSavesPanel (title + slots + cancel)
    public GameObject actionPanel;    // klein panel met Load/Delete/Cancel

    [Header("Action panel")]
    public TextMeshProUGUI actionTitle; 

    [Header("Start level")]
    public string firstLevelSceneName = "Level 1";

    int currentSlot = -1;

    // ------------------ Hoofdmenu ------------------

    public void StartGame()
    {
        Debug.Log("[MainMenuUI] StartGame");
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void LoadGame()
    {
        Debug.Log("[MainMenuUI] LoadGame knop geklikt");
        OpenLoadMenu();
    }

    public void OpenSettings()
    {
        Debug.Log("[MainMenuUI] OpenSettings");
        if (mainPanel) mainPanel.SetActive(false);
        if (loadSavesPanel) loadSavesPanel.SetActive(false);
        if (actionPanel) actionPanel.SetActive(false);

        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        Debug.Log("[MainMenuUI] CloseSettings");
        if (settingsPanel) settingsPanel.SetActive(false);
        if (mainPanel) mainPanel.SetActive(true);
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

    // Wordt opgeroepen door File1/Slot1 enz.
    public void SelectSlot(int slotIndex)
    {
        Debug.Log("[MainMenuUI] SelectSlot geklikt, slot = " + slotIndex);
        currentSlot = slotIndex;

        if (actionTitle)
            actionTitle.text = $"Save slot {slotIndex}";

        if (windowPanel) 
        {
            Debug.Log("[MainMenuUI] windowPanel uit");
            windowPanel.SetActive(false);
        }

        if (actionPanel) 
        {
            Debug.Log("[MainMenuUI] actionPanel aan");
            actionPanel.SetActive(true);
        }
    }

    // ------------------ ActionPanel knoppen ------------------

    public void OnLoadPressed()
    {
        Debug.Log("[MainMenuUI] OnLoadPressed, currentSlot = " + currentSlot);
        // HIER later echte load-logica
    }

    public void OnDeletePressed()
    {
        Debug.Log("[MainMenuUI] OnDeletePressed, currentSlot = " + currentSlot);
        // HIER later echte delete-logica
    }

    public void OnActionCancelPressed()
    {
        Debug.Log("[MainMenuUI] OnActionCancelPressed");
        if (actionPanel) actionPanel.SetActive(false);
        if (windowPanel) windowPanel.SetActive(true);
        currentSlot = -1;
    }
}
