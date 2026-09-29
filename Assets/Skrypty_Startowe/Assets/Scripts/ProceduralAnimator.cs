using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Active Ragdoll w stylu Human Fall Flat.
// Obiekt z tym skryptem (bot z NavMeshAgent albo szpieg z CharacterController)
// jest niewidzialnym "kontrolerem". Fizyczne cialo z jointow podaza za nim
// silami (balans + animacja proceduralna), a po strzale staje sie bezwladnym trupem.
public class ProceduralAnimator : MonoBehaviour
{
    [Header("Styl ruchu")]
    [Tooltip("Kanciasty, toporny chod bota. Boty maja zawsze wlaczone, szpieg tylko z SHIFT.")]
    public bool roboticMovement = true;
    [Tooltip("Dlugosc pelnego cyklu chodu (dwa kroki) w metrach.")]
    public float strideLength = 1.2f;
    [Tooltip("Rece w gorze i wymachiwanie - panika tlumu.")]
    public bool panicking;
    [Tooltip("Siegniecie po przedmiot (kradziez walizki) - bardzo ludzki ruch.")]
    public bool reaching;

    [Header("Wyglad")]
    [Tooltip("Kolorowe koszulki i spodnie. Wylaczone = wszyscy sa bialymi Bobami jak w Human Fall Flat.")]
    public bool randomOutfit = false;
    [Tooltip("-1 = losowy. Stala wartosc przyda sie do synchronizacji w multiplayerze.")]
    public int appearanceSeed = -1;

    [Header("Balans")]
    public float uprightStrength = 250f;
    public float uprightDamping = 25f;
    public float followStrength = 80f;
    public float followDamping = 14f;
    public float heightStrength = 150f;
    public float heightDamping = 20f;
    public float maxBalanceAcceleration = 60f;
    [Tooltip("Jesli cialo odjedzie dalej niz tyle od kontrolera, zostanie teleportowane.")]
    public float snapDistance = 3f;

    [Header("Smierc")]
    public float deathLaunchSpeed = 9f;
    public float deathUpSpeed = 3.5f;
    public float deathSpin = 6f;

    public bool IsDead { get; private set; }
    public Transform TorsoTransform => torso != null ? torso.rb.transform : transform;
    public float CurrentSpeed => smoothSpeed;

    // ---------------------------------------------------------------------

    class Part
    {
        public Rigidbody rb;
        public ConfigurableJoint joint;
        public Vector3 restPos;
        public float halfLength;
        public float spring, damper;
    }

    const float HoverOffset = 0.03f;   // stopy minimalnie nad ziemia
    const float RoboticStiffness = 2.5f;

    GameObject ragdollRoot;
    Part torso, head, upperArmL, upperArmR, foreArmL, foreArmR, thighL, thighR, shinL, shinR;
    readonly List<Part> parts = new List<Part>();
    float totalMass;

    NavMeshAgent agent;
    CharacterController characterController;
    Vector3 lastRootPos;
    Vector3 estimatedVelocity;

    float phase;
    float gait;          // 0 = stoi, 1 = normalny chod, >1 = bieg
    float smoothSpeed;
    bool appliedRobotic;
    float strideVariation = 1f;

    // --- Warstwy fizyki -----------------------------------------------------

    static bool layersReady;
    static int ragdollLayer = -1;
    static int rootLayer = -1;

    static void EnsureLayers()
    {
        if (layersReady) return;
        layersReady = true;
        ragdollLayer = LayerMask.NameToLayer("Ragdoll");
        rootLayer = LayerMask.NameToLayer("CharacterRoot");
        if (ragdollLayer < 0 || rootLayer < 0)
        {
            Debug.LogWarning("Brak warstw 'Ragdoll' / 'CharacterRoot' w Project Settings > Tags and Layers. Ragdolle beda dzialac, ale gorzej.");
            ragdollLayer = rootLayer = -1;
            return;
        }
        // Niewidzialne kontrolery nie zderzaja sie z cialami ani ze soba -
        // tloczenie sie i przepychanie robia fizyczne ragdolle.
        Physics.IgnoreLayerCollision(ragdollLayer, rootLayer, true);
        Physics.IgnoreLayerCollision(rootLayer, rootLayer, true);
    }

    // Maska dla raycastu snajpera: wszystko poza niewidzialnymi kontrolerami
    public static int ShootableMask
    {
        get
        {
            EnsureLayers();
            return rootLayer >= 0 ? ~(1 << rootLayer) : Physics.DefaultRaycastLayers;
        }
    }

