using UnityEngine;
using UnityEngine.InputSystem;

// Szpieg sterowany przez gracza (widok TPP).
// SHIFT  - tryb NPC: kanciasty ruch identyczny jak u botow
// SPACJA - bieg w panice z rekami w gorze (nasladowanie tlumu)
// E      - zlap walizke rekami i trzymaj, az zniknie pod plaszczem (tylko poza trybem NPC!)
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

    public Suitcase NearbySuitcase { get; private set; }
    public bool IsStealing => IsHolding || IsReaching;
    public bool IsHolding => heldSuitcase != null;
    public bool IsReaching { get; private set; }

    private Suitcase heldSuitcase;
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

        bool wantGrab = !isNpcMode && kb != null && kb.eKey.isPressed;

        // Puszczenie E / wejscie w tryb NPC = walizka spada na ziemie
        if (heldSuitcase != null && (!wantGrab || !animator.IsHolding))
        {
            animator.Release();
            heldSuitcase = null;
        }

        NearbySuitcase = heldSuitcase != null ? heldSuitcase : Suitcase.Nearest(transform.position, GameConfig.StealDistance);
        IsReaching = heldSuitcase == null && wantGrab && NearbySuitcase != null;

        animator.roboticMovement = isNpcMode;
        animator.panicking = isPanicRunning && !IsStealing;
        animator.reaching = IsReaching;
        animator.hasReachTarget = IsReaching;
        if (IsReaching) animator.reachTarget = NearbySuitcase.GrabPoint;

        if (IsReaching)
        {
            // Rece szukaja raczki; mozna lekko dreptac, zeby dosiegnac
            FaceTowards(NearbySuitcase.transform.position);
            if (animator.TryGrab(NearbySuitcase.Col, NearbySuitcase.Body)) heldSuitcase = NearbySuitcase;
            HandleMovement(kb, 0.35f, false);
        }
        else if (heldSuitcase != null)
        {
            // Trzymamy walizke - chowanie jej trwa chwile, mozna przy tym isc
            heldSuitcase.AddProgress(Time.deltaTime / GameConfig.PlayerStealTime, member);
            HandleMovement(kb, 0.6f, true);
        }
        else
        {
            HandleMovement(kb, 1f, true);
        }
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
