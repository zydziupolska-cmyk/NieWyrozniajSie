using UnityEngine;
using UnityEngine.AI;

// Bezmyslny bot: chodzi w losowe miejsca, staje, robi kanciaste obroty w miejscu,
// czasem podnosi lezacy przedmiot, nosi go i odklada gdzies indziej, siada na lawce,
// staje w kolejce, zaglada do auta albo gada w grupce. W czasie postoju gra na telefonie,
// drapie sie po glowie albo macha - szpieg musi umiec to wszystko nasladowac.
// Po strzale wpada w panike: rece w gore, bieg 2x szybciej, zwykle upuszcza to, co niesie.
[RequireComponent(typeof(NavMeshAgent))]
public class BotAI : MonoBehaviour
{
    public float wanderRadius = 15f;
    public float minIdleTime = 1.5f;
    public float maxIdleTime = 6f;
    [Tooltip("Szansa, ze po postoju bot pojdzie cos podniesc.")]
    public float fetchChance = 0.2f;
    [Tooltip("Szansa, ze po postoju bot pojdzie cos robic (lawka, kolejka, grupka, zagladanie).")]
    public float habitChance = 0.4f;

    enum Activity { Wander, Fetch, Carry, Habit }

    private NavMeshAgent agent;
    private ProceduralAnimator animator;
    private CrowdMember member;
    private float idleTimer;
    private float quirkTimer;
    private bool isIdle;
    private float panicTimer;

    private Activity activity = Activity.Wander;
    private Prop prop;
    private float activityTimer;
    private bool reachingNow;
    private ActivitySpot spot;
    private bool atSpot;
    private float chatToggle;
    private float idleEmoteTimer;

    public bool IsPanicking => panicTimer > 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        GameConfig.ConfigureBotAgent(agent);
        member = GetComponent<CrowdMember>();
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
        float dt = Time.deltaTime;

        if (panicTimer > 0f)
        {
            panicTimer -= dt;
            if (panicTimer <= 0f)
            {
                agent.speed = GameConfig.BotWalkSpeed;
                animator.panicking = false;
            }
        }

        if (activity == Activity.Habit && UpdateHabit(dt)) return;
        if (activity == Activity.Fetch && UpdateFetch(dt)) return;
        if (activity == Activity.Carry)
        {
            activityTimer -= dt;
            if (member.HeldProp == null) activity = Activity.Wander; // wypadlo z rak / ktos wyrwal
        }

