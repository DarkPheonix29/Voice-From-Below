using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class FootstepController : MonoBehaviour
{
    [Serializable] public class SurfaceClips
    {
        public string surfaceId = "Concrete";
        public AudioClip[] walk;
        public AudioClip[] run;      // optional; falls back to walk
        public AudioClip[] crouch;   // optional; falls back to walk
        public AudioClip[] climb;    // optional; falls back to walk
        public AudioClip[] landLight;
        public AudioClip[] landHeavy;
    }

    [Header("Movement")]
    public Transform feet;                    // sound emit point (bottom of capsule)
    public LayerMask groundMask = ~0;
    public float stepDistanceWalk = 2.1f;     // meters/step (baseline)
    public float climbInterval = 0.33f;
    public float landHeavySpeed = -9f;        // y-vel threshold for heavy landing

    [Header("State (feed from your controller)")]
    public bool isRunning;
    public bool isCrouching;
    public bool isClimbing;
    public float moveSpeed;                   // m/s from your controller
    public bool useExternalGrounded = false;  // set true to drive grounded yourself
    public bool externalGrounded = false;     // your controller sets this

    [Header("Tempo multipliers")]
    public float sprintStepMult = 0.65f;      // <1 => more frequent
    public float crouchStepMult = 1.35f;      // >1 => less frequent

    [Header("Pitch multipliers")]
    public float sprintPitchMult = 1.06f;
    public float crouchPitchMult = 0.94f;

    [Header("Audio")]
    public List<SurfaceClips> surfaces = new();
    [Range(0f,1f)] public float volume = 0.9f;
    public Vector2 pitchRange = new Vector2(0.98f, 1.02f);

    [Header("Ground check / debounce")]
    public float rayLength = 1.2f;            // ray for surface id
    public float groundCheckRadius = 0.18f;   // spherecast radius
    public float groundCheckDist = 0.25f;     // spherecast distance
    public float minSpeedForSteps = 0.6f;     // m/s to allow step
    public float minStepInterval = 0.22f;     // seconds between steps
    public float minLandInterval = 0.25f;     // seconds between land sounds

    [Header("Misc")]
    public bool randomizeStartOffset = true;

    AudioSource _src;
    Vector3 _lastPos;
    float _accumDist;
    float _climbTimer;
    float _verticalVel;
    string _currentSurface = "Concrete";

    // debounce state
    bool _wasGrounded;
    float _sinceLastStep;
    float _sinceLastLand;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.spatialBlend = 1f;
        _src.playOnAwake = false;
        _src.loop = false;

        if (feet == null) feet = transform;
        _lastPos = transform.position;

        if (randomizeStartOffset)
            _accumDist = UnityEngine.Random.Range(0f, 1f);

        _wasGrounded = IsGrounded();
    }

    void Update()
    {
        _sinceLastStep += Time.deltaTime;
        _sinceLastLand += Time.deltaTime;

        // track movement & vertical velocity
        var pos = transform.position;
        var horizDelta = Vector3.ProjectOnPlane(pos - _lastPos, Vector3.up);
        var frameDist = horizDelta.magnitude;
        _verticalVel = (pos.y - _lastPos.y) / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastPos = pos;

        bool grounded = IsGrounded();
UpdateSurfaceId();

// --- handle first-ground contact or land ---
if (grounded && !_wasGrounded)
{
    // full reset so we start fresh
    _accumDist = 0f;
    _sinceLastStep = 0f;
    _sinceLastLand = 0f;

    // optional: play landing only after falling, not initial spawn
    if (Time.timeSinceLevelLoad > 0.2f && Mathf.Abs(_verticalVel) > 1f)
    {
        if (_verticalVel < landHeavySpeed) PlayLanding(true);
        else if (_verticalVel < -2f)       PlayLanding(false);
    }
}

_wasGrounded = grounded;


        // climbing cadence (fallback to walk clips if climb not set)
        if (isClimbing)
        {
            _climbTimer += Time.deltaTime;
            if (_climbTimer >= climbInterval && grounded) // optional: require grounded on ladder
            {
                _climbTimer = 0f;
                var pack = Pack();
                PlayFromSet(FirstNonEmpty(pack.climb, pack.walk), 1f);
            }
            return;
        }

        // no steps if not grounded or moving too slow
        if (!grounded || moveSpeed < minSpeedForSteps)
            return;

        // accumulate horizontal distance for step timing
        _accumDist += frameDist;

        float targetDist = stepDistanceWalk;
        float statePitchMult = 1f;

        if (isRunning)
        {
            targetDist *= Mathf.Max(0.05f, sprintStepMult);
            statePitchMult = sprintPitchMult;
        }
        else if (isCrouching)
        {
            targetDist *= Mathf.Max(0.05f, crouchStepMult);
            statePitchMult = crouchPitchMult;
        }

        // gate on min time between steps AND distance
        if (_accumDist >= targetDist && _sinceLastStep >= minStepInterval)
        {
            _accumDist = 0f;
            _sinceLastStep = 0f;

            var pack = Pack();
            var set =
                isRunning   ? FirstNonEmpty(pack.run,    pack.walk) :
                isCrouching ? FirstNonEmpty(pack.crouch, pack.walk) :
                              pack.walk;

            PlayFromSet(set, statePitchMult);
        }
    }

    // CharacterController landing hook
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Optional: keep if you prefer controller's collision-based landing.
        // Debounced by time and grounded transition already, so you can remove if redundant.
        // If you keep it, only trigger when the contact is mostly below:
        if (_sinceLastLand >= minLandInterval && hit.moveDirection.y < -0.3f)
        {
            if (_verticalVel < landHeavySpeed) PlayLanding(true);
            else if (_verticalVel < -2f)       PlayLanding(false);
            _sinceLastLand = 0f;
        }
    }

    public void NotifyLanding(float yVelocity)
    {
        if (_sinceLastLand < minLandInterval) return;
        if (yVelocity < landHeavySpeed) PlayLanding(true);
        else if (yVelocity < -2f)       PlayLanding(false);
        _sinceLastLand = 0f;
        _accumDist = 0f;
    }

    bool IsGrounded()
    {
        if (useExternalGrounded) return externalGrounded;

        // Spherecast from feet slightly upward, down into ground
        Vector3 origin = feet.position + Vector3.up * 0.05f;
        return Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out _, groundCheckDist, groundMask, QueryTriggerInteraction.Ignore);
    }

    void PlayLanding(bool heavy)
    {
        var pack = Pack();
        var set = heavy ? FirstNonEmpty(pack.landHeavy, pack.walk)
                        : FirstNonEmpty(pack.landLight, pack.walk);
        PlayFromSet(set, 1f);
    }

    SurfaceClips Pack()
    {
        for (int i=0;i<surfaces.Count;i++)
            if (string.Equals(surfaces[i].surfaceId, _currentSurface, StringComparison.OrdinalIgnoreCase))
                return surfaces[i];
        return surfaces.Count > 0 ? surfaces[0] : new SurfaceClips(){ surfaceId = "Default" };
    }

    static AudioClip[] FirstNonEmpty(AudioClip[] primary, AudioClip[] fallback)
    {
        if (primary != null && primary.Length > 0) return primary;
        return (fallback != null && fallback.Length > 0) ? fallback : Array.Empty<AudioClip>();
    }

    void PlayFromSet(AudioClip[] set, float statePitchMult)
    {
        if (set == null || set.Length == 0) return;
        var clip = set[UnityEngine.Random.Range(0, set.Length)];
        _src.transform.position = feet.position;

        float p = UnityEngine.Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        p *= statePitchMult;
        if (p < 0.05f || float.IsNaN(p) || float.IsInfinity(p)) p = 1f;

        _src.pitch = p;
        _src.volume = volume;
        _src.clip = clip;
        _src.Play();
    }

    void UpdateSurfaceId()
    {
        // Raycast down to get tag/physmat/terrain/override
        Ray ray = new Ray(feet.position + Vector3.up * 0.05f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            var overrideComp = hit.collider.GetComponentInParent<SurfaceIdOverride>();
            if (overrideComp && !string.IsNullOrEmpty(overrideComp.surfaceId)) { _currentSurface = overrideComp.surfaceId; return; }

            string byTag = hit.collider.tag;
            if (!string.IsNullOrEmpty(byTag) && byTag != "Untagged") { _currentSurface = byTag; return; }

            if (hit.collider.sharedMaterial) { _currentSurface = hit.collider.sharedMaterial.name; return; }

            var terrain = hit.collider.GetComponent<Terrain>();
            if (terrain)
            {
                var data = terrain.terrainData;
                Vector3 tPos = terrain.transform.InverseTransformPoint(hit.point);
                int mapX = Mathf.Clamp((int)((tPos.x / data.size.x) * data.alphamapWidth), 0, data.alphamapWidth-1);
                int mapZ = Mathf.Clamp((int)((tPos.z / data.size.z) * data.alphamapHeight), 0, data.alphamapHeight-1);
                float[,,] mix = data.GetAlphamaps(mapX, mapZ, 1, 1);
                int maxIdx = 0; float max = 0f;
                for (int i=0;i<mix.GetLength(2);i++) if (mix[0,0,i] > max) { max = mix[0,0,i]; maxIdx = i; }
                var layer = data.terrainLayers[maxIdx];
                string layerName = layer != null ? (!string.IsNullOrEmpty(layer.name) ? layer.name :
                                  (layer.diffuseTexture ? layer.diffuseTexture.name : "")) : "";
                if (!string.IsNullOrEmpty(layerName)) { _currentSurface = layerName; return; }
            }
        }

        _currentSurface = "Concrete";
    }
}
