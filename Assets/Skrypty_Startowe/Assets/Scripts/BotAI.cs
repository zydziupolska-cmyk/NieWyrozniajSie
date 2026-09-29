using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotAI : MonoBehaviour
{
    public float wanderRadius = 15f;
    public float minIdleTime = 2f;
    public float maxIdleTime = 7f;
    public float panicDuration = 6f;
    public float panicSpeedMultiplier = 2f;

    private NavMeshAgent agent;
    private ProceduralAnimator animator;
    private float timer;
    private bool isIdle;
    private float baseSpeed;
    private float panicTimer;

    public bool IsDead { get; private set; }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        baseSpeed = agent.speed;
        animator = GetComponent<ProceduralAnimator>();
        if (animator == null) animator = gameObject.AddComponent<ProceduralAnimator>();
        SetNewDestination();
    }

    void Update()
    {
        if (IsDead) return;

        if (panicTimer > 0f)
        {
            panicTimer -= Time.deltaTime;
            if (panicTimer <= 0f) agent.speed = baseSpeed;
        }

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
                    if (panicTimer > 0f)
                    {
                        // W panice nikt nie stoi - od razu biegnie dalej
                        SetNewDestination();
                    }
                    else
                    {
                        isIdle = true;
                        timer = Random.Range(minIdleTime, maxIdleTime);
                    }
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
        if (IsDead || agent == null) return;
        // Predkosc liczona od bazowej - kolejne strzaly nie przyspieszaja botow w nieskonczonosc
        agent.speed = baseSpeed * panicSpeedMultiplier;
        panicTimer = panicDuration;
        isIdle = false;
        SetNewDestination();
    }

    public void Kill(Vector3 hitDirection, Rigidbody hitBody, Vector3 hitPoint)
    {
        if (IsDead) return;
        IsDead = true;
        if (animator != null) animator.EnableRagdoll(hitDirection, hitBody, hitPoint);
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }
        enabled = false;
    }
}
