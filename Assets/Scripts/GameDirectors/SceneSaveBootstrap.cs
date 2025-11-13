using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSaveBootstrap : MonoBehaviour
{
    void Start()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        // First time entering a level -> create autosave slot (once per scene)
        SaveFlags.Instance?.RecordLevelEntryAndSave(scene.name);

        // Re-apply dynamic state (boxes) if we have any persisted
        SaveFlags.Instance?.ApplyBoxStatesToScene(scene.name);
    }
}
