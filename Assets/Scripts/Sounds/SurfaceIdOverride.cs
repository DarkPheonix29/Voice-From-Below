using UnityEngine;

[DisallowMultipleComponent]
public class SurfaceIdOverride : MonoBehaviour
{
    [Tooltip("Surface id to report to FootstepController (e.g., Wood, Metal, CaveMud)")]
    public string surfaceId = "Concrete";
}
