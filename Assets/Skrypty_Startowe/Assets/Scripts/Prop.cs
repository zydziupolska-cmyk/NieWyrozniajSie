using System.Collections.Generic;
using UnityEngine;

public enum PropType { Box, Bag, Cone, TrashBag, Ball, Suitcase }

// Fizyczny przedmiot, ktory mozna zlapac rekami, niesc, upuscic i rzucic.
// Boty tez je nosza - dzieki temu noszenie czegos nie zdradza szpiega.
public class Prop : MonoBehaviour
{
    public static readonly List<Prop> All = new List<Prop>();

    public PropType Type { get; private set; }
    public Rigidbody Body { get; private set; }
    public Collider Col { get; private set; }
    public CrowdMember Holder { get; set; }
    public CrowdMember ReservedBy { get; set; } // bot, ktory wlasnie po niego idzie

    Transform grip;

    public Vector3 GrabPoint => grip != null ? grip.position : Col.bounds.center + Vector3.up * Col.bounds.extents.y;
    public bool IsHeld => Holder != null && Holder.Animator.HeldBody == Body;
    public bool LooksLikeSuitcase => Type == PropType.Suitcase;
    public bool IsTarget => GetComponent<Suitcase>() != null;

    public string DisplayName
    {
        get
        {
            switch (Type)
            {
                case PropType.Box: return "karton";
                case PropType.Bag: return "torba z zakupami";
                case PropType.Cone: return "pachołek";
                case PropType.TrashBag: return "worek na śmieci";
                case PropType.Ball: return "piłka";
                default: return IsTarget ? "WALIZKA (cel!)" : "walizka podróżnego";
            }
        }
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // Najblizszy wolny przedmiot w zasiegu (preferowane walizki-cele, jesli preferTargets)
    public static Prop Nearest(Vector3 pos, float maxDistance, bool preferTargets)
    {
        Prop best = null;
        float bestScore = float.MaxValue;
        foreach (var p in All)
        {
            if (p == null || p.IsHeld) continue;
            float d = Vector3.Distance(pos, p.transform.position);
            if (d > maxDistance) continue;
            float score = d - (preferTargets && p.IsTarget ? 1f : 0f);
            if (score < bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    // --- Fabryka -------------------------------------------------------------------

    static readonly Color Cardboard = new Color(0.72f, 0.55f, 0.35f);
    static readonly Color Leather = new Color(0.45f, 0.26f, 0.12f);
    static readonly Color Brass = new Color(0.8f, 0.75f, 0.55f);
    static readonly Color[] BagColors =
    {
        new Color(0.9f, 0.9f, 0.88f), new Color(0.3f, 0.6f, 0.35f), new Color(0.85f, 0.35f, 0.3f), new Color(0.3f, 0.45f, 0.8f)
    };

    public static Prop Create(PropType type, Vector3 position, Quaternion rotation, Transform parent)
    {
        var go = new GameObject("Prop_" + type);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var prop = go.AddComponent<Prop>();
        prop.Type = type;
        Transform t = go.transform;
        float mass = 2f;

        switch (type)
        {
            case PropType.Box:
            {
                LowPolyFactory.Box("Box", t, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.4f), Cardboard);
                LowPolyFactory.Box("Tape", t, new Vector3(0f, 0.405f, 0f), new Vector3(0.1f, 0.01f, 0.41f), new Color(0.85f, 0.75f, 0.5f));
                prop.Col = AddBox(go, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.4f));
                prop.grip = Marker(t, new Vector3(0f, 0.4f, 0f));
                mass = 2.5f;
                break;
            }
            case PropType.Bag:
            {
                Color c = BagColors[Random.Range(0, BagColors.Length)];
                LowPolyFactory.Box("Bag", t, new Vector3(0f, 0.22f, 0f), new Vector3(0.38f, 0.44f, 0.18f), c);
                LowPolyFactory.Box("HandleL", t, new Vector3(-0.08f, 0.5f, 0f), new Vector3(0.03f, 0.12f, 0.02f), c);
                LowPolyFactory.Box("HandleR", t, new Vector3(0.08f, 0.5f, 0f), new Vector3(0.03f, 0.12f, 0.02f), c);
                LowPolyFactory.Box("HandleTop", t, new Vector3(0f, 0.56f, 0f), new Vector3(0.19f, 0.03f, 0.02f), c);
                LowPolyFactory.Blob("Bread", t, new Vector3(0.06f, 0.48f, 0f), 0.05f, 0.18f, new Color(0.85f, 0.65f, 0.35f));
                prop.Col = AddBox(go, new Vector3(0f, 0.25f, 0f), new Vector3(0.38f, 0.5f, 0.18f));
                prop.grip = Marker(t, new Vector3(0f, 0.55f, 0f));
                mass = 1.5f;
                break;
            }
            case PropType.Cone:
            {
                Color orange = new Color(1f, 0.45f, 0.1f);
                LowPolyFactory.Box("Base", t, new Vector3(0f, 0.025f, 0f), new Vector3(0.4f, 0.05f, 0.4f), orange);
                var cone = new GameObject("Cone");
                cone.transform.SetParent(t, false);
                cone.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                cone.AddComponent<MeshFilter>().sharedMesh = LowPolyFactory.SmoothCapsule(0.14f, 0.03f, 0.45f, 1f, 1f, 1f, 12, 3);
                cone.AddComponent<MeshRenderer>().sharedMaterial = LowPolyFactory.GetMaterial(orange);
                LowPolyFactory.Box("Stripe", t, new Vector3(0f, 0.34f, 0f), new Vector3(0.17f, 0.06f, 0.17f), Color.white);
                prop.Col = AddBox(go, new Vector3(0f, 0.3f, 0f), new Vector3(0.34f, 0.6f, 0.34f));
                prop.grip = Marker(t, new Vector3(0f, 0.55f, 0f));
                mass = 1f;
                break;
            }
            case PropType.TrashBag:
            {
                LowPolyFactory.Blob("Bag", t, new Vector3(0f, 0.28f, 0f), 0.28f, 0.05f, new Color(0.1f, 0.1f, 0.12f), false, 9);
                LowPolyFactory.Blob("Knot", t, new Vector3(0f, 0.6f, 0f), 0.06f, 0.05f, new Color(0.1f, 0.1f, 0.12f), false, 6);
                var col = go.AddComponent<SphereCollider>();
                col.center = new Vector3(0f, 0.3f, 0f);
                col.radius = 0.29f;
                prop.Col = col;
                prop.grip = Marker(t, new Vector3(0f, 0.62f, 0f));
                mass = 2f;
                break;
            }
            case PropType.Ball:
            {
                Color c = BagColors[Random.Range(1, BagColors.Length)];
                var ball = new GameObject("Ball");
                ball.transform.SetParent(t, false);
                ball.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                ball.AddComponent<MeshFilter>().sharedMesh = LowPolyFactory.SmoothCapsule(0.2f, 0.2f, 0f, 1f, 1f, 1f, 14, 6);
                ball.AddComponent<MeshRenderer>().sharedMaterial = LowPolyFactory.GetBodyMaterial(c);
                var col = go.AddComponent<SphereCollider>();
                col.center = new Vector3(0f, 0.2f, 0f);
                col.radius = 0.2f;
                col.sharedMaterial = new PhysicsMaterial("Bouncy") { bounciness = 0.7f, bounceCombine = PhysicsMaterialCombine.Maximum };
                prop.Col = col;
                mass = 0.5f;
                break;
            }
            default: // Suitcase - identyczna dla celow i wabikow
            {
                LowPolyFactory.Box("Case", t, new Vector3(0f, 0.3f, 0f), new Vector3(0.75f, 0.5f, 0.25f), Leather);
                prop.grip = LowPolyFactory.Box("Handle", t, new Vector3(0f, 0.6f, 0f), new Vector3(0.25f, 0.08f, 0.06f), Brass).transform;
                LowPolyFactory.Box("Strap", t, new Vector3(0f, 0.3f, 0f), new Vector3(0.77f, 0.08f, 0.27f), Brass);
                prop.Col = AddBox(go, new Vector3(0f, 0.3f, 0f), new Vector3(0.75f, 0.6f, 0.25f));
                mass = 5f;
                break;
            }
        }

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.2f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        prop.Body = rb;
        return prop;
    }

    static BoxCollider AddBox(GameObject go, Vector3 center, Vector3 size)
    {
        var col = go.AddComponent<BoxCollider>();
        col.center = center;
        col.size = size;
        return col;
    }

    static Transform Marker(Transform parent, Vector3 localPos)
    {
        var m = new GameObject("Grip").transform;
        m.SetParent(parent, false);
        m.localPosition = localPos;
        return m;
    }
}
