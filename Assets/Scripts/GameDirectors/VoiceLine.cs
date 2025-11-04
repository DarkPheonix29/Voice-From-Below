using UnityEngine;

[System.Serializable]
public class VoiceLine
{
    public AudioClip clip;                 // optional (text-only okay)
    [TextArea] public string subtitle;     // full subtitle line
    public float preDelay = 0f;            // wait before speaking
    public float extraHold = 0.6f;         // keep subtitle up briefly after audio ends
    public bool  interruptible = false;    // reserved for future priority logic
    public string objectiveOverride;       // optional: set objective when this line starts
    public AudioSource overrideSource;     // play from a specific source (else uses queue default)
}
