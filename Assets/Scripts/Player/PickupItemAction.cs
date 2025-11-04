using UnityEngine;
using System.Collections; // needed for IEnumerator

[DisallowMultipleComponent]
public class PickupItemAction : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique id (e.g. Flashlight, LeverHandle, RedKey)")]
    public string itemId = "Flashlight";

    [Header("World Model")]
    [Tooltip("What to disable after pickup. Defaults to this GameObject.")]
    public GameObject worldModelToHide;

    [Header("Attach on Pickup (optional)")]
    public bool attachOnPickup = false;
    public Transform attachPoint;
    public Vector3 attachLocalPos;
    public Vector3 attachLocalEuler;

    [Header("Attach Target (optional, for your Flashlight)")]
    public Flashlight flashlightToAttach;
    public Flashlight flashlightPrefabToSpawn;

    [Header("Auto-grant in later scenes (optional)")]
    public bool attachIfOwnedOnStart = false;

    void Reset()
    {
        if (!worldModelToHide) worldModelToHide = gameObject;
        if (!flashlightToAttach) flashlightToAttach = GetComponentInChildren<Flashlight>(true);
    }

    void Start()
    {
        // Already own it? Hide world pickup
        if (Inventory.Instance != null && Inventory.Instance.Has(itemId))
        {
            if (worldModelToHide) worldModelToHide.SetActive(false);

            // Wait for Camera.main before attaching
            if (attachIfOwnedOnStart)
                StartCoroutine(WaitForCameraAndAttach());
        }
    }

    // Call this from Interactable.OnInteract (one script for all items!)
    public void DoPickup()
    {
        if (Inventory.Instance == null)
        {
            Debug.LogWarning("[PickupItemAction] No Inventory in scene.");
            return;
        }

        Inventory.Instance.Add(itemId);
        Debug.Log($"[PickupItemAction] Added {itemId}");
        if (SaveFlags.Instance) SaveFlags.Instance.Set(itemId);


        if (attachOnPickup)
            StartCoroutine(WaitForCameraAndAttach()); // same safety when picking up in-level

        if (worldModelToHide) worldModelToHide.SetActive(false);

        var col = GetComponent<Collider>(); if (col) col.enabled = false;
        var rb = GetComponent<Rigidbody>(); if (rb) rb.isKinematic = true;

        enabled = false;
    }

    // --- NEW ---
    IEnumerator WaitForCameraAndAttach()
    {
        int frames = 0;
        while (Camera.main == null && frames < 300) // ~5 seconds max wait
        {
            frames++;
            yield return null;
        }

        if (Camera.main == null)
        {
            Debug.LogWarning("[PickupItemAction] Timed out waiting for Camera.main.");
            yield break;
        }

        EnsureAttached();
    }

    // --- Existing helpers ---
    void EnsureAttached()
    {
        var parent = GetAttachPoint();
        if (!parent) return;

        Flashlight existing = parent.GetComponentInChildren<Flashlight>(true);

        if (!existing)
        {
            if (flashlightToAttach)
            {
                flashlightToAttach.transform.SetParent(parent, false);
                flashlightToAttach.transform.localPosition = attachLocalPos;
                flashlightToAttach.transform.localRotation = Quaternion.Euler(attachLocalEuler);
                flashlightToAttach.PickUp(parent, attachLocalPos, attachLocalEuler);
                return;
            }
            else if (flashlightPrefabToSpawn)
            {
                var fl = Instantiate(flashlightPrefabToSpawn, parent);
                fl.transform.localPosition = attachLocalPos;
                fl.transform.localRotation = Quaternion.Euler(attachLocalEuler);
                fl.PickUp(parent, attachLocalPos, attachLocalEuler);
                return;
            }
        }
        else
        {
            existing.gameObject.SetActive(true);
            existing.PickUp(parent, attachLocalPos, attachLocalEuler);
            return;
        }
    }

    Transform GetAttachPoint()
    {
        if (attachPoint) return attachPoint;

        var cam = Camera.main;
        if (cam)
        {
            var holder = cam.transform.Find("FlashlightHolder");
            if (holder) return holder;
            return cam.transform;
        }

        Debug.LogWarning("[PickupItemAction] No attach point and no Camera.main.");
        return null;
    }
}
