using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Walizka do ukradzenia. Kradziez wymaga stania obok i przytrzymania akcji -
// w tym czasie walizka sie trzesie, co moze zauwazyc snajper.
public class Suitcase : MonoBehaviour
{
    public static readonly List<Suitcase> All = new List<Suitcase>();

    public bool IsStolen { get; private set; }
    public float Progress { get; private set; }

    Transform body;
    Transform beacon;
    float lastProgressTime = -10f;

    public static Suitcase Create(Vector3 position, Quaternion rotation, Transform parent)
    {
        var go = new GameObject("Suitcase");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);

        var s = go.AddComponent<Suitcase>();
        s.body = new GameObject("Body").transform;
        s.body.SetParent(go.transform, false);

        Color leather = new Color(0.45f, 0.26f, 0.12f);
        Color metal = new Color(0.8f, 0.75f, 0.55f);
        LowPolyFactory.Box("Case", s.body, new Vector3(0f, 0.3f, 0f), new Vector3(0.75f, 0.5f, 0.25f), leather);
        LowPolyFactory.Box("Handle", s.body, new Vector3(0f, 0.6f, 0f), new Vector3(0.25f, 0.08f, 0.06f), metal);
        LowPolyFactory.Box("Strap", s.body, new Vector3(0f, 0.3f, 0f), new Vector3(0.77f, 0.08f, 0.27f), metal);

        // Znacznik widoczny dla obu stron - snajper wie, czego pilnowac
        s.beacon = LowPolyFactory.Blob("Beacon", go.transform, new Vector3(0f, 2.8f, 0f), 0.22f, 0.25f,
            new Color(1f, 0.85f, 0.2f), true, 4).transform;

        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.3f, 0f);
        col.size = new Vector3(0.75f, 0.6f, 0.25f);

        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = col.center;
        obstacle.size = col.size;
        obstacle.carving = true;

        return s;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void AddProgress(float amount, CrowdMember thief)
    {
        if (IsStolen) return;
        Progress += amount;
        lastProgressTime = Time.time;
        if (Progress >= 1f) Steal(thief);
    }

    void Steal(CrowdMember thief)
    {
        IsStolen = true;
        Progress = 1f;
        SoundFx.Play(SoundFx.Steal, transform.position, 0.8f);
        if (GameManager.Instance != null) GameManager.Instance.OnSuitcaseStolen(this, thief);
        Destroy(gameObject);
    }

    void Update()
    {
        // Przerwana kradziez = postep od zera
        if (Time.time - lastProgressTime > 0.25f) Progress = 0f;

        if (Progress > 0f)
        {
            float shake = 4f + Progress * 8f;
            body.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 40f) * shake, Mathf.Sin(Time.time * 33f) * shake * 0.5f);
        }
        else body.localRotation = Quaternion.identity;

        beacon.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
        beacon.localPosition = new Vector3(0f, 2.8f + Mathf.Sin(Time.time * 2f) * 0.15f, 0f);
    }

    public static Suitcase Nearest(Vector3 pos, float maxDistance)
    {
        Suitcase best = null;
        float bestD = maxDistance;
        foreach (var s in All)
        {
            if (s == null || s.IsStolen) continue;
            float d = Vector3.Distance(pos, s.transform.position);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }
}
