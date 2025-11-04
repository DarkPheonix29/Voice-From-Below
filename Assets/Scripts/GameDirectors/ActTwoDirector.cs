using System.Collections;
using UnityEngine;

public class ActTwoDirector : MonoBehaviour
{
    public static ActTwoDirector Instance { get; private set; }

    [Header("Flags (SaveFlags)")]
    public string walkieFoundFlag = "Act2_WalkieFound";
    public string leverInstalledFlag = "Act2_LeverInstalled";

    [Header("Walkie Talkie")]
    [Tooltip("AudioSource used to play the periodic walkie ping (PlayOneShot).")]
    public AudioSource walkieSource;
    public AudioClip walkiePingClip;
    [Tooltip("Seconds between pings while the walkie isn't found yet.")]
    public float walkiePingInterval = 8f;
    [Tooltip("Small randomization added to interval (+/-).")]
    public float walkiePingJitter = 0.75f;

    [Header("Voice Lines")]
    [Tooltip("VO line when the player picks up/found the walkie.")]
    public AudioClip voWalkieFound;
    [TextArea] public string subWalkieFound = "[Radio] *You found the walkie.*";

    [Tooltip("VO when the player tries to operate the lever without a handle.")]
    public AudioClip voLeverMissing;
    [TextArea] public string subLeverMissing = "[You] I need a lever handle for this.";

    [Tooltip("VO after the lever is installed (before elevator starts).")]
    public AudioClip voLeverInstalled;
    [TextArea] public string subLeverInstalled = "[You] That should do it.";

    [Header("Elevator")]
    [Tooltip("AudioSource located near/inside the elevator to play movement rumble.")]
    public AudioSource elevatorSource;
    public AudioClip elevatorMoveClip;

    [Header("Subtitle pacing")]
    public float extraHold = 0.6f;

    Coroutine pingLoop;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Start/stop ping loop depending on whether walkie already found (saved or in-session)
        bool alreadyFound = SaveFlags.Instance && SaveFlags.Instance.Has(walkieFoundFlag);
        if (!alreadyFound) StartPingLoop();
    }

    // --- PING LOOP ------------------------------------------------------------

    public void StartPingLoop()
    {
        if (pingLoop != null) StopCoroutine(pingLoop);
        pingLoop = StartCoroutine(PingRoutine());
    }

    public void StopPingLoop()
    {
        if (pingLoop != null)
        {
            StopCoroutine(pingLoop);
            pingLoop = null;
        }
    }

    IEnumerator PingRoutine()
    {
        // Keep pinging until the walkie is found
        while (!(SaveFlags.Instance && SaveFlags.Instance.Has(walkieFoundFlag)))
        {
            if (walkieSource && walkiePingClip)
                walkieSource.PlayOneShot(walkiePingClip);

            float jitter = Random.Range(-walkiePingJitter, walkiePingJitter);
            float wait = Mathf.Max(0.25f, walkiePingInterval + jitter);
            yield return new WaitForSeconds(wait);
        }
    }

    // --- PUBLIC HOOKS YOU CALL FROM GAMEPLAY ---------------------------------

    /// <summary>
    /// Call this when the player picks up / interacts with the walkie talkie.
    /// </summary>
    public void OnWalkieFound()
    {
        // Set flag (session). Persist later on checkpoint with SaveFlags.Commit()
        if (SaveFlags.Instance) SaveFlags.Instance.Set(walkieFoundFlag);

        StopPingLoop();

        if (voWalkieFound || !string.IsNullOrEmpty(subWalkieFound))
        {
            VoiceLineQueue.Instance?.Enqueue(new VoiceLine
            {
                clip = voWalkieFound,
                subtitle = subWalkieFound,
                extraHold = extraHold,
                overrideSource = walkieSource // plays from the radio if assigned
            });
        }
    }

    /// <summary>
    /// Call this when the player tries to use the lever without the required handle.
    /// </summary>
    public void OnLeverMissingAttempt()
    {
        if (voLeverMissing || !string.IsNullOrEmpty(subLeverMissing))
        {
            VoiceLineQueue.Instance?.Enqueue(new VoiceLine
            {
                clip = voLeverMissing,
                subtitle = subLeverMissing,
                extraHold = extraHold
            });
        }
    }

    /// <summary>
    /// Call this after the lever is successfully installed.
    /// ActTwoDirector will play a VO line, then elevator move SFX, then request level transfer.
    /// </summary>
    public void OnLeverInstalledThenTransfer(LeverBase lever)
    {
        if (SaveFlags.Instance) SaveFlags.Instance.Set(leverInstalledFlag);

        StartCoroutine(LeverInstalledFlow(lever));
    }

    IEnumerator LeverInstalledFlow(LeverBase lever)
    {
        // 1) VO: Lever installed
        if (voLeverInstalled || !string.IsNullOrEmpty(subLeverInstalled))
        {
            VoiceLineQueue.Instance?.Enqueue(new VoiceLine
            {
                clip = voLeverInstalled,
                subtitle = subLeverInstalled,
                extraHold = extraHold
            });
        }

        // Wait until all queued lines finish
        if (VoiceLineQueue.Instance != null)
            yield return VoiceLineQueue.Instance.WaitUntilIdle();

        // 2) Elevator moving SFX (fire & forget, or wait a split second if you like)
        if (elevatorSource && elevatorMoveClip)
            elevatorSource.PlayOneShot(elevatorMoveClip);

        // Optional: small delay to let the elevator sound begin before fade/transfer
        yield return new WaitForSeconds(0.2f);

        // 3) Ask the lever to perform the transfer (fade & scene change)
        if (lever != null)
            lever.PullAndTransfer(); // needs to be public (see updated LeverBase below)
    }
}
