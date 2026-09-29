using UnityEngine;
using UnityEngine.AI;

// Szpieg sterowany przez AI (tryb gry "Snajper").
// Udaje bota i powoli dryfuje w strone walizki. Zeby ja zlapac, musi na chwile
// wyjsc z roli (ludzkie siegniecie), potem niesie ja do furgonetki jak bot niosacy bagaz.
// Czasem popelnia bledy: krotka plynna "ludzka" przebiezka albo spozniona reakcja na panike.
[RequireComponent(typeof(NavMeshAgent))]
public class AISpy : MonoBehaviour
{
    enum State { Blend, Grab, Carry, Slip }

    NavMeshAgent agent;
    ProceduralAnimator anim;
    CrowdMember member;

    State state = State.Blend;
    Suitcase target;
    float nextTheftTime;
    float nextSlipTime;
    float slipTimer;
    float grabTimer;
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
            case State.Blend: UpdateWalking(dt, false); break;
            case State.Carry: UpdateWalking(dt, true); break;
            case State.Grab: UpdateGrab(dt); break;
            case State.Slip: UpdateSlip(dt); break;
        }
    }

    // --- Chodzenie jak bot (z walizka albo bez) ---------------------------------

    void UpdateWalking(float dt, bool carrying)
    {
        anim.roboticMovement = true;

        if (carrying && member.HeldProp == null)
        {
            // Walizka wypadla z rak (albo zostala dostarczona) - wracamy do udawania
            state = State.Blend;
            if (target != null && target.IsStolen) target = null;
            nextTheftTime = Time.time + Random.Range(4f, 10f);
            carrying = false;
        }

        if (!carrying)
        {
            if ((target == null || target.IsStolen || target.IsHeld) && Time.time >= nextTheftTime)
                target = PickSuitcase();

            if (target != null && panicTimer <= 0f && !target.IsHeld &&
                Vector3.Distance(transform.position, target.transform.position) < 1.1f)
            {
                BeginGrab();
                return;
            }

            if (panicTimer <= 0f && Time.time >= nextSlipTime)
            {
                BeginSlip();
                return;
            }
        }

        if (isIdle)
        {
            idleTimer -= dt;
            DoIdleQuirks(dt);
            if (idleTimer <= 0f) { isIdle = false; NextLeg(carrying); }
        }
        else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            bool closeToGoal = Vector3.Distance(transform.position, Goal(carrying)) < 6f;
            if (panicTimer > 0f || (closeToGoal && HasGoal(carrying))) NextLeg(carrying);
            else StartIdle(carrying);
        }
    }

    bool HasGoal(bool carrying) => carrying ? ExtractionZone.Nearest(transform.position) != null : target != null;

    Vector3 Goal(bool carrying)
    {
        if (carrying)
        {
            var z = ExtractionZone.Nearest(transform.position);
            return z != null ? z.transform.position : transform.position;
        }
        return target != null ? target.transform.position : transform.position;
    }

    // Kolejny odcinek marszu: jak bot, ale z lekkim ciazeniem do celu
    void NextLeg(bool carrying)
    {
        Vector3 pos = transform.position;
        if (HasGoal(carrying) && panicTimer <= 0f)
        {
            Vector3 goal = Goal(carrying);
            Vector3 to = goal - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 6f)
            {
                // Ostatnie metry - prosto do celu (przy walizce stajemy tuz obok)
                Vector3 spot = carrying ? goal : goal - to.normalized * 0.6f;
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

    void StartIdle(bool carrying = false)
    {
        isIdle = true;
        // Z walizka postoje sa krotsze - ale nie zerowe, bo to wygladaloby podejrzanie
        idleTimer = carrying ? Random.Range(0.6f, 2f) : Random.Range(1f, 4f);
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
            if (s == null || s.IsStolen || s.IsHeld) continue;
            float score = Vector3.Distance(transform.position, s.transform.position) + Random.Range(0f, 25f);
            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    // --- Lapanie walizki - tu szpieg sie odslania --------------------------------

    void BeginGrab()
    {
        state = State.Grab;
        isIdle = false;
        grabTimer = 0f;
        agent.isStopped = true;
        agent.ResetPath();
    }

    void UpdateGrab(float dt)
    {
        if (target == null || target.IsStolen || target.IsHeld || panicTimer > 0f)
        {
            EndGrab(false);
            return;
        }

        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 8f);

        anim.roboticMovement = false; // czlowiek siega plynnie - to widac
        if (member.TryGrabProp(target.Prop))
        {
            EndGrab(true);
            return;
        }

        grabTimer += dt;
        if (grabTimer > 4f)
        {
            EndGrab(false);
            nextTheftTime = Time.time + Random.Range(3f, 6f);
        }
    }

    void EndGrab(bool success)
    {
        member.StopReaching();
        anim.roboticMovement = true;
        agent.isStopped = false;
        state = success ? State.Carry : State.Blend;
        if (!success) nextTheftTime = Mathf.Max(nextTheftTime, Time.time + Random.Range(5f, 12f));
        NextLeg(success);
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
        if (state == State.Grab) EndGrab(false);
        if (state == State.Slip) EndSlip();
        // Czasem szpieg nie chce puscic walizki - jak boty zwykle rzucaja wszystko, to jest wpadka
        if (member.HeldProp != null && Random.value < 0.5f)
        {
            member.DropProp();
            state = State.Blend;
        }
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
