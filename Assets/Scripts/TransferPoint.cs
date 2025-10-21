using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TransferPoint : MonoBehaviour
{
    [Header("Destination")]
    public string destinationScene;       // e.g. "Level2"
    public string destinationSpawnId;     // e.g. "FromLevel1_MineEntrance"

    [Header("Fade Durations")]
    public float fadeOutDuration = 0.8f;
    public float fadeInDuration  = 1.2f;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Queue next-scene spawn point (optional)
        System.Action before = null;
        if (!string.IsNullOrEmpty(destinationSpawnId) && GameFlow.Instance != null)
            before = () => GameFlow.Instance.QueueSpawn(destinationSpawnId);

        // Use the RootHUD to fade out -> load -> fade in
        if (PersistentHUD.Instance != null)
        {
            PersistentHUD.Instance.LoadSceneWithFade(
                destinationScene,
                fadeOutDuration,
                fadeInDuration,
                before
            );
        }
        else
        {
            Debug.LogError("[TransferPoint] PersistentHUD not found in scene.");
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (!col) return;
        Gizmos.color = new Color(0, 0.8f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider b)
            Gizmos.DrawCube(b.center, b.size);
    }
#endif
}
