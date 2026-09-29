using UnityEngine;

// Czerwony znacznik "SZPIEG" nad glowa - pokazywany na koniec rundy
public class RevealMarker : MonoBehaviour
{
    public Transform follow;

    public static void Attach(Transform target, Transform parent)
    {
        var go = LowPolyFactory.Blob("RevealMarker", parent, Vector3.zero, 0.3f, 0.5f, new Color(1f, 0.15f, 0.1f), true, 4);
        go.AddComponent<RevealMarker>().follow = target;
    }

    void LateUpdate()
    {
        if (follow == null) { Destroy(gameObject); return; }
        transform.position = follow.position + Vector3.up * (1.6f + Mathf.Sin(Time.unscaledTime * 4f) * 0.15f);
        transform.rotation = Quaternion.Euler(0f, Time.unscaledTime * 120f, 0f);
    }
}
