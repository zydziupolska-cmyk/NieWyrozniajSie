using UnityEngine;
using UnityEngine.AI;

public class CrowdSpawner : MonoBehaviour
{
    public GameObject botPrefab;
    public int botsToSpawn = 50; // Zaczniemy od 50 dla testów, docelowo 500
    public float spawnRadius = 30f;

    void Start()
    {
        for (int i = 0; i < botsToSpawn; i++)
        {
            SpawnBot();
        }
    }

    void SpawnBot()
    {
        Vector3 randomPos = transform.position + Random.insideUnitSphere * spawnRadius;
        randomPos.y = transform.position.y; // Keep on ground level
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPos, out hit, spawnRadius, 1))
        {
            Instantiate(botPrefab, hit.position, Quaternion.identity);
        }
        else
        {
            // If failed to find navmesh, try again (simple fallback)
            Instantiate(botPrefab, transform.position, Quaternion.identity);
        }
    }
}
