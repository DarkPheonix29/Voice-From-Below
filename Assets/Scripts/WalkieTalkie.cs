using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class WalkieTalkie : MonoBehaviour
{
    [Header("Basis (optioneel) continue ruis")]
    public AudioSource radioNoise;
    public bool startOn = false;
    public float volumeOn = 0.5f;
    public float fadeTime = 0.2f;

    [Header("Trigger Noise (defaults)")]
    public AudioSource triggerNoiseSource;
    public AudioClip   defaultTriggerClip;
    public float       defaultTriggerDuration = 3f;
    [Range(0f,1f)] public float defaultTriggerVolume = 0.8f;

    [Header("3D Audio Settings")]
    [Range(0f,1f)] public float spatialBlend = 1f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    public float minDistance = 1f;
    public float maxDistance = 15f;
    [Range(0f, 360f)] public float spread = 0f;
    public bool muteWhileTriggerPlaysOnRadioNoise = true;

    [Header("Visibility")]
    [Tooltip("Schakel ook de collider uit wanneer de walkie onzichtbaar is (na pickup).")]
    public bool disableColliderWhenHidden = true;

    private bool isOn;
    private bool forceTriggerActive;
    private Coroutine triggerRoutine;

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private bool _pickedUp = false;

    // Nieuw: geforceerde zichtbaarheid (bij triggerzones)
    private float _forceShowTimer = 0f;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _colliders = GetComponentsInChildren<Collider>(includeInactive: true);

        if (radioNoise)
        {
            radioNoise.loop = true;
            radioNoise.playOnAwake = false;
            Ensure3D(radioNoise);
        }

        if (!triggerNoiseSource)
            triggerNoiseSource = gameObject.AddComponent<AudioSource>();
        triggerNoiseSource.playOnAwake = false;
        triggerNoiseSource.loop = false;
        Ensure3D(triggerNoiseSource);

        isOn = startOn;
        if (isOn && radioNoise) radioNoise.Play();

        UpdateVisibility();
    }

    void Update()
    {
        // timer aftellen voor geforceerde zichtbaarheid
        if (_forceShowTimer > 0f)
        {
            _forceShowTimer -= Time.deltaTime;
            if (_forceShowTimer < 0f) _forceShowTimer = 0f;
        }

        if (!radioNoise)
        {
            UpdateVisibility();
            return;
        }

        float dt = Time.deltaTime;
        float desired = (isOn && !(muteWhileTriggerPlaysOnRadioNoise && forceTriggerActive)) ? volumeOn : 0f;

        radioNoise.volume = Mathf.MoveTowards(radioNoise.volume, desired, dt / Mathf.Max(0.001f, fadeTime));

        if (radioNoise.volume <= 0.001f && radioNoise.isPlaying && !isOn) radioNoise.Stop();
        else if (radioNoise.volume > 0.001f && !radioNoise.isPlaying)    radioNoise.Play();

        UpdateVisibility();
    }

    public void Toggle()
    {
        isOn = !isOn;
        if (!isOn && radioNoise) { radioNoise.Stop(); radioNoise.volume = 0f; }
        UpdateVisibility();
    }

    // ---------- Pickup / Drop ----------
    public void PickUp()
    {
        gameObject.SetActive(true);
        _pickedUp = true;
        UpdateVisibility();
    }

    public void PickUp(Transform parent)
    {
        PickUp();
        if (parent)
        {
            transform.SetParent(parent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        UpdateVisibility();
    }

    public void PickUp(Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        PickUp(parent);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        UpdateVisibility();
    }

    public void PickUp(Transform parent, Vector3 localPosition, Vector3 localEulerAngles)
    {
        PickUp(parent);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEulerAngles);
        UpdateVisibility();
    }

    public void Drop()
    {
        transform.SetParent(null, true);
        if (radioNoise) { radioNoise.Stop(); radioNoise.volume = 0f; }
        isOn = false;
        _pickedUp = false; // op de grond: altijd zichtbaar
        _forceShowTimer = 0f;
        UpdateVisibility();
    }

    // ---------- Trigger Ruis ----------
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
        Ensure3D(triggerNoiseSource);

        triggerNoiseSource.clip = clip;
        triggerNoiseSource.volume = volume;
        triggerNoiseSource.Stop();
        triggerNoiseSource.Play();

        ForceShow(duration); // ook direct zichtbaar maken

        float wait = (clip.length > 0f) ? Mathf.Min(duration, clip.length) : duration;
        yield return new WaitForSeconds(wait);

        triggerNoiseSource.Stop();
        forceTriggerActive = false;

        UpdateVisibility();
    }

    // ---------- Helpers ----------
    private void Ensure3D(AudioSource src)
    {
        src.spatialBlend = spatialBlend;
        src.rolloffMode = rolloffMode;
        src.minDistance = Mathf.Max(0.01f, minDistance);
        src.maxDistance = Mathf.Max(src.minDistance + 0.01f, maxDistance);
        src.spread = spread;
    }

    /// <summary>
    /// Dwing zichtbaarheid af voor 'duration' seconden (gebruikt door triggerzones).
    /// </summary>
    public void ForceShow(float duration)
    {
        _forceShowTimer = Mathf.Max(_forceShowTimer, Mathf.Max(0f, duration));
        UpdateVisibility();
    }

    /// <summary>
    /// Vóór pickup: altijd zichtbaar.
    /// Na pickup: zichtbaar als er geluid speelt óf wanneer ForceShow actief is.
    /// </summary>
    private void UpdateVisibility()
    {
        bool baseAudible    = radioNoise && (isOn && radioNoise.volume > 0.01f);
        bool triggerAudible = triggerNoiseSource && triggerNoiseSource.isPlaying;
        bool forcedVisible  = _forceShowTimer > 0f;

        bool shouldShow = !_pickedUp || baseAudible || triggerAudible || forcedVisible;

        if (_renderers != null)
            foreach (var r in _renderers) if (r) r.enabled = shouldShow;

        if (disableColliderWhenHidden && _pickedUp && _colliders != null)
            foreach (var c in _colliders) if (c) c.enabled = shouldShow;
    }

    private void OnTriggerEnter(Collider other)
    {
        var trigger = other.GetComponent<WalkieNoiseTrigger>();
        if (trigger != null)
            trigger.OnWalkieEnter(this);
    }
}
