using UnityEngine;

[CreateAssetMenu(menuName = "Game/Journal Page", fileName = "JournalPageData")]
public class JournalPageData : ScriptableObject
{
    [Header("Identity")]
    public string id;             // unique, e.g. "L4_Journal_01"
    public string title;          // e.g. "Foreman's Notes"

    [Header("Content (for UI)")]
    [TextArea(2, 6)] public string body;   // full text for your journal UI

    [Header("Pickup VO (optional)")]
    public AudioClip voiceLine;            // plays after pickup
    [TextArea] public string subtitle;     // shown while VO plays
    public float subtitleExtraHold = 0.6f; // small hold after clip

    [Header("Objective (optional)")]
    public string objectiveAfterPickup;    // leave empty if not needed
}
