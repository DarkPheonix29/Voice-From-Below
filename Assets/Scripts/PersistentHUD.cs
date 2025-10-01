using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PersistentHUD : MonoBehaviour
{
    public static PersistentHUD Instance { get; private set; }

    [Header("Refs")]
    public TextMeshProUGUI subtitlesText;
    public TextMeshProUGUI objectiveText;
    public Image fadeImage;
    public AudioSource objectivePing; // optional

    [Header("Defaults")]
    public float typeSpeed = 45f;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // Safety: ensure fadeImage raycasts off
        if (fadeImage) fadeImage.raycastTarget = false;
    }

    // ---------- Objective ----------
    public void SetObjective(string text, bool ping = true)
    {
        if (objectiveText) objectiveText.text = text;
        if (ping && objectivePing) objectivePing.Play();
    }

    // ---------- Subtitles ----------
    Coroutine subRoutine;
    public void ShowSubtitle(string text, float holdSeconds = 1.0f, bool typewriter = true)
    {
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
            yield return new WaitForSeconds(hold);
            yield break;
        }

        subtitlesText.text = "";
        int i = 0;
        while (i < text.Length)
        {
            i = Mathf.Min(i + Mathf.Max(1, Mathf.RoundToInt(Time.deltaTime * typeSpeed)), text.Length);
            subtitlesText.text = text.Substring(0, i);
            yield return null;
        }
        if (hold > 0) yield return new WaitForSeconds(hold);
    }

    // ---------- Fade ----------
    Coroutine fadeRoutine;
    public void FadeFromBlack(float duration = 1f)
    {
        if (!fadeImage) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(1f, 0f, duration));
    }

    public void FadeToBlack(float duration = 1f)
    {
        if (!fadeImage) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(0f, 1f, duration));
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        var c = fadeImage.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / Mathf.Max(0.0001f, duration));
            fadeImage.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        fadeImage.color = new Color(c.r, c.g, c.b, to);
    }
}
