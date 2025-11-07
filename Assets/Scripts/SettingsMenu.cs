using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    [Header("Mouse Settings")]
    public Slider mouseSlider;
    public TextMeshProUGUI mouseValue;

    [Header("Sound Settings")]
    public Slider soundSlider;
    public TextMeshProUGUI soundValue;

    [Header("Display Settings (Brightness)")]
    [Tooltip("Slider die de helderheid aanpast (0 = 30%, 1 = 150%).")]
    public Slider brightnessSlider;
    public TextMeshProUGUI brightnessValue;

    [Header("Overlay")]
    [Tooltip("Zwarte overlay (Darkoverlay) boven de video die meedimt met helderheid.")]
    public Image brightnessOverlay;

    private void Awake()
    {
        if (mouseSlider)      mouseSlider.onValueChanged.AddListener(OnMouseSliderChanged);
        if (soundSlider)      soundSlider.onValueChanged.AddListener(OnSoundSliderChanged);
        if (brightnessSlider) brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    private void OnDestroy()
    {
        if (mouseSlider)      mouseSlider.onValueChanged.RemoveListener(OnMouseSliderChanged);
        if (soundSlider)      soundSlider.onValueChanged.RemoveListener(OnSoundSliderChanged);
        if (brightnessSlider) brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
    }

    private void OnEnable()
    {
        var mgr = GameSettingsManager.Instance;
        if (mgr == null) return;

        if (mouseSlider)      mouseSlider.SetValueWithoutNotify(mgr.MouseSensitivity);
        if (soundSlider)      soundSlider.SetValueWithoutNotify(mgr.MasterVolume);
        if (brightnessSlider) brightnessSlider.SetValueWithoutNotify(mgr.Brightness01);

        UpdateMouseLabel(mgr.MouseSensitivity);
        UpdateSoundLabel(mgr.MasterVolume);
        UpdateBrightnessUI(mgr.Brightness01);
    }

    // ---------------------------
    // Slider event handlers
    // ---------------------------

    public void OnMouseSliderChanged(float v)
    {
        GameSettingsManager.Instance?.SetMouseSensitivity(v);
        UpdateMouseLabel(v);
    }

    public void OnSoundSliderChanged(float v)
    {
        GameSettingsManager.Instance?.SetMasterVolume(v);
        UpdateSoundLabel(v);
    }

    public void OnBrightnessChanged(float v)
    {
        GameSettingsManager.Instance?.SetBrightness01(v);
        UpdateBrightnessUI(v);
    }

    // ---------------------------
    // UI update helpers
    // ---------------------------

    private void UpdateMouseLabel(float v)
    {
        if (mouseValue)
            mouseValue.text = v.ToString("0.00");
    }

    private void UpdateSoundLabel(float v)
    {
        if (soundValue)
            soundValue.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    private void UpdateBrightnessUI(float v01)
    {
        if (brightnessValue)
        {
            // Toon 30% - 150%
            float pct = Mathf.Lerp(17f, 150f, v01);
            brightnessValue.text = Mathf.RoundToInt(pct) + "%";
        }

        // Pas Darkoverlay-transparantie aan
        if (brightnessOverlay)
        {
            float darkness = Mathf.Lerp(0.7f, 0f, v01); // donkerder bij lage waarde
            Color c = brightnessOverlay.color;
            c.a = darkness;
            brightnessOverlay.color = c;
        }
    }
}