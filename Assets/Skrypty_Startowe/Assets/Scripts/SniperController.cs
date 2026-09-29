using UnityEngine;
using UnityEngine.InputSystem;

public class SniperController : MonoBehaviour
{
    public Camera spyCamera;
    public Camera sniperCamera;
    
    public float lookSpeed = 0.5f;
    private float pitch = 45f;
    private float yaw = 0f;
    
    private bool isSniperActive = false;
    private float targetFOV = 60f;

    void Start()
    {
        // Domyslny stan
        sniperCamera.enabled = false;
        if(spyCamera) spyCamera.enabled = true;
        
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            isSniperActive = !isSniperActive;
            sniperCamera.enabled = isSniperActive;
            if(spyCamera) spyCamera.enabled = !isSniperActive;
            
            // Zresetuj pozycje kursora przy zmianie trybu
            Cursor.lockState = CursorLockMode.Locked;
        }

        if (isSniperActive)
        {
            HandleAiming();
            HandleShooting();
        }
    }

    void HandleAiming()
    {
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * lookSpeed;
            pitch -= delta.y * lookSpeed;
            pitch = Mathf.Clamp(pitch, 10f, 85f); // Ogranicz spojrzenie snajpera w gore/dol
            
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Zoomowanie na prawym przycisku
            if (Mouse.current.rightButton.isPressed) {
                targetFOV = Mathf.Lerp(targetFOV, 15f, Time.deltaTime * 10f); // Przyblizenie
            } else {
                targetFOV = Mathf.Lerp(targetFOV, 60f, Time.deltaTime * 10f); // Odalenie
            }
            sniperCamera.fieldOfView = targetFOV;
        }
    }

    void HandleShooting()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Pusc promien ze srodka ekranu
            Ray ray = sniperCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, 500f, ProceduralAnimator.ShootableMask, QueryTriggerInteraction.Ignore))
            {
                Debug.Log("Strzal w: " + hit.collider.name);

                // Trafiona czesc ciala wie, do kogo nalezy
                RagdollPart part = hit.collider.GetComponent<RagdollPart>();
                ProceduralAnimator target = part != null ? part.owner : hit.collider.GetComponentInParent<ProceduralAnimator>();

                if (target != null && !target.IsDead)
                {
                    // Sprawdz czy trafiono gracza
                    SpyController spy = target.GetComponent<SpyController>();
                    if (spy != null)
                    {
                        Debug.Log("SNAJPER WYGRAL! Szpieg zastrzelony.");
                        spy.Kill(ray.direction, hit.rigidbody, hit.point);
                        return;
                    }

                    // Sprawdz czy trafiono Bota
                    BotAI bot = target.GetComponent<BotAI>();
                    if (bot != null)
                    {
                        bot.Kill(ray.direction, hit.rigidbody, hit.point);
                        TriggerGlobalPanic();
                        return;
                    }
                }

                // Strzal w trupa albo w otoczenie - popchnij to, co dostalo
                if (target != null) target.Push(ray.direction, hit.rigidbody, hit.point);
                else if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(ray.direction * 20f, hit.point, ForceMode.Impulse);
            }

            // Pudlo tez straszy tlum
            TriggerGlobalPanic();
        }
    }

    void TriggerGlobalPanic()
    {
        Debug.Log("Tlum panikuje!");
        BotAI[] allBots = FindObjectsByType<BotAI>(FindObjectsSortMode.None);
        foreach(var bot in allBots)
        {
            bot.TriggerPanic();
        }
    }

    void OnGUI()
    {
        if (isSniperActive)
        {
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;

            // Rysowanie efektu lunety (przyciemnienie po bokach)
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                GUI.color = new Color(0, 0, 0, 0.9f); // Prawie czarny
                float scopeRadius = Screen.height * 0.4f; // Wielkosc wizjera
                
                // Paski blokujace boki ekranu
                GUI.DrawTexture(new Rect(0, 0, centerX - scopeRadius, Screen.height), Texture2D.whiteTexture); // Lewo
                GUI.DrawTexture(new Rect(centerX + scopeRadius, 0, Screen.width, Screen.height), Texture2D.whiteTexture); // Prawo
                GUI.DrawTexture(new Rect(0, 0, Screen.width, centerY - scopeRadius), Texture2D.whiteTexture); // Gora
                GUI.DrawTexture(new Rect(0, centerY + scopeRadius, Screen.width, Screen.height), Texture2D.whiteTexture); // Dol
            }

            // Rysowanie prostego celownika (Czerwony krzyzyk)
            GUI.color = Color.red;
            float size = 30f;
            float thickness = 2f;
            
            // Kreska pozioma
            GUI.DrawTexture(new Rect(centerX - size/2, centerY - thickness/2, size, thickness), Texture2D.whiteTexture);
            // Kreska pionowa
            GUI.DrawTexture(new Rect(centerX - thickness/2, centerY - size/2, thickness, size), Texture2D.whiteTexture);
        }
    }
}
