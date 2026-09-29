using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Kazda postac w tlumie: bot, szpieg AI albo szpieg-gracz.
// Snajper widzi tylko ten komponent - nie wie, kto jest kim.
public class CrowdMember : MonoBehaviour
{
    public static readonly List<CrowdMember> All = new List<CrowdMember>();

    public static float PanicUntil;
    public static bool PanicActive => Time.time < PanicUntil;

    public bool isSpy;
    public bool isPlayer;

    public ProceduralAnimator Animator { get; private set; }
    public bool IsDead { get; private set; }
    public float NoiseSeed { get; private set; }
    public Vector3 TorsoPosition => Animator.TorsoTransform.position;

    // --- Przedmioty w rekach ---------------------------------------------------

    public Prop HeldProp
    {
        get
        {
            Rigidbody rb = Animator.HeldBody;
            return rb != null ? rb.GetComponent<Prop>() : null;
        }
    }

    // Wywolywac co klatke podczas siegania; zwraca true, gdy przedmiot jest juz w rekach
    public bool TryGrabProp(Prop prop)
    {
        if (prop == null || IsDead) return false;
        Animator.reaching = true;
        Animator.hasReachTarget = true;
        Animator.reachTarget = prop.GrabPoint;
        if (!Animator.TryGrab(prop.Col, prop.Body)) return false;
        prop.Holder = this;
        if (prop.ReservedBy == this) prop.ReservedBy = null;
        StopReaching();
        return true;
    }

    public void StopReaching()
    {
        Animator.reaching = false;
        Animator.hasReachTarget = false;
    }

    public void DropProp()
    {
        Prop held = HeldProp;
        Animator.Release();
        if (held != null && held.Holder == this) held.Holder = null;
    }

    // Rzut jak w Gang Beasts - do przodu i lekko w gore
    public void ThrowProp(float speed = 7f)
    {
        Prop held = HeldProp;
        if (held == null) return;
        DropProp();
        Vector3 fwd = Animator.TorsoTransform.forward;
        fwd.y = 0f;
        held.Body.AddForce(fwd.normalized * speed + Vector3.up * speed * 0.45f, ForceMode.VelocityChange);
        held.Body.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);
    }

    void Awake()
    {
        Animator = GetComponent<ProceduralAnimator>();
        if (Animator == null) Animator = gameObject.AddComponent<ProceduralAnimator>();
        NoiseSeed = Random.value * 100f;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void Kill(Vector3 hitDirection, Rigidbody hitBody, Vector3 hitPoint)
    {
        if (IsDead) return;
        IsDead = true;

        var bot = GetComponent<BotAI>();
        if (bot != null) bot.enabled = false;
        var aiSpy = GetComponent<AISpy>();
        if (aiSpy != null) aiSpy.enabled = false;
        var spy = GetComponent<SpyController>();
        if (spy != null) spy.enabled = false;

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Prop held = HeldProp;
        if (held != null && held.Holder == this) held.Holder = null;
        Animator.panicking = false;
        StopReaching();
        Animator.EnableRagdoll(hitDirection, hitBody, hitPoint); // wypuszcza przedmiot z rak

        if (GameManager.Instance != null) GameManager.Instance.OnCharacterKilled(this);
    }

    public static void TriggerPanic()
    {
        PanicUntil = Time.time + GameConfig.PanicDuration;
        // Kopia listy - reakcje moga modyfikowac stan
        foreach (var m in All.ToArray())
        {
            if (m == null || m.IsDead) continue;
            var bot = m.GetComponent<BotAI>();
            if (bot != null && bot.enabled) bot.TriggerPanic();
            var ai = m.GetComponent<AISpy>();
            if (ai != null && ai.enabled) ai.OnPanic();
        }
    }
}
