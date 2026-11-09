# Stan prac

Ten dokument rozdziela trzy rzeczy: **zaimplementowane**, **zweryfikowane** i **brakujące lub
uproszczone**. Nie oznaczam nic jako przetestowane, jeśli nie zostało uruchomione.

## Co zostało zweryfikowane i jak

| Obszar | Weryfikacja | Wynik |
|---|---|---|
| Core (droga, fizyka, ruch, kamera, jakość, wejście, layout, niebo, dźwięk, zapis, garaż) | `dotnet test tools/CoreTests` (.NET 8, NUnit 4) | **53/53 przechodzi** |
| Warstwa Unity — kompilacja semantyczna | `dotnet build tools/UnityCheck` przeciw `UnityEngine.Modules` 2021.3 | **Kompiluje się** (z stubami uGUI, URP, EventSystems i Input System) |
| Warstwa Unity — uruchomienie w Unity Editorze | — | **Nie wykonano** (brak Unity Editora i licencji w środowisku) |
| APK | — | **Nie wygenerowano** |
| Urządzenie (Galaxy Z Fold 8 Ultra) | — | **Nie wykonano** |

### Wyniki testów rdzenia

Pomiary z tego środowiska (desktop .NET, nie IL2CPP na telefonie). Liczby są orientacyjne i służą
do kontroli realizmu, nie są deklaracją wydajności na urządzeniu.

| Test | Wynik | Oczekiwanie |
|---|---|---|
| Przyspieszenie 0–100 km/h (RWD, Sport) | 5,70 s | 3,0–6,5 s |
| Przyspieszenie 0–100 km/h (trzy auta) | Aurora Veloce 5,63 s · Tempesta GTX 5,00 s · Kestrel SR 6,57 s | 2,5–9,0 s |
| Prędkość maksymalna (90 s pełnego gazu) | 310,9 km/h | 290–340 km/h |
| Hamowanie 100→0 km/h, ABS | 36,5 m | 30–48 m |
| Stała krzywizna R=100 m przy 20 m/s | 0,396 g (teoria 0,408 g) | ±15% |
| Nasycenie opon w zakręcie | 1,26 g | < 1,35 g |
| 0–72 km/h na mokrym, RWD / AWD | 7,50 s / 4,53 s | AWD szybsze |
| Autopilot, 240 s na wygenerowanej drodze | 7092 m, 0 kolizji z barierą | brak wyjazdu z drogi |
| Ten sam przebieg przy różnych klatkach | różnica < 1e-6 przy tych samych krokach fizyki | niezależność od FPS |
| Droga, 100 km | ciągła (odstęp próbek 2,000 m ±1e-6), kąt w stożku, bez NaN | wszystkie warunki spełnione |
| Droga, odtworzenie po cofnięciu | identyczne próbki (bit w bit) | deterministyczne |
| Ruch drogowy, 2 min, 12 aut | brak nakładania się aut w tym samym pasie; 7 kontaktów z graczem (gracz bez hamowania) | patrz ograniczenia |
| Generacja terenu, koszt | 700 wywołań wysokości terenu (jeden chunk, dalekie pierścienie): 1,3 ms; 10 km drogi: 20 ms | patrz ryzyka |

## Zaimplementowane funkcje

### Rdzeń (czysta logika, przetestowana)
- Nieskończona, deterministyczna droga z węzłami (ciągłość G2), budżetem skrętu, stożkiem kierunku,
  pochyleniem poprzecznym, pasami zmieniającymi się na prostych, mostami jako flagą (`Bridge`).
- Siedem biomów z płynnym mieszaniem parametrów.
- Teren z szumu Perlina (fBm), zgodny z drogą na pasie przejścia.
- Fizyka: opony magic formula z poślizgiem połączonym, zawieszenie sprężyna-tłumik, napęd
  (FWD/RWD/AWD, turbo z opóźnieniem, skrzynia auto/ręczna), hamulce z fadingiem, ABS, TC, ESP,
  asysta kierownicy, aero, opór toczenia, bariera z kolizją.
