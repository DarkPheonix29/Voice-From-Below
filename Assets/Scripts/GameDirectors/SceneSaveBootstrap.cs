using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap script that, on scene start, applies box state and
/// (only when a real save exists for this scene) moves the player
/// to the saved position/rotation. Stamina is reset to full instead of loaded.
/// If no save exists, the player stays where they are placed in the scene.
/// </summary>
public class SceneSaveBootstrap : MonoBehaviour
{
    [Tooltip("Player in this scene; if empty, will try to find by tag 'Player'.")]
    public Transform playerTransform;

    void Start()
    {
        var sf = SaveFlags.Instance;
        if (sf == null) return;

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid()) return;
        var sceneName = activeScene.name;

        // --- PHASE 1: Apply dynamic state (boxes etc.) ---
        sf.ApplyBoxStatesToScene(sceneName);

        // --- PHASE 2: See if we are resuming from a save for this scene ---
        var record = sf.GetMostRecentSave();
        if (record == null) return;
        if (string.IsNullOrEmpty(record.sceneName)) return;
        if (record.sceneName != sceneName) return;

        // --- PHASE 3: Find the player (if not wired in Inspector) ---
        if (!playerTransform)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) playerTransform = go.transform;
        }

        // --- PHASE 4: Apply saved pose & reset stamina ---
        if (playerTransform != null)
        {
            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;

            playerTransform.SetPositionAndRotation(record.playerPosition, record.playerRotation);

            if (cc) cc.enabled = true;

            var fp = playerTransform.GetComponent<FPPlayer>();
            if (!fp)
                fp = playerTransform.GetComponentInChildren<FPPlayer>(true);

            if (fp)
            {
                // Reset stamina instead of loading it
                fp.ResetStaminaToFull();
                fp.SyncSpeedAfterTeleport();
            }
        }
    }
}
