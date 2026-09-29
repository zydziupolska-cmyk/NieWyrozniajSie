using UnityEngine;

public class ProceduralAnimator : MonoBehaviour
{
    public float legSwingSpeed = 10f;
    public float legSwingAngle = 45f;
    
    private GameObject ragdollRoot;
    private Rigidbody torsoRb;
    private ConfigurableJoint lLegJoint, rLegJoint, lArmJoint, rArmJoint;

    private float movePhase = 0f;
    private bool isDead = false;
    private Vector3 lastPos;

    void Start()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
        
        Transform old = transform.Find("Model");
        if(old != null) Destroy(old.gameObject);

        lastPos = transform.position;
        BuildActiveRagdoll();
    }

    void BuildActiveRagdoll()
    {
        ragdollRoot = new GameObject("ActiveRagdoll_" + gameObject.name);
        ragdollRoot.transform.position = transform.position;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if(mat.shader == null) mat = new Material(Shader.Find("Standard"));
        mat.color = gameObject.name.Contains("Spy") ? Color.blue : Color.red;

        GameObject torso = CreatePart("Torso", PrimitiveType.Capsule, new Vector3(0, 1.2f, 0), new Vector3(0.6f, 0.8f, 0.4f), mat);
        torsoRb = torso.GetComponent<Rigidbody>();
        torsoRb.mass = 20f;
        
        GameObject head = CreatePart("Head", PrimitiveType.Sphere, new Vector3(0, 1.9f, 0), new Vector3(0.5f, 0.5f, 0.5f), mat);
        AttachJoint(head, torsoRb, new Vector3(0, -0.4f, 0), true);

        GameObject lLeg = CreatePart("LLeg", PrimitiveType.Capsule, new Vector3(-0.2f, 0.5f, 0), new Vector3(0.25f, 0.5f, 0.25f), mat);
        lLegJoint = AttachJoint(lLeg, torsoRb, new Vector3(0, 0.4f, 0), false);

        GameObject rLeg = CreatePart("RLeg", PrimitiveType.Capsule, new Vector3(0.2f, 0.5f, 0), new Vector3(0.25f, 0.5f, 0.25f), mat);
        rLegJoint = AttachJoint(rLeg, torsoRb, new Vector3(0, 0.4f, 0), false);

        GameObject lArm = CreatePart("LArm", PrimitiveType.Capsule, new Vector3(-0.45f, 1.4f, 0), new Vector3(0.2f, 0.6f, 0.2f), mat);
        lArmJoint = AttachJoint(lArm, torsoRb, new Vector3(0.2f, 0.2f, 0), false);

        GameObject rArm = CreatePart("RArm", PrimitiveType.Capsule, new Vector3(0.45f, 1.4f, 0), new Vector3(0.2f, 0.6f, 0.2f), mat);
        rArmJoint = AttachJoint(rArm, torsoRb, new Vector3(-0.2f, 0.2f, 0), false);

        if (gameObject.name.Contains("Spy"))
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                ThirdPersonCamera tpc = mainCam.GetComponent<ThirdPersonCamera>();
                if (tpc != null) tpc.target = torso.transform;
            }
        }
    }

    GameObject CreatePart(string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(ragdollRoot.transform);
        part.transform.localPosition = localPos;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = mat;
        
        Rigidbody rb = part.AddComponent<Rigidbody>();
        rb.mass = 5f;
        
        // Zabezpieczenie przed lataniem
        Collider myCol = part.GetComponent<Collider>();
        Collider rootCol = GetComponent<Collider>();
        if (myCol != null && rootCol != null) {
            Physics.IgnoreCollision(myCol, rootCol, true);
        }
        
        return part;
    }

    ConfigurableJoint AttachJoint(GameObject part, Rigidbody connectedTo, Vector3 anchor, bool lockAll)
    {
        ConfigurableJoint joint = part.AddComponent<ConfigurableJoint>();
        joint.connectedBody = connectedTo;
        joint.anchor = anchor;
        
        if (lockAll)
        {
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;
        }
        else
        {
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            
            JointDrive drive = new JointDrive();
            drive.positionSpring = 1000f; // Mniej sztywne nogi
            drive.positionDamper = 100f;
            drive.maximumForce = Mathf.Infinity;
            joint.slerpDrive = drive;
        }
        return joint;
    }

    void FixedUpdate()
    {
        if (isDead || torsoRb == null) return;

        Vector3 velocity = (transform.position - lastPos) / Time.fixedDeltaTime;
        lastPos = transform.position;
        float speed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        // Trzymaj pion
        Quaternion uprightRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        torsoRb.MoveRotation(Quaternion.Slerp(torsoRb.rotation, uprightRotation, Time.fixedDeltaTime * 15f));

        // Bezpieczne przyciaganie na boki, zeby nie odlatywaly do gory
        Vector3 targetPos = transform.position + Vector3.up * 1.2f;
        Vector3 diff = targetPos - torsoRb.position;
        diff.y = Mathf.Clamp(diff.y, -0.5f, 0.5f); // Ogranicz sile ciagniecia w pionie (ZAPOBIEGA LATANIU)
        
        Vector3 pullForce = diff * 500f; 
        torsoRb.AddForce(pullForce);
        
        // Animacja
        if (speed > 0.1f)
        {
            movePhase += speed * legSwingSpeed * Time.fixedDeltaTime;
            float angle = Mathf.Sin(movePhase) * legSwingAngle;

            lLegJoint.targetRotation = Quaternion.Euler(angle, 0, 0);
            rLegJoint.targetRotation = Quaternion.Euler(-angle, 0, 0);
            lArmJoint.targetRotation = Quaternion.Euler(-angle, 0, 0);
            rArmJoint.targetRotation = Quaternion.Euler(angle, 0, 0);
        }
        else
        {
            movePhase = 0f;
            lLegJoint.targetRotation = Quaternion.identity;
            rLegJoint.targetRotation = Quaternion.identity;
            lArmJoint.targetRotation = Quaternion.identity;
            rArmJoint.targetRotation = Quaternion.identity;
        }
    }

    public void EnableRagdoll(Vector3 hitDirection)
    {
        if (isDead) return;
        isDead = true;

        JointDrive looseDrive = new JointDrive() { positionSpring = 0f };
        lLegJoint.slerpDrive = looseDrive;
        rLegJoint.slerpDrive = looseDrive;
        lArmJoint.slerpDrive = looseDrive;
        rArmJoint.slerpDrive = looseDrive;

        torsoRb.AddForce(hitDirection * 500f + Vector3.up * 300f, ForceMode.Impulse);
    }
}
