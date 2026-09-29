using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

public class AutoSetupScene : EditorWindow
{
    [MenuItem("Nie Wyrozniaj Sie/1. Wygeneruj Scene Testowa")]
    public static void SetupScene()
    {
        // 1. Create Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(10, 1, 10);
        
        // Add Static flag for NavMesh
        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);

        // 2. Create Bot Prefab
        GameObject bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bot.name = "DummyBot";
        bot.AddComponent<NavMeshAgent>();
        bot.AddComponent<BotAI>();
        
        // Change color to red
        Renderer botRenderer = bot.GetComponent<Renderer>();
        Material redMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if(redMat.shader == null) redMat = new Material(Shader.Find("Standard")); // Fallback
        redMat.color = Color.red;
        botRenderer.sharedMaterial = redMat;

        // Save as prefab in Assets
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        string localPath = "Assets/Prefabs/DummyBot.prefab";
        localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);
        GameObject botPrefab = PrefabUtility.SaveAsPrefabAsset(bot, localPath);
        DestroyImmediate(bot); // Remove from scene

        // 3. Create Spawner
        GameObject spawner = new GameObject("CrowdSpawner");
        spawner.transform.position = Vector3.zero;
        CrowdSpawner spawnerScript = spawner.AddComponent<CrowdSpawner>();
        spawnerScript.botPrefab = botPrefab;
        spawnerScript.botsToSpawn = 50;
        spawnerScript.spawnRadius = 30f;

        // 4. Create Player
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "SpyPlayer";
        player.transform.position = new Vector3(0, 1, 0);
        player.AddComponent<SpyController>();
        
        // Change color to blue
        Renderer playerRenderer = player.GetComponent<Renderer>();
        Material blueMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if(blueMat.shader == null) blueMat = new Material(Shader.Find("Standard"));
        blueMat.color = Color.blue;
        playerRenderer.sharedMaterial = blueMat;

        // 5. Setup Camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0, 15, -15);
            mainCam.transform.rotation = Quaternion.Euler(45, 0, 0);
        }

        Debug.Log("Scena wygenerowana! Jedyne co musisz zrobić, to wejść w Window -> AI -> Navigation i kliknąć Bake.");
    }
}
