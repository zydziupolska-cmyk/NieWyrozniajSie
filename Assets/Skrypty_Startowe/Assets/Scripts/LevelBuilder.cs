using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public enum MapType { Parking, Metro, Airport }

// Buduje w runtime caly poziom: jedna z trzech map, budynek snajpera, furgonetki,
// miejsca dla nawykow botow (lawki, kolejki, grupki) i NavMesh.
// Nie trzeba niczego ustawiac recznie w edytorze.
public static class LevelBuilder
{
    public class LevelInfo
    {
        public MapType map;
        public GameObject root;
        public Vector3 sniperPosition;
        public Quaternion sniperRotation;
        public List<Pose> suitcaseSpots = new List<Pose>();
        public List<Pose> carouselSpots = new List<Pose>();   // walizki na tasmach (lotnisko)
        public Color sky = new Color(0.62f, 0.75f, 0.88f);
        public Color ambientSky = new Color(0.75f, 0.8f, 0.9f);
        public Color ambientEquator = new Color(0.6f, 0.6f, 0.62f);
        public Color ambientGround = new Color(0.35f, 0.33f, 0.3f);
        public float sunIntensity = 1.3f;
    }

    public static string MapName(MapType map)
    {
        switch (map)
        {
            case MapType.Metro: return "STACJA METRA";
            case MapType.Airport: return "LOTNISKO";
            default: return "PARKING";
        }
    }

    static readonly Color Asphalt = new Color(0.24f, 0.25f, 0.27f);
    static readonly Color Paint = new Color(0.9f, 0.88f, 0.8f);
    static readonly Color Concrete = new Color(0.62f, 0.6f, 0.57f);
    static readonly Color DarkGlass = new Color(0.12f, 0.14f, 0.18f);
    static readonly Color Tyre = new Color(0.08f, 0.08f, 0.09f);
    static readonly Color Yellow = new Color(0.95f, 0.8f, 0.25f);

