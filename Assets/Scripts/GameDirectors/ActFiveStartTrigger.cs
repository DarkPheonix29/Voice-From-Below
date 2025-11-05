using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ActFiveStartTrigger : MonoBehaviour
{
    public string firstNodeId = "L5_Start";
    public string triggerFlag = "L5_Chase_StartTrigger";
    public bool oneShot = true;

    public GameObject barrierPlane;
    public bool activateBarrierOnEnter = true;

    bool _fired;

    void Awake()
    {
        if (SaveFlags.Instance && SaveFlags.Instance.Has(triggerFlag))
        {
            _fired = true;
            if (barrierPlane) barrierPlane.SetActive(false);
            enabled = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_fired && oneShot) return;

        if (barrierPlane && activateBarrierOnEnter) barrierPlane.SetActive(true);

        ActFiveChaseDirector.Instance?.StartChaseFromLevel5(firstNodeId);

        _fired = true;
        if (SaveFlags.Instance) SaveFlags.Instance.Set(triggerFlag);
        if (oneShot) enabled = false;
    }
}
