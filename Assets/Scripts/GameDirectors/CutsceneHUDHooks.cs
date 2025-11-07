using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

public class CutsceneHUDHooks : MonoBehaviour
{
    public float fadeOutDur = 0.2f;
    public float fadeInDur = 0.6f;
    public float blackoutHold = 1.0f;

    public void FadeOut() => PersistentHUD.Instance?.FadeToBlack(fadeOutDur);
    public void FadeIn() => PersistentHUD.Instance?.FadeFromBlack(fadeInDur);
}
