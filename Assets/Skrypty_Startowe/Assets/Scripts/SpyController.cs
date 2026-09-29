using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SpyController : MonoBehaviour
{
    public float humanSpeed = 5f;
    public float npcSpeed = 3.5f; // Matches NavMeshAgent default speed
    public float humanRotationSpeed = 10f;
    
    private CharacterController controller;
    private Camera mainCamera;
    
    [Header("State")]
    public bool isNpcMode = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;
        
        if (GetComponent<ProceduralAnimator>() == null)
        {
            gameObject.AddComponent<ProceduralAnimator>();
        }
    }

    void Update()
    {
        // Sprawdzamy klawiaturę (New Input System)
        if (Keyboard.current != null)
        {
            isNpcMode = Keyboard.current.leftShiftKey.isPressed;
        }

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
        
        if (mainCamera != null && direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (isNpcMode)
            {
                // NPC Mode: Snappy rotation, fixed linear speed
                transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
                controller.Move(moveDir.normalized * npcSpeed * Time.deltaTime);
            }
            else
            {
                // Human Mode: Smooth rotation, fluid speed
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref humanRotationSpeed, 0.1f);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                controller.Move(moveDir.normalized * humanSpeed * Time.deltaTime);
            }
        }
    }
}
