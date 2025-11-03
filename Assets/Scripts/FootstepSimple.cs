using UnityEngine;
using System;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class FootstepSimple : MonoBehaviour
{
    [Serializable] public class SurfaceClips
    {
        public string surfaceId = "Concrete";
        public AudioClip[] walk;
        public AudioClip[] run;
        public AudioClip[] crouch;
        public AudioClip[] landLight;
        public AudioClip[] landHeavy;
        public AudioClip[] climb;    // <-- NEW: climbing/rung/hand clips
    }

    [Header("Refs")]
    public Transform feet;
    public SurfaceProbe probe;

    [Header("State (feed from your controller)")]
    public float moveSpeed;
    public bool  isRunning;
    public bool  isCrouching;
    public float verticalSpeed;
    public bool  isGrounded;

    // ---------- CLIMB ----------
    [Header("Climb")]
    public bool  isClimbing;            // set true while on ladder/chain
    public float climbInterval = 0.33f; // seconds between rungs
    public float climbSpeedScale = 0.5f;// how much moveSpeed speeds up cadence
    public float climbPitchMult = 1.0f; // global pitch multiplier while climbing
    float _climbTimer;

    [Header("Cadence (walking)")]
    public float stepDistanceWalk = 2.1f;
    public float sprintStepMult   = 0.65f;
    public float crouchStepMult   = 1.35f;
    public float minSpeedForSteps = 0.6f;
    public float minStepInterval  = 0.22f;

    [Header("Cadence Source")]
    public bool useSpeedForSteps   = true;

    [Header("Smoothing")]
    [Tooltip("Higher = snappier; 10–16 is a good range")]
    public float speedSmoothing    = 12f;

    [Header("Pitch (walking)")]
    public Vector2 pitchRange      = new Vector2(0.98f, 1.02f);
    public float   sprintPitchMult = 1.06f;
    public float   crouchPitchMult = 0.94f;

    [Header("Landing")]
    public float landHeavySpeed    = -9f;
    public float minLandInterval   = 0.25f;

    [Header("Audio")]
    [Range(0f, 1f)] public float volume = 0.9f;
    public List<SurfaceClips> surfaces = new();

    [Header("Debug")]
    public bool debug;

    [Header("Landing Gates")]
    public float minAirTime = 0.08f;

    [Header("Grounded Gates")]
    public float groundedSettleTime = 0.03f;

    [Header("AudioSource Location")]
    public string sourceChildName = "FootAudio";

    // internals
    AudioSource _src;
    Transform   _srcTransform;
    Vector3 _lastPos;
    float _accumDist;
    float _sinceStep;
    float _sinceLand;
    float _nextStepTime;
    bool  _wasGrounded;
    float _prevVerticalSpeed;
    float _smoothedSpeed;
    string _currentSurface = "Concrete";
    float _airTime;
    float _groundedTimer;

    const float kEpsilonSpeed = 0.01f;

    void Awake()
    {
        if (!feet) feet = transform;

        Transform host = (feet != null) ? feet : transform;
        Transform footAudio = host.Find(sourceChildName);
        if (!footAudio)
        {
            footAudio = new GameObject(sourceChildName).transform;
            footAudio.SetParent(host, false);
            footAudio.localPosition = Vector3.zero;
            footAudio.localRotation = Quaternion.identity;
        }

        _src = footAudio.GetComponent<AudioSource>();
        if (!_src) _src = footAudio.gameObject.AddComponent<AudioSource>();
        _srcTransform = _src.transform;

        _src.playOnAwake  = false;
        _src.loop         = false;
        _src.spatialBlend = 1f;
        _src.dopplerLevel = 0f;

        _lastPos = transform.position;
    }

    void Update()
    {
        _sinceStep += Time.deltaTime;
        _sinceLand += Time.deltaTime;

        // Smooth incoming speed (used for walk + climb cadence)
        float raw = Mathf.Max(0f, moveSpeed);
        float alpha = 1f - Mathf.Exp(-Mathf.Max(0f, speedSmoothing) * Time.deltaTime);
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, raw, alpha);

        // Optional distance accumulation (walking path)
        float frameDist;
        if (useSpeedForSteps) frameDist = _smoothedSpeed * Time.deltaTime;
        else
        {
            var pos = transform.position;
            var horizDelta = Vector3.ProjectOnPlane(pos - _lastPos, Vector3.up);
            frameDist = horizDelta.magnitude;
            _lastPos = pos;
        }

        // Identify surface
        if (probe && probe.TryGetSurface(feet.position, out var id))
            _currentSurface = id;

        // ---------------- CLIMB PATH ----------------
        if (isClimbing)
        {
            // While climbing, we don't do land/walk; just rung cadence.
            // Scale cadence a little with climb speed (feel).
            float scale = 1f + Mathf.Clamp01(_smoothedSpeed) * climbSpeedScale;
            _climbTimer += Time.deltaTime * scale;
            if (_climbTimer >= Mathf.Max(0.05f, climbInterval))
            {
                _climbTimer = 0f;
                var pack = GetPack(_currentSurface);
                var set  = (pack.climb != null && pack.climb.Length > 0) ? pack.climb : pack.walk;
                if (debug) Debug.Log($"[FootstepSimple] climb tick | speed={_smoothedSpeed:F2} surface={_currentSurface}");
                PlayFromSet(set, climbPitchMult);
            }
            // Early return so we don't also do walking/landing in the same frame
            _wasGrounded = isGrounded;
            _prevVerticalSpeed = verticalSpeed;
            return;
        }

        // ---------------- LANDING PATH ----------------
        if (isGrounded) _groundedTimer += Time.deltaTime;
        else { _groundedTimer = 0f; _airTime += Time.deltaTime; }

        if (isGrounded && !_wasGrounded)
        {
            if (_sinceLand >= minLandInterval && _airTime >= minAirTime)
            {
                bool wasFalling = _prevVerticalSpeed < -2f;
                if (wasFalling)
                {
                    bool heavy = _prevVerticalSpeed < landHeavySpeed;
                    PlayLanding(heavy);
                    _accumDist = 0f;
                }
                _sinceLand = 0f;
            }
            _airTime = 0f;
        }

        // ---------------- WALK/RUN/CROUCH PATH ----------------
        if (isGrounded && _groundedTimer >= groundedSettleTime && _smoothedSpeed >= Mathf.Max(kEpsilonSpeed, minSpeedForSteps))
        {
            _accumDist += frameDist;

            float targetDist = stepDistanceWalk;
            float statePitch = 1f;
            if (isRunning)        { targetDist *= Mathf.Max(0.05f, sprintStepMult); statePitch = sprintPitchMult; }
            else if (isCrouching) { targetDist *= Mathf.Max(0.05f, crouchStepMult); statePitch = crouchPitchMult; }

            bool passedDistance = _accumDist >= targetDist;
            bool passedTime     = Time.time >= _nextStepTime;

            if (passedDistance && passedTime)
            {
                _accumDist    = 0f;
                _sinceStep    = 0f;
                _nextStepTime = Time.time + Mathf.Max(0.12f, minStepInterval);

                if (debug) Debug.Log($"[FootstepSimple] step | smooth={_smoothedSpeed:F2} target={targetDist:F2} surface={_currentSurface}");
                PlayFromSet(ChooseClips(), statePitch);
            }
        }
        else
        {
            _accumDist = 0f;
        }

        _wasGrounded       = isGrounded;
        _prevVerticalSpeed = verticalSpeed;
    }

    AudioClip[] ChooseClips()
    {
        var pack = GetPack(_currentSurface);
        if (isRunning   && pack.run    != null && pack.run.Length    > 0) return pack.run;
        if (isCrouching && pack.crouch != null && pack.crouch.Length > 0) return pack.crouch;
        return pack.walk;
    }

    void PlayLanding(bool heavy)
    {
        var pack = GetPack(_currentSurface);
        var set = heavy
            ? ((pack.landHeavy != null && pack.landHeavy.Length > 0) ? pack.landHeavy : pack.walk)
            : ((pack.landLight != null && pack.landLight.Length > 0) ? pack.landLight : pack.walk);
        PlayFromSet(set, 1f);
    }

    void PlayFromSet(AudioClip[] set, float statePitch)
    {
        if (set == null || set.Length == 0) return;
        var clip = set[UnityEngine.Random.Range(0, set.Length)];
        if (!clip) return;

        float p = UnityEngine.Random.Range(Mathf.Min(pitchRange.x, pitchRange.y),
                                           Mathf.Max(pitchRange.x, pitchRange.y)) * statePitch;
        if (p < 0.05f || float.IsNaN(p) || float.IsInfinity(p)) p = 1f;

        _src.loop         = false;
        _src.dopplerLevel = 0f;
        _src.pitch        = p;
        _src.PlayOneShot(clip, volume);
    }

    SurfaceClips GetPack(string id)
    {
        for (int i = 0; i < surfaces.Count; i++)
            if (string.Equals(surfaces[i].surfaceId, id, StringComparison.OrdinalIgnoreCase))
                return surfaces[i];
        return surfaces.Count > 0 ? surfaces[0] : new SurfaceClips(){ surfaceId = "Default" };
    }
}
