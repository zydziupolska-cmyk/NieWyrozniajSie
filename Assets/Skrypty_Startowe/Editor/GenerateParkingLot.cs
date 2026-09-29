using UnityEngine;
using UnityEditor;

public class GenerateParkingLot : EditorWindow
{
    [MenuItem("Nie Wyrozniaj Sie/3. Zbuduj Parking i Snajpera")]
    public static void BuildParkingLot()
    {
        // 1. Pomaluj podloge na asfaltowy kolor
        GameObject floor = GameObject.Find("Floor");
        if (floor != null)
        {
            floor.transform.localScale = new Vector3(8, 1, 8); // Powiekszamy parking
            Renderer floorRend = floor.GetComponent<Renderer>();
            Material asphalt = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if(asphalt.shader == null) asphalt = new Material(Shader.Find("Standard"));
            asphalt.color = new Color(0.2f, 0.2f, 0.2f);
            floorRend.sharedMaterial = asphalt;
        }

        // Usuwamy stare przeszkody, zeby nie powielac
        GameObject oldProps = GameObject.Find("ParkingProps");
        if (oldProps != null) DestroyImmediate(oldProps);

        GameObject propsGroup = new GameObject("ParkingProps");

        // 2. Generowanie "Samochodow"
        Material carMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if(carMat.shader == null) carMat = new Material(Shader.Find("Standard"));
        carMat.color = new Color(0.1f, 0.4f, 0.8f); // Niebieskie auta

        for (int i = 0; i < 15; i++)
        {
            GameObject car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.name = "Car_" + i;
            car.transform.SetParent(propsGroup.transform);
            
            // Losowa pozycja, ale upewniamy sie ze stoja na ziemi
            float rx = Random.Range(-35f, 35f);
            float rz = Random.Range(-35f, 35f);
            car.transform.position = new Vector3(rx, 1f, rz);
            car.transform.localScale = new Vector3(2f, 1.5f, 4.5f); // Proporcje auta
            car.transform.rotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0); // Obracamy co 90 stopni
            
            GameObjectUtility.SetStaticEditorFlags(car, StaticEditorFlags.NavigationStatic);
            car.GetComponent<Renderer>().sharedMaterial = carMat;
        }

        // 3. Generowanie Filarow
        Material pillarMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if(pillarMat.shader == null) pillarMat = new Material(Shader.Find("Standard"));
        pillarMat.color = Color.gray;

        for (int i = 0; i < 8; i++)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar_" + i;
            pillar.transform.SetParent(propsGroup.transform);
            
            float rx = Random.Range(-30f, 30f);
            float rz = Random.Range(-30f, 30f);
            pillar.transform.position = new Vector3(rx, 3f, rz);
            pillar.transform.localScale = new Vector3(2f, 3f, 2f);
            
            GameObjectUtility.SetStaticEditorFlags(pillar, StaticEditorFlags.NavigationStatic);
            pillar.GetComponent<Renderer>().sharedMaterial = pillarMat;
        }

        // 4. Setup Snajpera
        GameObject oldSniper = GameObject.Find("SniperCameraObj");
        if (oldSniper != null) DestroyImmediate(oldSniper);

        GameObject sniperObj = new GameObject("SniperCameraObj");
        sniperObj.transform.position = new Vector3(0, 25f, -30f); // Wysoko nad ziemia, z boku
        sniperObj.transform.rotation = Quaternion.Euler(45f, 0f, 0f); // Patrzy w dol

        Camera sniperCam = sniperObj.AddComponent<Camera>();
        sniperCam.enabled = false;

        SniperController sniperCtrl = sniperObj.AddComponent<SniperController>();
        sniperCtrl.sniperCamera = sniperCam;
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            sniperCtrl.spyCamera = mainCam;
        }

        Debug.Log("Zbudowano Parking i dodano pozycje Snajpera! PAMIĘTAJ KLIKNĄĆ BAKE W OKNIE NAVIGATION!");
    }
}
