// GameFlow.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }
    [SerializeField] private string pendingSpawnId; // set by TransferPoints

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void QueueSpawn(string spawnId)
    {
        pendingSpawnId = spawnId;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(pendingSpawnId)) return;
        var markers = GameObject.FindObjectsOfType<PlayerSpawnMarker>(true);
        foreach (var m in markers)
        {
            if (m.spawnId == pendingSpawnId)
            {
                // find player (persistent or scene-based)
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    Debug.LogWarning("Player not found in scene. Make sure your player has tag 'Player'.");
                    return;
                }

                // If player uses CharacterController, disable before warp to avoid step jitter
                var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;

                player.transform.position = m.transform.position;
                if (m.applyRotation) player.transform.rotation = m.transform.rotation;

                if (cc) cc.enabled = true;

                // clear once used
                pendingSpawnId = null;
                return;
            }
        }

        Debug.LogWarning($"Spawn marker with id '{pendingSpawnId}' not found in scene '{scene.name}'.");
    }
}
