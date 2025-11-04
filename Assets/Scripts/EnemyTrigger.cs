using UnityEngine;

public class ZombieTrigger : MonoBehaviour
{
    [Header("References")]
    public Animator zombieAnimator;   // Animator op je Enemy
    public Transform zombie;          // Root transform van je Enemy
    public Transform player;          // Player (tag "Player")

    [Header("Chase Settings")]
    public float chaseSpeed = 2.2f;   // loopsnelheid
    public float turnLerp = 10f;      // draaivlotheid

    private bool chasing;
    private static readonly int HashPlayerDetected = Animator.StringToHash("PlayerDetected");

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !chasing)
        {
            chasing = true;
            // Start animatie-flow: Idle -> Turn -> (Scream?) -> Run
            zombieAnimator.ResetTrigger(HashPlayerDetected);
            zombieAnimator.SetTrigger(HashPlayerDetected);
        }
    }

    private void Update()
    {
        if (!chasing || zombie == null || player == null) return;

        // Altijd wel naar speler kijken (mag ook tijdens Turn/Scream)
        Vector3 to = player.position - zombie.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.Slerp(
                zombie.rotation,
                Quaternion.LookRotation(to),
                turnLerp * Time.deltaTime
            );
            zombie.rotation = targetRot;
        }

        // >>> Alleen bewegen als de (huidige of volgende) state de Tag "Run" heeft <<<
        var st = zombieAnimator.GetCurrentAnimatorStateInfo(0);
        var next = zombieAnimator.GetNextAnimatorStateInfo(0);
        bool isInOrGoingToRun = st.IsTag("Run") || next.IsTag("Run");

        if (isInOrGoingToRun)
        {
            zombie.position += zombie.forward * chaseSpeed * Time.deltaTime;
        }
        // Niet in Run? -> NIET verplaatsen.
    }
}
