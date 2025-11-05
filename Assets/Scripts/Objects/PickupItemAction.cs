using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class PickupItemAction : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique id (e.g. Flashlight, WalkieTalkie, RedKey)")]
    public string itemId = "Flashlight";

    [Header("World Model")]
    [Tooltip("What to disable after pickup. Defaults to this GameObject.")]
    public GameObject worldModelToHide;

    [Header("Attach on Pickup (optional)")]
    public bool attachOnPickup = false;
    public Transform attachPoint;
    public Vector3 attachLocalPos;
    public Vector3 attachLocalEuler;

    [Header("Attach Targets (optional)")]
    // Flashlight support (existing)
    public Flashlight flashlightToAttach;
    public Flashlight flashlightPrefabToSpawn;

    // NEW: WalkieTalkie support
    public WalkieTalkie walkieToAttach;
    public WalkieTalkie walkiePrefabToSpawn;

    [Header("Auto-grant in later scenes (optional)")]
    public bool attachIfOwnedOnStart = false;

    void Reset()
    {
        if (!worldModelToHide) worldModelToHide = gameObject;
        if (!flashlightToAttach) flashlightToAttach = GetComponentInChildren<Flashlight>(true);
        if (!walkieToAttach) walkieToAttach = GetComponentInChildren<WalkieTalkie>(true);
    }

    void Start()
    {
        // Already owned? Hide world pickup and optionally attach
        if (Inventory.Instance != null && Inventory.Instance.Has(itemId))
        {
            if (worldModelToHide) worldModelToHide.SetActive(false);
            if (attachIfOwnedOnStart) StartCoroutine(WaitForCameraAndAttach());
        }
    }

    // Hook this from Interactable.onInteract in the Inspector
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
            StartCoroutine(WaitForCameraAndAttach());

        if (worldModelToHide) worldModelToHide.SetActive(false);

        var col = GetComponent<Collider>(); if (col) col.enabled = false;
        var rb = GetComponent<Rigidbody>(); if (rb) rb.isKinematic = true;

        enabled = false;
    }

    IEnumerator WaitForCameraAndAttach()
    {
        int frames = 0;
        while (Camera.main == null && frames < 300) { frames++; yield return null; }
        if (Camera.main == null) { Debug.LogWarning("[PickupItemAction] Timed out waiting for Camera.main."); yield break; }

        EnsureAttached();
    }

    void EnsureAttached()
    {
        var parent = GetAttachPoint();
        if (!parent) return;

        // 1) Flashlight path
        var existingFlashlight = parent.GetComponentInChildren<Flashlight>(true);
        if (TryEnsureFlashlight(parent, existingFlashlight)) return;

        // 2) Walkie path
        var existingWalkie = parent.GetComponentInChildren<WalkieTalkie>(true);
        if (TryEnsureWalkie(parent, existingWalkie)) return;
    }

    bool TryEnsureFlashlight(Transform parent, Flashlight existing)
    {
        if (existing)
        {
            existing.gameObject.SetActive(true);
            existing.PickUp(parent, attachLocalPos, attachLocalEuler);
            return true;
        }

        if (flashlightToAttach)
        {
            flashlightToAttach.transform.SetParent(parent, false);
            flashlightToAttach.transform.localPosition = attachLocalPos;
            flashlightToAttach.transform.localRotation = Quaternion.Euler(attachLocalEuler);
            flashlightToAttach.PickUp(parent, attachLocalPos, attachLocalEuler);
            return true;
        }

        if (flashlightPrefabToSpawn)
        {
            var fl = Instantiate(flashlightPrefabToSpawn, parent);
            fl.transform.localPosition = attachLocalPos;
            fl.transform.localRotation = Quaternion.Euler(attachLocalEuler);
            fl.PickUp(parent, attachLocalPos, attachLocalEuler);
            return true;
        }

        return false;
    }

    bool TryEnsureWalkie(Transform parent, WalkieTalkie existing)
{
    if (existing)
    {
        existing.gameObject.SetActive(true);
        existing.PickUp(parent, attachLocalPos, attachLocalEuler);
        ActTwoDirector.Instance?.OnWalkieFound(); // <<< notify director
        return true;
    }

    if (walkieToAttach)
    {
        walkieToAttach.transform.SetParent(parent, false);
        walkieToAttach.transform.localPosition = attachLocalPos;
        walkieToAttach.transform.localRotation = Quaternion.Euler(attachLocalEuler);
        walkieToAttach.PickUp(parent, attachLocalPos, attachLocalEuler);
        ActTwoDirector.Instance?.OnWalkieFound(); // <<< notify director
        return true;
    }

    if (walkiePrefabToSpawn)
    {
        var wk = Instantiate(walkiePrefabToSpawn, parent);
        wk.transform.localPosition = attachLocalPos;
        wk.transform.localRotation = Quaternion.Euler(attachLocalEuler);
        wk.PickUp(parent, attachLocalPos, attachLocalEuler);
        ActTwoDirector.Instance?.OnWalkieFound(); // <<< notify director
        return true;
    }

    return false;
}


    Transform GetAttachPoint()
    {
        if (attachPoint) return attachPoint;

        var cam = Camera.main;
        if (cam)
        {
            var holder = cam.transform.Find("FlashlightHolder"); // use if you share a holder
            if (holder) return holder;
            return cam.transform;
        }

        Debug.LogWarning("[PickupItemAction] No attach point and no Camera.main.");
        return null;
    }
}
