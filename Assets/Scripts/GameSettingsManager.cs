using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance;

    [Range(0.1f, 3f)] public float MouseSensitivity { get; private set; } = 1.0f;
    [Range(0f, 1f)]  public float MasterVolume     { get; private set; } = 1.0f;
    [Range(0f, 1f)]  public float Brightness01     { get; private set; } = 0.5f;

    const string KEY_SENS = "settings_mouse_sens";
    const string KEY_VOL  = "settings_master_vol";
    const string KEY_BRI  = "settings_brightness01";

    Volume globalVolume;
    ColorAdjustments colorAdj;

    // 👉 Nieuw event om te luisteren naar muissensitiviteit
    public event Action<float> OnMouseSensitivityChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        MouseSensitivity = PlayerPrefs.GetFloat(KEY_SENS, 1.0f);
        MasterVolume     = PlayerPrefs.GetFloat(KEY_VOL , 1.0f);
        Brightness01     = PlayerPrefs.GetFloat(KEY_BRI , 0.5f);

        EnsurePostFX();
        ApplyBrightness();
        ApplyVolume();
        ApplyToAllCameras();

        SceneManager.sceneLoaded += (_, __) =>
        {
            EnsurePostFX();
            ApplyBrightness();
            ApplyToAllCameras();
        };
    }

    void EnsurePostFX()
    {
        if (!globalVolume)
            globalVolume = FindFirstObjectByType<Volume>(FindObjectsInactive.Include);

        if (!globalVolume)
        {
            var go = new GameObject("Global PostFX");
            DontDestroyOnLoad(go);
            globalVolume = go.AddComponent<Volume>();
            globalVolume.isGlobal = true;
            globalVolume.priority = 999f;
        }

        if (!globalVolume.profile)
            globalVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

        if (!globalVolume.profile.TryGet(out colorAdj))
        {
            colorAdj = globalVolume.profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.overrideState = true;
        }
    }

    void ApplyToAllCameras()
    {
        foreach (var cam in Camera.allCameras)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
        }
    }

    // ========= SETTINGS FUNCTIES =========

    public void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, 0.1f, 3f);
        PlayerPrefs.SetFloat(KEY_SENS, MouseSensitivity);
        PlayerPrefs.Save();

        // 👉 Roep event aan
        OnMouseSensitivityChanged?.Invoke(MouseSensitivity);
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KEY_VOL, MasterVolume);
        PlayerPrefs.Save();
        ApplyVolume();
    }

    public void SetBrightness01(float value)
    {
        Brightness01 = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KEY_BRI, Brightness01);
        PlayerPrefs.Save();
        ApplyBrightness();
    }

    void ApplyBrightness()
    {
        if (!colorAdj) return;
        float exposure = Mathf.Lerp(-1.2f, 1.0f, Brightness01);
        colorAdj.postExposure.Override(exposure);
    }

    void ApplyVolume()
    {
        AudioListener.volume = MasterVolume;

        var videos = FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var vp in videos)
        {
            if (!vp) continue;

            if (vp.audioOutputMode == VideoAudioOutputMode.AudioSource)
            {
                ushort count = (ushort)Mathf.Max(1, vp.audioTrackCount);
                for (ushort t = 0; t < count; t++)
                {
                    var src = vp.GetTargetAudioSource(t);
                    if (!src) continue;
                    src.mute = false;
                    src.ignoreListenerVolume = false;
                    src.volume = 1f;
                }
            }
            else if (vp.audioOutputMode == VideoAudioOutputMode.Direct)
            {
                ushort count = (ushort)Mathf.Max(1, vp.audioTrackCount);
                for (ushort t = 0; t < count; t++)
                {
                    vp.SetDirectAudioMute(t, false);
                    vp.SetDirectAudioVolume(t, MasterVolume);
                }
            }
        }
    }
}
