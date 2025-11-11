using UnityEngine;
using System;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    [Header("UI (Must be assigned in the Inspector in the starting scene)")]
    public GameObject qteUI;
    public TextMeshProUGUI qteText;

    [Header("Render/Order")]
    public bool forceCanvasOnTop = true;
    public int topSortingOrder = 4000;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        // 1. Make the QTEManager persistent
        DontDestroyOnLoad(gameObject);
        
        // 2. Make the assigned UI persistent by making it a child
        MakeUIPersistent();

        if (qteUI) qteUI.SetActive(false);

        if (forceCanvasOnTop && qteUI)
        {
            var canvas = qteUI.GetComponentInParent<Canvas>(includeInactive: true);
            if (canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                canvas.sortingOrder = topSortingOrder;
        }
    }

    /// <summary>
    /// Ensures the assigned UI elements persist across scene loads by setting the 
    /// persistent QTEManager as their parent.
    /// </summary>
    private void MakeUIPersistent()
    {
        if (qteUI != null)
        {
            // Find the root Canvas of the QTE_UI hierarchy
            Canvas rootCanvas = qteUI.GetComponentInParent<Canvas>(includeInactive: true);
            
            // If the QTE UI is part of a scene-specific Canvas, we need to move the Canvas/UI
            if (rootCanvas != null)
            {
                // Set the QTEManager as the new parent for the root Canvas
                rootCanvas.transform.SetParent(this.transform, worldPositionStays: true);
                
                // Note: The root Canvas itself is now saved by DontDestroyOnLoad
                Debug.Log("[QTEManager] QTE Canvas successfully moved under persistent manager.");
            }
            else
            {
                // If it wasn't parented to a Canvas, just make the qteUI object the child
                qteUI.transform.SetParent(this.transform, worldPositionStays: true);
                Debug.Log("[QTEManager] QTE UI object successfully moved under persistent manager.");
            }
        }
    }

    // --- Public QTE Flow (The rest of the script is unchanged and functional) ---

    public void Begin(KeyCode key, float duration, Action<bool> onDone)
    {
        if (qteUI == null || qteText == null)
        {
             Debug.LogError("[QTEManager] Cannot begin QTE: UI references are missing. Assign the QTE_UI and QTE_Text fields in the Inspector.");
             onDone?.Invoke(false);
             return;
        }

        StopAllCoroutines();
        StartCoroutine(QTEFlow(key, duration, onDone));
    }

    IEnumerator QTEFlow(KeyCode key, float duration, Action<bool> onDone)
    {
        if (qteUI) qteUI.SetActive(true);
        if (qteText) qteText.text = $"PRESS {key}";

        bool success = false;
        float t = 0f;
        while (t < duration)
        {
            if (Pressed(key)) { success = true; break; }
            t += Time.deltaTime;
            yield return null;
        }

        if (qteUI) qteUI.SetActive(false);
        onDone?.Invoke(success);
    }

    // ... Input functions here ...
    bool Pressed(KeyCode key)
    {
        // ... (Input system code)
        #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // ... (Implementation for new input system)
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;

        switch (key)
        {
            case KeyCode.E:            return kb.eKey.wasPressedThisFrame;
            case KeyCode.F:            return kb.fKey.wasPressedThisFrame;
            case KeyCode.Q:            return kb.qKey.wasPressedThisFrame;
            case KeyCode.R:            return kb.rKey.wasPressedThisFrame;
            case KeyCode.Space:        return kb.spaceKey.wasPressedThisFrame;
            case KeyCode.LeftShift:    return kb.leftShiftKey.wasPressedThisFrame;
            case KeyCode.RightShift:   return kb.rightShiftKey.wasPressedThisFrame;
            case KeyCode.LeftControl:  return kb.leftCtrlKey.wasPressedThisFrame;
            case KeyCode.RightControl: return kb.rightCtrlKey.wasPressedThisFrame;
            case KeyCode.A:            return kb.aKey.wasPressedThisFrame;
            case KeyCode.D:            return kb.dKey.wasPressedThisFrame;
            case KeyCode.W:            return kb.wKey.wasPressedThisFrame;
            case KeyCode.S:            return kb.sKey.wasPressedThisFrame;
            case KeyCode.Escape:       return kb.escapeKey.wasPressedThisFrame;
            case KeyCode.Return:       return kb.enterKey.wasPressedThisFrame;
            default:
                var name = key.ToString().ToLowerInvariant();
                if (name.Length == 1)
                {
                    var c = name[0];
                    UnityEngine.InputSystem.Key k = UnityEngine.InputSystem.Key.None;
                    if (c >= 'a' && c <= 'z') k = UnityEngine.InputSystem.Key.A + (c - 'a');
                    else if (c >= '0' && c <= '9') k = UnityEngine.InputSystem.Key.Digit0 + (c - '0');
                    if (k != UnityEngine.InputSystem.Key.None)
                    {
                        var control = kb[k];
                        if (control != null) return control.wasPressedThisFrame;
                    }
                }
                return false;
        }
        #elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
        #else
        return false;
        #endif
    }
}