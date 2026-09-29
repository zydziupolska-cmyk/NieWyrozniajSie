using System.Collections.Generic;
using UnityEngine;

// Walizka-cel. Wyglada dokladnie jak walizki podroznych, ktore nosza boty -
// rozni sie tylko zoltym znacznikiem, widocznym dopoki lezy na ziemi.
// Szpieg musi ja zlapac (tylko jako czlowiek) i doniesc do furgonetki.
[RequireComponent(typeof(Prop))]
public class Suitcase : MonoBehaviour
{
    public static readonly List<Suitcase> All = new List<Suitcase>();

    public bool IsStolen { get; private set; }
    public Prop Prop { get; private set; }
    public Rigidbody Body => Prop.Body;
    public Collider Col => Prop.Col;
    public Vector3 GrabPoint => Prop.GrabPoint;
    public bool IsHeld => Prop.IsHeld;

    Transform beacon;
    bool wasHeld;

    public static Suitcase Create(Vector3 position, Quaternion rotation, Transform parent)
    {
        var prop = Prop.Create(PropType.Suitcase, position, rotation, parent);
        prop.name = "Suitcase_Target";
        var s = prop.gameObject.AddComponent<Suitcase>();
        s.Prop = prop;

        // Znacznik nie jest dzieckiem walizki - ma wisiec pionowo nawet gdy walizka sie przewroci
        s.beacon = LowPolyFactory.Blob("Beacon", parent, position + Vector3.up * 2.8f, 0.22f, 0.25f,
            new Color(1f, 0.85f, 0.2f), true, 4).transform;
        return s;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void OnDestroy()
    {
        if (beacon != null) Destroy(beacon.gameObject);
    }

    void Update()
    {
        if (IsStolen) return;

        bool held = IsHeld;
        if (held != wasHeld)
        {
            wasHeld = held;
            if (GameManager.Instance != null)
            {
                if (held) GameManager.Instance.OnSuitcasePicked(this, Prop.Holder);
                else GameManager.Instance.OnSuitcaseDropped(this);
            }
        }

        // Znacznik tylko nad lezaca walizka - niesiona wtapia sie w walizki podroznych
        beacon.gameObject.SetActive(!held);
        beacon.position = transform.position + Vector3.up * (2.8f + Mathf.Sin(Time.time * 2f) * 0.15f);
        beacon.rotation = Quaternion.Euler(0f, Time.time * 90f, 0f);

        if (ExtractionZone.Contains(transform.position)) Deliver();
    }

    void Deliver()
    {
        IsStolen = true;
        CrowdMember thief = Prop.Holder;
        if (Prop.IsHeld) thief.DropProp();
        SoundFx.Play(SoundFx.Steal, transform.position, 0.9f);
        if (GameManager.Instance != null) GameManager.Instance.OnSuitcaseStolen(this, thief);
        Destroy(gameObject);
    }

    public static Suitcase NearestResting(Vector3 pos, float maxDistance)
    {
        Suitcase best = null;
        float bestD = maxDistance;
        foreach (var s in All)
        {
            if (s == null || s.IsStolen || s.IsHeld) continue;
            float d = Vector3.Distance(pos, s.transform.position);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }
}
