using UnityEngine;

// Tasma bagazowa na lotnisku: przesuwa lezace na niej przedmioty po owalu.
// Walizki-wabiki jezdza w kolko - a czasem ktos zdejmie z niej te wlasciwa.
public class ConveyorBelt : MonoBehaviour
{
    public float speed = 0.9f;
    public Vector2 halfSize = new Vector2(5f, 2f);

    void OnCollisionStay(Collision collision)
    {
        Rigidbody rb = collision.rigidbody;
        if (rb == null || rb.isKinematic) return;
        Prop prop = rb.GetComponent<Prop>();
        if (prop == null || prop.IsHeld) return;

        // Styczna do elipsy wokol srodka tasmy
        Vector3 local = transform.InverseTransformPoint(rb.position);
        Vector3 radial = new Vector3(local.x / halfSize.x, 0f, local.z / halfSize.y);
        if (radial.sqrMagnitude < 0.0001f) return;
        Vector3 tangent = transform.TransformDirection(Vector3.Cross(Vector3.up, radial).normalized);
        // Lekko do srodka, zeby nie zsuwaly sie z krawedzi
        Vector3 inward = -transform.TransformDirection(radial.normalized) * 0.15f;

        Vector3 v = rb.linearVelocity;
        Vector3 want = (tangent + inward) * speed;
        rb.linearVelocity = Vector3.Lerp(v, new Vector3(want.x, v.y, want.z), 0.25f);
        rb.angularVelocity *= 0.9f;
    }
}
