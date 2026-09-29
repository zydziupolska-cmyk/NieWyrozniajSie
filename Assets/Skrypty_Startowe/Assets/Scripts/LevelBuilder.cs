using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// Buduje w runtime caly poziom: parking z autami, placem, latarniami,
// budynek snajpera i NavMesh. Nie trzeba niczego ustawiac recznie w edytorze.
public static class LevelBuilder
{
    public class LevelInfo
    {
        public GameObject root;
        public Vector3 sniperPosition;
        public Quaternion sniperRotation;
        public List<Pose> suitcaseSpots = new List<Pose>();
    }

    static readonly Color Asphalt = new Color(0.24f, 0.25f, 0.27f);
    static readonly Color Paint = new Color(0.9f, 0.88f, 0.8f);
    static readonly Color Concrete = new Color(0.62f, 0.6f, 0.57f);
    static readonly Color DarkGlass = new Color(0.12f, 0.14f, 0.18f);
    static readonly Color Tyre = new Color(0.08f, 0.08f, 0.09f);

    static readonly Color[] CarColors =
    {
        new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.75f), new Color(0.9f, 0.9f, 0.9f),
        new Color(0.15f, 0.15f, 0.17f), new Color(0.95f, 0.75f, 0.2f), new Color(0.35f, 0.6f, 0.4f),
        new Color(0.6f, 0.62f, 0.65f), new Color(0.5f, 0.3f, 0.55f)
    };

    public static LevelInfo Build(int seed)
    {
        var oldState = Random.state;
        Random.InitState(seed);

        var info = new LevelInfo();
        info.root = new GameObject("Level");
        var level = new GameObject("Walkable").transform;   // tylko to trafia do NavMesha
        level.SetParent(info.root.transform, false);
        var scenery = new GameObject("Scenery").transform;
        scenery.SetParent(info.root.transform, false);

        float half = GameConfig.LotHalfSize + 2f;

        // --- Podloze ---
        Solid("Ground", level, new Vector3(0f, -0.5f, 0f), new Vector3(half * 2f, 1f, half * 2f), Asphalt, false);
        Solid("OuterGround", scenery, new Vector3(0f, -0.56f, 0f), new Vector3(400f, 1f, 400f), new Color(0.42f, 0.5f, 0.36f), false);
        Solid("Sidewalk", scenery, new Vector3(0f, -0.45f, 0f), new Vector3(half * 2f + 8f, 1f, half * 2f + 8f), Concrete, false);

        // --- Murki dookola ---
        float wallH = 1.2f;
        Solid("Wall_N", level, new Vector3(0f, wallH / 2f, half), new Vector3(half * 2f + 0.5f, wallH, 0.5f), Concrete, true);
        Solid("Wall_S", level, new Vector3(0f, wallH / 2f, -half), new Vector3(half * 2f + 0.5f, wallH, 0.5f), Concrete, true);
        Solid("Wall_E", level, new Vector3(half, wallH / 2f, 0f), new Vector3(0.5f, wallH, half * 2f), Concrete, true);
        Solid("Wall_W", level, new Vector3(-half, wallH / 2f, 0f), new Vector3(0.5f, wallH, half * 2f), Concrete, true);

        // --- Rzedy miejsc parkingowych ---
        var carSpots = new List<Pose>();
        float[] rows = { -30f, -18f, 18f, 30f };
        foreach (float rz in rows)
        {
            for (float x = -36f; x <= 36.01f; x += 3f)
            {
                LowPolyFactory.Box("Line", level, new Vector3(x - 1.5f, 0.01f, rz), new Vector3(0.12f, 0.02f, 5f), Paint);
                if (Random.value < 0.55f)
                {
                    Quaternion rot = Quaternion.Euler(0f, Random.value < 0.5f ? 0f : 180f, 0f);
                    BuildCar(level, new Vector3(x + Random.Range(-0.2f, 0.2f), 0f, rz + Random.Range(-0.3f, 0.3f)), rot);
                    // Walizka moze stac przy przodzie albo tyle auta (od strony alejki)
                    float side = Random.value < 0.5f ? -1f : 1f;
                    carSpots.Add(new Pose(new Vector3(x, 0f, rz + side * 2.9f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
                }
            }
            LowPolyFactory.Box("Line", level, new Vector3(37.5f, 0.01f, rz), new Vector3(0.12f, 0.02f, 5f), Paint);
        }

        // Linie srodkowe alejek
        foreach (float lz in new[] { -24f, 0f, 24f })
            for (float x = -36f; x <= 36f; x += 6f)
                LowPolyFactory.Box("LaneMark", level, new Vector3(x, 0.01f, lz), new Vector3(2.5f, 0.02f, 0.15f), new Color(0.95f, 0.8f, 0.25f));

        // --- Plac na srodku: kiosk, donice z drzewkami, lawki ---
        BuildKiosk(level, new Vector3(0f, 0f, 0f));
        foreach (var p in new[] { new Vector3(-12f, 0f, -7f), new Vector3(12f, 0f, 7f), new Vector3(-24f, 0f, 6f), new Vector3(24f, 0f, -6f), new Vector3(-34f, 0f, -8f), new Vector3(34f, 0f, 8f) })
            BuildPlanter(level, p);
        foreach (var p in new[] { new Vector3(-7f, 0f, 9f), new Vector3(7f, 0f, -9f), new Vector3(-18f, 0f, -2f), new Vector3(18f, 0f, 2f) })
            BuildBench(level, p, Quaternion.Euler(0f, Random.value < 0.5f ? 0f : 90f, 0f));

        // --- Furgonetki szpiegow po obu stronach placu ---
        BuildVan(level, new Vector3(39f, 0f, 0f), -1f);
        BuildVan(level, new Vector3(-39f, 0f, 0f), 1f);

        // --- Latarnie ---
        foreach (float lz in new[] { -24f, 24f, -38f, 38f })
            for (float x = -30f; x <= 30f; x += 20f)
                BuildLamp(level, new Vector3(x, 0f, lz));

        // --- Budynek snajpera (poludnie) i tlo miasta ---
        Vector3 bPos = new Vector3(0f, 11f, -half - 12f);
        Solid("SniperBuilding", scenery, bPos, new Vector3(26f, 22f, 10f), new Color(0.55f, 0.5f, 0.48f), false);
        for (int fy = 0; fy < 6; fy++)
            for (int fx = -5; fx <= 5; fx++)
                LowPolyFactory.Box("Window", scenery, new Vector3(fx * 2.2f, 2.5f + fy * 3.3f, bPos.z + 5.02f), new Vector3(1.2f, 1.6f, 0.05f), DarkGlass);
        Solid("Parapet", scenery, new Vector3(0f, 22.4f, bPos.z + 4.8f), new Vector3(26f, 0.8f, 0.4f), Concrete, false);

        info.sniperPosition = new Vector3(0f, 23.6f, bPos.z + 5.6f);
        info.sniperRotation = Quaternion.LookRotation(new Vector3(0f, 0f, 4f) - info.sniperPosition);

        for (int i = 0; i < 14; i++)
        {
            float ang = i / 14f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * Random.Range(75f, 95f);
            if (p.z < -40f && Mathf.Abs(p.x) < 30f) continue; // nie zaslaniaj budynku snajpera
            float h = Random.Range(8f, 30f);
            Color c = Color.Lerp(new Color(0.5f, 0.52f, 0.58f), new Color(0.75f, 0.68f, 0.6f), Random.value);
            LowPolyFactory.Box("CityBlock", scenery, p + Vector3.up * h / 2f, new Vector3(Random.Range(10f, 20f), h, Random.Range(10f, 20f)), c);
        }

        // --- NavMesh z samego parkingu ---
        var surface = level.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1 << 0;
        surface.BuildNavMesh();

        // --- Miejsca na walizki: przy autach, daleko od siebie, osiagalne ---
        Shuffle(carSpots);
        foreach (var spot in carSpots)
        {
            if (info.suitcaseSpots.Count >= GameConfig.SuitcaseCount) break;
            if (!NavMesh.SamplePosition(spot.position, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) continue;
            bool farEnough = true;
            foreach (var s in info.suitcaseSpots)
                if (Vector3.Distance(s.position, hit.position) < 22f) { farEnough = false; break; }
            if (farEnough) info.suitcaseSpots.Add(new Pose(hit.position, spot.rotation));
        }

        Random.state = oldState;
        return info;
    }

    // Kostka z colliderem. Przeszkody sa oznaczone jako nie do chodzenia.
    static GameObject Solid(string name, Transform parent, Vector3 pos, Vector3 size, Color color, bool obstacle)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = LowPolyFactory.GetMaterial(color);
        if (obstacle) MarkObstacle(go);
        return go;
    }

    static void MarkObstacle(GameObject go)
    {
        var mod = go.AddComponent<NavMeshModifier>();
        mod.overrideArea = true;
        mod.area = 1; // Not Walkable
    }

    static GameObject Group(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 colliderSize, float colliderY)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var col = go.AddComponent<BoxCollider>();
        col.size = colliderSize;
        col.center = new Vector3(0f, colliderY, 0f);
        MarkObstacle(go);
        return go;
    }

    static void BuildCar(Transform parent, Vector3 pos, Quaternion rot)
    {
        var car = Group("Car", parent, pos, rot, new Vector3(1.9f, 1.5f, 4.2f), 0.75f).transform;
        Color paint = CarColors[Random.Range(0, CarColors.Length)];
        LowPolyFactory.Box("Body", car, new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 0.7f, 4.2f), paint);
        LowPolyFactory.Box("Cabin", car, new Vector3(0f, 1.2f, -0.2f), new Vector3(1.7f, 0.55f, 2.3f), paint);
        LowPolyFactory.Box("Glass", car, new Vector3(0f, 1.2f, -0.2f), new Vector3(1.72f, 0.4f, 2.2f), DarkGlass);
        LowPolyFactory.Box("Windshield", car, new Vector3(0f, 1.2f, 0.93f), new Vector3(1.55f, 0.42f, 0.06f), DarkGlass);
        foreach (float sx in new[] { -0.88f, 0.88f })
            foreach (float sz in new[] { -1.35f, 1.35f })
                LowPolyFactory.Box("Wheel", car, new Vector3(sx, 0.32f, sz), new Vector3(0.28f, 0.64f, 0.64f), Tyre);
        LowPolyFactory.Box("LightL", car, new Vector3(-0.6f, 0.7f, 2.1f), new Vector3(0.4f, 0.15f, 0.05f), new Color(1f, 0.95f, 0.75f), true);
        LowPolyFactory.Box("LightR", car, new Vector3(0.6f, 0.7f, 2.1f), new Vector3(0.4f, 0.15f, 0.05f), new Color(1f, 0.95f, 0.75f), true);
        LowPolyFactory.Box("TailL", car, new Vector3(-0.6f, 0.7f, -2.1f), new Vector3(0.4f, 0.15f, 0.05f), new Color(0.8f, 0.1f, 0.1f), true);
        LowPolyFactory.Box("TailR", car, new Vector3(0.6f, 0.7f, -2.1f), new Vector3(0.4f, 0.15f, 0.05f), new Color(0.8f, 0.1f, 0.1f), true);
    }

    // Furgonetka z otwartymi tylnymi drzwiami. inward = kierunek (po osi X) do srodka parkingu.
    static void BuildVan(Transform parent, Vector3 pos, float inward)
    {
        Quaternion rot = Quaternion.Euler(0f, inward > 0f ? 90f : -90f, 0f); // lokalne +Z = do srodka
        var van = Group("Van", parent, pos, rot, new Vector3(2.2f, 2.4f, 5f), 1.2f).transform;
        Color white = new Color(0.93f, 0.93f, 0.9f);
        LowPolyFactory.Box("Body", van, new Vector3(0f, 1.35f, -0.4f), new Vector3(2.2f, 2.1f, 4.2f), white);
        LowPolyFactory.Box("Hood", van, new Vector3(0f, 0.85f, -2.3f), new Vector3(2.1f, 1.1f, 0.8f), white);
        LowPolyFactory.Box("Windshield", van, new Vector3(0f, 1.75f, -2.52f), new Vector3(1.9f, 0.7f, 0.05f), DarkGlass);
        LowPolyFactory.Box("Stripe", van, new Vector3(0f, 1.2f, -0.4f), new Vector3(2.22f, 0.25f, 4.22f), new Color(0.2f, 0.4f, 0.75f));
        LowPolyFactory.Box("Interior", van, new Vector3(0f, 1.4f, 1.71f), new Vector3(1.9f, 1.8f, 0.02f), new Color(0.05f, 0.05f, 0.06f));
        // Otwarte drzwi
        var doorL = LowPolyFactory.Box("DoorL", van, new Vector3(-1.45f, 1.35f, 2.2f), new Vector3(0.05f, 1.9f, 1.05f), white);
        doorL.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
        var doorR = LowPolyFactory.Box("DoorR", van, new Vector3(1.45f, 1.35f, 2.2f), new Vector3(0.05f, 1.9f, 1.05f), white);
        doorR.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1.7f, 1.2f })
                LowPolyFactory.Box("Wheel", van, new Vector3(sx, 0.35f, sz), new Vector3(0.3f, 0.7f, 0.7f), Tyre);

        // Strefa dostawy tuz za tylnymi drzwiami + slup swiatla widoczny z daleka
        var zoneGo = new GameObject("ExtractionZone");
        zoneGo.transform.SetParent(parent, false);
        zoneGo.transform.position = pos + new Vector3(inward * 4.4f, 0f, 0f);
        Color green = new Color(0.3f, 1f, 0.45f);
        LowPolyFactory.Box("Pad", zoneGo.transform, new Vector3(0f, 0.015f, 0f), new Vector3(3.4f, 0.02f, 3.4f), green, true);
        var beam = LowPolyFactory.Box("Beam", zoneGo.transform, new Vector3(0f, 7f, 0f), new Vector3(0.35f, 14f, 0.35f), green, true);
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var zone = zoneGo.AddComponent<ExtractionZone>();
        zone.beam = beam.transform;
    }

    static void BuildKiosk(Transform parent, Vector3 pos)
    {
        var k = Group("Kiosk", parent, pos, Quaternion.identity, new Vector3(4f, 3f, 3f), 1.5f).transform;
        LowPolyFactory.Box("Base", k, new Vector3(0f, 1.25f, 0f), new Vector3(4f, 2.5f, 3f), new Color(0.3f, 0.55f, 0.5f));
        LowPolyFactory.Box("Roof", k, new Vector3(0f, 2.75f, 0f), new Vector3(4.6f, 0.5f, 3.6f), new Color(0.85f, 0.3f, 0.25f));
        LowPolyFactory.Box("Window", k, new Vector3(0f, 1.5f, 1.51f), new Vector3(2.8f, 1f, 0.05f), DarkGlass);
        LowPolyFactory.Box("Sign", k, new Vector3(0f, 3.3f, 0f), new Vector3(2.5f, 0.6f, 0.2f), new Color(0.95f, 0.85f, 0.3f));
    }

    static void BuildPlanter(Transform parent, Vector3 pos)
    {
        var p = Group("Planter", parent, pos, Quaternion.identity, new Vector3(2f, 0.7f, 2f), 0.35f).transform;
        LowPolyFactory.Box("Pot", p, new Vector3(0f, 0.35f, 0f), new Vector3(2f, 0.7f, 2f), Concrete);
        LowPolyFactory.Box("Trunk", p, new Vector3(0f, 1.3f, 0f), new Vector3(0.25f, 1.6f, 0.25f), new Color(0.4f, 0.28f, 0.18f));
        LowPolyFactory.Blob("Crown", p, new Vector3(0f, 2.6f, 0f), 1.1f, 0.4f, new Color(0.3f, 0.55f, 0.28f), false, 6);
    }

    static void BuildBench(Transform parent, Vector3 pos, Quaternion rot)
    {
        var b = Group("Bench", parent, pos, rot, new Vector3(2f, 0.9f, 0.6f), 0.45f).transform;
        LowPolyFactory.Box("Seat", b, new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.1f, 0.5f), new Color(0.55f, 0.38f, 0.22f));
        LowPolyFactory.Box("Back", b, new Vector3(0f, 0.8f, -0.22f), new Vector3(2f, 0.5f, 0.08f), new Color(0.55f, 0.38f, 0.22f));
        LowPolyFactory.Box("LegL", b, new Vector3(-0.8f, 0.22f, 0f), new Vector3(0.1f, 0.45f, 0.45f), Tyre);
        LowPolyFactory.Box("LegR", b, new Vector3(0.8f, 0.22f, 0f), new Vector3(0.1f, 0.45f, 0.45f), Tyre);
    }

    static void BuildLamp(Transform parent, Vector3 pos)
    {
        var l = Group("Lamp", parent, pos, Quaternion.identity, new Vector3(0.3f, 5f, 0.3f), 2.5f).transform;
        LowPolyFactory.Box("Pole", l, new Vector3(0f, 2.5f, 0f), new Vector3(0.2f, 5f, 0.2f), new Color(0.25f, 0.27f, 0.3f));
        LowPolyFactory.Box("Arm", l, new Vector3(0f, 4.9f, 0.5f), new Vector3(0.15f, 0.15f, 1f), new Color(0.25f, 0.27f, 0.3f));
        LowPolyFactory.Box("Bulb", l, new Vector3(0f, 4.75f, 0.9f), new Vector3(0.4f, 0.15f, 0.4f), new Color(1f, 0.95f, 0.8f), true);
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
