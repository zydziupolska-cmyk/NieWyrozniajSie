using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SpyController : MonoBehaviour
{
    public float humanSpeed = 5f;
    public float npcSpeed = 3.5f; // Matches NavMeshAgent default speed
    public float turnSmoothTime = 0.1f;
    public float gravity = -20f;

    private CharacterController controller;
    private Camera mainCamera;
    private ProceduralAnimator animator;
    private float turnVelocity;
    private float verticalVelocity;

    [Header("State")]
    public bool isNpcMode = false;
    public bool IsDead { get; private set; }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        animator = GetComponent<ProceduralAnimator>();
        if (animator == null) animator = gameObject.AddComponent<ProceduralAnimator>();

        // Kamera sledzi plynny kontroler, a nie trzesacy sie tulow ragdolla
        ThirdPersonCamera tpc = mainCamera != null ? mainCamera.GetComponent<ThirdPersonCamera>() : null;
        if (tpc != null) tpc.target = transform;
    }

    void Update()
    {
        if (IsDead) return;

        // Sprawdzamy klawiaturę (New Input System)
        if (Keyboard.current != null)
        {
            isNpcMode = Keyboard.current.leftShiftKey.isPressed;
        }
        animator.roboticMovement = isNpcMode;

        HandleMovement();
    }

    void HandleMovement()
    {
        float h = 0f;
        float v = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
        }

        Vector3 direction = new Vector3(h, 0f, v).normalized;
        Vector3 motion = Vector3.zero;

        if (mainCamera != null && direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (isNpcMode)
            {
                // NPC Mode: Snappy rotation, fixed linear speed
                transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
                motion = moveDir * npcSpeed;
            }
            else
            {
                // Human Mode: Smooth rotation, fluid speed
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                motion = moveDir * humanSpeed;
            }
        }

        // Grawitacja - CharacterController sam jej nie ma
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        motion.y = verticalVelocity;

        controller.Move(motion * Time.deltaTime);
    }

    public void Kill(Vector3 hitDirection, Rigidbody hitBody, Vector3 hitPoint)
    {
        if (IsDead) return;
        IsDead = true;
        animator.EnableRagdoll(hitDirection, hitBody, hitPoint);
        controller.enabled = false;

        // Kamera leci za trupem
        ThirdPersonCamera tpc = mainCamera != null ? mainCamera.GetComponent<ThirdPersonCamera>() : null;
        if (tpc != null) tpc.target = animator.TorsoTransform;
    }
}
