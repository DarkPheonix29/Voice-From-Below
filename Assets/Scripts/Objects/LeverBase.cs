using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class LeverBase : MonoBehaviour, IInteractable
{
    [Header("Identity")]
    [Tooltip("Unique ID for THIS lever base (e.g. L2_MainLever, L3_ElevatorLever). Used to remember installation.")]
    public string leverId = "L2_MainLever";

    [Header("Requirements")]
    [Tooltip("Inventory item required to install the handle. Leave empty if no item is required.")]
    public string requiredItemId = "LeverHandle";

    [Tooltip("If true, the lever starts installed (for prefabs that visually include the handle).")]
    public bool startInstalled = false;

    [Tooltip("If true, after installation it immediately pulls and transfers (single press). NOTE: If ActTwoDirector is present, the director will override timing to play VO first.")]
    public bool installThenPull = false;

    [Header("Socket")]
    public Transform socket;

    [Header("Installed Visual")]
    public GameObject leverInstalledPrefab;
    public Vector3 installedLocalPosOffset;
    public Vector3 installedLocalEulerOffset;

    [Header("UI (optional)")]
    public Canvas promptCanvas;
    public TextMeshProUGUI promptLabel;
    public string promptNeedLever = "You need a lever.";
    public string promptInstall   = "Press E to Install Lever";
    public string promptPull      = "Press E to Pull";

    [Header("Transfer")]
    public string destinationScene = "Level3";
    public string destinationSpawnId = "FromLevel2_Lever";
    public float fadeOutDuration = 0.8f;
    public float fadeInDuration  = 1.2f;

    // Runtime
    bool installed = false;
    Transform installedLever;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;
    }

    void Start()
    {
        if (promptCanvas) promptCanvas.enabled = false;

        // Restore installed state from SaveFlags if present
        if (!installed && SaveFlags.Instance && SaveFlags.Instance.Has(leverId))
        {
            installed = true;
            SpawnInstalledVisual();
        }
        else if (!installed && startInstalled)
        {
            installed = true;
            SpawnInstalledVisual();
            if (SaveFlags.Instance) SaveFlags.Instance.Set(leverId);
        }

        UpdatePrompt();
    }

    void UpdatePrompt()
    {
        if (!promptLabel) return;
        promptLabel.text = installed ? promptPull : promptInstall;
    }

    public void SetHighlighted(bool on)
    {
        if (promptCanvas) promptCanvas.enabled = on;
        UpdatePrompt();
    }

    public void Interact()
    {
        if (!installed)
        {
            if (!TryInstall()) return;

            // If ActTwoDirector is present, let it handle VO/elevator/transfer timing.
            if (ActTwoDirector.Instance != null)
            {
                ActTwoDirector.Instance.OnLeverInstalledThenTransfer(this);
                return;
            }

            // Fallback: original behavior
            if (!installThenPull) return; // second press to pull unless one-press desired
        }

        PullAndTransfer();
    }

    bool TryInstall()
    {
        string needId = string.IsNullOrWhiteSpace(requiredItemId) ? null : requiredItemId.Trim();

        if (needId != null)
        {
            if (Inventory.Instance == null || !Inventory.Instance.Has(needId))
            {
                PersistentHUD.Instance?.ShowSubtitle(promptNeedLever, 1.2f, false);

                // NEW: Notify ActTwoDirector for a VO line when lever is missing
                if (ActTwoDirector.Instance != null)
                    ActTwoDirector.Instance.OnLeverMissingAttempt();

                return false;
            }

            // If you want to consume the handle, uncomment:
            // Inventory.Instance.Remove(needId);
        }

        if (!socket)
        {
            Debug.LogError("[LeverBase] No socket assigned.");
            return false;
        }

        installed = true;
        SpawnInstalledVisual();

        // Remember this lever is installed for this playthrough (session; persist on Commit)
        if (SaveFlags.Instance) SaveFlags.Instance.Set(leverId);

        PersistentHUD.Instance?.ShowSubtitle("Lever installed.", 1.0f, false);
        UpdatePrompt();
        return true;
    }

    void SpawnInstalledVisual()
    {
        if (!socket || !leverInstalledPrefab) return;
        var go = Instantiate(leverInstalledPrefab, socket, false);
        go.SetActive(true);
        installedLever = go.transform;
        installedLever.localPosition = installedLocalPosOffset;
        installedLever.localRotation = Quaternion.Euler(installedLocalEulerOffset);
    }

    // Made public so ActTwoDirector can call this after VO & elevator SFX.
    public void PullAndTransfer()
    {
        // Optional: play a tiny lever pull anim here using installedLever

        System.Action before = null;
        if (!string.IsNullOrEmpty(destinationSpawnId) && GameFlow.Instance != null)
            before = () => GameFlow.Instance.QueueSpawn(destinationSpawnId);

        if (PersistentHUD.Instance != null)
        {
            PersistentHUD.Instance.LoadSceneWithFade(destinationScene, fadeOutDuration, fadeInDuration, before);
        }
        else
        {
            // Fallback: load without fade
            before?.Invoke();
            if (!string.IsNullOrEmpty(destinationScene))
                SceneManager.LoadScene(destinationScene);
            else
                Debug.LogError("[LeverBase] No destinationScene set.");
        }

        if (promptCanvas) promptCanvas.enabled = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!socket) return;
        Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.35f);
        Gizmos.DrawSphere(socket.position, 0.05f);
    }
#endif
}
