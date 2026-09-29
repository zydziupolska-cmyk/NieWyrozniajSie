using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 4f;
    public float height = 1.5f;
    public float rotationSpeed = 2f;
    
    private float currentX = 0f;
    private float currentY = 15f;

    void Start()
    {
        // Zablokuj kursor myszy na środku ekranu, żeby wygodnie obracać kamerą
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            currentX += mouseDelta.x * rotationSpeed;
            currentY -= mouseDelta.y * rotationSpeed;
            currentY = Mathf.Clamp(currentY, -10f, 60f); // Ograniczenie góra/dół
        }

        Vector3 offset = new Vector3(0, 0, -distance);
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        
        transform.position = target.position + Vector3.up * height + rotation * offset;
        transform.LookAt(target.position + Vector3.up * height);
    }
}
