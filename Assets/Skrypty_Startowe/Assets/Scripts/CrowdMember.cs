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

        Animator.panicking = false;
        Animator.reaching = false;
        Animator.EnableRagdoll(hitDirection, hitBody, hitPoint);

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
