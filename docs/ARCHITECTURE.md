# Architektura

Gra jest podzielona na dwie warstwy:

- **Core** (`Assets/Race1/Core`) — czysta logika bez `UnityEngine`. Każdy algorytm, który musi być
  deterministyczny albo testowalny (droga, fizyka, ruch AI, skalowanie jakości), mieszka tutaj.
  Ten sam kod jest kompilowany przez Unity (asmdef `Race1.Core`, `noEngineReferences`) i przez `dotnet test`.
- **Game** (`Assets/Race1/Game`) — adaptery do silnika: tworzą obiekty sceny, mesh, materiały, UI,
  wejście i audio. Nie zawierają reguł fizyki ani generacji drogi.

## Punkt wejścia

`GameSession` (`Game/Bootstrap/GameSession.cs`) jest korzeniem kompozycji. Tworzy się automatycznie
przez `RuntimeInitializeOnLoadMethod`, więc gra działa z dowolnej sceny (narzędzie konfiguracji tworzy
`Main.unity` tylko po to, żeby budowa miała scenę). Ma trzy stany:

| Stan | Co się dzieje |
|---|---|
| `Menu` | Showroom z obracającym się autem, wybór trybu, auta, lakieru, pogody, pory dnia, asyst |
| `Driving` | Pętla: wejście → fizyka → ruch AI → tryb (timer/punkty) → strumień dróg → widoki → kamera → HUD |
| `Paused` | Zamrożony kadr, panel pauzy, zapis |

## Układy współrzędnych

**Droga jest przestrzenią nadrzędną.** Samochód nie ma współrzędnych świata w fizyce, tylko
współrzędne drogi:

- `s` — długość łuku wzdłuż osi drogi (metry, `double`),
- `t` — odległość boczna od osi (dodatnia w prawo),
- `eps` — błąd kąta (kierunek auta względem stycznej drogi).

Kinematyka (`VehicleDynamics.IntegrateRoadRelative`):

```
s'   = (vx·cos eps − vy·sin eps) / (1 − κ·t)
t'   = vx·sin eps + vy·cos eps
eps' = r − κ·s'
```

Dzięki temu auto jedzie po nieskończonej trasie bez dryfu współrzędnych, a przejście przez zakręt
o promieniu 28 m nie wymaga żadnej specjalnej logiki. Położenie w świecie to
`RoadSample.PointAt(t)`, kierunek to `Forward` obrócony o `eps`, a „góra” to normalna drogi.

Konwencja: Unity (X w prawo, Y w górę, Z do przodu, układ lewoskrętny). Dodatnia krzywizna = skręt w prawo.
Ruch AI używa tej samej ramy (`TrafficSimulation`), więc gracz i ruch są spójne bez przeliczeń.

## Droga proceduralna (`Core/Road`)

- **Węzły (knots).** Między węzłami krzywizna i nachylenie zmieniają się liniowo (rampy zbliżone do
  klotoid). Dzięki temu kąt jest ciągły w pierwszym rzędzie, a położenie w drugim (G2) na granicach.
  Każdy węzeł losuje cele z RNG seedowanego przez `(seed świata, numer węzła)`.
- **Ograniczenia jazdy.** Promień min. 28 m, zmiana krzywizny ≤ `MaxCurvatureRate` (1/m²), zmiana
  nachylenia ≤ `MaxGradientRate`, budżet skrętu na węzeł (`MaxTurnPerKnot`, domyślnie 0,4 rad).
  Budżet zapobiega pętlom: bez niego ciasny łuk trzymany przez 200 m daje ~6,7 rad (pełne kółko).
- **Cone kierunku.** Kąt nagłówka jest utrzymywany w granicach `MaxHeadingDrift` (0,9 rad), więc droga
  nie zawija się na siebie. Test 100 km sprawdza, że odchylenie mieści się w tym stożku.
- **Droga podąża za terenem.** Cel nachylenia jest przyciągany do naturalnego terenu (`deltaH`), więc
  rzadziej trzeba budować mosty i wykopy.
- **Pochylenie poprzeczne** (bank) = `atan(κ·v²/g)` przycięte do 7°. Ciągłe, ograniczone szybkością zmiany κ.
- **Chunki.** `RoadTrack` trzyma okno próbek (co 2 m) i punkty kontrolne co 2 km (`RoadState`).
  Powrót do wcześniejszego miejsca odtwarza trasę od najbliższego punktu kontrolnego i daje
  **bit-w-bit identyczne** próbki (test `Rewind_RegeneratesIdenticalSamples`).
- **Teren na pasie drogi.** `RoadTerrainBlend.GroundHeight`: na jezdni i poboczu powierzchnia to
  dokładnie droga; dalej teren łączy się z naturalnym przez 22 m (`smoothstep`). Ciągłość na krawędzi
  i dopasowanie do naturalnego terenu są testowane.

### Biomy

Siedem biomów (`Core/World/Biomes.cs`): łąka, las, alpy, wybrzeże, autostrada, pustynia, miasto.
Wagi biomów to szum niskiej częstotliwości (`BiomeField`), normalizowany do sumy 1. Parametry
(amplituda terenu, promienie zakrętów, nachylenie, gęstość roślinności, kolor) są mieszane **ważoną
średnią**, więc przejście biomów jest płynne. Liczba pasów zależy od dominującego biomu i zmienia się
tylko na prostych odcinkach, z rampą szerokości.

## Fizyka (`Core/Vehicle`)

