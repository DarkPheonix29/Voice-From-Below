using UnityEngine;

public class FlashlightPickup : MonoBehaviour
{
    public Flashlight flashlight;
    public Transform attachPoint;
    public Vector3 attachLocalPos = Vector3.zero;
    public Vector3 attachLocalRotEuler = Vector3.zero;

    void Reset()
    {
        if (!flashlight) flashlight = GetComponentInChildren<Flashlight>();
    }

    public void DoPickup()
    {
        if (!flashlight) return;
        if (!attachPoint)
        {
            var cam = Camera.main;
            if (cam) attachPoint = cam.transform;
        }

        transform.SetParent(attachPoint, false);
        transform.localPosition = attachLocalPos;
        transform.localRotation = Quaternion.Euler(attachLocalRotEuler);

        flashlight.PickUp(attachPoint, attachLocalPos, attachLocalRotEuler);

        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        enabled = false;
    }
}