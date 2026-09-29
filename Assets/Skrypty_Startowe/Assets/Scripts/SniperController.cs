using UnityEngine;
using UnityEngine.InputSystem;

// Snajper sterowany przez gracza (widok FPP z dachu).
// Mysz - rozgladanie, PPM - luneta, kolko - zmiana przyblizenia,
// LPM - strzal, SHIFT (w lunecie) - wstrzymanie oddechu.
public class SniperController : MonoBehaviour
{
    public Camera sniperCamera;
    public int ammo = GameConfig.PlayerSniperAmmo;
    public float sensitivity = 0.08f;
    public float boltTime = 1.5f;
    public float normalFOV = 60f;
    public float[] zoomFOVs = { 15f, 7f };

    public bool IsScoped { get; private set; }
    public bool IsReloading => boltTimer > 0f;
    public float Breath => breath;
    public int ZoomLevel => zoomIndex;

    private float pitch = 20f;
    private float yaw = 0f;
    private float baseYaw;
    private float currentFOV;
    private int zoomIndex;
    private float boltTimer;
    private float breath = 1f;
    private Texture2D scopeTexture;

    void OnEnable()
    {
        if (sniperCamera == null) sniperCamera = GetComponent<Camera>();
        Vector3 e = transform.eulerAngles;
        yaw = baseYaw = e.y;
        pitch = e.x > 180f ? e.x - 360f : e.x;
        currentFOV = normalFOV;
        boltTimer = 0f;
        breath = 1f;
    }

    void OnDisable()
    {
        if (sniperCamera != null) sniperCamera.fieldOfView = normalFOV;
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        HandleAiming();
        HandleShooting();
    }

    void HandleAiming()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        IsScoped = mouse.rightButton.isPressed;
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0.01f) zoomIndex = Mathf.Min(zoomIndex + 1, zoomFOVs.Length - 1);
        if (scroll < -0.01f) zoomIndex = Mathf.Max(zoomIndex - 1, 0);

        float targetFOV = IsScoped ? zoomFOVs[zoomIndex] : normalFOV;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * 12f);
        sniperCamera.fieldOfView = currentFOV;

        // Czulosc maleje razem z przyblizeniem
        float sens = sensitivity * (currentFOV / normalFOV);
        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * sens;
        pitch -= delta.y * sens;
        pitch = Mathf.Clamp(pitch, 3f, 80f);
        yaw = Mathf.Clamp(yaw, baseYaw - 80f, baseYaw + 80f);

        // Oddech: w lunecie celownik lekko plywa, SHIFT go uspokaja na chwile
        bool holding = IsScoped && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && breath > 0f;
        breath = holding ? Mathf.Max(0f, breath - Time.deltaTime / 4f) : Mathf.Min(1f, breath + Time.deltaTime / 6f);
        float sway = IsScoped && !holding ? (currentFOV / normalFOV) * 1.2f + 0.08f : 0f;
        float swayX = (Mathf.PerlinNoise(Time.time * 0.6f, 1.3f) - 0.5f) * sway;
        float swayY = (Mathf.PerlinNoise(4.1f, Time.time * 0.6f) - 0.5f) * sway;

        transform.rotation = Quaternion.Euler(pitch + swayY, yaw + swayX, 0f);
    }

    void HandleShooting()
    {
        if (boltTimer > 0f)
        {
            boltTimer -= Time.deltaTime;
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        if (ammo <= 0)
        {
            SoundFx.Play2D(SoundFx.Click);
            return;
        }

        ammo--;
        SniperShot.Fire(transform.position, transform.forward);
        if (ammo > 0)
        {
            boltTimer = boltTime;
            SoundFx.Play2D(SoundFx.Reload, 0.8f);
        }
    }

    void OnGUI()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        GUI.depth = 10; // pod HUD-em

        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;
        bool scopeVisible = IsScoped && currentFOV < normalFOV * 0.6f;

        if (scopeVisible)
        {
            if (scopeTexture == null) scopeTexture = MakeScopeTexture(512);
            float size = Screen.height;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - size / 2f, 0f, size, size), scopeTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, cx - size / 2f + 1f, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size / 2f - 1f, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Krzyz lunety z przerwa na srodku
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            float t = 2f, gap = 12f, len = size * 0.45f;
            GUI.DrawTexture(new Rect(cx - len, cy - t / 2, len - gap, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + gap, cy - t / 2, len - gap, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - t / 2, cy + gap, t, len - gap), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - t / 2, cy - len, t, len - gap), Texture2D.whiteTexture);
            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Texture2D.whiteTexture);
        }
        else
        {
            GUI.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            float size = 22f, t = 2f;
            GUI.DrawTexture(new Rect(cx - size / 2, cy - t / 2, size, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - t / 2, cy - size / 2, t, size), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    static Texture2D MakeScopeTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float r = size * 0.47f, c = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01((d - r) / 6f);
                float vignette = Mathf.Clamp01((d - r * 0.8f) / (r * 0.2f)) * 0.35f;
                px[y * size + x] = new Color32(0, 0, 0, (byte)(Mathf.Max(a, vignette) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
