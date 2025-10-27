using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class Climbable : MonoBehaviour
{
    [Header("Segment")]
    public Transform bottom;              // start point (world-space)
    public Transform top;                 // end point (world-space)

    [Header("Prompt")]
    public string grabPrompt = "Press E to grab chain";

    Interactable _interact;

    void Awake()
    {
        _interact = GetComponent<Interactable>();
        if (_interact) _interact.oneShot = false; // can reuse infinitely
    }

    void Start()
    {
        if (_interact) _interact.SetPromptText(grabPrompt);
    }

    public Vector3 GetClosestPointOnChain(Vector3 worldPos)
    {
        Vector3 a = bottom.position;
        Vector3 b = top.position;
        Vector3 ab = b - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr < 1e-6f) return a;
        float t = Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / abSqr);
        return a + ab * t;
    }

    public Vector3 UpDir => (top.position - bottom.position).normalized;
    public float Length => Vector3.Distance(bottom.position, top.position);

    public void Grab()
    {
        var climber = FindObjectOfType<PlayerClimber>();
        if (!climber) { Debug.LogWarning("[Climbable] No PlayerClimber found in scene."); return; }

        // If already climbing something, ignore (prevents spam) — we never disable Interactable.
        if (climber.IsClimbing) return;

        climber.BeginClimb(this);

        // If you want to hide the prompt while climbing (without disabling Interactable):
        _interact?.SetHighlighted(false);
    }

#if UNITY_EDITOR
    // Helpful gizmos to verify endpoints/length in Scene view
    void OnDrawGizmos()
    {
        if (!bottom || !top) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(bottom.position, top.position);

        Gizmos.color = Color.yellow; Gizmos.DrawSphere(bottom.position, 0.08f);
        Gizmos.color = Color.cyan;   Gizmos.DrawSphere(top.position, 0.08f);

        var mid = (bottom.position + top.position) * 0.5f;
        UnityEditor.Handles.Label(mid, $"len: {Vector3.Distance(bottom.position, top.position):F2} m");
    }
#endif
}