        if (isIdle)
        {
            idleTimer -= dt;
            if (idleEmoteTimer > 0f)
            {
                idleEmoteTimer -= dt;
                if (idleEmoteTimer <= 0f) animator.emote = Emote.None;
            }
            else DoIdleQuirks();
            if (idleTimer <= 0f)
            {
                isIdle = false;
                DecideNext();
            }
        }
        else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (panicTimer > 0f) SetNewDestination(); // w panice nikt nie stoi
            else StartIdle();
        }
    }

    // Po postoju: odloz niesiona rzecz, idz cos podniesc albo po prostu dalej spaceruj
    void DecideNext()
    {
        animator.emote = Emote.None;
        idleEmoteTimer = 0f;
        if (activity == Activity.Carry && activityTimer <= 0f)
        {
            member.DropProp();
            activity = Activity.Wander;
        }
        else if (activity == Activity.Wander)
        {
            float r = Random.value;
            if (r < fetchChance && TryStartFetch()) return;
            if (r >= fetchChance && r < fetchChance + habitChance && TryStartHabit()) return;
        }
        SetNewDestination();
    }

    // --- Nawyki: lawka, kolejka, zagladanie, grupka ------------------------------------

    bool TryStartHabit()
    {
        var s = ActivitySpot.FindFree(transform.position, 25f);
        if (s == null) return false;
        GoToSpot(s);
        activity = Activity.Habit;
        return true;
    }

    void GoToSpot(ActivitySpot s)
    {
        if (spot != null && spot.occupant == member) spot.occupant = null;
        spot = s;
        spot.occupant = member;
        atSpot = false;
        activityTimer = 25f; // limit na dojscie
        animator.emote = Emote.None;
        animator.bodyOffset = Vector3.zero;
        agent.isStopped = false;
        agent.SetDestination(spot.standPoint);
    }

    // Zwraca true, gdy bot jest zajety nawykiem (reszta Update pomijana)
    bool UpdateHabit(float dt)
    {
        activityTimer -= dt;
        if (spot == null || spot.occupant != member || panicTimer > 0f)
        {
            EndHabit();
            return false;
        }

        if (!atSpot)
        {
            if (activityTimer <= 0f) { EndHabit(); return false; }
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f)
            {
                atSpot = true;
                agent.isStopped = true;
                agent.ResetPath();
                animator.bodyOffset = spot.bodyOffset;
                animator.emote = spot.Emote;
                chatToggle = Random.Range(1.5f, 4f);
                switch (spot.kind)
                {
                    case SpotKind.Bench: activityTimer = Random.Range(8f, 25f); break;
                    case SpotKind.Peek: activityTimer = Random.Range(3f, 7f); break;
                    case SpotKind.Chat: activityTimer = Random.Range(6f, 16f); break;
                    default: activityTimer = spot.nextInQueue == null ? Random.Range(3f, 6f) : 25f; break;
                }
            }
            return true;
        }

        transform.rotation = Quaternion.LookRotation(spot.facing);

        if (spot.kind == SpotKind.Queue && spot.nextInQueue != null && spot.nextInQueue.IsFree)
        {
            GoToSpot(spot.nextInQueue); // kolejka sie przesuwa
            return true;
        }
        if (spot.kind == SpotKind.Chat)
        {
            // Raz mowi (gestykuluje), raz slucha
            chatToggle -= dt;
            if (chatToggle <= 0f)
            {
                chatToggle = Random.Range(1.5f, 4f);
                animator.emote = animator.emote == Emote.Chat ? Emote.None : Emote.Chat;
            }
        }

        if (activityTimer <= 0f) { EndHabit(); return false; }
        return true;
    }

    void EndHabit()
    {
        if (spot != null && spot.occupant == member) spot.occupant = null;
        spot = null;
        atSpot = false;
        animator.emote = Emote.None;
        animator.bodyOffset = Vector3.zero;
        if (agent.isOnNavMesh) agent.isStopped = false;
        if (activity == Activity.Habit) activity = Activity.Wander;
        StartIdle();
    }

    bool TryStartFetch()
    {
        Prop best = null;
        float bestD = 12f;
        foreach (var p in Prop.All)
        {
            // Walizek-celow boty nie ruszaja (nie chca ich "przypadkiem" ukrasc)
            if (p == null || p.IsHeld || p.IsTarget || (p.ReservedBy != null && p.ReservedBy != member && !p.ReservedBy.IsDead)) continue;
            float d = Vector3.Distance(transform.position, p.transform.position) + Random.Range(0f, 4f);
            if (d < bestD) { bestD = d; best = p; }
        }
        if (best == null) return false;

        prop = best;
        prop.ReservedBy = member;
        activity = Activity.Fetch;
        activityTimer = 12f;
        reachingNow = false;
        GoNear(prop.transform.position);
        return true;
    }

    // Zwraca true, jesli bot jest zajety podnoszeniem (reszta Update pomijana)
    bool UpdateFetch(float dt)
    {
        activityTimer -= dt;
        if (prop == null || prop.IsHeld || panicTimer > 0f || activityTimer <= 0f)
        {
            AbortFetch();
            return false;
        }

        float dist = Vector3.Distance(transform.position, prop.transform.position);
        if (!reachingNow)
        {
            if (dist < 1.0f)
            {
                reachingNow = true;
                activityTimer = Mathf.Min(activityTimer, 3.5f); // na siegniecie max ~3 s
                agent.isStopped = true;
                agent.ResetPath();
            }
            else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                GoNear(prop.transform.position); // przedmiot sie przesunal
            }
            return true;
        }

        // Toporne siegniecie - bot celuje rekami w przedmiot, stojac jak slup
        Vector3 to = prop.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);

        if (member.TryGrabProp(prop))
        {
            agent.isStopped = false;
            activity = Activity.Carry;
            activityTimer = Random.Range(8f, 30f);
            prop = null;
            reachingNow = false;
            SetNewDestination();
        }
        return true;
    }

    void AbortFetch()
    {
        if (prop != null && prop.ReservedBy == member) prop.ReservedBy = null;
        prop = null;
        reachingNow = false;
        member.StopReaching();
        if (agent.isOnNavMesh) agent.isStopped = false;
        activity = member.HeldProp != null ? Activity.Carry : Activity.Wander;
        StartIdle();
    }

    void GoNear(Vector3 target)
    {
        Vector3 from = target - transform.position;
        from.y = 0f;
        Vector3 spot = target - from.normalized * 0.6f;
        if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        else AbortFetch();
    }

    void StartIdle()
    {
        isIdle = true;
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
        quirkTimer = Random.Range(0.4f, 1.5f);

        // Drobne czynnosci w czasie postoju
        float r = Random.value;
        if (r < 0.15f) { animator.emote = Emote.Phone; idleEmoteTimer = idleTimer; }
        else if (r < 0.22f) { animator.emote = Emote.ScratchHead; idleEmoteTimer = Random.Range(1.5f, 3f); }
        else if (r < 0.27f) { animator.emote = Emote.Wave; idleEmoteTimer = Random.Range(1.2f, 2.2f); }
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
        if (activity == Activity.Fetch) AbortFetch();
        if (activity == Activity.Habit) EndHabit();
        animator.emote = Emote.None;
        idleEmoteTimer = 0f;
        // Wiekszosc botow w panice rzuca wszystko i ucieka
        if (member.HeldProp != null && Random.value < 0.7f)
        {
            member.DropProp();
            activity = Activity.Wander;
        }
        agent.isStopped = false;
        agent.speed = GameConfig.BotPanicSpeed; // zawsze od bazowej - bez kumulowania
        panicTimer = GameConfig.PanicDuration * Random.Range(0.85f, 1.15f);
        animator.panicking = true;
        isIdle = false;
        SetNewDestination();
    }
}