Stały krok `1/600 s` niezależny od klatek renderowania (`VehicleDynamics.Advance` dzieli czas
na podkroki z tolerancją zmiennoprzecinkową). Test sprawdza, że ten sam przebieg wejścia
daje ten sam wynik przy różnych długościach klatek.

| Podsystem | Implementacja |
|---|---|
| Opony | Pacejka-podobna krzywa „magic formula” (znormalizowana do szczytu 1), połączony poślizg (friction ellipse), zależność od obciążenia |
| Zawieszenie | Sprężyny i tłumiki na każdym narożniku; obciążenie kół = siła sprężyno-tłumika, więc przenoszenie masy ma realne opóźnienie |
| Koła | Niejawna aktualizacja prędkości kątowej (stabilna przy dużej sztywności poślizgu) |
| Napęd | Biegi, przełożenie, turbo z opóźnieniem, hamowanie silnikiem, FWD/RWD/AWD, limiter, zmiana biegu z przerwą momentu |
| Hamulce | Rozdział przód/tył, fading od ciepła, ręczny na tylnych kołach |
| Asysty | ABS (modulacja momentu hamowania), TC (redukcja momentu napędu), ESP (moment korygujący), asysta kierownicy |
| Aero/opór | Opór aerodynamiczny, docisk (wpływa na obciążenie kół), opór toczenia (×2,5 poza jezdnią) |
| Bariera | Barierka za poboczem: odbicie, utrata prędkości, licznik kolizji i uszkodzeń |

Model **nie** symuluje obrotów kół z pełnym sprzęgłem; sprzęgło jest zablokowane powyżej obrotów
jałowych, a przy ruszaniu moment jest ograniczany przez przyczepność opon. Jest to uproszczenie
świadome i opisane w `docs/STATUS.md`.

## Ruch drogowy (`Core/Traffic`)

- **IDM** (Intelligent Driver Model) do podążania za pojazdem z przodu — gładkie hamowanie bez kolizji.
- Gracz jest przeszkodą dla pojazdów jadących w tym samym kierunku.
- Zmiana pasa tylko przy bezpiecznej przerwie (`LaneChangeGap`) i z cooldownem.
- Pojazdy pojawiają się przed graczem i są recyklowane za nim. Ruch przeciwny jedzie po lewym pasie.
- Zdarzenia (wyprzedzenie, bliski mijanie, kolizja) są krawędziowe, nie co klatkę.

## Strumieniowanie i wydajność (`Game/Road`, `Game/Vegetation`)

- **Chunki** po 100 m. Każdy to jeden mesh z pierścieniami poprzecznymi (jezdnia, pobocze, teren
  w pasie przejścia) i osobnym submeshem oznakowania. Wierzchołki są względne do początku chunka.
- **Budżet budowy**: 1–2 chunki na klatkę (zależnie od poziomu jakości), z limitem czasu.
- **Pula obiektów**: chunki poza zasięgiem są wyłączane i wracają do puli.
- **Floating origin**: gdy samochód oddali się o 400 m od początku układu, początek przesuwa się do
  samochodu, a wszystkie transformacje są przeliczane. Dzięki temu `float` w Unity nie traci precyzji.
- **Instancjonowanie**: drzewa i skały to `Graphics.DrawMeshInstanced` (paczki po 1023), nie
  osobne obiekty. Rozmieszczenie jest deterministyczne dla każdego chunka.
- **Jakość** (`QualityGovernor`): najpierw skala renderowania (dynamiczna rozdzielczość), potem
  poziom jakości; wzrost dopiero po 3 s zapasu, żeby nie oscylować. Termika Androida (PowerManager)
  obniża jakość natychmiast. Poziomy: Battery Saver, Balanced, High, Ultra.

## Wejście (`Game/Input`, `Core/Input`)

Źródła: ekran dotykowy (pady sterowania, gaz, hamulec, ręczny, zmiana biegów), gamepad/Bluetooth
(Input System), klawiatura (edytor) i przechył (akcelerometr, do kalibracji). Wszystkie przechodzą
przez `InputShaping` z rdzenia: martwa strefa, krzywa, czułość, ograniczenie prędkości kierownicy
i płynne pedały (szybkie zwolnienie, wolniejsze wciśnięcie).

## Layout Folda (`Core/Display`, `Game/Platform`)

`DisplayLayout.Classify` rozpoznaje klasę ekranu po wymiarach i proporcjach (nie po nazwie urządzenia):
Cover (bardzo szeroki, mały), Unfolded (duży, bliski kwadratu), Tablet, Phone. Na tej podstawie
rozmieszczane są HUD i kontrolki w bezpiecznym obszarze (`Screen.safeArea`). Wymiary są odpytywane
co 0,5 s, więc złożenie/rozłożenie przebudowuje HUD w locie.

## Zapis

`SaveData` używa tylko typów, które obsługuje `JsonUtility` (bez `double` i `ulong`). Pozycja na drodze
jest zapisywana jako tekst w formacie „round-trip”, więc nie traci precyzji. Zapis jest atomowy
(plik tymczasowy + zamiana). `SaveMigrator` przenosi stare wersje do bieżącej i nie usuwa danych,
których nie rozumie.

## Testy

Testy NUnit (`Assets/Race1/Tests/Core`) są napisane tak, żeby działały zarówno w NUnit 3 (Unity 6),
jak i w NUnit 4 (`dotnet test`). Helper `Check` zastępuje metody klasyczne.
