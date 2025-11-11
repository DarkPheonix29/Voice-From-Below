using UnityEngine;
using System;
using System.Collections;

public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    [Header("UI")]
    public GameObject qteUI;       // assign HUD prompt (disable by default)
    public TMPro.TextMeshProUGUI qteText; // optional: show key text

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (qteUI) qteUI.SetActive(false);
    }

    public void Begin(KeyCode key, float duration, Action<bool> onDone)
    {
        StopAllCoroutines();
        StartCoroutine(QTEFlow(key, duration, onDone));
    }

    IEnumerator QTEFlow(KeyCode key, float duration, Action<bool> onDone)
    {
        if (qteUI) qteUI.SetActive(true);
        if (qteText) qteText.text = $"PRESS {key}!";

        bool success = false;
        float t = 0f;
        while (t < duration)
        {
            if (Input.GetKeyDown(key)) { success = true; break; }
            t += Time.deltaTime;
            yield return null;
        }

        if (qteUI) qteUI.SetActive(false);
        onDone?.Invoke(success);
    }
}
