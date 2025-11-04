using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class WalkieTalkie : MonoBehaviour
{
    [Header("Basis (optioneel) continue ruis")]
    public AudioSource radioNoise;     // optioneel: zachte basisruis als walkie 'aan' is
    public bool startOn = false;
    public float volumeOn = 0.5f;
    public float fadeTime = 0.2f;

    [Header("Trigger Noise (defaults)")]
    public AudioSource triggerNoiseSource;   // dedicated source voor korte ruis
    public AudioClip   defaultTriggerClip;
    public float       defaultTriggerDuration = 3f;
    [Range(0f,1f)] public float defaultTriggerVolume   = 0.8f;

    [Header("3D Audio Settings")]
    [Tooltip("1 = volledig 3D, 0 = volledig 2D")]
    [Range(0f,1f)] public float spatialBlend = 1f;             // **zorgt voor 3D**
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    public float minDistance = 1f;
    public float maxDistance = 15f;
    [Range(0f, 360f)] public float spread = 0f;                 // laat meestal op 0
    public bool muteWhileTriggerPlaysOnRadioNoise = true;       // demp basisruis tijdens trigger

    private bool isOn;
    private bool forceTriggerActive;
    private Coroutine triggerRoutine;

    void Awake()
    {
        // Maak (of configureer) de bronnen
        if (radioNoise)
        {
            radioNoise.loop = true;
            radioNoise.playOnAwake = false;
            Ensure3D(radioNoise);
        }

        if (!triggerNoiseSource)
        {
            triggerNoiseSource = gameObject.AddComponent<AudioSource>();
        }
        triggerNoiseSource.playOnAwake = false;
        triggerNoiseSource.loop = false;
        Ensure3D(triggerNoiseSource);

        isOn = startOn;
        if (isOn && radioNoise) radioNoise.Play();
    }

    void Update()
    {
        if (!radioNoise) return;

        float dt = Time.deltaTime;
        // netjes in/uitfaden voor basisruis
        float desired = (isOn && !(muteWhileTriggerPlaysOnRadioNoise && forceTriggerActive)) ? volumeOn : 0f;

        radioNoise.volume = Mathf.MoveTowards(
            radioNoise.volume,
            desired,
            dt / Mathf.Max(0.001f, fadeTime)
        );

        if (radioNoise.volume <= 0.001f && radioNoise.isPlaying && !isOn)
            radioNoise.Stop();
        else if (radioNoise.volume > 0.001f && !radioNoise.isPlaying)
            radioNoise.Play();
    }

    public void Toggle()
    {
        isOn = !isOn;
        if (!isOn && radioNoise) radioNoise.Stop();
    }

    // ---------- Pickup / Drop ----------
    public void PickUp() => gameObject.SetActive(true);

    public void PickUp(Transform parent)
    {
        PickUp();
        if (parent)
        {
            transform.SetParent(parent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }

    public void PickUp(Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        PickUp(parent);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
    }

    public void PickUp(Transform parent, Vector3 localPosition, Vector3 localEulerAngles)
    {
        PickUp(parent);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEulerAngles);
    }

    public void Drop()
    {
        transform.SetParent(null, true);
        if (radioNoise) { radioNoise.Stop(); radioNoise.volume = 0f; }
        isOn = false;
    }

    // ---------- Trigger Ruis (speelt AF op de walkie-positie) ----------
    public void PlayTriggerNoise(float duration, float volume, AudioClip clip = null)
    {
        if (triggerRoutine != null) StopCoroutine(triggerRoutine);
        triggerRoutine = StartCoroutine(DoTriggerNoise(
            clip ? clip : defaultTriggerClip,
            duration > 0 ? duration : defaultTriggerDuration,
            Mathf.Clamp01(volume <= 0 ? defaultTriggerVolume : volume)
        ));
    }

    private IEnumerator DoTriggerNoise(AudioClip clip, float duration, float volume)
    {
        if (!triggerNoiseSource || clip == null) yield break;

        forceTriggerActive = true;

        // zorg dat 3D-instellingen zeker kloppen (voor het geval je iets aanpast in runtime)
        Ensure3D(triggerNoiseSource);

        triggerNoiseSource.clip = clip;
        triggerNoiseSource.volume = volume;
        triggerNoiseSource.Stop(); // reset playback positie
        triggerNoiseSource.Play();

        float wait = (clip.length > 0f) ? Mathf.Min(duration, clip.length) : duration;
        yield return new WaitForSeconds(wait);

        triggerNoiseSource.Stop();
        forceTriggerActive = false;
    }

    // ---------- Helpers ----------
    private void Ensure3D(AudioSource src)
    {
        src.spatialBlend = spatialBlend;   // 1 = volledig 3D
        src.rolloffMode = rolloffMode;
        src.minDistance = Mathf.Max(0.01f, minDistance);
        src.maxDistance = Mathf.Max(src.minDistance + 0.01f, maxDistance);
        src.spread = spread;
    }
    private void OnTriggerEnter(Collider other)
    {
        // Controleer of de triggerzone een WalkieNoiseTrigger heeft
        var trigger = other.GetComponent<WalkieNoiseTrigger>();
        if (trigger != null)
        {
            // Laat de trigger zelf afspelen (zo kun je alles in de Editor instellen)
            trigger.OnWalkieEnter(this);
        }
    }
}