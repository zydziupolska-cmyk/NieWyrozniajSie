using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotAI : MonoBehaviour
{
    public float wanderRadius = 15f;
    public float minIdleTime = 2f;
    public float maxIdleTime = 7f;

    private NavMeshAgent agent;
    private float timer;
    private bool isIdle;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (GetComponent<ProceduralAnimator>() == null)
        {
            gameObject.AddComponent<ProceduralAnimator>();
        }
        SetNewDestination();
    }

    void Update()
    {
        if (isIdle)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                isIdle = false;
                SetNewDestination();
            }
        }
        else
        {
            // If bot reached destination
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                {
                    isIdle = true;
                    timer = Random.Range(minIdleTime, maxIdleTime);
                }
            }
        }
    }

    void SetNewDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    // Call this when sniper misses and hits a civilian
    public void TriggerPanic()
    {
        // Simple panic logic: run fast in random direction
        agent.speed *= 2f;
        SetNewDestination();
    }
}
