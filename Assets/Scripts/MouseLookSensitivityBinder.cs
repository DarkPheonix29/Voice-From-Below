using System;
using System.Reflection;
using UnityEngine;

#if CINEMACHINE
using Cinemachine;
#endif

public class MouseLookSensitivityBinder : MonoBehaviour
{
    [Header("Algemeen")]
    public float baseSensitivity = 1.0f;
    public bool applyToY = true;

#if CINEMACHINE
    [Header("Cinemachine (optioneel)")]
    public CinemachineFreeLook freeLook;
    public CinemachineVirtualCamera vcam;
    float cm_X_base, cm_Y_base;
#endif

    FieldInfo fSensitivity, fXSens, fYSens;
    PropertyInfo pSensitivity, pXSens, pYSens;
    MonoBehaviour lookTarget;

    void Awake()
    {
#if CINEMACHINE
        if (!freeLook) freeLook = GetComponentInChildren<CinemachineFreeLook>(true);
        if (!vcam)     vcam     = GetComponentInChildren<CinemachineVirtualCamera>(true);

        if (freeLook)
        {
            cm_X_base = freeLook.m_XAxis.m_MaxSpeed;
            cm_Y_base = freeLook.m_YAxis.m_MaxSpeed;
        }
        else if (vcam)
        {
            var pov = vcam.GetCinemachineComponent<CinemachinePOV>();
            if (pov)
            {
                cm_X_base = pov.m_HorizontalAxis.m_MaxSpeed;
                cm_Y_base = pov.m_VerticalAxis.m_MaxSpeed;
            }
        }
#endif

        var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in behaviours)
        {
            if (mb == null) continue;
            var t = mb.GetType();
            if (t == typeof(MouseLookSensitivityBinder)) continue;

            fSensitivity = t.GetField("sensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                        ?? t.GetField("mouseSensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                        ?? t.GetField("lookSensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            fXSens = t.GetField("XSensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            fYSens = t.GetField("YSensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            pSensitivity = t.GetProperty("Sensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                        ?? t.GetProperty("MouseSensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                        ?? t.GetProperty("LookSensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            pXSens = t.GetProperty("XSensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            pYSens = t.GetProperty("YSensitivity", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

            if (fSensitivity != null || pSensitivity != null || fXSens != null || pXSens != null)
            {
                lookTarget = mb;
                break;
            }
        }
    }

    void OnEnable()
    {
        Apply(GameSettingsManager.Instance ? GameSettingsManager.Instance.MouseSensitivity : 1f);
        if (GameSettingsManager.Instance != null)
            GameSettingsManager.Instance.OnMouseSensitivityChanged += Apply;
    }

    void OnDisable()
    {
        if (GameSettingsManager.Instance != null)
            GameSettingsManager.Instance.OnMouseSensitivityChanged -= Apply;
    }

    void Apply(float setting)
    {
        float eff = baseSensitivity * setting;

#if CINEMACHINE
        if (freeLook)
        {
            freeLook.m_XAxis.m_MaxSpeed = cm_X_base * eff;
            if (applyToY) freeLook.m_YAxis.m_MaxSpeed = cm_Y_base * eff;
        }
        else if (vcam)
        {
            var pov = vcam.GetCinemachineComponent<CinemachinePOV>();
            if (pov)
            {
                pov.m_HorizontalAxis.m_MaxSpeed = cm_X_base * eff;
                if (applyToY) pov.m_VerticalAxis.m_MaxSpeed = cm_Y_base * eff;
            }
        }
#endif

        if (lookTarget)
        {
            if (fSensitivity != null) fSensitivity.SetValue(lookTarget, eff);
            if (pSensitivity != null && pSensitivity.CanWrite) pSensitivity.SetValue(lookTarget, eff);
            if (fXSens != null) fXSens.SetValue(lookTarget, eff);
            if (pXSens != null && pXSens.CanWrite) pXSens.SetValue(lookTarget, eff);

            if (applyToY)
            {
                if (fYSens != null) fYSens.SetValue(lookTarget, eff);
                if (pYSens != null && pYSens.CanWrite) pYSens.SetValue(lookTarget, eff);
            }
        }
    }
}
