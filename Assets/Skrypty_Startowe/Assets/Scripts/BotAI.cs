using UnityEngine;
using UnityEngine.AI;

// Bezmyslny bot: chodzi w losowe miejsca, staje, robi kanciaste obroty w miejscu,
// a po strzale wpada w panike (biegnie 2x szybciej z rekami w gorze).
[RequireComponent(typeof(NavMeshAgent))]
public class BotAI : MonoBehaviour
{
    public float wanderRadius = 15f;
    public float minIdleTime = 1.5f;
    public float maxIdleTime = 6f;

    private NavMeshAgent agent;
    private ProceduralAnimator animator;
    private float idleTimer;
    private float quirkTimer;
    private bool isIdle;
    private float panicTimer;

    public bool IsPanicking => panicTimer > 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        GameConfig.ConfigureBotAgent(agent);
        animator = GetComponent<ProceduralAnimator>();
        if (animator == null) animator = gameObject.AddComponent<ProceduralAnimator>();
        animator.roboticMovement = true;

        // Czesc botow startuje stojac, zeby tlum nie ruszal jak na komende
        if (Random.value < 0.4f) StartIdle();
        else SetNewDestination();
    }

    void Update()
    {
        if (!agent.isOnNavMesh) return;

        if (panicTimer > 0f)
        {
            panicTimer -= Time.deltaTime;
            if (panicTimer <= 0f)
            {
                agent.speed = GameConfig.BotWalkSpeed;
                animator.panicking = false;
            }
        }

        if (isIdle)
        {
            idleTimer -= Time.deltaTime;
            DoIdleQuirks();
            if (idleTimer <= 0f)
            {
                isIdle = false;
                SetNewDestination();
            }
        }
        else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (panicTimer > 0f) SetNewDestination(); // w panice nikt nie stoi
            else StartIdle();
        }
    }

    void StartIdle()
    {
        isIdle = true;
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
        quirkTimer = Random.Range(0.4f, 1.5f);
    }

    // "Glitche" botow: nagle obroty o 90 stopni w miejscu
    void DoIdleQuirks()
    {
        quirkTimer -= Time.deltaTime;
        if (quirkTimer > 0f) return;
        quirkTimer = Random.Range(0.6f, 2f);
        if (Random.value < 0.5f)
        {
            float turn = Random.value < 0.5f ? 90f : -90f;
            if (Random.value < 0.2f) turn *= 2f;
            transform.Rotate(0f, turn, 0f);
        }
    }

    void SetNewDestination()
    {
        if (GameConfig.RandomNavPoint(transform.position, wanderRadius, out Vector3 p))
            agent.SetDestination(p);
    }

    // Wywolywane gdy snajper spudluje albo zabije cywila
    public void TriggerPanic()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        agent.speed = GameConfig.BotPanicSpeed; // zawsze od bazowej - bez kumulowania
        panicTimer = GameConfig.PanicDuration * Random.Range(0.85f, 1.15f);
        animator.panicking = true;
        isIdle = false;
        SetNewDestination();
    }
}
