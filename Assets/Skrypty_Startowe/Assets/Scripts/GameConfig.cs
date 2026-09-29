using UnityEngine;
using UnityEngine.AI;

// Wspolne stale rozgrywki. Szpieg w trybie NPC musi poruszac sie dokladnie jak bot,
// dlatego predkosci sa zdefiniowane w jednym miejscu.
public static class GameConfig
{
    public const float BotWalkSpeed = 2.5f;
    public const float PanicSpeedMultiplier = 2f;
    public const float BotPanicSpeed = BotWalkSpeed * PanicSpeedMultiplier;
    public const float SpyHumanSpeed = 4.5f;
    public const float PanicDuration = 6f;

    public const float StealDistance = 1.7f;

    public const float RoundTime = 240f;
    public const int SuitcaseCount = 3;
    public const int AISpyCount = 2;
    public const int PlayerSniperAmmo = 5;
    public const int AISniperAmmo = 4;

    public const float LotHalfSize = 40f;
    public const int DecoySuitcases = 8;     // walizki podroznych - wygladaja jak cele
    public const float PropsPerBot = 0.3f;   // kartony, torby, pacholki, worki, pilki

    // Ustawienia agenta wspolne dla botow i szpiegow AI - toporne, natychmiastowe skrety i starty
    public static void ConfigureBotAgent(NavMeshAgent agent)
    {
        agent.speed = BotWalkSpeed;
        agent.acceleration = 40f;
        agent.angularSpeed = 720f;
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.stoppingDistance = 0.3f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(30, 70);
    }

    // Losowy punkt na NavMeshu w poblizu center
    public static bool RandomNavPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            p.x = Mathf.Clamp(p.x, -LotHalfSize, LotHalfSize);
            p.z = Mathf.Clamp(p.z, -LotHalfSize, LotHalfSize);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }
}
