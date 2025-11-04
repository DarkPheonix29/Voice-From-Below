using UnityEngine;

public class FadeInOnStart : MonoBehaviour
{
    public float duration = 1.2f;
    void Start()
    {
        if (PersistentHUD.Instance) PersistentHUD.Instance.FadeFromBlack(duration);
    }
}
