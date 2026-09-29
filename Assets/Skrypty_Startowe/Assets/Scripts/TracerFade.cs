using UnityEngine;

// Zanikajaca smuga pocisku
public class TracerFade : MonoBehaviour
{
    LineRenderer lr;
    float t;

    void Start() { lr = GetComponent<LineRenderer>(); }

    void Update()
    {
        t += Time.deltaTime;
        float k = 1f - t / 0.35f;
        if (k <= 0f) { Destroy(gameObject); return; }
        lr.startWidth = 0.05f * k;
        lr.endWidth = 0.02f * k;
    }
}