    static readonly Color[] CarColors =
    {
        new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.75f), new Color(0.9f, 0.9f, 0.9f),
        new Color(0.15f, 0.15f, 0.17f), new Color(0.95f, 0.75f, 0.2f), new Color(0.35f, 0.6f, 0.4f),
        new Color(0.6f, 0.62f, 0.65f), new Color(0.5f, 0.3f, 0.55f)
    };

    public static LevelInfo Build(MapType map, int seed)
    {
        var oldState = Random.state;
        Random.InitState(seed);
        ActivitySpot.All.Clear();

        var info = new LevelInfo { map = map };
        info.root = new GameObject("Level_" + map);
        var level = new GameObject("Walkable").transform;   // tylko to trafia do NavMesha
        level.SetParent(info.root.transform, false);
        var scenery = new GameObject("Scenery").transform;
        scenery.SetParent(info.root.transform, false);

        var candidates = new List<Pose>();
        switch (map)
        {
            case MapType.Metro: BuildMetro(info, level, scenery, candidates); break;
            case MapType.Airport: BuildAirport(info, level, scenery, candidates); break;
            default: BuildParking(info, level, scenery, candidates); break;
        }

        // --- NavMesh z samego obszaru gry ---
        var surface = level.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1 << 0;
        surface.BuildNavMesh();

        // --- Miejsca aktywnosci: tylko osiagalne ---
        for (int i = ActivitySpot.All.Count - 1; i >= 0; i--)
        {
            var spot = ActivitySpot.All[i];
            if (NavMesh.SamplePosition(spot.standPoint, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                spot.standPoint = hit.position;
            else
                ActivitySpot.All.RemoveAt(i);
        }
        // Kolejka zerwana w srodku = dalsze miejsca "przesuwaja sie" do przodu
        foreach (var spot in ActivitySpot.All)
            if (spot.nextInQueue != null && !ActivitySpot.All.Contains(spot.nextInQueue)) spot.nextInQueue = null;

        // --- Miejsca na walizki-cele: daleko od siebie, osiagalne ---
        Shuffle(candidates);
        foreach (var spot in candidates)
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

    // =====================================================================================
    // Wspolne elementy
    // =====================================================================================

    const float Half = GameConfig.LotHalfSize + 2f;

    static void BuildFloorAndWalls(Transform level, Transform scenery, Color floor, Color wall, Color outer, float wallH)
    {
        Solid("Ground", level, new Vector3(0f, -0.5f, 0f), new Vector3(Half * 2f, 1f, Half * 2f), floor, false);
        Solid("OuterGround", scenery, new Vector3(0f, -0.56f, 0f), new Vector3(400f, 1f, 400f), outer, false);
        Solid("Wall_N", level, new Vector3(0f, wallH / 2f, Half), new Vector3(Half * 2f + 0.5f, wallH, 0.5f), wall, true);
        Solid("Wall_S", level, new Vector3(0f, 0.6f, -Half), new Vector3(Half * 2f + 0.5f, 1.2f, 0.5f), wall, true);
        Solid("Wall_E", level, new Vector3(Half, wallH / 2f, 0f), new Vector3(0.5f, wallH, Half * 2f), wall, true);
        Solid("Wall_W", level, new Vector3(-Half, wallH / 2f, 0f), new Vector3(0.5f, wallH, Half * 2f), wall, true);
    }

    // Budynek na poludniu z pozycja snajpera na dachu
    static void BuildSniperBuilding(LevelInfo info, Transform scenery, Color facade, Color windows)
    {
        Vector3 bPos = new Vector3(0f, 11f, -Half - 12f);
        Solid("SniperBuilding", scenery, bPos, new Vector3(26f, 22f, 10f), facade, false);
        for (int fy = 0; fy < 6; fy++)
            for (int fx = -5; fx <= 5; fx++)
                LowPolyFactory.Box("Window", scenery, new Vector3(fx * 2.2f, 2.5f + fy * 3.3f, bPos.z + 5.02f), new Vector3(1.2f, 1.6f, 0.05f), windows);
        Solid("Parapet", scenery, new Vector3(0f, 22.4f, bPos.z + 4.8f), new Vector3(26f, 0.8f, 0.4f), Concrete, false);

        info.sniperPosition = new Vector3(0f, 23.6f, bPos.z + 5.6f);
        info.sniperRotation = Quaternion.LookRotation(new Vector3(0f, 0f, 4f) - info.sniperPosition);
    }

    static void BuildCity(Transform scenery, Color a, Color b)
    {
        for (int i = 0; i < 14; i++)
        {
            float ang = i / 14f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * Random.Range(75f, 95f);
            if (p.z < -40f && Mathf.Abs(p.x) < 30f) continue; // nie zaslaniaj budynku snajpera
            float h = Random.Range(8f, 30f);
            LowPolyFactory.Box("CityBlock", scenery, p + Vector3.up * h / 2f, new Vector3(Random.Range(10f, 20f), h, Random.Range(10f, 20f)), Color.Lerp(a, b, Random.value));
        }
    }

    static void FloorGrid(Transform level, float step, Color color)
    {
        for (float v = -Half + step; v < Half; v += step)
        {
            LowPolyFactory.Box("GridX", level, new Vector3(v, 0.005f, 0f), new Vector3(0.06f, 0.01f, Half * 2f), color);
            LowPolyFactory.Box("GridZ", level, new Vector3(0f, 0.005f, v), new Vector3(Half * 2f, 0.01f, 0.06f), color);
        }
    }

    // --- Rejestracja miejsc aktywnosci -------------------------------------------------------

    static void AddChatCircle(Vector3 center, int count)
    {
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, start + 360f / count * i, 0f) * Vector3.forward;
            ActivitySpot.Add(SpotKind.Chat, center + dir * 0.85f, -dir);
        }
    }

    // front = miejsce tuz przy okienku, back = kierunek, w ktorym rosnie kolejka
    static void AddQueue(Vector3 front, Vector3 back, int count)
    {
        back.y = 0f;
        back.Normalize();
        ActivitySpot prev = null;
        for (int i = 0; i < count; i++)
        {
            var s = ActivitySpot.Add(SpotKind.Queue, front + back * (0.95f * i), -back);
            s.nextInQueue = prev;
            prev = s;
        }
    }

    static void AddPeek(Vector3 stand, Vector3 lookAt)
    {
        ActivitySpot.Add(SpotKind.Peek, stand, lookAt - stand);
    }

    // =====================================================================================
    // Mapa 1: PARKING
    // =====================================================================================

    static void BuildParking(LevelInfo info, Transform level, Transform scenery, List<Pose> candidates)
    {
        BuildFloorAndWalls(level, scenery, Asphalt, Concrete, new Color(0.42f, 0.5f, 0.36f), 1.2f);
        Solid("Sidewalk", scenery, new Vector3(0f, -0.52f, 0f), new Vector3(Half * 2f + 8f, 1f, Half * 2f + 8f), Concrete, false);

        // Rzedy miejsc parkingowych
        float[] rows = { -30f, -18f, 18f, 30f };
        foreach (float rz in rows)
        {
            for (float x = -36f; x <= 36.01f; x += 3f)
            {
                LowPolyFactory.Box("Line", level, new Vector3(x - 1.5f, 0.01f, rz), new Vector3(0.12f, 0.02f, 5f), Paint);
                if (Random.value < 0.55f)
                {
                    Quaternion rot = Quaternion.Euler(0f, Random.value < 0.5f ? 0f : 180f, 0f);
                    Vector3 carPos = new Vector3(x + Random.Range(-0.2f, 0.2f), 0f, rz + Random.Range(-0.3f, 0.3f));
                    BuildCar(level, carPos, rot);
                    // Walizka moze stac przy przodzie albo tyle auta (od strony alejki)
                    float side = Random.value < 0.5f ? -1f : 1f;
                    candidates.Add(new Pose(new Vector3(x, 0f, rz + side * 2.9f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
                    // Zagladanie przez szybe (gdy obok jest wolne miejsce)
                    if (Random.value < 0.5f)
                    {
                        float sx = Random.value < 0.5f ? -1f : 1f;
                        AddPeek(carPos + new Vector3(sx * 1.45f, 0f, 0.3f), carPos + Vector3.up);
                    }
                }
            }
            LowPolyFactory.Box("Line", level, new Vector3(37.5f, 0.01f, rz), new Vector3(0.12f, 0.02f, 5f), Paint);
        }

        foreach (float lz in new[] { -24f, 0f, 24f })
            for (float x = -36f; x <= 36f; x += 6f)
                LowPolyFactory.Box("LaneMark", level, new Vector3(x, 0.01f, lz), new Vector3(2.5f, 0.02f, 0.15f), Yellow);

        // Plac: kiosk z kolejka, donice, lawki, grupki rozmawiajacych
        BuildKiosk(level, Vector3.zero, new Color(0.3f, 0.55f, 0.5f), "Kiosk");
        AddQueue(new Vector3(0f, 0f, 2.2f), Vector3.forward, 5);
        foreach (var p in new[] { new Vector3(-12f, 0f, -7f), new Vector3(12f, 0f, 7f), new Vector3(-24f, 0f, 6f), new Vector3(24f, 0f, -6f), new Vector3(-34f, 0f, -8f), new Vector3(34f, 0f, 8f) })
            BuildPlanter(level, p);
        foreach (var p in new[] { new Vector3(-7f, 0f, 9f), new Vector3(7f, 0f, -9f), new Vector3(-18f, 0f, -2f), new Vector3(18f, 0f, 2f) })
            candidates.Add(BuildBench(level, p, Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f)));
        foreach (var c in new[] { new Vector3(-6f, 0f, -4f), new Vector3(6f, 0f, 4f), new Vector3(-28f, 0f, -3f), new Vector3(28f, 0f, 3f) })
            AddChatCircle(c, Random.Range(3, 5));

        BuildVan(level, new Vector3(39f, 0f, 0f), -1f, new Color(0.93f, 0.93f, 0.9f), new Color(0.2f, 0.4f, 0.75f));
        BuildVan(level, new Vector3(-39f, 0f, 0f), 1f, new Color(0.93f, 0.93f, 0.9f), new Color(0.2f, 0.4f, 0.75f));

        foreach (float lz in new[] { -24f, 24f, -38f, 38f })
            for (float x = -30f; x <= 30f; x += 20f)
                BuildLamp(level, new Vector3(x, 0f, lz));

        BuildSniperBuilding(info, scenery, new Color(0.55f, 0.5f, 0.48f), DarkGlass);
        BuildCity(scenery, new Color(0.5f, 0.52f, 0.58f), new Color(0.75f, 0.68f, 0.6f));
    }

    // =====================================================================================
    // Mapa 2: STACJA METRA
    // =====================================================================================

    static void BuildMetro(LevelInfo info, Transform level, Transform scenery, List<Pose> candidates)
    {
        info.sky = new Color(0.16f, 0.17f, 0.2f);
        info.ambientSky = new Color(0.6f, 0.62f, 0.7f);
        info.ambientEquator = new Color(0.5f, 0.5f, 0.52f);
        info.ambientGround = new Color(0.3f, 0.3f, 0.32f);
        info.sunIntensity = 0.9f;

        Color tile = new Color(0.72f, 0.72f, 0.7f);
        Color wallTile = new Color(0.45f, 0.6f, 0.62f);
        BuildFloorAndWalls(level, scenery, tile, wallTile, new Color(0.12f, 0.12f, 0.14f), 3f);
        FloorGrid(level, 4f, new Color(0.6f, 0.6f, 0.58f));

        // Wysokie sciany hali (tlo)
        Solid("HallN", scenery, new Vector3(0f, 6f, Half + 3f), new Vector3(Half * 2f + 12f, 12f, 1f), wallTile, false);
        Solid("HallE", scenery, new Vector3(Half + 3f, 6f, 0f), new Vector3(1f, 12f, Half * 2f + 6f), wallTile, false);
        Solid("HallW", scenery, new Vector3(-Half - 3f, 6f, 0f), new Vector3(1f, 12f, Half * 2f + 6f), wallTile, false);

        // Peron: tory za barierka, pociag z otwartymi drzwiami = punkty odbioru walizek
        float fenceZ = 29.5f;
        LowPolyFactory.Box("EdgeLine", level, new Vector3(0f, 0.012f, fenceZ - 0.6f), new Vector3(Half * 2f, 0.02f, 0.3f), Yellow);
        foreach (var seg in new[] { new Vector2(-Half, -21.4f), new Vector2(-18.6f, 18.6f), new Vector2(21.4f, Half) })
        {
            float len = seg.y - seg.x;
            var fence = Solid("Fence", level, new Vector3((seg.x + seg.y) / 2f, 0.5f, fenceZ), new Vector3(len, 1f, 0.15f), new Color(0.55f, 0.57f, 0.6f), true);
            LowPolyFactory.Box("FenceTop", fence.transform.parent, new Vector3((seg.x + seg.y) / 2f, 1.02f, fenceZ), new Vector3(len, 0.06f, 0.2f), Yellow);
        }
        LowPolyFactory.Box("Trackbed", level, new Vector3(0f, 0.01f, 38f), new Vector3(Half * 2f, 0.02f, 8f), new Color(0.2f, 0.18f, 0.16f));
        foreach (float rz in new[] { 37.3f, 38.7f })
            LowPolyFactory.Box("Rail", level, new Vector3(0f, 0.08f, rz), new Vector3(Half * 2f, 0.12f, 0.1f), new Color(0.5f, 0.5f, 0.52f));

        var train = Group("Train", level, new Vector3(0f, 0f, 35f), Quaternion.identity, new Vector3(66f, 3.4f, 3.2f), 1.7f).transform;
        LowPolyFactory.Box("Body", train, new Vector3(0f, 1.9f, 0f), new Vector3(66f, 2.8f, 3.2f), new Color(0.85f, 0.85f, 0.82f));
        LowPolyFactory.Box("Stripe", train, new Vector3(0f, 1.2f, 0f), new Vector3(66.05f, 0.3f, 3.25f), new Color(0.9f, 0.2f, 0.2f));
        for (float x = -31f; x <= 31f; x += 3.5f)
            LowPolyFactory.Box("Window", train, new Vector3(x, 2.3f, -1.61f), new Vector3(2f, 0.9f, 0.05f), DarkGlass);
        foreach (float dx in new[] { -20f, 20f })
            LowPolyFactory.Box("OpenDoor", train, new Vector3(dx, 1.5f, -1.62f), new Vector3(2.2f, 2.6f, 0.05f), new Color(0.03f, 0.03f, 0.04f));
        BuildExtractionZone(level, new Vector3(-20f, 0f, 31.6f));
        BuildExtractionZone(level, new Vector3(20f, 0f, 31.6f));

        // Filary z tablicami informacyjnymi i lawkami
        foreach (float x in new[] { -30f, -18f, -6f, 6f, 18f, 30f })
            foreach (float z in new[] { -20f, -8f, 8f, 20f })
            {
                Vector3 p = new Vector3(x, 0f, z);
                Solid("Pillar", level, p + Vector3.up * 3.5f, new Vector3(1.2f, 7f, 1.2f), wallTile, true);
                LowPolyFactory.Box("Band", level, p + Vector3.up * 1.2f, new Vector3(1.22f, 0.2f, 1.22f), Yellow);
                float r = Random.value;
                if (r < 0.35f)
                {
                    LowPolyFactory.Box("MapBoard", level, p + new Vector3(0f, 1.6f, 0.62f), new Vector3(0.9f, 0.9f, 0.04f), new Color(0.2f, 0.35f, 0.7f));
                    AddPeek(p + new Vector3(0f, 0f, 1.4f), p + new Vector3(0f, 1.6f, 0.6f));
                }
                else if (r < 0.7f)
                {
                    candidates.Add(BuildBench(level, p + new Vector3(0f, 0f, -1.35f), Quaternion.Euler(0f, 180f, 0f)));
                }
                candidates.Add(new Pose(p + new Vector3(Random.value < 0.5f ? -1.2f : 1.2f, 0f, 0f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
            }

        // Kiosk z gazetami + biletomaty z kolejkami
        BuildKiosk(level, Vector3.zero, new Color(0.25f, 0.4f, 0.65f), "Newsstand");
        AddQueue(new Vector3(0f, 0f, 2.2f), Vector3.forward, 4);
        foreach (float x in new[] { -5f, -2.5f, 2.5f, 5f })
        {
            var m = Group("TicketMachine", level, new Vector3(x, 0f, -32f), Quaternion.identity, new Vector3(1.2f, 1.8f, 0.8f), 0.9f).transform;
            LowPolyFactory.Box("Case", m, new Vector3(0f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 0.8f), new Color(0.9f, 0.5f, 0.15f));
            LowPolyFactory.Box("Screen", m, new Vector3(0f, 1.3f, 0.41f), new Vector3(0.7f, 0.45f, 0.02f), new Color(0.4f, 0.8f, 1f), true);
            AddQueue(new Vector3(x, 0f, -30.9f), Vector3.forward, 3);
        }

        // Schody ruchome
        foreach (float x in new[] { -24f, 24f })
        {
            var st = Group("Escalator", level, new Vector3(x, 0f, -32f), Quaternion.identity, new Vector3(4f, 3.5f, 8f), 1.75f).transform;
            LowPolyFactory.Box("Side", st, new Vector3(0f, 1.75f, 0f), new Vector3(4f, 3.5f, 8f), new Color(0.5f, 0.52f, 0.55f));
            var steps = LowPolyFactory.Box("Steps", st, new Vector3(0f, 2.2f, 1.5f), new Vector3(3.2f, 0.2f, 7f), new Color(0.25f, 0.25f, 0.27f));
            steps.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        }

        foreach (var c in new[] { new Vector3(-12f, 0f, 0f), new Vector3(12f, 0f, 0f), new Vector3(-24f, 0f, 14f), new Vector3(24f, 0f, -14f), new Vector3(0f, 0f, 22f), new Vector3(0f, 0f, -20f) })
            AddChatCircle(c, Random.Range(3, 5));

        // Swietlowki pod sufitem
        foreach (float z in new[] { -26f, -14f, -2f, 14f, 26f })
            LowPolyFactory.Box("LightStrip", scenery, new Vector3(0f, 9f, z), new Vector3(70f, 0.15f, 0.4f), new Color(1f, 0.97f, 0.88f), true);

        BuildSniperBuilding(info, scenery, new Color(0.35f, 0.42f, 0.45f), new Color(0.9f, 0.85f, 0.6f));
    }

    // =====================================================================================
    // Mapa 3: LOTNISKO - hala przylotow z tasmami bagazowymi
    // =====================================================================================

    static void BuildAirport(LevelInfo info, Transform level, Transform scenery, List<Pose> candidates)
    {
        info.sky = new Color(0.7f, 0.82f, 0.95f);
        info.ambientSky = new Color(0.85f, 0.88f, 0.95f);
        info.sunIntensity = 1.4f;

        Color floor = new Color(0.86f, 0.83f, 0.76f);
        Color frame = new Color(0.55f, 0.58f, 0.62f);
        BuildFloorAndWalls(level, scenery, floor, frame, new Color(0.45f, 0.47f, 0.45f), 1.2f);
        FloorGrid(level, 6f, new Color(0.78f, 0.75f, 0.68f));

        // Szklana fasada terminalu (tlo) + samolot za szyba
        Color glass = new Color(0.55f, 0.72f, 0.85f);
        foreach (var w in new[] { new Vector4(0f, Half + 2f, Half * 2f + 8f, 1f), new Vector4(Half + 2f, 0f, 1f, Half * 2f + 4f), new Vector4(-Half - 2f, 0f, 1f, Half * 2f + 4f) })
        {
            Solid("Facade", scenery, new Vector3(w.x, 5f, w.y), new Vector3(w.z, 10f, w.w), glass, false);
            Solid("FacadeTop", scenery, new Vector3(w.x, 10.3f, w.y), new Vector3(w.z + 0.2f, 0.6f, w.w + 0.2f), frame, false);
        }
        var plane = new GameObject("Plane").transform;
        plane.SetParent(scenery, false);
        plane.localPosition = new Vector3(10f, 0f, 75f);
        plane.localRotation = Quaternion.Euler(0f, 80f, 0f);
        Color white = new Color(0.95f, 0.95f, 0.95f);
        var fus = LowPolyFactory.Blob("Fuselage", plane, new Vector3(0f, 4f, 0f), 2.5f, 30f, white, false, 12);
        fus.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        LowPolyFactory.Box("Wings", plane, new Vector3(0f, 3.2f, 1f), new Vector3(36f, 0.4f, 5f), white);
        LowPolyFactory.Box("Tail", plane, new Vector3(0f, 8f, -15f), new Vector3(0.5f, 7f, 4f), new Color(0.2f, 0.4f, 0.8f));

        // Tasmy bagazowe z walizkami podroznych
        foreach (float x in new[] { -16f, 16f })
        {
            Vector3 c = new Vector3(x, 0f, 6f);
            var belt = Group("Carousel", level, c, Quaternion.identity, new Vector3(11f, 0.55f, 5f), 0.275f);
            var conveyor = belt.AddComponent<ConveyorBelt>();
            conveyor.halfSize = new Vector2(4.6f, 1.9f);
            LowPolyFactory.Box("Base", belt.transform, new Vector3(0f, 0.25f, 0f), new Vector3(11f, 0.5f, 5f), new Color(0.35f, 0.37f, 0.4f));
            LowPolyFactory.Box("Belt", belt.transform, new Vector3(0f, 0.52f, 0f), new Vector3(10.6f, 0.05f, 4.6f), new Color(0.12f, 0.12f, 0.13f));
            var island = new GameObject("Island");
            island.transform.SetParent(belt.transform, false);
            island.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            island.AddComponent<BoxCollider>().size = new Vector3(8f, 0.5f, 2.2f);
            LowPolyFactory.Box("IslandVis", belt.transform, new Vector3(0f, 0.8f, 0f), new Vector3(8f, 0.5f, 2.2f), new Color(0.6f, 0.62f, 0.65f));
            // Tablica "BAGAZ" na slupkach
            LowPolyFactory.Box("SignPole", belt.transform, new Vector3(0f, 2f, 0f), new Vector3(0.15f, 2.4f, 0.15f), frame);
            LowPolyFactory.Box("Sign", belt.transform, new Vector3(0f, 3.4f, 0f), new Vector3(2.4f, 0.6f, 0.15f), Yellow);

            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Vector3 tangent = new Vector3(-Mathf.Sin(a) * 4.6f, 0f, Mathf.Cos(a) * 1.9f);
                info.carouselSpots.Add(new Pose(c + new Vector3(Mathf.Cos(a) * 4.6f, 0.6f, Mathf.Sin(a) * 1.9f),
                    Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 90f, 0f)));
            }
            // Ludzie czekajacy na bagaz = "zagladanie" na tasme
            for (int i = 0; i < 4; i++)
            {
                Vector3 stand = c + new Vector3(Random.Range(-4.5f, 4.5f), 0f, (i % 2 == 0 ? 1f : -1f) * 3.3f);
                AddPeek(stand, new Vector3(stand.x, 0.5f, c.z));
            }
            candidates.Add(new Pose(c + new Vector3(Random.Range(-6f, 6f), 0f, -3.4f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
        }

        // Stanowiska odpraw z kolejkami
        foreach (float x in new[] { -27f, -19f, -11f, 11f, 19f, 27f })
        {
            var desk = Group("CheckIn", level, new Vector3(x, 0f, -24f), Quaternion.identity, new Vector3(3f, 1.1f, 1f), 0.55f).transform;
            LowPolyFactory.Box("Desk", desk, new Vector3(0f, 0.55f, 0f), new Vector3(3f, 1.1f, 1f), new Color(0.3f, 0.45f, 0.7f));
            LowPolyFactory.Box("Screen", desk, new Vector3(0f, 2.2f, -0.3f), new Vector3(1.6f, 0.5f, 0.1f), new Color(0.3f, 0.7f, 1f), true);
            LowPolyFactory.Box("ScreenPole", desk, new Vector3(0f, 1.5f, -0.3f), new Vector3(0.1f, 1.2f, 0.1f), frame);
            AddQueue(new Vector3(x, 0f, -22.6f), Vector3.forward, 4);
            candidates.Add(new Pose(new Vector3(x + 2.2f, 0f, -23f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
        }

        // Tablice odlotow
        foreach (float x in new[] { -6f, 6f })
        {
            var board = Group("DeparturesBoard", level, new Vector3(x, 0f, -8f), Quaternion.identity, new Vector3(3.6f, 3.5f, 0.4f), 1.75f).transform;
            LowPolyFactory.Box("Legs", board, new Vector3(0f, 1f, 0f), new Vector3(0.2f, 2f, 0.2f), frame);
            LowPolyFactory.Box("Board", board, new Vector3(0f, 2.6f, 0f), new Vector3(3.6f, 1.6f, 0.2f), new Color(0.08f, 0.08f, 0.1f));
            for (int i = 0; i < 5; i++)
                LowPolyFactory.Box("Row", board, new Vector3(0f, 3.1f - i * 0.25f, 0.11f), new Vector3(3.2f, 0.1f, 0.01f), new Color(1f, 0.8f, 0.2f), true);
            for (int i = 0; i < 3; i++)
                AddPeek(new Vector3(x - 1f + i, 0f, -6.2f), new Vector3(x, 2.6f, -8f));
        }

        // Rzedy siedzen w poczekalni
        foreach (float z in new[] { 22f, 27f })
            for (float x = -30f; x <= 30f; x += 5f)
            {
                if (Mathf.Abs(x) < 3f) continue;
                candidates.Add(BuildBench(level, new Vector3(x, 0f, z), Quaternion.Euler(0f, z < 25f ? 0f : 180f, 0f)));
            }

        // Filary i donice
        foreach (var p in new[] { new Vector3(-30f, 0f, -12f), new Vector3(30f, 0f, -12f), new Vector3(-30f, 0f, 14f), new Vector3(30f, 0f, 14f), new Vector3(0f, 0f, 14f) })
            Solid("Pillar", level, p + Vector3.up * 5f, new Vector3(1.4f, 10f, 1.4f), frame, true);
        foreach (var p in new[] { new Vector3(-8f, 0f, 18f), new Vector3(8f, 0f, 18f), new Vector3(-36f, 0f, -34f), new Vector3(36f, 0f, -34f) })
            BuildPlanter(level, p);

        foreach (var c in new[] { new Vector3(0f, 0f, 0f), new Vector3(-22f, 0f, -12f), new Vector3(22f, 0f, -12f), new Vector3(-12f, 0f, 30f), new Vector3(12f, 0f, 34f), new Vector3(0f, 0f, -34f) })
            AddChatCircle(c, Random.Range(3, 5));

        // Taksowki przy wyjsciach
        BuildVan(level, new Vector3(39f, 0f, 0f), -1f, new Color(0.98f, 0.8f, 0.15f), new Color(0.1f, 0.1f, 0.1f));
        BuildVan(level, new Vector3(-39f, 0f, 0f), 1f, new Color(0.98f, 0.8f, 0.15f), new Color(0.1f, 0.1f, 0.1f));

        BuildSniperBuilding(info, scenery, new Color(0.7f, 0.72f, 0.75f), new Color(0.45f, 0.62f, 0.78f));
    }

    // =====================================================================================
    // Elementy
    // =====================================================================================

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

    // Furgonetka z otwartymi tylnymi drzwiami. inward = kierunek (po osi X) do srodka mapy.
    static void BuildVan(Transform parent, Vector3 pos, float inward, Color paint, Color stripe)
    {
        Quaternion rot = Quaternion.Euler(0f, inward > 0f ? 90f : -90f, 0f); // lokalne +Z = do srodka
        var van = Group("Van", parent, pos, rot, new Vector3(2.2f, 2.4f, 5f), 1.2f).transform;
        Color white = paint;
        LowPolyFactory.Box("Body", van, new Vector3(0f, 1.35f, -0.4f), new Vector3(2.2f, 2.1f, 4.2f), white);
        LowPolyFactory.Box("Hood", van, new Vector3(0f, 0.85f, -2.3f), new Vector3(2.1f, 1.1f, 0.8f), white);
        LowPolyFactory.Box("Windshield", van, new Vector3(0f, 1.75f, -2.52f), new Vector3(1.9f, 0.7f, 0.05f), DarkGlass);
        LowPolyFactory.Box("Stripe", van, new Vector3(0f, 1.2f, -0.4f), new Vector3(2.22f, 0.25f, 4.22f), stripe);
        LowPolyFactory.Box("Interior", van, new Vector3(0f, 1.4f, 1.71f), new Vector3(1.9f, 1.8f, 0.02f), new Color(0.05f, 0.05f, 0.06f));
        // Otwarte drzwi
        var doorL = LowPolyFactory.Box("DoorL", van, new Vector3(-1.45f, 1.35f, 2.2f), new Vector3(0.05f, 1.9f, 1.05f), white);
        doorL.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
        var doorR = LowPolyFactory.Box("DoorR", van, new Vector3(1.45f, 1.35f, 2.2f), new Vector3(0.05f, 1.9f, 1.05f), white);
        doorR.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1.7f, 1.2f })
                LowPolyFactory.Box("Wheel", van, new Vector3(sx, 0.35f, sz), new Vector3(0.3f, 0.7f, 0.7f), Tyre);

        // Strefa dostawy tuz za tylnymi drzwiami
        BuildExtractionZone(parent, pos + new Vector3(inward * 4.4f, 0f, 0f));
    }

    // Zielona strefa + slup swiatla widoczny z daleka
    static void BuildExtractionZone(Transform parent, Vector3 pos)
    {
        var zoneGo = new GameObject("ExtractionZone");
        zoneGo.transform.SetParent(parent, false);
        zoneGo.transform.position = pos;
        Color green = new Color(0.3f, 1f, 0.45f);
        LowPolyFactory.Box("Pad", zoneGo.transform, new Vector3(0f, 0.015f, 0f), new Vector3(3.4f, 0.02f, 3.4f), green, true);
        var beam = LowPolyFactory.Box("Beam", zoneGo.transform, new Vector3(0f, 7f, 0f), new Vector3(0.35f, 14f, 0.35f), green, true);
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var zone = zoneGo.AddComponent<ExtractionZone>();
        zone.beam = beam.transform;
    }

    static void BuildKiosk(Transform parent, Vector3 pos, Color color, string name)
    {
        var k = Group(name, parent, pos, Quaternion.identity, new Vector3(4f, 3f, 3f), 1.5f).transform;
        LowPolyFactory.Box("Base", k, new Vector3(0f, 1.25f, 0f), new Vector3(4f, 2.5f, 3f), color);
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

    // Lawka: collider tylko do wysokosci siedziska, zeby dalo sie na niej usiasc.
    // Rejestruje 2 miejsca do siedzenia i zwraca pozycje obok (na walizke).
    static Pose BuildBench(Transform parent, Vector3 pos, Quaternion rot)
    {
        var b = Group("Bench", parent, pos, rot, new Vector3(2f, 0.45f, 0.5f), 0.225f).transform;
        Color wood = new Color(0.55f, 0.38f, 0.22f);
        LowPolyFactory.Box("Seat", b, new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.1f, 0.5f), wood);
        LowPolyFactory.Box("Back", b, new Vector3(0f, 0.8f, -0.22f), new Vector3(2f, 0.5f, 0.08f), wood);
        LowPolyFactory.Box("LegL", b, new Vector3(-0.8f, 0.22f, 0f), new Vector3(0.1f, 0.45f, 0.45f), Tyre);
        LowPolyFactory.Box("LegR", b, new Vector3(0.8f, 0.22f, 0f), new Vector3(0.1f, 0.45f, 0.45f), Tyre);

        Vector3 fwd = rot * Vector3.forward;
        foreach (float sx in new[] { -0.5f, 0.5f })
        {
            Vector3 seat = pos + rot * new Vector3(sx, 0f, 0f);
            Vector3 stand = seat + fwd * 0.85f;
            ActivitySpot.Add(SpotKind.Bench, stand, fwd, seat - stand);
        }
        return new Pose(pos + rot * new Vector3(1.5f, 0f, 0.2f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
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
