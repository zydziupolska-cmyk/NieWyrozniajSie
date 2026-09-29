using System.Collections.Generic;
using UnityEngine;

// Generuje kanciaste (flat-shaded) siatki i wspoldzielone materialy.
// Wszystko jest cache'owane, wiec 500 botow korzysta z tych samych assetow.
public static class LowPolyFactory
{
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    static readonly Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();
    static Shader cachedShader;

    // Kapsula wzdluz osi Y, wysrodkowana w (0,0,0).
    // length = odleglosc miedzy srodkami polkul (0 = kula).
    // sx / sz = splaszczenie przekroju (np. szerszy niz glebszy tulow).
    public static Mesh Capsule(float radius, float length, float sx = 1f, float sz = 1f, int sides = 8, int capRings = 2)
    {
        string key = $"{radius:F3}_{length:F3}_{sx:F2}_{sz:F2}_{sides}_{capRings}";
        if (meshCache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        float half = length * 0.5f;
        bool isSphere = length <= 0.0001f;

        // Pierscienie od dolu do gory: (wysokosc, promien)
        var rings = new List<Vector2>();
        for (int i = 1; i <= capRings; i++)
        {
            float lat = (-90f + 90f * i / capRings) * Mathf.Deg2Rad;
            rings.Add(new Vector2(Mathf.Sin(lat) * radius - half, Mathf.Cos(lat) * radius));
        }
        for (int i = isSphere ? 1 : 0; i < capRings; i++)
        {
            float lat = (90f * i / capRings) * Mathf.Deg2Rad;
            rings.Add(new Vector2(Mathf.Sin(lat) * radius + half, Mathf.Cos(lat) * radius));
        }

        Vector3 bottomPole = new Vector3(0f, -half - radius, 0f);
        Vector3 topPole = new Vector3(0f, half + radius, 0f);
        float step = Mathf.PI * 2f / sides;

        Vector3 P(int ring, int s)
        {
            // Przesuniecie o pol kroku -> plaska sciana patrzy do przodu (+Z)
            float a = step * s + step * 0.5f;
            Vector2 r = rings[ring];
            return new Vector3(Mathf.Sin(a) * r.y * sx, r.x, Mathf.Cos(a) * r.y * sz);
        }

        var verts = new List<Vector3>();

        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            Vector3 centroid = (a + b + c) / 3f;
            Vector3 axisPoint = new Vector3(0f, Mathf.Clamp(centroid.y, -half, half), 0f);
            if (Vector3.Dot(n, centroid - axisPoint) < 0f) { Vector3 t = b; b = c; c = t; }
            verts.Add(a); verts.Add(b); verts.Add(c);
        }

        int last = rings.Count - 1;
        for (int s = 0; s < sides; s++)
        {
            Tri(bottomPole, P(0, s), P(0, s + 1));
            for (int r = 0; r < last; r++)
            {
                Vector3 a = P(r, s), b = P(r, s + 1), c = P(r + 1, s + 1), d = P(r + 1, s);
                Tri(a, b, c);
                Tri(a, c, d);
            }
            Tri(topPole, P(last, s + 1), P(last, s));
        }

        // Kazdy trojkat ma wlasne wierzcholki -> twarde krawedzie (low-poly)
        var tris = new int[verts.Count];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;

        var mesh = new Mesh { name = "LowPolyCapsule_" + key };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshCache[key] = mesh;
        return mesh;
    }

    // Matowy, "plastelinowy" material jak w Human Fall Flat
    public static Material GetMaterial(Color color)
    {
        if (materialCache.TryGetValue(color, out Material cached) && cached != null) return cached;

        if (cachedShader == null)
        {
            cachedShader = Shader.Find("Universal Render Pipeline/Lit");
            if (cachedShader == null) cachedShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (cachedShader == null) cachedShader = Shader.Find("Standard");
        }

        var mat = new Material(cachedShader) { name = "Clay_" + ColorUtility.ToHtmlStringRGB(color) };
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.15f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        mat.enableInstancing = true;
        materialCache[color] = mat;
        return mat;
    }
}
