using System.Collections.Generic;
using UnityEngine;

// Snajper sterowany przez AI (tryb gry "Szpieg").
// Przeczesuje tlum laserem, obserwuje pojedyncze postacie i buduje "podejrzliwosc"
// na podstawie tego, co widac: plynny ruch, siegniecie po walizke, spokoj w panice...
// Gdy podejrzliwosc przekroczy prog - celuje i strzela. Moze sie pomylic.
public class AISniper : MonoBehaviour
{
    public int ammo = GameConfig.AISniperAmmo;
    public float shootThreshold = 1f;
    public float reactionTime = 0.9f;
    public float aimError = 0.22f;
    public float reloadTime = 2.5f;
    public float sweepSpeed = 22f;

    public CrowdMember CurrentTarget { get; private set; }
    public bool IsAiming => state == State.Aim;

    enum State { Scan, Aim, Reload }
    State state = State.Scan;

    readonly Dictionary<CrowdMember, float> suspicion = new Dictionary<CrowdMember, float>();
    float observeTimer;
    float stateTimer;
    Vector3 gazePoint;
    LineRenderer laser;
    Transform dot;
    Color laserColor = new Color(1f, 0.1f, 0.1f);

    void Start()
    {
        gazePoint = Vector3.zero;

        var laserGo = new GameObject("SniperLaser");
        laserGo.transform.SetParent(transform, false);
        laser = laserGo.AddComponent<LineRenderer>();
        laser.sharedMaterial = LowPolyFactory.GetUnlitMaterial(laserColor);
        laser.positionCount = 2;
        laser.startWidth = 0.03f;
        laser.endWidth = 0.05f;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        dot = LowPolyFactory.Blob("LaserDot", transform, Vector3.zero, 0.07f, 0f, laserColor, true, 6).transform;

        // Blysk lunety - szpiedzy widza, skad patrzy snajper
        LowPolyFactory.Blob("ScopeGlint", transform, Vector3.zero, 0.25f, 0f, new Color(1f, 1f, 0.8f), true, 6);
    }

    public float GetSuspicion(CrowdMember m)
    {
        return m != null && suspicion.TryGetValue(m, out float s) ? s : 0f;
    }

    // Walizka znikla - snajper wie, ze ktos stal obok
    public void OnSuitcaseStolen(Vector3 position)
    {
        foreach (var m in CrowdMember.All)
        {
            if (m.IsDead) continue;
            if (Vector3.Distance(m.transform.position, position) < 5f) Add(m, 0.5f);
        }
        observeTimer = 0f;
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
        {
            laser.enabled = false;
            dot.gameObject.SetActive(false);
            return;
        }

        float dt = Time.deltaTime;
        Observe(dt);

        switch (state)
        {
            case State.Scan:
                observeTimer -= dt;
                if (CurrentTarget == null || CurrentTarget.IsDead || observeTimer <= 0f) PickTarget();
                if (ammo > 0 && CurrentTarget != null && GetSuspicion(CurrentTarget) >= shootThreshold &&
                    Vector3.Distance(gazePoint, CurrentTarget.TorsoPosition) < 0.6f)
                {
                    state = State.Aim;
                    stateTimer = reactionTime;
                }
                break;

            case State.Aim:
                if (CurrentTarget == null || CurrentTarget.IsDead) { state = State.Scan; break; }
                stateTimer -= dt;
                if (stateTimer <= 0f) Shoot();
                break;

            case State.Reload:
                stateTimer -= dt;
                if (stateTimer <= 0f) { state = State.Scan; PickTarget(); }
                break;
        }

        // Laser plynnie przesuwa sie po tlumie
        Vector3 wanted = CurrentTarget != null ? CurrentTarget.TorsoPosition : gazePoint;
        float speed = state == State.Aim ? sweepSpeed * 3f : sweepSpeed;
        gazePoint = Vector3.MoveTowards(gazePoint, wanted, speed * dt);

        bool blink = state == State.Aim && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f;
        laser.enabled = state != State.Reload;
        laser.SetPosition(0, transform.position);
        laser.SetPosition(1, gazePoint);
        laser.startWidth = state == State.Aim ? 0.05f : 0.03f;
        dot.gameObject.SetActive(state != State.Reload && !blink);
        dot.position = gazePoint + (transform.position - gazePoint).normalized * 0.3f;
    }

    // Zbieranie dowodow: obserwowany cel w pelni, reszta tlumu "katem oka"
    void Observe(float dt)
    {
        bool panic = CrowdMember.PanicActive;
        foreach (var m in CrowdMember.All)
        {
            if (m == null || m.IsDead) continue;
            float weight = m == CurrentTarget ? 1f : 0.22f;
            float s = GetSuspicion(m) + Evidence(m, panic) * weight * dt - 0.04f * dt;
            suspicion[m] = Mathf.Clamp(s, 0f, 2f);
        }
    }

    float Evidence(CrowdMember m, bool panic)
    {
        ProceduralAnimator a = m.Animator;
        float speed = a.CurrentSpeed;
        float e = 0f;

        if (!a.roboticMovement && speed > 0.3f) e += 1.3f;        // plynny, ludzki ruch
        if (a.reaching || a.IsHolding) e += 2.5f;                 // siega po walizke albo ja trzyma
        if (panic)
        {
            if (!a.panicking) e += 0.9f;                           // spokojny, gdy wszyscy wariuja
        }
        else
        {
            if (a.panicking) e += 1.2f;                            // panikuje bez powodu
            if (speed > GameConfig.BotWalkSpeed * 1.35f) e += 0.6f; // za szybko jak na bota
        }

        // Szum - snajper jest tylko czlowiekiem i czasem widzi cos, czego nie ma
        e += Mathf.PerlinNoise(m.NoiseSeed, Time.time * 0.25f) * 0.35f - 0.12f;
        return e;
    }

    void Add(CrowdMember m, float amount)
    {
        suspicion[m] = Mathf.Clamp(GetSuspicion(m) + amount, 0f, 2f);
    }

    void PickTarget()
    {
        observeTimer = Random.Range(1.5f, 3.5f);
        var alive = new List<CrowdMember>();
        foreach (var m in CrowdMember.All) if (m != null && !m.IsDead) alive.Add(m);
        if (alive.Count == 0) { CurrentTarget = null; return; }

        float r = Random.value;
        if (r < 0.45f)
        {
            // Najbardziej podejrzany (inny niz obecny cel, chyba ze tylko on jest podejrzany)
            CrowdMember best = null;
            float bestS = 0.15f;
            foreach (var m in alive)
            {
                float s = GetSuspicion(m) + Random.Range(0f, 0.1f);
                if (m == CurrentTarget) s -= 0.2f;
                if (s > bestS) { bestS = s; best = m; }
            }
            if (best != null) { CurrentTarget = best; return; }
        }
        else if (r < 0.75f)
        {
            // Ktos krecacy sie przy walizce
            var near = new List<CrowdMember>();
            foreach (var m in alive)
                if (Suitcase.Nearest(m.transform.position, 7f) != null) near.Add(m);
            if (near.Count > 0) { CurrentTarget = near[Random.Range(0, near.Count)]; return; }
        }
        CurrentTarget = alive[Random.Range(0, alive.Count)];
    }

    void Shoot()
    {
        CrowdMember victim = CurrentTarget;
        Vector3 aim = victim.TorsoPosition + Random.insideUnitSphere * aimError;
        ammo--;
        SniperShot.Fire(transform.position, aim - transform.position);
        suspicion[victim] = 0f;
        state = State.Reload;
        stateTimer = reloadTime;
        CurrentTarget = null;
    }
}
