using UnityEngine;
using UnityEngine.InputSystem;

// Kamera TPP szpiega: orbitowanie mysza, zoom kolkiem, nie przenika przez auta i filary.
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float minDistance = 2f;
    public float maxDistance = 9f;
    public float height = 1.6f;
    public float sensitivity = 0.12f;

    private float yaw = 0f;
    private float pitch = 15f;
    private float currentDistance;

    void OnEnable()
    {
        currentDistance = distance;
        if (target != null) yaw = target.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (GameManager.Instance != null && GameManager.Instance.KillCamActive) return; // powtorka steruje kamera

        bool inputAllowed = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        if (inputAllowed && Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            yaw += mouseDelta.x * sensitivity;
            pitch -= mouseDelta.y * sensitivity;
            pitch = Mathf.Clamp(pitch, -10f, 65f);

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 0.6f, minDistance, maxDistance);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + Vector3.up * height;
        Vector3 back = rotation * Vector3.back;

        // Kolizja kamery tylko z otoczeniem (nie z postaciami)
        float wanted = distance;
        if (Physics.SphereCast(focus, 0.25f, back, out RaycastHit hit, distance, 1 << 0, QueryTriggerInteraction.Ignore))
            wanted = Mathf.Max(0.5f, hit.distance - 0.1f);
        currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, Time.deltaTime * 5f);

        transform.position = focus + back * currentDistance;
        transform.rotation = rotation;
    }
}
