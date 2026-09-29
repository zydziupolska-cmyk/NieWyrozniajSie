using UnityEngine;
using UnityEngine.InputSystem;

// Szpieg sterowany przez gracza (widok TPP).
// SHIFT  - tryb NPC: kanciasty ruch identyczny jak u botow
// SPACJA - bieg w panice z rekami w gorze (nasladowanie tlumu)
// E      - przytrzymaj, zeby zlapac przedmiot (TYLKO jako czlowiek, bez SHIFT);
//          zlapany niesiesz dalej (takze w trybie NPC), ponowne E = odloz
// LPM    - rzut trzymanym przedmiotem
// 1-6    - czynnosci jak u botow: 1 machanie, 2 drapanie po glowie, 3 telefon,
//          4 siadanie (na lawce, jesli jest obok), 5 gadanie z gestami, 6 zagladanie
[RequireComponent(typeof(CharacterController))]
public class SpyController : MonoBehaviour
{
    public float humanSpeed = GameConfig.SpyHumanSpeed;
    public float npcSpeed = GameConfig.BotWalkSpeed;
    public float panicSpeed = GameConfig.BotPanicSpeed;
    public float turnSmoothTime = 0.1f;
    public float gravity = -20f;

    private CharacterController controller;
    private ProceduralAnimator animator;
    private CrowdMember member;
    private float turnVelocity;
    private float verticalVelocity;

    [Header("State")]
    public bool isNpcMode = false;
    public bool isPanicRunning = false;

    public Prop NearbyProp { get; private set; }
    public Prop HeldProp => member != null ? member.HeldProp : null;
    public bool IsHolding => HeldProp != null;
    public bool IsHoldingTarget => HeldProp != null && HeldProp.IsTarget;
    public bool IsReaching { get; private set; }

    private bool grabKeyReleased;
    private float emoteTimer;
    private ActivitySpot sitSpot;

    public Emote CurrentEmote => animator != null ? animator.emote : Emote.None;
    public Transform CameraTransform { get; set; }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<ProceduralAnimator>();
        if (animator == null) animator = gameObject.AddComponent<ProceduralAnimator>();
        member = GetComponent<CrowdMember>();
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        var kb = Keyboard.current;

        isNpcMode = kb != null && kb.leftShiftKey.isPressed;
        isPanicRunning = kb != null && kb.spaceKey.isPressed;

        bool ePressed = kb != null && kb.eKey.isPressed;
        bool eDown = kb != null && kb.eKey.wasPressedThisFrame;
        bool throwDown = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (IsHolding)
        {
            if (!ePressed) grabKeyReleased = true;
            if (throwDown) member.ThrowProp();
            else if (eDown && grabKeyReleased) member.DropProp();
        }

        NearbyProp = IsHolding ? HeldProp : Prop.Nearest(transform.position, GameConfig.StealDistance, true);
        // Lapanie to "zadanie" - w trybie NPC sie nie da
        IsReaching = !IsHolding && ePressed && !isNpcMode && NearbyProp != null;

        animator.roboticMovement = isNpcMode;
        animator.panicking = isPanicRunning && !IsReaching;

        HandleEmotes(kb);

        if (IsReaching)
        {
            // Rece szukaja przedmiotu; mozna lekko dreptac, zeby dosiegnac
            FaceTowards(NearbyProp.transform.position);
            if (member.TryGrabProp(NearbyProp)) grabKeyReleased = false;
            HandleMovement(kb, 0.35f, false);
        }
        else
        {
            member.StopReaching();
            // Z walizka idzie sie troche wolniej, ale tryb NPC zachowuje predkosc bota
            HandleMovement(kb, IsHolding && !isNpcMode ? 0.8f : 1f, true);
        }
    }

    // --- Emotki ---------------------------------------------------------------------------

    void HandleEmotes(Keyboard kb)
    {
        if (kb == null) return;
        bool moveInput = kb.wKey.isPressed || kb.sKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed ||
                         kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed;

        if (kb.digit1Key.wasPressedThisFrame) StartEmote(Emote.Wave, 2f);
        else if (kb.digit2Key.wasPressedThisFrame) StartEmote(Emote.ScratchHead, 2.5f);
        else if (kb.digit3Key.wasPressedThisFrame) StartEmote(Emote.Phone, 0f);
        else if (kb.digit4Key.wasPressedThisFrame) StartSit();
        else if (kb.digit5Key.wasPressedThisFrame) StartEmote(Emote.Chat, 0f);
        else if (kb.digit6Key.wasPressedThisFrame) StartEmote(Emote.Peek, 0f);

        if (animator.emote == Emote.None) return;

        // Ruch, siegniecie albo panika przerywaja czynnosc (machac mozna w biegu)
        bool movingBreaks = moveInput && animator.emote != Emote.Wave && animator.emote != Emote.Phone;
        if (movingBreaks || IsReaching || isPanicRunning) { StopEmote(); return; }

        if (emoteTimer > 0f)
        {
            emoteTimer -= Time.deltaTime;
            if (emoteTimer <= 0f) StopEmote();
        }
        if (sitSpot != null) transform.rotation = Quaternion.LookRotation(sitSpot.facing);
    }

    // duration <= 0 = trwa do ponownego nacisniecia albo ruchu
    void StartEmote(Emote e, float duration)
    {
        bool same = animator.emote == e;
        StopEmote();
        if (same && duration <= 0f) return;
        animator.emote = e;
        emoteTimer = duration;
    }

    void StartSit()
    {
        if (animator.emote == Emote.Sit || animator.emote == Emote.SitGround) { StopEmote(); return; }
        StopEmote();
        var bench = ActivitySpot.NearestFree(transform.position, SpotKind.Bench, 1.6f);
        if (bench != null)
        {
            sitSpot = bench;
            bench.occupant = member;
            Vector3 seat = bench.standPoint + bench.bodyOffset;
            Vector3 offset = seat - transform.position;
            offset.y = 0f;
            animator.bodyOffset = offset;
            animator.emote = Emote.Sit;
            transform.rotation = Quaternion.LookRotation(bench.facing);
        }
        else animator.emote = Emote.SitGround;
        emoteTimer = 0f;
    }

    void StopEmote()
    {
        animator.emote = Emote.None;
        animator.bodyOffset = Vector3.zero;
        emoteTimer = 0f;
        if (sitSpot != null && sitSpot.occupant == member) sitSpot.occupant = null;
        sitSpot = null;
    }

    void HandleMovement(Keyboard kb, float speedMultiplier, bool rotate)
    {
        float h = 0f;
        float v = 0f;

        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
        }

        Vector3 direction = new Vector3(h, 0f, v).normalized;
        Vector3 motion = Vector3.zero;
        float camYaw = CameraTransform != null ? CameraTransform.eulerAngles.y : 0f;

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camYaw;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (!rotate)
            {
                motion = moveDir * humanSpeed * speedMultiplier;
            }
            else if (isNpcMode)
            {
                // NPC Mode: natychmiastowy obrot i stala predkosc - jak bot
                transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
                motion = moveDir * (isPanicRunning ? panicSpeed : npcSpeed);
            }
            else
            {
                // Human Mode: plynny obrot i szybszy chod
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                motion = moveDir * (isPanicRunning ? panicSpeed : humanSpeed) * speedMultiplier;
            }
        }

        ApplyMotion(motion);
    }

    void ApplyMotion(Vector3 motion)
    {
        // Grawitacja - CharacterController sam jej nie ma
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    void FaceTowards(Vector3 point)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 10f);
    }
}
