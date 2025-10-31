using UnityEngine;

[RequireComponent(typeof(SFXRouterSimple))]
public class ObjectSFX : MonoBehaviour
{
    public string sfxOnInteract = "Default";
    SFXRouterSimple router;

    void Awake() => router = GetComponent<SFXRouterSimple>();

    // Call this from your Interactable or event
    public void InteractSFX()
    {
        if (router)
            router.PlaySFX(sfxOnInteract);
    }
}
