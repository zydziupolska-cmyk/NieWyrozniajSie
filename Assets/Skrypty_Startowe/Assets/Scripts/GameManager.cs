using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Glowny kontroler prototypu: menu, start rundy, zasady wygranej, HUD.
// Scena potrzebuje tylko jednego obiektu z tym skryptem - reszta powstaje w runtime.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum Mode { Spy, Sniper }
    public enum State { Menu, Playing, Paused, GameOver }

    [Range(30, 400)] public int botCount = 120;

    public State CurrentState { get; private set; }
    public Mode CurrentMode { get; private set; }
    public bool IsPlaying => CurrentState == State.Playing;

    Camera cam;
    ThirdPersonCamera tpc;
    SniperController sniper;
    LevelBuilder.LevelInfo level;
    GameObject roundRoot;

    AISniper aiSniper;
    CrowdMember playerSpy;
    SpyController playerSpyCtrl;
    readonly List<CrowdMember> spies = new List<CrowdMember>();

    int suitcasesTotal;
    int suitcasesStolen;
    float timeLeft;
    float endAt = -1f;
    bool spiesWon;
    string endReason = "";
    float menuOrbit;

    struct Message { public string text; public Color color; public float until; }
    readonly List<Message> messages = new List<Message>();

    static readonly Color Sky = new Color(0.62f, 0.75f, 0.88f);

    // ------------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Time.timeScale = 1f;

        SetupEnvironment();
        SetupCamera();
        level = LevelBuilder.Build(Random.Range(0, 100000));
        EnterMenu();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }

    void SetupEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.8f, 0.9f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.6f, 0.62f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.3f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Sky;
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance = 260f;

        Light sun = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;
        cam.fieldOfView = 60f;

        tpc = cam.GetComponent<ThirdPersonCamera>();
        if (tpc == null) tpc = cam.gameObject.AddComponent<ThirdPersonCamera>();
        tpc.enabled = false;
        sniper = cam.GetComponent<SniperController>();
        if (sniper == null) sniper = cam.gameObject.AddComponent<SniperController>();
        sniper.sniperCamera = cam;
        sniper.enabled = false;
    }

    // --- Przebieg gry ------------------------------------------------------------------

    void EnterMenu()
    {
        ClearRound();
        Time.timeScale = 1f;
        CurrentState = State.Menu;
        tpc.enabled = false;
        sniper.enabled = false;
        cam.fieldOfView = 60f;

        // Tlo menu: sam tlum spacerujacy po parkingu
        int n = Mathf.Min(botCount, 80);
        for (int i = 0; i < n; i++) SpawnBot();
    }

    void StartRound(Mode mode)
    {
        ClearRound();
        Time.timeScale = 1f;
        CurrentMode = mode;
        CrowdMember.PanicUntil = 0f;
        suitcasesStolen = 0;
        endAt = -1f;
        timeLeft = GameConfig.RoundTime;
        messages.Clear();

        foreach (var spot in level.suitcaseSpots)
            Suitcase.Create(spot.position, spot.rotation, roundRoot.transform);
        suitcasesTotal = level.suitcaseSpots.Count;

        for (int i = 0; i < botCount; i++) SpawnBot();

        if (mode == Mode.Spy)
        {
            playerSpy = SpawnPlayerSpy();
            spies.Add(playerSpy);

            var sniperGo = new GameObject("AISniper");
            sniperGo.transform.SetParent(roundRoot.transform, false);
            sniperGo.transform.SetPositionAndRotation(level.sniperPosition, level.sniperRotation);
            aiSniper = sniperGo.AddComponent<AISniper>();

            sniper.enabled = false;
            cam.fieldOfView = 60f;
            tpc.target = playerSpy.transform;
            tpc.enabled = true;
            ShowMessage("Ukradnij " + suitcasesTotal + " walizki. Trzymaj SHIFT, żeby udawać bota!", Color.white, 5f);
        }
        else
        {
            for (int i = 0; i < GameConfig.AISpyCount; i++) spies.Add(SpawnAISpy());

            tpc.enabled = false;
            cam.transform.SetPositionAndRotation(level.sniperPosition, level.sniperRotation);
            sniper.enabled = true;
            sniper.ammo = GameConfig.PlayerSniperAmmo;
            ShowMessage("W tłumie ukrywa się " + GameConfig.AISpyCount + " szpiegów. Znajdź ich, zanim ukradną walizki!", Color.white, 5f);
        }

        CurrentState = State.Playing;
    }

    void ClearRound()
    {
        if (roundRoot != null) Destroy(roundRoot);
        roundRoot = new GameObject("Round");
        spies.Clear();
        playerSpy = null;
        playerSpyCtrl = null;
        aiSniper = null;
    }

    Vector3 RandomSpawnPoint(float minDistFromSuitcases)
    {
        for (int i = 0; i < 20; i++)
        {
            GameConfig.RandomNavPoint(Vector3.zero, GameConfig.LotHalfSize - 2f, out Vector3 p);
            bool ok = true;
            foreach (var s in level.suitcaseSpots)
                if (Vector3.Distance(s.position, p) < minDistFromSuitcases) { ok = false; break; }
            if (ok) return p;
        }
        GameConfig.RandomNavPoint(Vector3.zero, GameConfig.LotHalfSize - 2f, out Vector3 fallback);
        return fallback;
    }

    GameObject NewCharacterObject(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(roundRoot.transform, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f));
        return go;
    }

    CrowdMember SpawnBot()
    {
        var go = NewCharacterObject("Bot", RandomSpawnPoint(0f));
        go.AddComponent<NavMeshAgent>();
        var m = go.AddComponent<CrowdMember>();
        go.AddComponent<BotAI>();
        return m;
    }

    CrowdMember SpawnAISpy()
    {
        var go = NewCharacterObject("Bot", RandomSpawnPoint(12f)); // ta sama nazwa co boty - bez podpowiedzi w hierarchii
        go.AddComponent<NavMeshAgent>();
        var m = go.AddComponent<CrowdMember>();
        m.isSpy = true;
        go.AddComponent<AISpy>();
        return m;
    }

    CrowdMember SpawnPlayerSpy()
    {
        var go = NewCharacterObject("PlayerSpy", RandomSpawnPoint(12f));
        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.03f;
        var m = go.AddComponent<CrowdMember>();
        m.isSpy = true;
        m.isPlayer = true;
        playerSpyCtrl = go.AddComponent<SpyController>();
        playerSpyCtrl.CameraTransform = cam.transform;
        return m;
    }

    // --- Zdarzenia z rozgrywki -------------------------------------------------------------

    public void OnCharacterKilled(CrowdMember m)
    {
        if (CurrentState != State.Playing) return;

        if (m.isSpy)
        {
            if (m.isPlayer)
            {
                ShowMessage("ZOSTAŁEŚ ZDEMASKOWANY!", new Color(1f, 0.3f, 0.3f), 4f);
                tpc.target = m.Animator.TorsoTransform;
            }
            else
            {
                ShowMessage("Szpieg wyeliminowany! (" + DeadSpies() + "/" + spies.Count + ")", new Color(0.4f, 1f, 0.4f), 4f);
            }
        }
        else
        {
            if (CurrentMode == Mode.Sniper) ShowMessage("To był niewinny bot! Tłum panikuje!", new Color(1f, 0.6f, 0.2f), 3f);
            else ShowMessage("Snajper zastrzelił niewinnego bota — PANIKA!", new Color(1f, 0.6f, 0.2f), 3f);
        }
        CheckEnd();
    }

    public void OnShotFired(SniperShot.Result result)
    {
        if (CurrentState != State.Playing) return;
        if (result.victim == null)
        {
            if (CurrentMode == Mode.Sniper) ShowMessage("Pudło! Tłum panikuje!", new Color(1f, 0.6f, 0.2f), 2.5f);
            else ShowMessage("Snajper spudłował — PANIKA!", new Color(1f, 0.6f, 0.2f), 2.5f);
        }
        CheckEnd();
    }

    public void OnSuitcaseStolen(Suitcase s, CrowdMember thief)
    {
        if (CurrentState != State.Playing) return;
        suitcasesStolen++;
        if (CurrentMode == Mode.Spy)
        {
            ShowMessage("Walizka zdobyta! (" + suitcasesStolen + "/" + suitcasesTotal + ")", new Color(0.4f, 1f, 0.4f), 3f);
        }
        else
        {
            ShowMessage("Walizka skradziona! (" + suitcasesStolen + "/" + suitcasesTotal + ") — szpieg był tuż obok!", new Color(1f, 0.4f, 0.3f), 4f);
            var marker = LowPolyFactory.Box("TheftMarker", roundRoot.transform, s.transform.position + Vector3.up * 6f,
                new Vector3(0.4f, 12f, 0.4f), new Color(1f, 0.2f, 0.1f), true);
            Destroy(marker, 5f);
        }
        if (aiSniper != null) aiSniper.OnSuitcaseStolen(s.transform.position);
        CheckEnd();
    }

    int DeadSpies()
    {
        int n = 0;
        foreach (var s in spies) if (s == null || s.IsDead) n++;
        return n;
    }

    int SniperAmmo => CurrentMode == Mode.Sniper ? sniper.ammo : (aiSniper != null ? aiSniper.ammo : 0);

    void CheckEnd()
    {
        if (CurrentState != State.Playing || endAt > 0f) return;

        if (DeadSpies() >= spies.Count)
            EndRound(false, spies.Count > 1 ? "Wszyscy szpiedzy zostali wyeliminowani." : "Szpieg został zdemaskowany.", 2.5f);
        else if (suitcasesStolen >= suitcasesTotal)
            EndRound(true, "Wszystkie walizki zostały skradzione.", 1.5f);
        else if (SniperAmmo <= 0)
            EndRound(true, "Snajperowi skończyła się amunicja.", 2f);
        else if (timeLeft <= 0f)
            EndRound(false, "Czas minął — szpiedzy nie zdążyli.", 0.5f);
    }

    void EndRound(bool spiesWin, string reason, float delay)
    {
        spiesWon = spiesWin;
        endReason = reason;
        endAt = Time.time + delay;
    }

    void FinishRound()
    {
        CurrentState = State.GameOver;
        endAt = -1f;
        foreach (var s in spies)
            if (s != null) RevealMarker.Attach(s.Animator.TorsoTransform, roundRoot.transform);
    }

    // --- Petla ---------------------------------------------------------------------------------

    void Update()
    {
        var kb = Keyboard.current;
        bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;

        switch (CurrentState)
        {
            case State.Menu:
                menuOrbit += Time.deltaTime * 4f;
                Quaternion rot = Quaternion.Euler(0f, menuOrbit, 0f);
                cam.transform.position = rot * new Vector3(0f, 24f, -48f);
                cam.transform.LookAt(new Vector3(0f, 0f, 0f));
                break;

            case State.Playing:
                timeLeft -= Time.deltaTime;
                if (timeLeft <= 0f) { timeLeft = 0f; CheckEnd(); }
                if (endAt > 0f && Time.time >= endAt) FinishRound();
                if (esc) { CurrentState = State.Paused; Time.timeScale = 0f; }
                break;

            case State.Paused:
                if (esc) Resume();
                break;

            case State.GameOver:
                if (esc) EnterMenu();
                else if (kb != null && kb.rKey.wasPressedThisFrame) StartRound(CurrentMode);
                break;
        }

        bool lockCursor = CurrentState == State.Playing;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;

        messages.RemoveAll(m => Time.unscaledTime > m.until);
    }

    void Resume()
    {
        CurrentState = State.Playing;
        Time.timeScale = 1f;
    }

    public void ShowMessage(string text, Color color, float duration)
    {
        messages.Add(new Message { text = text, color = color, until = Time.unscaledTime + duration });
        if (messages.Count > 4) messages.RemoveAt(0);
    }

    // --- Interfejs (IMGUI - dziala bez zadnych dodatkowych pakietow) ------------------------------

    GUIStyle titleStyle, bigStyle, textStyle, smallStyle, buttonStyle;
    float uiScale;

    void EnsureStyles()
    {
        float s = Mathf.Max(0.6f, Screen.height / 1080f);
        if (titleStyle != null && Mathf.Approximately(s, uiScale)) return;
        uiScale = s;

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(84 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        bigStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(40 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s), alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * s), alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(30 * s), fontStyle = FontStyle.Bold };
    }

    void Label(Rect r, string text, GUIStyle style, Color color)
    {
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, color.a * 0.8f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = old;
    }

    void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void OnGUI()
    {
        GUI.depth = 0;
        EnsureStyles();
        float s = uiScale;

        switch (CurrentState)
        {
            case State.Menu: DrawMenu(s); break;
            case State.Playing: DrawHud(s); break;
            case State.Paused: DrawHud(s); DrawPause(s); break;
            case State.GameOver: DrawHud(s); DrawGameOver(s); break;
        }
        DrawMessages(s);
    }

    void DrawMenu(float s)
    {
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f));
        float w = Screen.width, cx = w / 2f;

        Label(new Rect(0, 60 * s, w, 110 * s), "NIE WYRÓŻNIAJ SIĘ", titleStyle, new Color(1f, 0.9f, 0.4f));
        Label(new Rect(0, 165 * s, w, 50 * s), "Ukryj się w tłumie botów. Albo znajdź tego, kto się ukrywa.", bigStyle, Color.white);

        float bw = 520 * s, bh = 80 * s, y = 280 * s;
        if (GUI.Button(new Rect(cx - bw - 20 * s, y, bw, bh), "GRAJ JAKO SZPIEG", buttonStyle)) StartRound(Mode.Spy);
        if (GUI.Button(new Rect(cx + 20 * s, y, bw, bh), "GRAJ JAKO SNAJPER", buttonStyle)) StartRound(Mode.Sniper);

        string spyHelp =
            "<b>Cel:</b> ukradnij " + GameConfig.SuitcaseCount + " walizki, zanim snajper Cię wypatrzy.\n\n" +
            "<b>WSAD</b> – ruch, <b>mysz</b> – kamera\n" +
            "<b>SHIFT</b> (trzymaj) – TRYB NPC: ruszasz się jak bot\n" +
            "<b>SPACJA</b> – bieg w panice z rękami w górze\n" +
            "<b>E</b> (trzymaj) – kradzież walizki (NIE działa w trybie NPC)\n\n" +
            "Czerwony laser pokazuje, gdzie patrzy snajper. Gdy tłum wpadnie w panikę – panikuj razem z nim!";
        string sniperHelp =
            "<b>Cel:</b> zastrzel " + GameConfig.AISpyCount + " szpiegów, mając tylko " + GameConfig.PlayerSniperAmmo + " naboi.\n\n" +
            "<b>Mysz</b> – celowanie, <b>PPM</b> – luneta\n" +
            "<b>Kółko</b> – przybliżenie, <b>LPM</b> – strzał\n" +
            "<b>SHIFT</b> w lunecie – wstrzymanie oddechu\n\n" +
            "Szukaj płynnych, ludzkich ruchów, grzebania przy walizkach i spóźnionej reakcji na panikę. " +
            "Pudło albo zabity bot wywołuje panikę tłumu.";
        Label(new Rect(cx - bw - 20 * s, y + bh + 20 * s, bw, 420 * s), spyHelp, textStyle, Color.white);
        Label(new Rect(cx + 20 * s, y + bh + 20 * s, bw, 420 * s), sniperHelp, textStyle, Color.white);

        float sy = Screen.height - 120 * s;
        Label(new Rect(cx - 260 * s, sy, 520 * s, 36 * s), "Liczba botów: " + botCount + (botCount > 250 ? "  (może zwolnić!)" : ""), textStyle, Color.white);
        botCount = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(cx - 260 * s, sy + 44 * s, 520 * s, 30 * s), botCount, 30f, 400f) / 10f) * 10;
    }

    void DrawHud(float s)
    {
        float w = Screen.width, h = Screen.height;
        string time = string.Format("{0}:{1:00}", (int)(timeLeft / 60f), (int)(timeLeft % 60f));
        bool flash = Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.6f;

        if (CurrentMode == Mode.Spy)
        {
            Label(new Rect(20 * s, 16 * s, 700 * s, 200 * s),
                "<b>SZPIEG</b>   Czas: " + time + "\nWalizki: " + suitcasesStolen + "/" + suitcasesTotal +
                "\nNaboje snajpera: " + (aiSniper != null ? aiSniper.ammo : 0), textStyle, Color.white);

            if (playerSpyCtrl != null && playerSpy != null && !playerSpy.IsDead)
            {
                // Tryb ruchu
                string modeText = playerSpyCtrl.isNpcMode ? "TRYB NPC" : "TRYB CZŁOWIEK";
                Color modeColor = playerSpyCtrl.isNpcMode ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.7f, 0.3f);
                if (playerSpyCtrl.isPanicRunning) modeText += " + PANIKA";
                Label(new Rect(0, h - 90 * s, w, 60 * s), modeText, bigStyle, modeColor);

                // Podejrzliwosc snajpera wobec gracza
                if (aiSniper != null)
                {
                    float sus = Mathf.Clamp01(aiSniper.GetSuspicion(playerSpy) / aiSniper.shootThreshold);
                    Rect bar = new Rect(w - 380 * s, 24 * s, 340 * s, 26 * s);
                    Fill(bar, new Color(0f, 0f, 0f, 0.5f));
                    Fill(new Rect(bar.x, bar.y, bar.width * sus, bar.height), Color.Lerp(new Color(0.3f, 0.9f, 0.3f), new Color(1f, 0.15f, 0.1f), sus));
                    Label(new Rect(bar.x, bar.y + bar.height + 2 * s, bar.width, 30 * s), "Podejrzliwość snajpera", smallStyle, Color.white);

                    if (aiSniper.CurrentTarget == playerSpy && flash)
                        Label(new Rect(0, 90 * s, w, 60 * s), aiSniper.IsAiming ? "CELUJE W CIEBIE!" : "SNAJPER CIĘ OBSERWUJE", bigStyle, new Color(1f, 0.2f, 0.2f));
                }

                if (CrowdMember.PanicActive && !playerSpyCtrl.isPanicRunning && flash)
                    Label(new Rect(0, 150 * s, w, 60 * s), "PANIKA! Trzymaj SPACJĘ i uciekaj jak boty!", bigStyle, new Color(1f, 0.6f, 0.2f));

                // Kradziez
                Suitcase near = playerSpyCtrl.NearbySuitcase;
                if (near != null)
                {
                    string prompt = playerSpyCtrl.isNpcMode ? "Puść SHIFT, żeby móc ukraść walizkę"
                                  : playerSpyCtrl.IsStealing ? "Kradzież..." : "Przytrzymaj E, aby ukraść walizkę";
                    Label(new Rect(0, h * 0.62f, w, 50 * s), prompt, bigStyle, Color.white);
                    if (playerSpyCtrl.IsStealing)
                    {
                        Rect bar = new Rect(w / 2f - 200 * s, h * 0.62f + 56 * s, 400 * s, 20 * s);
                        Fill(bar, new Color(0f, 0f, 0f, 0.5f));
                        Fill(new Rect(bar.x, bar.y, bar.width * near.Progress, bar.height), new Color(1f, 0.85f, 0.2f));
                    }
                }
            }

            Label(new Rect(20 * s, h - 60 * s, 900 * s, 50 * s),
                "WSAD ruch · SHIFT tryb NPC · SPACJA panika · E kradzież · Esc pauza", smallStyle, new Color(1f, 1f, 1f, 0.7f));
        }
        else
        {
            int alive = spies.Count - DeadSpies();
            Label(new Rect(20 * s, 16 * s, 700 * s, 200 * s),
                "<b>SNAJPER</b>   Czas: " + time + "\nSkradzione walizki: " + suitcasesStolen + "/" + suitcasesTotal +
                "\nSzpiedzy w tłumie: " + alive + "/" + spies.Count, textStyle, Color.white);

            string ammo = "Naboje: " + sniper.ammo + "/" + GameConfig.PlayerSniperAmmo;
            Label(new Rect(w - 420 * s, h - 130 * s, 400 * s, 50 * s), ammo, bigStyle, Color.white);
            string status = sniper.ammo <= 0 ? "BRAK AMUNICJI" : sniper.IsReloading ? "PRZEŁADOWANIE..." :
                            sniper.IsScoped ? "Zoom x" + (sniper.ZoomLevel == 0 ? "4" : "8") : "";
            Label(new Rect(w - 420 * s, h - 80 * s, 400 * s, 40 * s), status, bigStyle, new Color(1f, 0.85f, 0.4f));

            if (sniper.IsScoped)
            {
                Rect bar = new Rect(w / 2f - 150 * s, h - 60 * s, 300 * s, 12 * s);
                Fill(bar, new Color(0f, 0f, 0f, 0.5f));
                Fill(new Rect(bar.x, bar.y, bar.width * sniper.Breath, bar.height), new Color(0.6f, 0.85f, 1f));
            }

            Label(new Rect(20 * s, h - 60 * s, 900 * s, 50 * s),
                "Mysz celowanie · PPM luneta · Kółko zoom · LPM strzał · SHIFT oddech · Esc pauza", smallStyle, new Color(1f, 1f, 1f, 0.7f));
        }
    }

    void DrawMessages(float s)
    {
        float y = Screen.height * 0.28f;
        foreach (var m in messages)
        {
            Label(new Rect(0, y, Screen.width, 50 * s), m.text, bigStyle, m.color);
            y += 52 * s;
        }
    }

    void DrawPause(float s)
    {
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
        float cx = Screen.width / 2f, bw = 420 * s, bh = 70 * s;
        Label(new Rect(0, Screen.height * 0.25f, Screen.width, 110 * s), "PAUZA", titleStyle, Color.white);
        if (GUI.Button(new Rect(cx - bw / 2f, Screen.height * 0.45f, bw, bh), "Wznów", buttonStyle)) Resume();
        if (GUI.Button(new Rect(cx - bw / 2f, Screen.height * 0.45f + bh + 20 * s, bw, bh), "Menu główne", buttonStyle)) EnterMenu();
    }

    void DrawGameOver(float s)
    {
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
        bool playerWon = CurrentMode == Mode.Spy ? spiesWon : !spiesWon;
        float cx = Screen.width / 2f, bw = 420 * s, bh = 70 * s;

        Label(new Rect(0, Screen.height * 0.2f, Screen.width, 110 * s), playerWon ? "WYGRAŁEŚ!" : "PRZEGRAŁEŚ",
            titleStyle, playerWon ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f));
        Label(new Rect(0, Screen.height * 0.2f + 110 * s, Screen.width, 50 * s),
            (spiesWon ? "Szpiedzy wygrywają. " : "Snajper wygrywa. ") + endReason, bigStyle, Color.white);
        Label(new Rect(0, Screen.height * 0.2f + 165 * s, Screen.width, 50 * s),
            "Czerwone znaczniki pokazują szpiegów.", bigStyle, new Color(1f, 1f, 1f, 0.8f));

        if (GUI.Button(new Rect(cx - bw / 2f, Screen.height * 0.55f, bw, bh), "Zagraj ponownie (R)", buttonStyle)) StartRound(CurrentMode);
        if (GUI.Button(new Rect(cx - bw / 2f, Screen.height * 0.55f + bh + 20 * s, bw, bh), "Menu (Esc)", buttonStyle)) EnterMenu();
    }
}
