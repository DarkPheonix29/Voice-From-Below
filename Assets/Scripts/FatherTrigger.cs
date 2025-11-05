using UnityEngine;

public class FatherTrigger : MonoBehaviour
{
    [Header("Refs")]
    public Animator fatherAnimator;   // Animator op de Father
    public Transform player;          // optioneel, alleen als je iets met richting zou willen

    private bool triggered = false;
    private static readonly int HashPlayerDetected = Animator.StringToHash("PlayerDetected");

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        // start de flow: Idle_Laying -> Seizure -> Laying_Breathless
        fatherAnimator.ResetTrigger(HashPlayerDetected);
        fatherAnimator.SetTrigger(HashPlayerDetected);
    }
}