- Trzy samochody o różnym charakterze (`VehicleCatalog`), oryginalne nazwy.
- Tryb wyścigu: pozycje, kolejność na mecie i odstępy (`RaceDirector`), przeciwnicy AI z IDM w pasach (`AiRacerModel`), testy w CoreTests.
- Tunele i mosty jako zdarzenia drogi (`RoadSample.Tunnel`, `Bridge`): przełączają się po 60 m trwania, z histerezą; testy w RoadStructureTests. Renderowanie tuneli i filarów mostów jeszcze nie istnieje. Udział jest wysoki w alpejskim terenie i wymaga strojenia.
- Ruch AI: unik na wielopasmowych drogach, bezpieczna luka przy zmianie pasa, testy scenariuszy z hamującym graczem (`TrafficEvasionTests`). Ograniczenie: na dwupasmowej drodze auto nie ma gdzie uciec, więc gracz jadący bez hamowania w wolniejsze auto uderzy.
- Harmonogram pogody i pory dnia (`WeatherSchedule`): deterministyczne przejścia pogody i godzina doby; testy płynności, testy w CoreTests. Jeszcze nie podpięty do GameSession.
- Strojenie aut w garażu (`VehicleTuning`): sprężyny, tłumienie, przełożenie, hamulce i mieszanka opon z limitami zakresów; testy w CoreTests.
- Ruch AI (IDM), zmiana pasa z przerwą, reakcja na gracza, zdarzenia wyprzedzania i bliskiego mijania.
- Kamery: chase, bumper, hood, cockpit, cinematic (krytycznie tłumione, bez przeregulowania).
- Governor jakości z histerezą, reakcją na termikę i baterię.
- Wejście: martwa strefa, krzywe, czułość, pedały z rampą, przechył.
- Layout ekranu (klasy Cover/Unfolded/Tablet/Phone), kontrolki w bezpiecznym obszarze.
- Niebo: pozycja słońca z szerokości, daty i godziny; noc, złota godzina; pogoda z mokrą nawierzchnią.
- Dźwięk silnika: syntezator harmoniczny, zależny od obrotów i obciążenia.
- Tryby: Time Attack (sektory, najlepszy czas), Traffic Challenge (punktacja, mnożnik, kary).
- Zapis: wersjonowany, z migracją, bez utraty nieznanych danych, atomowy zapis.

### Warstwa Unity (napisana, kompiluje się semantycznie, nieuruchomiona)
- Proceduralne chunki drogi i terenu z oznakowaniem, pulą i floating origin.
- Instancjonowane drzewa i skały, deterministyczne na chunk.
- Samochód: proceduralne nadwozie (superellipsa, przeszklenie, światła), koła z oponą i felgą,
  przechyły i zanurzenie z zawieszenia, obrót kół, światła hamowania.
- Ruch AI jako osobne wizualne samochody.
- Kamery i przełączanie widoków; HUD (prędkość, bieg, obrotomierz, asysty, tryb, pogoda, flash kolizji).
- Kontrolki dotykowe (pady, gaz, hamulec, ręczny, biegi, widok, pauza) oraz pad, klawiatura i przechył.
- Silnik audio przez `OnAudioFilterRead` z buforem przydzielonym raz.
- Menu: start, pauza, garaż z obracającym się autem, wybór trybu/auta/lakieru/pogody/pory dnia/asyst.
- Platforma: odczyt safe area, odświeżania, baterii; termika Androida przez JNI (tylko na Androidzie).
- Wydajność: skala renderowania URP, liczba chunków, MSAA, docelowy FPS według poziomu.
- Narzędzia edytora: konfiguracja projektu (IL2CPP, ARM64, Vulkan, orientacja pozioma, liniowa
  przestrzeń barw, minSdk 30), utworzenie sceny i `RaceConfig`, budowa APK (również `-batchmode`).

## Brakujące lub uproszczone (świadome ograniczenia)

Ta lista jest ważniejsza niż lista funkcji. Wymagania projektu opisują wiele elementów, których
**nie zrobiono**.

