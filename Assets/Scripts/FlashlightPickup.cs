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

    void Start()
    {
        // Als hij nog niet in de holder zit, mag hij geen input lezen.
        if (flashlight && (!attachPoint || transform.parent != attachPoint))
            flashlight.enabled = false;
    }

    public void DoPickup()
    {
        // Parent + uitlijnen in de holder
        transform.SetParent(attachPoint, false);
        transform.localPosition = attachLocalPos;
        transform.localRotation = Quaternion.Euler(attachLocalRotEuler);

        // Laat het Flashlight-script zichzelf configureren (sway etc.)
        flashlight.PickUp(attachPoint, attachLocalPos, attachLocalRotEuler);

        // >>> BELANGRIJK: vanaf nu mag hij pas input (F) lezen
        flashlight.enabled = true;

        // Fysica uit zodat hij niet meer rondstuitert in de hand
        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        // Dit pickup-script is klaar
        enabled = false;
    }
}
