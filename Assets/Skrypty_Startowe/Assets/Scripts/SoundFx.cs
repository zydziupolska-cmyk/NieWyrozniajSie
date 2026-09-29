using UnityEngine;

// Proceduralnie generowane dzwieki - prototyp nie potrzebuje zadnych plikow audio.
public static class SoundFx
{
    const int Rate = 44100;
    static AudioClip gunshot, crack, reload, steal, click;

    public static AudioClip Gunshot => gunshot != null ? gunshot : (gunshot = MakeGunshot());
    public static AudioClip Crack => crack != null ? crack : (crack = MakeCrack());
    public static AudioClip Reload => reload != null ? reload : (reload = MakeReload());
    public static AudioClip Steal => steal != null ? steal : (steal = MakeSteal());
    public static AudioClip Click => click != null ? click : (click = MakeClick());

    // 3D w swiecie gry (glosnosc zalezy od odleglosci od kamery)
    public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float minDistance = 8f)
    {
        var go = new GameObject("Sfx_" + clip.name);
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.minDistance = minDistance;
        src.maxDistance = 200f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    // Dzwiek "w glowie" gracza (UI, bron snajpera)
    public static void Play2D(AudioClip clip, float volume = 1f)
    {
        var go = new GameObject("Sfx2D_" + clip.name);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 0f;
        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    static AudioClip Make(string name, float seconds, System.Func<float, System.Random, float> f)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(name.GetHashCode());
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, rng), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

    static AudioClip MakeGunshot()
    {
        float lp = 0f;
        return Make("Gunshot", 1.2f, (t, r) =>
        {
            lp = Mathf.Lerp(lp, Noise(r), 0.25f);
            float boom = Mathf.Sin(t * 2f * Mathf.PI * (70f - t * 30f)) * Mathf.Exp(-t * 7f);
            return (lp * Mathf.Exp(-t * 9f) * 1.4f + boom * 0.8f + Noise(r) * Mathf.Exp(-t * 60f)) * 0.9f;
        });
    }

    static AudioClip MakeCrack()
    {
        return Make("Crack", 0.25f, (t, r) => Noise(r) * Mathf.Exp(-t * 35f));
    }

    static AudioClip MakeReload()
    {
        return Make("Reload", 0.7f, (t, r) =>
        {
            float a = t < 0.08f ? Noise(r) * Mathf.Exp(-t * 60f) : 0f;
            float b = t > 0.35f && t < 0.45f ? Noise(r) * Mathf.Exp(-(t - 0.35f) * 50f) : 0f;
            return (a + b) * 0.7f;
        });
    }

    static AudioClip MakeSteal()
    {
        return Make("Steal", 0.45f, (t, r) =>
        {
            float freq = t < 0.15f ? 660f : 990f;
            return Mathf.Sin(t * 2f * Mathf.PI * freq) * Mathf.Exp(-(t % 0.15f) * 10f) * 0.4f;
        });
    }

    static AudioClip MakeClick()
    {
        return Make("Click", 0.05f, (t, r) => Noise(r) * Mathf.Exp(-t * 120f) * 0.5f);
    }
}
