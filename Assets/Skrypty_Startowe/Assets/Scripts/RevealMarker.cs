using UnityEngine;

// Znacznik nad glowa: czerwony "SZPIEG" na koniec rundy, pomaranczowy - podejrzany oznaczony przez snajpera
public class RevealMarker : MonoBehaviour
{
    public Transform follow;

    public static GameObject Attach(Transform target, Transform parent)
    {
        return Attach(target, parent, new Color(1f, 0.15f, 0.1f), 0.3f);
    }

    public static GameObject Attach(Transform target, Transform parent, Color color, float size)
    {
        var go = LowPolyFactory.Blob("Marker", parent, Vector3.zero, size, size * 1.6f, color, true, 4);
        go.AddComponent<RevealMarker>().follow = target;
        return go;
    }

    void LateUpdate()
    {
        if (follow == null) { Destroy(gameObject); return; }
        transform.position = follow.position + Vector3.up * (1.6f + Mathf.Sin(Time.unscaledTime * 4f) * 0.15f);
        transform.rotation = Quaternion.Euler(0f, Time.unscaledTime * 120f, 0f);
    }
}