    // --- Cykl zycia ------------------------------------------------------------

    void Start()
    {
        EnsureLayers();
        agent = GetComponent<NavMeshAgent>();
        characterController = GetComponent<CharacterController>();

        // Ukryj placeholder (kapsule) i wylacz jego collider - cialem jest ragdoll
        foreach (var r in GetComponents<Renderer>()) r.enabled = false;
        foreach (var c in GetComponents<Collider>())
        {
            if (!(c is CharacterController)) c.enabled = false;
        }
        Transform old = transform.Find("Model");
        if (old != null) Destroy(old.gameObject);
        if (rootLayer >= 0) gameObject.layer = rootLayer;

        lastRootPos = transform.position;
        phase = Random.Range(0f, Mathf.PI * 2f);
        strideVariation = Random.Range(0.9f, 1.1f);

        BuildRagdoll();
        ApplyDrives();
    }

    void OnDestroy()
    {
        if (ragdollRoot != null) Destroy(ragdollRoot);
    }

    void Update()
    {
        if (Time.deltaTime > 0f)
        {
            Vector3 v = (transform.position - lastRootPos) / Time.deltaTime;
            estimatedVelocity = Vector3.Lerp(estimatedVelocity, Vector3.ClampMagnitude(v, 15f), 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
        lastRootPos = transform.position;
    }

    // --- Budowa ciala -----------------------------------------------------------

    void BuildRagdoll()
    {
        var rng = appearanceSeed >= 0 ? new System.Random(appearanceSeed) : new System.Random(Random.Range(int.MinValue, int.MaxValue));
        PickOutfit(rng, out Color skin, out Color shirt, out Color pants, out Color shoes);
        Material skinMat = LowPolyFactory.GetBodyMaterial(skin);
        Material shirtMat = LowPolyFactory.GetBodyMaterial(shirt);
        Material pantsMat = LowPolyFactory.GetBodyMaterial(pants);
        Material shoeMat = LowPolyFactory.GetBodyMaterial(shoes);

        ragdollRoot = new GameObject("Ragdoll_" + gameObject.name);
        ragdollRoot.transform.SetPositionAndRotation(GetFeetPosition(), GetFacing());

        // Proporcje Boba z Human Fall Flat: tulow jak jajko (szerszy dol), duza gladka glowa
        // bez szyi i bez twarzy, grube rece z lapkami-rekawiczkami, krotkie nogi ze stopkami.
        // Punkty to srodki polkul kapsul (gora, dol), lokalnie wzgledem stop.
        torso = CreatePart("Torso", new Vector3(0f, 1.20f, 0f), new Vector3(0f, 0.80f, 0f), 0.25f, 0.29f, 15f, shirtMat, 1f, 1f, 0.82f);
        head = CreatePart("Head", new Vector3(0f, 1.66f, 0f), new Vector3(0f, 1.66f, 0f), 0.25f, 0.25f, 4f, skinMat, 1f, 1.06f, 1f);

        upperArmL = CreatePart("UpperArm_L", new Vector3(-0.32f, 1.36f, 0f), new Vector3(-0.32f, 1.11f, 0f), 0.09f, 0.08f, 2f, shirtMat);
        upperArmR = CreatePart("UpperArm_R", new Vector3(0.32f, 1.36f, 0f), new Vector3(0.32f, 1.11f, 0f), 0.09f, 0.08f, 2f, shirtMat);
        foreArmL = CreatePart("ForeArm_L", new Vector3(-0.32f, 1.11f, 0f), new Vector3(-0.32f, 0.88f, 0f), 0.08f, 0.10f, 1.5f, skinMat);
        foreArmR = CreatePart("ForeArm_R", new Vector3(0.32f, 1.11f, 0f), new Vector3(0.32f, 0.88f, 0f), 0.08f, 0.10f, 1.5f, skinMat);

        thighL = CreatePart("Thigh_L", new Vector3(-0.15f, 0.70f, 0f), new Vector3(-0.15f, 0.42f, 0f), 0.125f, 0.11f, 4f, pantsMat);
        thighR = CreatePart("Thigh_R", new Vector3(0.15f, 0.70f, 0f), new Vector3(0.15f, 0.42f, 0f), 0.125f, 0.11f, 4f, pantsMat);
        shinL = CreatePart("Shin_L", new Vector3(-0.15f, 0.42f, 0f), new Vector3(-0.15f, 0.12f, 0f), 0.105f, 0.10f, 3f, pantsMat);
        shinR = CreatePart("Shin_R", new Vector3(0.15f, 0.42f, 0f), new Vector3(0.15f, 0.12f, 0f), 0.105f, 0.10f, 3f, pantsMat);

        // Stopki - czysto wizualne, wystaja lekko do przodu
        AddFoot(shinL, shoeMat);
        AddFoot(shinR, shoeMat);

        // Stawy: (dziecko, rodzic, punkt obrotu, limity X low/high, limit Y, limit Z, sprezyna, tlumienie)
        // Punkt obrotu konczyn = srodek gornej polkuli czesci.
        Connect(head, torso, new Vector3(0f, -0.2f, 0f), -30f, 30f, 30f, 20f, 150f, 4f);
        Connect(upperArmL, torso, TopAnchor(upperArmL), -150f, 150f, 40f, 80f, 150f, 10f);
        Connect(upperArmR, torso, TopAnchor(upperArmR), -150f, 150f, 40f, 80f, 150f, 10f);
        Connect(foreArmL, upperArmL, TopAnchor(foreArmL), -60f, 140f, 5f, 5f, 100f, 4f);
        Connect(foreArmR, upperArmR, TopAnchor(foreArmR), -60f, 140f, 5f, 5f, 100f, 4f);
        Connect(thighL, torso, TopAnchor(thighL), -90f, 90f, 20f, 30f, 450f, 30f);
        Connect(thighR, torso, TopAnchor(thighR), -90f, 90f, 20f, 30f, 450f, 30f);
        Connect(shinL, thighL, TopAnchor(shinL), -140f, 60f, 5f, 5f, 300f, 10f);
        Connect(shinR, thighR, TopAnchor(shinR), -140f, 60f, 5f, 5f, 300f, 10f);

        // Czesci jednego ciala nie zderzaja sie ze soba (brak drgan)
        for (int i = 0; i < parts.Count; i++)
        {
            Collider a = parts[i].rb.GetComponent<Collider>();
            for (int j = i + 1; j < parts.Count; j++)
                Physics.IgnoreCollision(a, parts[j].rb.GetComponent<Collider>(), true);
            if (characterController != null)
                Physics.IgnoreCollision(a, characterController, true);
        }

        totalMass = 0f;
        foreach (var p in parts) totalMass += p.rb.mass;
    }

    static Vector3 TopAnchor(Part p) => new Vector3(0f, p.halfLength, 0f);

    void PickOutfit(System.Random rng, out Color skin, out Color shirt, out Color pants, out Color shoes)
    {
        // Klasyczny Bob: caly bialy, lekko cieply odcien
        Color bob = new Color(0.95f, 0.94f, 0.92f);
        skin = shirt = pants = shoes = bob;
        if (!randomOutfit) return;

        // "Stroje" jak w HFF: bialy ludzik w kolorowej koszulce i spodniach
        Color[] shirts =
        {
            new Color(0.85f, 0.33f, 0.30f), new Color(0.30f, 0.50f, 0.80f), new Color(0.95f, 0.75f, 0.30f),
            new Color(0.40f, 0.70f, 0.45f), new Color(0.60f, 0.45f, 0.75f), bob,
            new Color(0.35f, 0.35f, 0.40f), new Color(0.95f, 0.55f, 0.35f)
        };
        Color[] trousers =
        {
            new Color(0.20f, 0.25f, 0.40f), new Color(0.30f, 0.30f, 0.32f), new Color(0.45f, 0.35f, 0.25f),
            new Color(0.55f, 0.60f, 0.70f), bob
        };
        Color[] footwear = { new Color(0.15f, 0.15f, 0.17f), new Color(0.45f, 0.3f, 0.2f), bob };
        shirt = shirts[rng.Next(shirts.Length)];
        pants = trousers[rng.Next(trousers.Length)];
        shoes = footwear[rng.Next(footwear.Length)];
    }

    // Czesc ciala: gladka kapsula od "top" do "bottom" (pionowo), rTop/rBottom = promienie polkul,
    // sx/sy/sz = splaszczenie siatki (np. tulow plytszy niz szerszy)
    Part CreatePart(string partName, Vector3 top, Vector3 bottom, float rTop, float rBottom, float mass, Material mat,
                    float sx = 1f, float sy = 1f, float sz = 1f)
    {
        float length = Vector3.Distance(top, bottom);
        Vector3 center = (top + bottom) * 0.5f;

        var go = new GameObject(partName);
        go.transform.SetParent(ragdollRoot.transform, false);
        go.transform.localPosition = center;
        if (ragdollLayer >= 0) go.layer = ragdollLayer;

        go.AddComponent<MeshFilter>().sharedMesh = LowPolyFactory.SmoothCapsule(rBottom, rTop, length, sx, sy, sz);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;

        float colRadius = (rTop + rBottom) * 0.5f * (sx + sz) * 0.5f;
        if (length <= 0.0001f)
        {
            go.AddComponent<SphereCollider>().radius = colRadius * sy;
        }
        else
        {
            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = colRadius;
            col.height = length + colRadius * 2f;
        }

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.solverIterations = 12;
        rb.solverVelocityIterations = 4;
        rb.maxAngularVelocity = 20f;

        go.AddComponent<RagdollPart>().owner = this;

        var part = new Part { rb = rb, restPos = center, halfLength = length * 0.5f };
        parts.Add(part);
        return part;
    }

    void AddFoot(Part shin, Material mat)
    {
        var foot = new GameObject("Foot");
        foot.transform.SetParent(shin.rb.transform, false);
        // Srodek stopy: 7.5 cm nad ziemia, 4 cm do przodu; kapsula polozona wzdluz osi Z
        foot.transform.localPosition = new Vector3(0f, 0.075f - shin.restPos.y, 0.04f);
        foot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        foot.AddComponent<MeshFilter>().sharedMesh = LowPolyFactory.SmoothCapsule(0.075f, 0.075f, 0.12f, 1.3f, 1f, 0.85f, 16, 6);
        foot.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    void Connect(Part child, Part parent, Vector3 anchor, float lowX, float highX, float limY, float limZ, float spring, float damper)
    {
        var j = child.rb.gameObject.AddComponent<ConfigurableJoint>();
        j.connectedBody = parent.rb;
        j.anchor = anchor;
        j.axis = Vector3.right;
        j.secondaryAxis = Vector3.up;
        j.autoConfigureConnectedAnchor = true;
        j.enableCollision = false;

        j.xMotion = ConfigurableJointMotion.Locked;
        j.yMotion = ConfigurableJointMotion.Locked;
        j.zMotion = ConfigurableJointMotion.Locked;
        j.angularXMotion = ConfigurableJointMotion.Limited;
        j.angularYMotion = ConfigurableJointMotion.Limited;
        j.angularZMotion = ConfigurableJointMotion.Limited;
        j.lowAngularXLimit = new SoftJointLimit { limit = lowX };
        j.highAngularXLimit = new SoftJointLimit { limit = highX };
        j.angularYLimit = new SoftJointLimit { limit = limY };
        j.angularZLimit = new SoftJointLimit { limit = limZ };

        // KLUCZOWE: bez trybu Slerp slerpDrive jest ignorowany i konczyny wisza bezwladnie
        j.rotationDriveMode = RotationDriveMode.Slerp;

        child.joint = j;
        child.spring = spring;
        child.damper = damper;
    }

    void ApplyDrives()
    {
        appliedRobotic = roboticMovement;
        float s = roboticMovement ? RoboticStiffness : 1f;
        float d = roboticMovement ? 1.6f : 1f;
        foreach (var p in parts)
        {
            if (p.joint == null) continue;
            p.joint.slerpDrive = new JointDrive
            {
                positionSpring = p.spring * s,
                positionDamper = p.damper * d,
                maximumForce = float.MaxValue
            };
        }
    }

    // Obrot czesci wzgledem rodzica. ConfigurableJoint oczekuje odwrotnosci.
    static void SetTarget(Part p, Quaternion relative)
    {
        if (p.joint != null) p.joint.targetRotation = Quaternion.Inverse(relative);
    }

    // --- Pozycja kontrolera ----------------------------------------------------

    Vector3 GetFeetPosition()
    {
        if (characterController != null)
        {
            Vector3 c = transform.TransformPoint(characterController.center);
            return c - Vector3.up * (characterController.height * 0.5f * transform.lossyScale.y);
        }
        if (agent != null) return transform.position - Vector3.up * agent.baseOffset;
        return transform.position;
    }

    Quaternion GetFacing()
    {
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
        return Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    Vector3 GetRootVelocity()
    {
        if (agent != null && agent.enabled) return agent.velocity;
        if (characterController != null && characterController.enabled) return characterController.velocity;
        return estimatedVelocity;
    }

    // Natychmiastowe ustawienie ciala w pozycji kontrolera (spawn, zaklinowanie)
    public void SnapToController()
    {
        if (ragdollRoot == null) return;
        Vector3 feet = GetFeetPosition();
        Quaternion facing = GetFacing();
        foreach (var p in parts)
        {
            p.rb.position = feet + facing * p.restPos;
            p.rb.rotation = facing;
            p.rb.linearVelocity = Vector3.zero;
            p.rb.angularVelocity = Vector3.zero;
        }
    }

    // --- Fizyka / animacja -------------------------------------------------------

    void FixedUpdate()
    {
        if (IsDead || torso == null) return;
        if (appliedRobotic != roboticMovement) ApplyDrives();

        float dt = Time.fixedDeltaTime;
        Vector3 rootVel = GetRootVelocity();
        Vector3 flatVel = new Vector3(rootVel.x, 0f, rootVel.z);
        smoothSpeed = Mathf.Lerp(smoothSpeed, flatVel.magnitude, 1f - Mathf.Exp(-10f * dt));

        float targetGait = smoothSpeed > 0.15f ? Mathf.Clamp(smoothSpeed / 3.5f, 0.3f, 1.6f) : 0f;
        gait = Mathf.MoveTowards(gait, targetGait, dt * 4f);
        if (smoothSpeed > 0.15f)
            phase += dt * smoothSpeed / (strideLength * strideVariation) * Mathf.PI * 2f;

        float bob, lean, roll;
        if (roboticMovement) AnimateRobotic(out bob, out lean, out roll);
        else AnimateHuman(out bob, out lean, out roll);

        if (panicking) AnimatePanicArms();
        else if (reaching) AnimateReach(ref lean);

        Balance(rootVel, bob, lean, roll);
    }

    // Plynny, "ludzki" chod: zginane kolana, wymachy rak, pochylenie, kolysanie
    void AnimateHuman(out float bob, out float lean, out float roll)
    {
        float s = Mathf.Sin(phase);
        float c = Mathf.Cos(phase);
        float hip = 32f * gait, knee = 45f * gait, arm = 30f * gait;

        SetTarget(thighL, Quaternion.Euler(-s * hip, 0f, 0f));
        SetTarget(thighR, Quaternion.Euler(s * hip, 0f, 0f));
        SetTarget(shinL, Quaternion.Euler(Mathf.Max(0f, c) * knee, 0f, 0f));
        SetTarget(shinR, Quaternion.Euler(Mathf.Max(0f, -c) * knee, 0f, 0f));

        SetTarget(upperArmL, Quaternion.Euler(s * arm, 0f, -8f));
        SetTarget(upperArmR, Quaternion.Euler(-s * arm, 0f, 8f));
        SetTarget(foreArmL, Quaternion.Euler(-(15f + 20f * gait), 0f, 0f));
        SetTarget(foreArmR, Quaternion.Euler(-(15f + 20f * gait), 0f, 0f));

        SetTarget(head, Quaternion.Euler(Mathf.Sin(phase * 2f) * 3f * gait, 0f, 0f));

        bob = -0.04f * gait * Mathf.Abs(s);
        lean = Mathf.Min(smoothSpeed * 2.5f, 15f);
        roll = s * 3f * gait;
    }

    // Kanciasty chod bota: sztywne nogi, rece "na bacznosc", ruch skokowy
    void AnimateRobotic(out float bob, out float lean, out float roll)
    {
        float q = Mathf.Round(Mathf.Sin(phase) * 2f) * 0.5f; // -1, -0.5, 0, 0.5, 1
        float hip = 28f * gait, arm = 12f * gait;

        SetTarget(thighL, Quaternion.Euler(-q * hip, 0f, 0f));
        SetTarget(thighR, Quaternion.Euler(q * hip, 0f, 0f));
        SetTarget(shinL, Quaternion.Euler(5f * gait, 0f, 0f));
        SetTarget(shinR, Quaternion.Euler(5f * gait, 0f, 0f));

        SetTarget(upperArmL, Quaternion.Euler(q * arm, 0f, -3f));
        SetTarget(upperArmR, Quaternion.Euler(-q * arm, 0f, 3f));
        SetTarget(foreArmL, Quaternion.Euler(-5f, 0f, 0f));
        SetTarget(foreArmR, Quaternion.Euler(-5f, 0f, 0f));

        SetTarget(head, Quaternion.identity);

        bob = -0.03f * gait * Mathf.Abs(q);
        lean = 0f;
        roll = 0f;
    }

    // Rece nad glowa i machanie - wspolne dla botow i szpiegow
    void AnimatePanicArms()
    {
        float t = Time.time * 11f + phase;
        float wave = roboticMovement ? Mathf.Round(Mathf.Sin(t)) : Mathf.Sin(t);
        SetTarget(upperArmL, Quaternion.Euler(-140f + wave * 25f, 0f, -25f));
        SetTarget(upperArmR, Quaternion.Euler(-140f - wave * 25f, 0f, 25f));
        SetTarget(foreArmL, Quaternion.Euler(-30f, 0f, 0f));
        SetTarget(foreArmR, Quaternion.Euler(-30f, 0f, 0f));
    }

    // Pochylenie i siegniecie obiema rekami w dol-przod
    void AnimateReach(ref float lean)
    {
        float t = Mathf.Sin(Time.time * 9f) * 8f;
        SetTarget(upperArmL, Quaternion.Euler(-70f + t, 0f, 5f));
        SetTarget(upperArmR, Quaternion.Euler(-70f - t, 0f, -5f));
        SetTarget(foreArmL, Quaternion.Euler(-20f, 0f, 0f));
        SetTarget(foreArmR, Quaternion.Euler(-20f, 0f, 0f));
        SetTarget(head, Quaternion.Euler(25f, 0f, 0f));
        lean += 25f;
    }

    void Balance(Vector3 rootVel, float bob, float lean, float roll)
    {
        Rigidbody rb = torso.rb;
        Vector3 feet = GetFeetPosition();
        Quaternion facing = GetFacing();

        Vector3 target = feet + Vector3.up * (torso.restPos.y + HoverOffset + bob);
        Vector3 toTarget = target - rb.position;

        // Zaklinowany albo odepchniety za daleko -> teleport
        if (new Vector3(toTarget.x, 0f, toTarget.z).magnitude > snapDistance)
        {
            SnapToController();
            return;
        }

        // Podazanie za kontrolerem (PD) + podtrzymanie calego ciala wbrew grawitacji.
        // Sila pionowa jest ograniczona, wiec ragdoll nie odlatuje w gore.
        Vector3 velErr = rootVel - rb.linearVelocity;
        Vector3 accel = new Vector3(
            toTarget.x * followStrength + velErr.x * followDamping,
            toTarget.y * heightStrength + velErr.y * heightDamping,
            toTarget.z * followStrength + velErr.z * followDamping);
        accel = Vector3.ClampMagnitude(accel, maxBalanceAcceleration);
        rb.AddForce((accel - Physics.gravity) * totalMass, ForceMode.Force);

        // Trzymanie pionu momentem obrotowym (zamiast MoveRotation, ktore walczy z fizyka)
        float stiff = roboticMovement ? 2f : 1f;
        Quaternion targetRot = facing * Quaternion.Euler(lean, 0f, roll);
        Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x) && Mathf.Abs(angle) > 0.01f)
        {
            Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad * uprightStrength * stiff)
                             - rb.angularVelocity * uprightDamping * Mathf.Sqrt(stiff);
            rb.AddTorque(torque, ForceMode.Acceleration);
        }
    }

    // --- Smierc ---------------------------------------------------------------------

    public void EnableRagdoll(Vector3 hitDirection, Rigidbody hitBody = null, Vector3 hitPoint = default)
    {
        if (IsDead || torso == null) return;
        IsDead = true;

        foreach (var p in parts)
        {
            if (p.joint != null)
                p.joint.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = 1.5f, maximumForce = float.MaxValue };
            p.rb.angularDamping = 0.5f;
            p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        Vector3 dir = Vector3.ProjectOnPlane(hitDirection, Vector3.up);
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;

        // Cale cialo odlatuje do tylu + lekko w gore i robi komiczne salto
        Vector3 launch = dir * deathLaunchSpeed + Vector3.up * deathUpSpeed;
        foreach (var p in parts) p.rb.AddForce(launch, ForceMode.VelocityChange);
        torso.rb.AddTorque(Vector3.Cross(Vector3.up, dir) * deathSpin, ForceMode.VelocityChange);

        Push(hitDirection, hitBody, hitPoint);
    }

    // Uderzenie w konkretny punkt (np. strzal w trupa)
    public void Push(Vector3 direction, Rigidbody hitBody, Vector3 hitPoint, float speed = 6f)
    {
        if (hitBody == null) return;
        hitBody.AddForceAtPosition(direction.normalized * speed * hitBody.mass, hitPoint, ForceMode.Impulse);
    }
}
