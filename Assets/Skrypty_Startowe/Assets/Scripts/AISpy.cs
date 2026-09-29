using UnityEngine;
using UnityEngine.AI;

// Szpieg sterowany przez AI (tryb gry "Snajper").
// Udaje bota, powoli dryfuje w strone walizek, kradnie je (wtedy rusza sie po ludzku)
// i czasem popelnia bledy - krotkie, plynne "ludzkie" przebiezki albo spozniona reakcja na panike.
[RequireComponent(typeof(NavMeshAgent))]
public class AISpy : MonoBehaviour
{
    enum State { Blend, Steal, Slip }

    NavMeshAgent agent;
    ProceduralAnimator anim;
    CrowdMember member;

    State state = State.Blend;
    Suitcase target;
    float nextTheftTime;
    float nextSlipTime;
    float slipTimer;
    bool isIdle;
    float idleTimer;
    float quirkTimer;
    float panicReactTimer = -1f;
    float panicTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        GameConfig.ConfigureBotAgent(agent);
        member = GetComponent<CrowdMember>();
        anim = GetComponent<ProceduralAnimator>();
        anim.roboticMovement = true;

        nextTheftTime = Time.time + Random.Range(15f, 35f);
        nextSlipTime = Time.time + Random.Range(12f, 25f);
        StartIdle();
    }

    void Update()
    {
        if (!agent.isOnNavMesh) return;
        float dt = Time.deltaTime;

        // Spozniona reakcja na panike - klasyczny blad czlowieka
        if (panicReactTimer > 0f)
        {
            panicReactTimer -= dt;
            if (panicReactTimer <= 0f) StartPanic();
        }
        if (panicTimer > 0f)
        {
            panicTimer -= dt;
            if (panicTimer <= 0f) StopPanic();
        }

        switch (state)
        {
            case State.Blend: UpdateBlend(dt); break;
            case State.Steal: UpdateSteal(dt); break;
            case State.Slip: UpdateSlip(dt); break;
        }
    }

    // --- Udawanie bota -------------------------------------------------------

    void UpdateBlend(float dt)
    {
        anim.roboticMovement = true;
        anim.reaching = false;

        if ((target == null || target.IsStolen) && Time.time >= nextTheftTime)
            target = PickSuitcase();

        if (target != null && panicTimer <= 0f &&
            Vector3.Distance(transform.position, target.transform.position) < GameConfig.StealDistance - 0.2f)
        {
            BeginSteal();
            return;
        }

        if (panicTimer <= 0f && Time.time >= nextSlipTime)
        {
            BeginSlip();
            return;
        }

        if (isIdle)
        {
            idleTimer -= dt;
            DoIdleQuirks(dt);
            if (idleTimer <= 0f) { isIdle = false; NextLeg(); }
        }
        else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (panicTimer > 0f) NextLeg();
            else if (target != null && Vector3.Distance(transform.position, target.transform.position) < 3f) NextLeg();
            else StartIdle();
        }
    }

    // Kolejny odcinek marszu: jak bot, ale z lekkim ciazeniem w strone walizki
    void NextLeg()
    {
        Vector3 pos = transform.position;
        if (target != null && panicTimer <= 0f)
        {
            Vector3 to = target.transform.position - pos;
            float dist = to.magnitude;
            if (dist < 6f)
            {
                // Ostatnie metry - prosto do walizki (stajemy obok niej)
                Vector3 spot = target.transform.position - to.normalized * 0.9f;
                if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                    return;
                }
            }
            Vector3 dir = to / Mathf.Max(dist, 0.01f);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 p = pos + dir * Mathf.Min(dist, Random.Range(4f, 9f)) + side * Random.Range(-4f, 4f);
            if (GameConfig.RandomNavPoint(p, 1.5f, out Vector3 dest))
            {
                agent.SetDestination(dest);
                return;
            }
        }
        if (GameConfig.RandomNavPoint(pos, 15f, out Vector3 w)) agent.SetDestination(w);
    }

    void StartIdle()
    {
        isIdle = true;
        idleTimer = Random.Range(1f, 4f);
        quirkTimer = Random.Range(0.4f, 1.5f);
    }

    void DoIdleQuirks(float dt)
    {
        quirkTimer -= dt;
        if (quirkTimer > 0f) return;
        quirkTimer = Random.Range(0.6f, 2f);
        if (Random.value < 0.5f) transform.Rotate(0f, Random.value < 0.5f ? 90f : -90f, 0f);
    }

    Suitcase PickSuitcase()
    {
        Suitcase best = null;
        float bestScore = float.MaxValue;
        foreach (var s in Suitcase.All)
        {
            if (s == null || s.IsStolen) continue;
            float score = Vector3.Distance(transform.position, s.transform.position) + Random.Range(0f, 25f);
            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    // --- Kradziez - tu szpieg sie odslania ------------------------------------

    void BeginSteal()
    {
        state = State.Steal;
        isIdle = false;
        agent.isStopped = true;
        agent.ResetPath();
    }

    void UpdateSteal(float dt)
    {
        if (target == null || target.IsStolen || panicTimer > 0f)
        {
            EndSteal();
            return;
        }

        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 8f);

        anim.roboticMovement = false;
        anim.reaching = true;
        target.AddProgress(dt / GameConfig.AIStealTime, member);
        if (target == null || target.IsStolen) EndSteal();
    }

    void EndSteal()
    {
        anim.reaching = false;
        anim.roboticMovement = true;
        agent.isStopped = false;
        target = null;
        nextTheftTime = Time.time + Random.Range(12f, 28f);
        state = State.Blend;
        NextLeg();
    }

    // --- Wpadka: krotki plynny, szybki "ludzki" marsz ---------------------------

    void BeginSlip()
    {
        state = State.Slip;
        isIdle = false;
        slipTimer = Random.Range(1.2f, 2.5f);
        agent.speed = GameConfig.SpyHumanSpeed * 0.9f;
        agent.angularSpeed = 240f;
        agent.acceleration = 10f;
        anim.roboticMovement = false;
        if (GameConfig.RandomNavPoint(transform.position + transform.forward * 8f, 4f, out Vector3 p))
            agent.SetDestination(p);
    }

    void UpdateSlip(float dt)
    {
        slipTimer -= dt;
        if (slipTimer <= 0f) EndSlip();
    }

    void EndSlip()
    {
        GameConfig.ConfigureBotAgent(agent);
        if (panicTimer > 0f) agent.speed = GameConfig.BotPanicSpeed;
        anim.roboticMovement = true;
        nextSlipTime = Time.time + Random.Range(15f, 30f);
        state = State.Blend;
    }

    // --- Panika ------------------------------------------------------------------

    public void OnPanic()
    {
        // Czlowiek potrzebuje chwili, zeby zauwazyc, ze tlum wpadl w panike
        panicReactTimer = Random.Range(0.35f, 1.2f);
    }

    void StartPanic()
    {
        if (state == State.Steal) EndSteal();
        if (state == State.Slip) EndSlip();
        panicTimer = GameConfig.PanicDuration * Random.Range(0.8f, 1.1f);
        agent.speed = GameConfig.BotPanicSpeed;
        anim.panicking = true;
        isIdle = false;
        if (GameConfig.RandomNavPoint(transform.position, 15f, out Vector3 p)) agent.SetDestination(p);
    }

    void StopPanic()
    {
        agent.speed = GameConfig.BotWalkSpeed;
        anim.panicking = false;
    }
}
