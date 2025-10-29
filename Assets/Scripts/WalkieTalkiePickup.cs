using UnityEngine;

public class WalkieTalkiePickup : MonoBehaviour
{
    public WalkieTalkie walkieTalkie;
    public Transform attachPoint;
    public Vector3 attachLocalPos = Vector3.zero;
    public Vector3 attachLocalRotEuler = Vector3.zero;

    void Reset()
    {
        if (!walkieTalkie) walkieTalkie = GetComponentInChildren<WalkieTalkie>();
    }

    public void DoPickup()
    {
        if (!walkieTalkie) return;
        if (!attachPoint)
        {
            var cam = Camera.main;
            if (cam) attachPoint = cam.transform;
        }

        transform.SetParent(attachPoint, false);
        transform.localPosition = attachLocalPos;
        transform.localRotation = Quaternion.Euler(attachLocalRotEuler);

        walkieTalkie.PickUp(attachPoint, attachLocalPos, attachLocalRotEuler);

        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        enabled = false;
    }
}
