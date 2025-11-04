// PlayerSpawnMarker.cs
using UnityEngine;

public class PlayerSpawnMarker : MonoBehaviour
{
    [Tooltip("Unique ID used by TransferPoint to place the player here")]
    public string spawnId = "Default";

    [Tooltip("If true, player will match this rotation on spawn")]
    public bool applyRotation = true;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"Spawn: {spawnId}");
        #endif
    }
}
