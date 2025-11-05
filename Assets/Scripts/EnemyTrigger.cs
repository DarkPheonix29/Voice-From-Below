using UnityEngine;
using UnityEngine.AI;

public class EnemyTrigger : MonoBehaviour
{
    [Header("Refs")]
    public Animator enemyAnimator;
    public NavMeshAgent agent;
    public Transform player;

    [Header("Turn Settings")]
    public float turnLerpBeforeScream = 12f; // hoe snel hij richt vóór scream (Turn/Scream)

    private bool chasing = false;
    private static readonly int HashPlayerDetected = Animator.StringToHash("PlayerDetected");

    void OnTriggerEnter(Collider other)
    {
        if (!chasing && other.CompareTag("Player"))
        {
            chasing = true;
            enemyAnimator.SetTrigger(HashPlayerDetected); // Idle -> Turn -> Scream -> Run
        }
    }

    void Update()
    {
        if (!chasing || player == null || agent == null) return;
        if (!agent.isOnNavMesh) return;

        var st  = enemyAnimator.GetCurrentAnimatorStateInfo(0);
        var nxt = enemyAnimator.GetNextAnimatorStateInfo(0);
        bool inRun = st.IsTag("Run") || nxt.IsTag("Run");

        if (inRun)
        {
            // Tijdens Run: agent navigeert én mag zelf draaien
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            // Tijdens Turn/Scream: NIET lopen, WEL naar speler kijken
            agent.isStopped = true;
            agent.updateRotation = false; // we draaien handmatig

            Vector3 to = player.position - agent.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(to);
                agent.transform.rotation = Quaternion.Slerp(
                    agent.transform.rotation,
                    target,
                    turnLerpBeforeScream * Time.deltaTime
                );
            }
        }
    }

    // (Optioneel) roep deze via een Animation Event op frame 0 van je Scream-clip
    public void AlignToPlayer()
    {
        if (!player || !agent) return;
        Vector3 to = player.position - agent.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
            agent.transform.rotation = Quaternion.LookRotation(to);
    }
}
