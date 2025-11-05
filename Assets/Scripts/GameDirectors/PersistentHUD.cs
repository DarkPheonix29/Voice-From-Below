using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class PersistentHUD : MonoBehaviour
{
    public static PersistentHUD Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private TextMeshProUGUI subtitlesText;
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private Image fadeImage;          // child of ScreenFade (optional fallback)
    [SerializeField] private CanvasGroup fadeGroup;    // CanvasGroup on ScreenFade (recommended)
    [SerializeField] private AudioSource objectivePing;

    [Header("Defaults")]
    public float typeSpeed = 45f;

    [Header("Fade Settings")]
    public AnimationCurve fadeEase = AnimationCurve.EaseInOut(0,0,1,1);
    public bool fadeUseUnscaledTime = true;
    public bool startBlack = true;
    [Range(0,1)] public float startBlackAlpha = 1f;
    public float defaultFadeDuration = 1.2f;

    [Header("Startup Fade")]
    public bool autoFadeInOnStart = true;
    public float autoFadeInDuration = 1.2f;

    [Header("Audio Fade (optional)")]
    public bool fadeAudioWithScreen = false;
    [Range(0,1)] public float audioMinVolume = 0f;
    [Range(0,1)] public float audioMaxVolume = 1f;

    // internal fade state
    Coroutine fadeRoutine;
    bool fadeRunning;
    float lockedAlpha; // last value we set this frame

    // internal scene-fade state
    Coroutine _sceneFadeRoutine;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!fadeGroup && fadeImage)
        {
            // try to find a CanvasGroup on the parent (recommended)
            fadeGroup = fadeImage.GetComponentInParent<CanvasGroup>();
        }

        if (!fadeGroup && !fadeImage)
        {
            Debug.LogError("[HUD] No fade target assigned (CanvasGroup or Image).");
            return;
        }

        SetFadeInstant(startBlack ? startBlackAlpha : 0f);
        if (fadeAudioWithScreen)
            AudioListener.volume = startBlack ? audioMinVolume : audioMaxVolume;
    }

    IEnumerator Start()
    {
        // wait a frame so other UI initializes (prevents “pop”)
        yield return null;
        if (autoFadeInOnStart && startBlack) FadeFromBlack(autoFadeInDuration);
    }

    void LateUpdate()
    {
        // while fading, enforce the alpha we calculated this frame
        if (!fadeRunning) return;
        ApplyAlpha(lockedAlpha, force:true);
    }

    // ---------- Objectives ----------
    public void SetObjective(string text, bool ping = true)
    {
        if (objectiveText) objectiveText.text = text;
        if (ping && objectivePing) objectivePing.Play();
    }

    // ---------- Subtitles ----------
    Coroutine subRoutine;
    public void ShowSubtitle(string text, float holdSeconds = 1.0f, bool typewriter = true)
    {
        if (!subtitlesText) return;
        if (subRoutine != null) StopCoroutine(subRoutine);
        subRoutine = StartCoroutine(SubtitleRoutine(text, holdSeconds, typewriter));
    }
    public void ClearSubtitle()
    {
        if (subRoutine != null) StopCoroutine(subRoutine);
        if (subtitlesText) subtitlesText.text = "";
    }
    IEnumerator SubtitleRoutine(string text, float hold, bool typewriter)
    {
        if (!subtitlesText) yield break;
        if (!typewriter)
        {
            subtitlesText.text = text;
            if (hold > 0f) yield return new WaitForSeconds(hold);
            yield break;
        }
        subtitlesText.text = "";
        int i = 0;
        while (i < text.Length)
        {
            float dt = fadeUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            i = Mathf.Min(i + Mathf.Max(1, Mathf.RoundToInt(dt * typeSpeed)), text.Length);
            subtitlesText.text = text.Substring(0, i);
            yield return null;
        }
        if (hold > 0) yield return new WaitForSeconds(hold);
    }

    // ---------- Fade API ----------
    public void FadeFromBlack()           => Fade(1f, 0f, defaultFadeDuration);
    public void FadeToBlack()             => Fade(0f, 1f, defaultFadeDuration);
    public void FadeFromBlack(float d)    => Fade(1f, 0f, d);
    public void FadeToBlack(float d)      => Fade(0f, 1f, d);

    public void SetBlack() => SetFadeInstant(1f);
    public void SetClear() => SetFadeInstant(0f);

    public IEnumerator FadeOutIn(float outDur, float hold, float inDur)
    {
        yield return FadeRoutine(Alpha, 1f, outDur);
        if (hold > 0f)
        {
            float t = 0f;
            while (t < hold)
            {
                t += fadeUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }
        yield return FadeRoutine(1f, 0f, inDur);
    }

    public void Fade(float from, float to, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(from, to, duration));
    }

    // ---------- Scene Load WITH Fade (call this from triggers / buttons) ----------
    /// <summary>
    /// Fades out, loads scene by name, then fades in. Uses default durations if you pass negative values.
    /// </summary>
    public void LoadSceneWithFade(string sceneName, float fadeOut = -1f, float fadeIn = -1f, System.Action beforeLoad = null)
    {
        if (fadeOut < 0f) fadeOut = defaultFadeDuration;
        if (fadeIn  < 0f) fadeIn  = defaultFadeDuration;

        if (!fadeGroup && !fadeImage)
        {
            Debug.LogError("[PersistentHUD] No fade target set (CanvasGroup or Image).");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[PersistentHUD] Scene '{sceneName}' is not in Build Settings or name is wrong.");
            return;
        }

        if (_sceneFadeRoutine != null) StopCoroutine(_sceneFadeRoutine);
        _sceneFadeRoutine = StartCoroutine(SceneFadeRoutine(() => SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single),
                                                           fadeOut, fadeIn, beforeLoad));
    }

    /// <summary>
    /// Fades out, loads scene by build index, then fades in.
    /// </summary>
    public void LoadSceneWithFade(int buildIndex, float fadeOut = -1f, float fadeIn = -1f, System.Action beforeLoad = null)
    {
        if (fadeOut < 0f) fadeOut = defaultFadeDuration;
        if (fadeIn  < 0f) fadeIn  = defaultFadeDuration;

        if (!fadeGroup && !fadeImage)
        {
            Debug.LogError("[PersistentHUD] No fade target set (CanvasGroup or Image).");
            return;
        }

        if (_sceneFadeRoutine != null) StopCoroutine(_sceneFadeRoutine);
        _sceneFadeRoutine = StartCoroutine(SceneFadeRoutine(() => SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single),
                                                           fadeOut, fadeIn, beforeLoad));
    }

    IEnumerator SceneFadeRoutine(System.Func<AsyncOperation> loadOpFactory, float fadeOut, float fadeIn, System.Action beforeLoad)
    {
        // 1) Fade OUT
        FadeToBlack(fadeOut);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.0001f, fadeOut));

        // 2) optional hook (e.g., set spawnId)
        beforeLoad?.Invoke();

        // 3) Load scene
        var op = loadOpFactory?.Invoke();
        if (op == null)
        {
            Debug.LogError("[PersistentHUD] Load operation was null.");
            FadeFromBlack(0.3f);
            _sceneFadeRoutine = null;
            yield break;
        }
        while (!op.isDone) yield return null;

        // 4) let objects initialize for a frame (spawns, OnEnable, etc.)
        yield return null;

        // 5) Fade IN
        FadeFromBlack(fadeIn);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.0001f, fadeIn));

        _sceneFadeRoutine = null;
    }

    // ---------- Internals ----------
    float Alpha
    {
        get
        {
            if (fadeGroup) return fadeGroup.alpha;
            if (fadeImage) return fadeImage.color.a;
            return 0f;
        }
    }

    void ApplyAlpha(float a, bool force=false)
    {
        a = Mathf.Clamp01(a);

        if (fadeGroup)
        {
            if (force || !Mathf.Approximately(fadeGroup.alpha, a))
                fadeGroup.alpha = a;
            fadeGroup.blocksRaycasts = a >= 0.999f; // block clicks when black
        }
        else if (fadeImage)
        {
            var c = fadeImage.color;
            if (force || !Mathf.Approximately(c.a, a))
                fadeImage.color = new Color(c.r, c.g, c.b, a);
            fadeImage.raycastTarget = a >= 0.999f;
        }

        if (fadeAudioWithScreen)
            AudioListener.volume = Mathf.Lerp(audioMaxVolume, audioMinVolume, a);
    }

    void SetFadeInstant(float a)
    {
        lockedAlpha = a;
        ApplyAlpha(a, force:true);
    }

    IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (duration <= 0.0001f)
        {
            SetFadeInstant(to);
            yield break;
        }

        float startA = Alpha;
        float endA = to;

        fadeRunning = true;

        float t = 0f;
        while (t < duration)
        {
            float dt = fadeUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            float u = Mathf.Clamp01(t / duration);
            float eased = fadeEase != null ? fadeEase.Evaluate(u) : u;
            lockedAlpha = Mathf.Lerp(startA, endA, eased);
            ApplyAlpha(lockedAlpha); // set now (LateUpdate will enforce)
            yield return null;
        }

        lockedAlpha = endA;
        ApplyAlpha(endA, force:true);
        fadeRunning = false;
    }
}
