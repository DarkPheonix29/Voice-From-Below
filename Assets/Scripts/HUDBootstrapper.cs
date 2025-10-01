using UnityEngine;

public class HUDBootstrapper : MonoBehaviour
{
    public GameObject hudPrefab; // assign HUDRoot prefab in Inspector

    void Start()
    {
        if (!PersistentHUD.Instance)
        {
            var hud = Instantiate(hudPrefab);
            // Optional: rename for clarity in Hierarchy at runtime
            hud.name = "RootHUD";
        }
        else
        {
            // If someone accidentally left a scene-local HUD, remove it
            var locals = GameObject.FindObjectsOfType<PersistentHUD>();
            foreach (var h in locals)
            {
                if (h != PersistentHUD.Instance) Destroy(h.gameObject);
            }
        }
    }
}
