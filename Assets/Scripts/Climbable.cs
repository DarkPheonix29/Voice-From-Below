using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class Climbable : MonoBehaviour
{
    [Header("Segment")]
    public Transform bottom;   // start point of the chain
    public Transform top;      // end point of the chain
    public float snapRadius = 0.4f;   // how tightly we lock player onto the chain

    [Header("Prompt")]
    public string grabPrompt = "Press E to grab chain";

    Interactable _interact;

    void Awake()
    {
        _interact = GetComponent<Interactable>();
        if (_interact) _interact.oneShot = false;
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
        float t = Vector3.Dot(worldPos - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    public Vector3 UpDir => (top.position - bottom.position).normalized;
    public float Length => Vector3.Distance(top.position, bottom.position);

    // Climbable.cs (add this inside the class)
    public void Grab()
    {
        // Find the player's climber (cache this in Awake if you prefer)
        var climber = FindObjectOfType<PlayerClimber>();
        if (!climber)
        {
            Debug.LogWarning("[Climbable] No PlayerClimber found in scene.");
            return;
        }

        climber.BeginClimb(this);
    }
}
