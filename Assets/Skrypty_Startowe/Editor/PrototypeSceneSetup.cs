using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Nie Wyrozniaj Sie > ...
// Scena prototypu potrzebuje tylko obiektu z GameManagerem - poziom, tlum,
// kamera i swiatlo powstaja w runtime.
public static class PrototypeSceneSetup
{
    const string ScenePath = "Assets/Skrypty_Startowe/Scenes/NieWyrozniajSie.unity";

    [MenuItem("Nie Wyrozniaj Sie/Otworz scene prototypu")]
    public static void OpenPrototypeScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            CreatePrototypeScene();
            return;
        }
        EditorSceneManager.OpenScene(ScenePath);
        AddToBuildSettings();
    }

    [MenuItem("Nie Wyrozniaj Sie/Utworz scene prototypu od nowa")]
    public static void CreatePrototypeScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("GameManager").AddComponent<GameManager>();

        if (!AssetDatabase.IsValidFolder("Assets/Skrypty_Startowe/Scenes")) AssetDatabase.CreateFolder("Assets/Skrypty_Startowe", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        Debug.Log("Scena prototypu gotowa: " + ScenePath + ". Wcisnij Play!");
    }

    static void AddToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
