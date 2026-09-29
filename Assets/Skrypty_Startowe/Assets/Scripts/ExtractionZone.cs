using System.Collections.Generic;
using UnityEngine;

// Strefa przy otwartej furgonetce - walizka-cel, ktora sie w niej znajdzie, jest skradziona.
public class ExtractionZone : MonoBehaviour
{
    public static readonly List<ExtractionZone> All = new List<ExtractionZone>();
    public float radius = 1.8f;
    public Transform beam;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static bool Contains(Vector3 pos)
    {
        foreach (var z in All)
        {
            Vector3 d = pos - z.transform.position;
            d.y = 0f;
            if (d.magnitude < z.radius) return true;
        }
        return false;
    }

    public static ExtractionZone Nearest(Vector3 pos)
    {
        ExtractionZone best = null;
        float bestD = float.MaxValue;
        foreach (var z in All)
        {
            float d = Vector3.Distance(pos, z.transform.position);
            if (d < bestD) { bestD = d; best = z; }
        }
        return best;
    }

    void Update()
    {
        // Pulsujacy slup swiatla - widoczny z daleka dla obu stron
        if (beam != null)
        {
            float k = 1f + Mathf.Sin(Time.time * 3f) * 0.08f;
            beam.localScale = new Vector3(0.35f * k, beam.localScale.y, 0.35f * k);
        }
    }
}
