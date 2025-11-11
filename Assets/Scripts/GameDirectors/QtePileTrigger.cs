using UnityEngine;

[RequireComponent(typeof(Collider))]
public class QtePileTrigger : MonoBehaviour
{
    [Header("QTE")]
    public string pileId = "L5_Pile_A"; // unique per pile!
    public KeyCode key = KeyCode.E;
    [Range(0.5f, 5f)] public float windowSeconds = 1.5f;

    [Header("One-shot")]
    public bool oneShot = true;

    bool _fired;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired && oneShot) return;
        if (!other.CompareTag("Player")) return;

        _fired = true;
        ActFiveChaseDirector.Instance?.BeginQteAtPile(pileId, key, windowSeconds);

        if (oneShot) enabled = false;
    }
}
