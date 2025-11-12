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
    public Slider brightnessSlider;
    public TextMeshProUGUI brightnessValue;
    public Image brightnessOverlay;

    void Awake()
    {
        if (mouseSlider)      mouseSlider.onValueChanged.AddListener(OnMouseSliderChanged);
        if (soundSlider)      soundSlider.onValueChanged.AddListener(OnSoundSliderChanged);
        if (brightnessSlider) brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    void OnDestroy()
    {
        if (mouseSlider)      mouseSlider.onValueChanged.RemoveListener(OnMouseSliderChanged);
        if (soundSlider)      soundSlider.onValueChanged.RemoveListener(OnSoundSliderChanged);
        if (brightnessSlider) brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
    }

    void OnEnable()
    {
        var g = GameSettingsManager.Instance;
        if (!g) return;

        if (mouseSlider)      mouseSlider.SetValueWithoutNotify(g.MouseSensitivity);
        if (soundSlider)      soundSlider.SetValueWithoutNotify(g.MasterVolume);
        if (brightnessSlider) brightnessSlider.SetValueWithoutNotify(g.Brightness01);

        UpdateMouseLabel(g.MouseSensitivity);
        UpdateSoundLabel(g.MasterVolume);
        UpdateBrightnessUI(g.Brightness01);
    }

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

    void UpdateMouseLabel(float v)
    {
        if (mouseValue)
            mouseValue.text = v.ToString("0.00");
    }

    void UpdateSoundLabel(float v)
    {
        if (soundValue)
            soundValue.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    void UpdateBrightnessUI(float v01)
    {
        if (brightnessValue)
        {
            float pct = Mathf.Lerp(30f, 150f, v01);
            brightnessValue.text = Mathf.RoundToInt(pct) + "%";
        }

        if (brightnessOverlay)
        {
            float darkness = Mathf.Lerp(0.7f, 0f, v01);
            var c = brightnessOverlay.color;
            c.a = darkness;
            brightnessOverlay.color = c;
        }
    }
}
