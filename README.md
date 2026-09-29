# Nie Wyróżniaj Się — prototyp

Asymetryczna skradanka w tłumie botów (Unity 6000.3, URP, Input System, AI Navigation).

## Jak uruchomić
1. Otwórz projekt w Unity **6000.3.25f1**.
2. Otwórz scenę `Assets/Skrypty_Startowe/Scenes/NieWyrozniajSie.unity`
   (albo w menu: **Nie Wyrozniaj Sie → Otworz scene prototypu**).
3. Wciśnij **Play**. Poziom, NavMesh, tłum, kamera i światło budują się same.

## Tryby (solo przeciw AI)
**Graj jako SZPIEG** – ukradnij 3 walizki, zanim snajper-AI Cię wypatrzy.
- WSAD ruch, mysz kamera, kółko oddalenie
- SHIFT (trzymaj) – tryb NPC: ruch identyczny jak u botów
- SPACJA – bieg w panice z rękami w górze
- E (trzymaj) – kradzież walizki (nie działa w trybie NPC)
- Czerwony laser pokazuje, gdzie patrzy snajper; pasek w rogu – jak bardzo Cię podejrzewa

**Graj jako SNAJPER** – w tłumie ukrywa się 2 szpiegów-AI. Masz 5 naboi.
- Mysz celowanie, PPM luneta, kółko przybliżenie x4/x8, LPM strzał
- SHIFT w lunecie – wstrzymanie oddechu
- Szukaj płynnych ruchów, grzebania przy walizkach i spóźnionej reakcji na panikę

Pudło albo zabity bot = panika tłumu. Esc – pauza. Po rundzie: R – jeszcze raz.

## Zasady rundy (4 minuty)
- Szpiedzy wygrywają: wszystkie walizki skradzione albo snajperowi skończyła się amunicja.
- Snajper wygrywa: wszyscy szpiedzy martwi albo skończył się czas.

## Gdzie co jest (`Assets/Skrypty_Startowe/Assets/Scripts`)
- `GameManager` – menu, przebieg rundy, zasady, HUD
- `LevelBuilder` – parking, budynek snajpera, NavMesh (runtime)
- `ProceduralAnimator` – Active Ragdoll w stylu Human Fall Flat
- `BotAI`, `AISpy`, `AISniper`, `SpyController`, `SniperController` – postacie
- `GameConfig` – wszystkie liczby do balansu w jednym miejscu
