using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class JournalPage : MonoBehaviour
{
    [Header("Identity / Save Flag")]
    [Tooltip("Unique page ID. Used as SaveFlags key so this page won't respawn after pickup.")]
    public string pageId = "L4_Journal_01";

    [Header("UI Text (for prompts/UI; optional)")]
    public string pageTitle = "Journal Page";
    [TextArea(2,6)] public string pageBody; // if you later show it in a Journal UI

    [Header("World & SFX")]
    [Tooltip("Mesh root to hide when picked up. Defaults to this GameObject.")]
    public GameObject worldModel;
    public AudioSource pickupSfx; // small rustle/flap sfx (optional)

    [Header("Voice Line (optional)")]
    public AudioClip voiceLine;
    [TextArea] public string subtitle;
    public float subtitleExtraHold = 0.6f;
    [Tooltip("Leave empty to auto-use the Main Camera as a 2D source.")]
    public AudioSource voiceSource;

    [Header("Objective (optional)")]
    public string objectiveAfterPickup;

    // runtime
    Interactable _interact;

    void Reset()
    {
        if (!worldModel) worldModel = gameObject;
    }

    void Awake()
    {
        _interact = GetComponent<Interactable>();
        _interact.oneShot = true;

        if (string.IsNullOrWhiteSpace(pageId))
            Debug.LogWarning("[JournalPage] pageId is empty. Persistence will not work.");

        // If already collected (this session or from a saved checkpoint), hide immediately.
        if (!string.IsNullOrWhiteSpace(pageId) && SaveFlags.Instance && SaveFlags.Instance.Has(JournalFlag(pageId)))
        {
            if (worldModel) worldModel.SetActive(false);
            // If you also want to destroy the pickup object, uncomment:
            // Destroy(gameObject);
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // Prompt text for the Interactable
        string title = string.IsNullOrEmpty(pageTitle) ? "page" : pageTitle;
        _interact.SetPromptText($"Pick up: {title}");
    }

    // Hook this in Interactable's event: OnInteract -> JournalPage.Collect
    public void Collect()
    {
        // Set the session flag so it won't respawn if you revisit the scene this session
        if (!string.IsNullOrWhiteSpace(pageId) && SaveFlags.Instance)
            SaveFlags.Instance.Set(JournalFlag(pageId));

        // SFX
        if (pickupSfx) pickupSfx.Play();

        // Hide world model right away
        if (worldModel) worldModel.SetActive(false);

        // Queue VO + subtitle through the shared queue (serializes with other dialogue)
        QueuePickupVoiceLine();

        // Optional objective change
        if (!string.IsNullOrEmpty(objectiveAfterPickup))
            PersistentHUD.Instance?.SetObjective(objectiveAfterPickup);

        // Clean up this pickup object after SFX (if any)
        float delay = (pickupSfx && pickupSfx.clip) ? pickupSfx.clip.length : 0f;
        Destroy(gameObject, delay > 0 ? delay : 0.05f);
    }

    void QueuePickupVoiceLine()
    {
        if (voiceLine == null && string.IsNullOrEmpty(subtitle)) return;

        // Get or create a 2D AudioSource on the main camera if none assigned
        AudioSource src = voiceSource ? voiceSource : GetOrMakeCameraAudioSource();
        if (src) src.spatialBlend = 0f; // 2D so it follows the player

        VoiceLineQueue.Instance?.Enqueue(new VoiceLine
        {
            clip = voiceLine,
            subtitle = subtitle,
            extraHold = Mathf.Max(0f, subtitleExtraHold),
            overrideSource = src
        });
    }

    AudioSource GetOrMakeCameraAudioSource()
    {
        var cam = Camera.main;
        if (!cam) return null;
        var src = cam.GetComponent<AudioSource>();
        if (!src) src = cam.gameObject.AddComponent<AudioSource>();
        return src;
    }

    static string JournalFlag(string id) => $"Journal_{id}";
}