### Grafika i świat
- **Modele samochodów nie są szczegółowe.** Karoseria to proceduralna superellipsa; nie ma wnętrza,
  LOD, PBR z teksturami ani licencjonowanych modeli. Wymagania mówią wprost, że to nie może być wersja docelowa.
- **Brak budynków, znaków, lamp, barierek ze zdobieniami.** Świat ma teren, drogę, oznakowanie,
  drzewa i skały.
- **Tunele nie są renderowane jako tuby.** Flaga `Bridge` jest liczona, ale nie ma pilonów mostu.
  Głęboki wykop jest zwykłą ścianą skalną.
- **Brak wody** (morze/jezioro) mimo biomu wybrzeża.
- **Brak deszczu i kropli na szybie, rozprysków, cząsteczek.** Pogoda wpływa na przyczepność,
  mgłę, kolor nieba i nasycenie nawierzchni, ale nie na efekty cząsteczkowe.
- **Brak refleksów mokrego asfaltu** (SSR/planar); mokra nawierzchnia to niższa przyczepność i ciemniejszy kolor.
- Brak dedykowanego shadera asfaltu z połyskiem; używany jest URP Lit z teksturą proceduralną.

### Rozgrywka
- **Brak trybu AI Racing** (wyścig z przeciwnikami o pozycję) i **Photo Mode**.
- **Free Ride** to Endless Drive z wyborem pogody i pory dnia; brak suwaków trudności i gęstości ruchu w menu.
- **Strojenie auta w garażu nie jest wystawione w UI** (sprężyny, przełożenia, zawieszenie). Model
  to przyjmuje (`VehicleSpec`, `VehicleTuningData`), ale nie ma ekranów.
- **Brak powtórek i nagrywania.** Kamera filmowa działa na żywo.
- **Ruch AI nie gwarantuje braku kolizji z graczem:** w testach 2-minutowych bez hamowania było
  7 kontaktów. Samochody zmieniające pas nie przewidują wszystkich sytuacji.
- **Brak uszkodzeń wizualnych** (wgnieceń). Uszkodzenie jest liczbą w HUD.
- Pozycje w wyścigu i zapis wyników wyścigów z innymi autami nie są zaimplementowane.

### Fizyka
- **Sprzęgło jest uproszczone**: zablokowane powyżej obrotów jałowych, bez poślizgu sprzęgła.
- **Brak modelu kół na nierównościach**: nawierzchnia jest gładka, bez profilu wibracji.
- **ESP działa jako moment korygujący**, nie jako rzeczywiste hamowanie pojedynczych kół.
- **Brak wzajemnej fizyki zderzeń z ruchem AI** (kolizja jest zdarzeniem, bez odrzutu).
- Opony nie mają temperatury ani zużycia; wpływ wody to współczynnik tarcia.
- Model nie zawiera sił bocznych wiatru ani aerodynamiki zależnej od pochylenia nadwozia.

### Dźwięk
- Silnik jest syntetyczny (brak nagrań i miksu przestrzennego). Brak dźwięków opon, wiatru,
  deszczu, zderzeń i przełączania biegów (poza zmianą obrotów).
- Dźwięk nie reaguje na pozycję kamery (kokpit vs zewnętrzna).

### Urządzenie i wydajność
- **Nie zmierzono wydajności na Galaxy Z Fold 8 Ultra.** Koszt budowy chunka na telefonie nie jest znany.
  Na desktopie 700 wywołań terenu zajmuje 1,3 ms; na telefonie w IL2CPP spodziewam się kilkukrotnie
  więcej. Dlatego na słabszych poziomach budowa jest ograniczona do jednego chunka na klatkę. To ryzyko
  do sprawdzenia w profilerze (Unity Profiler, Android GPU Inspector).
- Nie zweryfikowano składania na Foldzie: klasy ekranów są testowane na wymiarach, ale nie na
  rzeczywistym cover/inner screenie. Przechył wymaga kalibracji osi (`TiltInvert`).
- Nie sprawdzono termiki (PowerManager przez JNI), wyświetlania odświeżania 120 Hz ani trybów
  Battery Saver na urządzeniu.
