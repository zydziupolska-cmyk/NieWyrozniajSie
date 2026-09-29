using UnityEngine;

// Wspolna logika strzalu dla snajpera-gracza i snajpera AI.
public static class SniperShot
{
    public struct Result
    {
        public bool hitSomething;
        public Vector3 point;
        public CrowdMember victim;   // zabity w tym strzale (null = pudlo)
    }

    public static Result Fire(Vector3 origin, Vector3 direction)
    {
        var result = new Result();
        direction.Normalize();
        Vector3 end = origin + direction * 400f;

        SoundFx.Play(SoundFx.Gunshot, origin, 1f, 30f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, 400f, ProceduralAnimator.ShootableMask, QueryTriggerInteraction.Ignore))
        {
            result.hitSomething = true;
            result.point = hit.point;
            end = hit.point;

            RagdollPart part = hit.collider.GetComponent<RagdollPart>();
            ProceduralAnimator anim = part != null ? part.owner : hit.collider.GetComponentInParent<ProceduralAnimator>();
            CrowdMember member = anim != null ? anim.GetComponent<CrowdMember>() : null;

            if (member != null && !member.IsDead)
            {
                result.victim = member;
                member.Kill(direction, hit.rigidbody, hit.point);
            }
            else if (anim != null)
            {
                anim.Push(direction, hit.rigidbody, hit.point); // strzal w trupa
            }
            else if (hit.rigidbody != null)
            {
                hit.rigidbody.AddForceAtPosition(direction * 20f, hit.point, ForceMode.Impulse);
            }
            SoundFx.Play(SoundFx.Crack, hit.point, 0.7f, 5f);
        }

        SpawnTracer(origin + direction * 1.5f, end);

        // Zabicie szpiega nie straszy tlumu. Pudlo albo niewinny bot - panika.
        bool killedSpy = result.victim != null && result.victim.isSpy;
        if (!killedSpy) CrowdMember.TriggerPanic();

        if (GameManager.Instance != null) GameManager.Instance.OnShotFired(result);
        return result;
    }

    static void SpawnTracer(Vector3 from, Vector3 to)
    {
        var go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = LowPolyFactory.GetUnlitMaterial(new Color(1f, 0.95f, 0.7f));
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = 0.05f;
        lr.endWidth = 0.02f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<TracerFade>();
    }
}
