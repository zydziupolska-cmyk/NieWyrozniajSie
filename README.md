# Nie Wyróżniaj Się — prototyp

Asymetryczna skradanka w tłumie botów (Unity 6000.3, URP, Input System, AI Navigation).

## Jak uruchomić
1. Otwórz projekt w Unity **6000.3.25f1**.
2. Otwórz scenę `Assets/Skrypty_Startowe/Scenes/NieWyrozniajSie.unity`
   (albo w menu: **Nie Wyrozniaj Sie → Otworz scene prototypu**).
3. Wciśnij **Play**. Poziom, NavMesh, tłum, kamera i światło budują się same.

## Tryby (solo przeciw AI)
**Graj jako SZPIEG** – donieś 3 walizki (żółte znaczniki) do furgonetek (zielone światło), zanim snajper-AI Cię wypatrzy.
- WSAD ruch, mysz kamera, kółko oddalenie
- SHIFT (trzymaj) – tryb NPC: ruch identyczny jak u botów
- SPACJA – bieg w panice z rękami w górze
- E (przytrzymaj) – złap przedmiot rękami (tylko jako człowiek, nie w trybie NPC); E ponownie – odłóż; LPM – rzuć
- 1–6 – czynności jak u botów: 1 machanie, 2 drapanie się po głowie, 3 telefon,
  4 siadanie (na ławce, jeśli jest obok; inaczej na ziemi), 5 gadanie z gestami, 6 zaglądanie
- Złapaną walizkę możesz nieść w trybie NPC – wyglądasz wtedy jak bot z walizką podróżnego
- Czerwony laser pokazuje, gdzie patrzy snajper; pasek w rogu – jak bardzo Cię podejrzewa

**Graj jako SNAJPER** – w tłumie ukrywa się 2 szpiegów-AI. Masz 5 naboi.
- Mysz celowanie, PPM luneta, kółko przybliżenie x4/x8, LPM strzał
- SHIFT w lunecie – wstrzymanie oddechu, Q – oznacz/odznacz podejrzanego
- Szukaj płynnego, ludzkiego sięgania po walizkę, spóźnionej reakcji na panikę
  i kogoś, kto w panice nie puszcza walizki (boty zwykle wszystko rzucają)

Pudło albo zabity bot = panika tłumu. Esc – pauza. Po rundzie: R – jeszcze raz.

## Mapy (wybór w menu)
- **Parking** – auta, kiosk z kolejką, ławki, donice; furgonetki po bokach.
- **Stacja metra** – filary z tablicami, ławki, biletomaty i kiosk z kolejkami, schody ruchome;
  walizki trzeba wnieść do otwartych drzwi pociągu za barierką peronu.
- **Lotnisko** – taśmy bagażowe z krążącymi walizkami podróżnych, stanowiska odpraw z kolejkami,
  tablice odlotów, poczekalnia; walizki odjeżdżają taksówkami.

## Nawyki botów
Boty siadają na ławkach, stają w kolejkach (kolejka się przesuwa, pierwszy "kupuje"),
zaglądają do aut / na tablice / na taśmę, gadają w grupkach, grają na telefonie, drapią się
po głowie i machają. Szpieg może robić to samo (klawisze 1–6), ale płynnie wykonana czynność
jest dla snajpera podejrzana – najlepiej robić ją w trybie NPC (SHIFT).

## Powtórka i statystyki
Zabicie szpiega pokazuje powtórkę w zwolnionym tempie z boku ofiary. Po rundzie widać
statystyki: czas, strzały, niewinne ofiary, paniki, najdłuższy celny strzał i to,
jak blisko wpadki był szpieg.

## Fizyczne przedmioty
Po parkingu leżą kartony, torby z zakupami, pachołki, worki, piłki i walizki podróżnych
(identyczne jak walizki-cele). Boty same je podnoszą, noszą, odkładają i upuszczają w panice –
dlatego samo niesienie czegoś nie zdradza szpiega. Wszystko można rzucać, kopać i odstrzelić.

## Zasady rundy (4 minuty)
- Szpiedzy wygrywają: wszystkie walizki w furgonetkach albo snajperowi skończyła się amunicja.
- Snajper wygrywa: wszyscy szpiedzy martwi albo skończył się czas.

## Gdzie co jest (`Assets/Skrypty_Startowe/Assets/Scripts`)
- `GameManager` – menu, przebieg rundy, zasady, HUD
- `LevelBuilder` – trzy mapy, budynek snajpera, NavMesh (runtime)
- `ProceduralAnimator` – Active Ragdoll w stylu Human Fall Flat
- `BotAI`, `AISpy`, `AISniper`, `SpyController`, `SniperController` – postacie
- `Prop`, `Suitcase`, `ExtractionZone` – przedmioty do noszenia, walizki-cele, punkty odbioru
- `ActivitySpot`, `ConveyorBelt` – miejsca nawyków botów, taśma bagażowa
- `GameConfig` – wszystkie liczby do balansu w jednym miejscu