- Brak wizualnej reakcji na zmianę orientacji poza blokadą poziomą.

### Build i narzędzia
- **APK nie został wygenerowany.** Narzędzie `Race1 > Build Android APK` i tryb `-batchmode`
  są gotowe, ale nie były uruchomione.
- Pliki `.meta` nie są w repozytorium; Unity utworzy je przy pierwszym imporcie. Zalecane jest
  dodanie ich do repozytorium po pierwszym otwarciu, żeby GUID-y były stabilne.
- Ustawienie *Active Input Handling = Both* trzeba wykonać ręcznie (wymagane przez kod wejścia).
- Pakiety w `Packages/manifest.json` mają wersje z linii Unity 6; nie były testowane z Unity 6.0.23.
- Brak podpisu release: budowa używa domyślnego klucza debug. Do Google Play potrzebny jest własny keystore.

### Sprawdzenie warstwy Unity (co dokładnie zostało zrobione)
`tools/UnityCheck` kompiluje cały kod `Game` i `Core` przeciw oficjalnym referencjom
`UnityEngine.Modules` 2021.3. Brakujące pakiety (uGUI, URP, EventSystems, Input System) są zastąpione
stubami z `tools/UnityCheck/Stubs.cs`. Wyłączone są gałęzie Androida (JNI) i edytora. Wynik to
**błędy kompilacji zero**. Nie jest to walidacja zachowania ani API Unity 6 (np. `refreshRateRatio`
jest chroniony `UNITY_6000_0_OR_NEWER`). Warstwa `Editor` nie jest kompilowana tym sprawdzeniem.

Podczas tej kontroli wykryto i naprawiono realne błędy: zduplikowana nazwa `DisplayInfo`
(koliza z `UnityEngine.DisplayInfo`), literały `double` w `new Color(...)`, brak `using`
dla `GraphicsSettings`, właściwość `Path` zasłaniająca `System.IO.Path` oraz metody UI
zasłaniające typy `Text` i `Button`.

## Następne kroki (w kolejności)

1. Zbudować APK (`docs/BUILD_AND_INSTALL.md`), uruchomić na Galaxy Z Fold 8 Ultra i zapisać wyniki
   w tabeli poniżej.
2. Zmierzyć koszt budowy chunka i klatki w Unity Profilerze, przy 60 i 120 Hz.
3. Zastąpić proceduralne samochody autorskimi modelami (Blender, LOD, PBR) lub modelami z licencją.
4. Dodać tunele (tuba), mosty (filary), wodę i barierki.
5. Dodać tryb AI Racing i Photo Mode.
6. Wystawić strojenie auta w garażu (sprężyny, przełożenia, zapis w `VehicleTuningData`).

## Tabela testów urządzeniowych (do uzupełnienia)

| # | Test | Wynik na Fold 8 Ultra | Uwagi |
|---|---|---|---|
| 1 | Start gry i wejście na trasę | nie wykonano | |
| 2 | Gaz, hamulec, kierownica dotykowa | nie wykonano | |
| 3 | Zakręty przy 50/100/200/300 km/h | nie wykonano | |
| 4 | 100 km ciągłej jazdy (bez zatrzymań) | nie wykonano | |
| 5 | Brak niepołączonych segmentów, brak skoków wysokości | nie wykonano | |
| 6 | Mosty i tunele (tunele: wykop) | nie wykonano | |
| 7 | Pamięć podczas długiej jazdy | nie wykonano | |
| 8 | Zmiana kamery w trakcie jazdy | nie wykonano | |
| 9 | Zmiana pogody i pory dnia | nie wykonano | |
| 10 | Zapis i odczyt stanu | nie wykonano | |
| 11 | Różne proporcje ekranu, złożenie/rozłożenie Folda | nie wykonano | |
| 12 | Termika po 20 min | nie wykonano | |
| 13 | Kontroler Bluetooth | nie wykonano | |
| 14 | Przechył telefonu | nie wykonano | wymaga kalibracji |
