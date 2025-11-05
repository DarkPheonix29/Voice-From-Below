using UnityEngine;

[DisallowMultipleComponent]
public class SurfaceProbe : MonoBehaviour
{
    [Header("Probe")]
    public LayerMask groundMask = ~0;
    public float rayLength = 1.5f;
    public Vector3 rayOriginOffset = new Vector3(0, 0.05f, 0);
    public string defaultSurface = "Concrete";

    /// <summary>
    /// Raycast straight down from 'origin'. Returns true if something was hit and outputs a surfaceId.
    /// </summary>
    public bool TryGetSurface(Vector3 origin, out string surfaceId)
    {
        surfaceId = defaultSurface;

        Ray ray = new Ray(origin + rayOriginOffset, Vector3.down);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        // 0) Explicit override on the collider or its parents
        var overrideComp = hit.collider.GetComponentInParent<SurfaceIdOverride>();
        if (overrideComp && !string.IsNullOrEmpty(overrideComp.surfaceId))
        {
            surfaceId = overrideComp.surfaceId;
            return true;
        }

        // 1) Collider tag
        if (!string.IsNullOrEmpty(hit.collider.tag) && hit.collider.tag != "Untagged")
        {
            surfaceId = hit.collider.tag;
            return true;
        }

        // 2) PhysicMaterial
        if (hit.collider.sharedMaterial)
        {
            surfaceId = hit.collider.sharedMaterial.name;
            return true;
        }

        // 3) Unity Terrain — use layer/texture name
        var terrain = hit.collider.GetComponent<Terrain>();
        if (terrain)
        {
            var data = terrain.terrainData;
            Vector3 tPos = terrain.transform.InverseTransformPoint(hit.point);
            int mapX = Mathf.Clamp((int)((tPos.x / data.size.x) * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int mapZ = Mathf.Clamp((int)((tPos.z / data.size.z) * data.alphamapHeight), 0, data.alphamapHeight - 1);

            float[,,] mix = data.GetAlphamaps(mapX, mapZ, 1, 1);
            int maxIdx = 0; float max = 0f;
            for (int i = 0; i < mix.GetLength(2); i++)
                if (mix[0,0,i] > max) { max = mix[0,0,i]; maxIdx = i; }

            var layer = data.terrainLayers[maxIdx];
            string layerName = layer ? (!string.IsNullOrEmpty(layer.name) ? layer.name :
                               (layer.diffuseTexture ? layer.diffuseTexture.name : "")) : "";
            if (!string.IsNullOrEmpty(layerName)) surfaceId = layerName;
            return true;
        }

        // Fallback
        surfaceId = defaultSurface;
        return true;
    }
}
