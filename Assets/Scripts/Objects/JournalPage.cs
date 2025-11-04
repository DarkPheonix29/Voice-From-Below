using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class JournalPage : MonoBehaviour
{
    public JournalPageData data;

    [Header("Visuals & Audio")]
    public GameObject worldModel;        // mesh/renderer root to hide on pickup
    public AudioSource pickupSfx;        // small page flutter or pickup sound
    public AudioClip voiceLine;          // voice clip to play after pickup
    public AudioSource voiceSource;      // optional dedicated source (or will use Camera.main)

    Interactable _interact;

    void Reset()
    {
        worldModel = worldModel ? worldModel : gameObject;
    }

    void Awake()
    {
        _interact = GetComponent<Interactable>();
        _interact.oneShot = true;
    }

    void Start()
    {
        _interact.SetPromptText(string.IsNullOrEmpty(data.title) ? "Pick up page" : $"Pick up: {data.title}");
    }

    // Wire this in Interactable.onInteract (drag this component -> Collect)
    public void Collect()
    {
        if (JournalManager.Instance == null) { Debug.LogWarning("No JournalManager in scene"); return; }
        if (JournalManager.Instance.Has(data.id)) return;

        JournalManager.Instance.Collect(data);

        // play pickup sfx
        if (pickupSfx) pickupSfx.Play();

        // hide the physical model
        if (worldModel) worldModel.SetActive(false);

        // trigger optional voice line
        PlayVoiceLine();

        // remove the object once done
        float delay = pickupSfx && pickupSfx.clip ? pickupSfx.clip.length : 0f;
        Destroy(gameObject, delay > 0 ? delay : 0.1f);
    }

    void PlayVoiceLine()
    {
        if (!voiceLine) return;

        // pick a valid source
        AudioSource src = voiceSource;
        if (!src)
        {
            Camera cam = Camera.main;
            if (cam)
            {
                src = cam.GetComponent<AudioSource>();
                if (!src)
                    src = cam.gameObject.AddComponent<AudioSource>();
            }
            else
            {
                src = gameObject.AddComponent<AudioSource>();
            }
        }

        src.clip = voiceLine;
        src.spatialBlend = 0f; // make sure it's 2D so it follows the player
        src.Play();
    }
}
