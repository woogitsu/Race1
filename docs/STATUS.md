# Stan prac

Ten dokument rozdziela trzy rzeczy: **zaimplementowane**, **zweryfikowane** i **brakujące lub
uproszczone**. Nie oznaczam nic jako przetestowane, jeśli nie zostało uruchomione.

Ostatni przegląd jakości (G10): 2026-11-09. Wszystko, co dotyczy grafiki i dźwięku, jest **napisane i
skompilowane, ale nieobejrzane ani nieusłyszane** w Unity. Nie ma w tym środowisku renderingu ani Unity Editora.

## Co zostało zweryfikowane i jak

| Obszar | Weryfikacja | Wynik |
|---|---|---|
| Core (droga, fizyka, ruch, kamera, jakość, wejście, layout, niebo, dźwięk, zapis, garaż, opony) | `dotnet test tools/CoreTests` (.NET 8, NUnit 4) | **206/206 przechodzi** |
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
| Zużycie tylnej lewej opony, łuk R=100 m, autopilot, 20 s | 5,1e-4 (5 s) · 8,0e-4 (10 s) · 1,39e-3 (20 s) | rośnie monotonicznie |
| Odchylenie od osi pasa, łuk R=60 m, autopilot | świeże 90 °C: 0,05 m · w pełni zużyte: 7,0 m · zimne 25 °C: 7,0 m | świeże trzymają linię, zużyte i zimne nie |
| Ten sam przebieg przy różnych klatkach | różnica < 1e-6 przy tych samych krokach fizyki | niezależność od FPS |
| Droga, 100 km | ciągła (odstęp próbek 2,000 m ±1e-6), kąt w stożku, bez NaN | wszystkie warunki spełnione |
| Droga, odtworzenie po cofnięciu | identyczne próbki (bit w bit) | deterministyczne |
| Ruch drogowy, 2 min, 12 aut | brak nakładania się aut w tym samym pasie; 7 kontaktów z graczem (gracz bez hamowania) | patrz ograniczenia |
| Generacja terenu, koszt | 700 wywołań wysokości terenu (jeden chunk, dalekie pierścienie): 1,3 ms; 10 km drogi: 20 ms | patrz ryzyka |

## Lista wizualna: co powinno być widać i czy kod istnieje

Stan w każdym wierszu dotyczy tego, co zostało zrobione w repozytorium. Żadna pozycja nie jest opisem wyniku
renderingu. „Napisane” znaczy: jest kod, kompiluje się w `tools/UnityCheck`, a część logiki ma testy w Core.
„Nieobejrzane” znaczy: nikt nie widział tego w grze.

| Element | Gdzie w kodzie | Stan | Co sprawdzić na urządzeniu |
|---|---|---|---|
| Droga i asfalt, oznaczenia pasów | `RoadMeshBuilder`, `RoadMaterialFactory` | napisane, nieobejrzane | ciągłość między chunkami, brak skoków wysokości, oznaczenia |
| Pobocza, bariery, słupki, kilometrówki | `RoadsideProps`, `RoadChunkStreamer` | napisane, nieobejrzane | rytm słupków, kilometrówki co 1000 m |
| Latarnie (głowy i do 8 świateł nocą) | `RoadsideProps`, `StreetLampLights`, `LampBudget` | napisane, nieobejrzane | wygląd nocą, przeskoki świateł przy jeździe |
| Teren i 7 biomów, tekstury proceduralne | `TerrainField`, `TerrainMaterialFactory`, `TextureUtil` | napisane, nieobejrzane | przejścia między biomami, szwy tekstur |
| Drzewa i skały | `VegetationStreamer`, `ProceduralMeshes`, `TreeTextures` | napisane, nieobejrzane | gęstość, limit instancji przy szybkiej jeździe |
| Woda przybrzeżna i piana | `CoastWaterBuilder`, `Race1/Water` | napisane, nieobejrzane, rzadka | czy shader się ładuje w buildzie, czy woda nie zalewa drogi |
| Filary mostów, obudowa tuneli | `StructureProps` | napisane, nieobejrzane | położenie względem asfaltu, nawinięcie z wnętrza tunelu |
| Niebo, słońce, gwiazdy, chmury, zachód | `Race1/Sky`, `SkyModel`, `Atmosphere`, `GameSession.UpdateSky` | napisane, nieobejrzane | kolor w złotej godzinie, gwiazdy w nocy |
| Deszcz i śnieg (cząsteczki), mgła | `WeatherVisuals`, `WeatherState` | napisane, nieobejrzane | gęstość cząsteczek, widoczność w mgle |
| Śnieg na terenie | `SnowCover` (Core), `RoadChunkStreamer.SetSnowCover` | napisane, nieobejrzane: kolor terenu miesza się z bielą, tekstura `TerrainMaterialFactory.Snow` nie jest używana | czy przejście jest płynne, czy biel nie wygląda płasko |
| Mokra nawierzchnia | `RoadMaterialFactory.SetWetness` | napisane, nieobejrzane | ciemniejszy asfalt, bez refleksów |
| Post-processing (bloom, tonemapping, winieta, ziarno) | `PostProcessingController` | napisane, nieobejrzane | czy nie przepala się noc i nie ciemnieje dzień |
| Odbicia otoczenia | `ReflectionProbeRig` | napisane, nieobejrzane | czy sonda nie zostawia artefaktów na karoserii |
| Samochód: karoseria, szkło, koła, paint | `CarMeshFactory`, `CarView`, `CarPaint` | napisane, nieobejrzane | kształt, szkło, błysk lakieru |
| Poziomy szczegółów aut | `CarLod`, `LODGroup` w `CarView` | napisane, nieobejrzane | przełączanie 90 m i 300 m, czy reflektory wyciętych aut nie świecą |
| Światła aut (reflektory, tylne, hamowanie) | `CarLights` | napisane, nieobejrzane | światło nocą, czerwień przy hamowaniu |
| Ruch uliczny i rywale w wyścigu | `TrafficView`, `CarView`, `RivalRace` | napisane, nieobejrzane | brak nakładania się aut w tym samym pasie |
| Kamery (chase, bumper, hood, cockpit, cinematic) | `CameraDirector`, `CameraEffects` | napisane, nieobejrzane | przeskoki przy zmianie widoku, wstrząs, kick FOV |
| Tryb foto | `PhotoMode`, `PhotoOrbit`, panel w `MenuController` | napisane, nieobejrzane | kierunek przeciągania, zapis zrzutu, układ przycisków |
| HUD (prędkość, bieg, asysty, linia wyścigu, odliczanie) | `HudController` | napisane, nieobejrzane | czytelność na Foldzie w obu stanach |
| Menu, garaż, panel TUNING, pauza | `MenuController` | napisane, nieobejrzane | układ w poziomie, przeciąganie suwaków |
| Sterowanie dotykowe | `OnScreenControls`, `PlayerInputReader` | napisane, nieobejrzane | pady, przyciski, martwa strefa |
| Ślady opon, dym, iskry, pył, rozpryski | — | **brak** | — |
| Krople na szybie | — | **brak** | — |
| Uszkodzenia wizualne (wgniecenia) | — | **brak**, uszkodzenie to liczba w HUD | — |
| Dźwięk: silnik, wiatr, pisk opon, deszcz | `EngineSynth`, `AmbientSynth`, `AmbientAudio` | napisane, **nieusłyszane** | poziomy, barwa, czy pisk nie jest zbyt ostry |

## Zaimplementowane funkcje

### Rdzeń (czysta logika, przetestowana)
- Nieskończona, deterministyczna droga z węzłami (ciągłość G2), budżetem skrętu, stożkiem kierunku,
  pochyleniem poprzecznym, pasami zmieniającymi się na prostych, mostami jako flagą (`Bridge`).
- Siedem biomów z płynnym mieszaniem parametrów.
- Teren z szumu Perlina (fBm), zgodny z drogą na pasie przejścia.
- Fizyka: opony magic formula z poślizgiem połączonym, zawieszenie sprężyna-tłumik, napęd
  (FWD/RWD/AWD, turbo z opóźnieniem, skrzynia auto/ręczna), hamulce z fadingiem, ABS, TC, ESP,
  asysta kierownicy, aero, opór toczenia, bariera z kolizją.
- Zużycie i temperatura opon (`TyreCondition`): zużycie rośnie z mocy poślizgu, temperatura z ciepła
  tarcia i chłodzenia powietrzem; oba zmniejszają przyczepność. Stałe są dobrane z rozsądku, nie
  skalibrowane na danych z opon. Testy w TyreConditionTests.
- Trzy samochody o różnym charakterze (`VehicleCatalog`), oryginalne nazwy.
- Tryb wyścigu: pozycje, kolejność na mecie i odstępy (`RaceDirector`), przeciwnicy AI z IDM w pasach (`AiRacerModel`), wyścig z rywalami na siatce z odliczaniem i trzema poziomami trudności (`RivalRace`). Testy w CoreTests (RaceDirectorTests, RivalRaceTests).
- Tunele i mosty jako zdarzenia drogi (`RoadSample.Tunnel`, `Bridge`): przełączają się po 60 m trwania, z histerezą; testy w RoadStructureTests. Geometria (rozstaw filarów, wysokość, profil obudowy tunelu) w `StructureGeometry`, testy w StructureGeometryTests. Udział jest wysoki w alpejskim terenie i wymaga strojenia: na 50 km trasy 30–43 % próbek to most, a 10–25 % to tunel.
- Latarnie nocą: wybór najbliższych N latarni z histerezą (`LampBudget`), testy w LampBudgetTests.
- Poziom morza i brzeg (`Shoreline`): głębokość, próg biomu Coast, siła piany; testy w ShorelineTests.
- Pokrycie śniegiem (`SnowCover`): cel z ilości śniegu, narastanie i topnienie z ograniczoną szybkością;
  testy w SnowCoverTests.
- Kamera fotograficzna (`PhotoOrbit`): orbita, zoom, ogniskowa i FOV z 24 mm sensora, nazwy zrzutów
  (`PhotoShots`); testy w PhotoOrbitTests.
- Ruch AI: unik na wielopasmowych drogach, bezpieczna luka przy zmianie pasa, testy scenariuszy z hamującym graczem (`TrafficEvasionTests`). Ograniczenie: na dwupasmowej drodze auto nie ma gdzie uciec, więc gracz jadący bez hamowania w wolniejsze auto uderzy.
- Harmonogram pogody i pory dnia (`WeatherSchedule`): deterministyczne przejścia pogody i godzina doby; testy płynności, testy w CoreTests. Jeszcze nie podpięty do GameSession.
- Strojenie aut w garażu (`VehicleTuning`): sprężyny, tłumienie, przełożenie, hamulce i mieszanka opon z limitami zakresów. Suwaki i zapis per auto w save (`TuningControls`, `VehicleTuningStore`); testy w GarageTuningTests.
- Ruch AI (IDM), zmiana pasa z przerwą, reakcja na gracza, zdarzenia wyprzedzania i bliskiego mijania.
- Kamery: chase, bumper, hood, cockpit, cinematic (krytycznie tłumione, bez przeregulowania).
- Governor jakości z histerezą, reakcją na termikę i baterię.
- Wejście: martwa strefa, krzywe, czułość, pedały z rampą, przechył.
- Layout ekranu (klasy Cover/Unfolded/Tablet/Phone), kontrolki w bezpiecznym obszarze.
- Niebo: pozycja słońca z szerokości, daty i godziny; noc, złota godzina; pogoda z mokrą nawierzchnią.
- Dźwięk silnika: syntezator harmoniczny, zależny od obrotów i obciążenia.
- Dźwięki otoczenia (`AmbientSynth`, `AmbientSoundModel`): wiatr rosnący z prędkością, pisk opon z poślizgu
  kąta i współczynnika, deszcz z kropli i szumu wysokoczęstotliwościowego. Testy w AmbientSoundTests, w tym
  pasmo sygnału zmierzone algorytmem Goertzela.
- Tryby: Time Attack (sektory, najlepszy czas), Traffic Challenge (punktacja, mnożnik, kary).
- Zapis: wersjonowany, z migracją, bez utraty nieznanych danych, atomowy zapis.

### Warstwa Unity (napisana, kompiluje się semantycznie, nieuruchomiona)
- Proceduralne chunki drogi i terenu z oznakowaniem, pulą i floating origin.
- Instancjonowane drzewa i skały, deterministyczne na chunk.
- Poziomy szczegółów aut (`CarLod`, `LODGroup` w `CarView`): pełna karoseria, uproszczona od 90 m i wycięcie
  od 300 m, progi przeliczone z odległości na udział wysokości ekranu. Testy w CarLodTests. Nie oglądane.
- Samochód: proceduralne nadwozie (superellipsa, przeszklenie, światła), koła z oponą i felgą,
  przechyły i zanurzenie z zawieszenia, obrót kół, światła hamowania.
- Ruch AI jako osobne wizualne samochody.
- Tryb AI Race w menu: siatka pięciu rywali za startem, odliczanie z hamulcem, linia HUD z miejscem
  i dystansem, powrót do menu po mecie. Rywale poza strumieniowanym odcinkiem drogi są ukryci.
- Tryb foto z pauzy (PHOTO): orbita przeciąganiem, zoom, ogniskowa, zrzut PNG do `persistentDataPath`
  przez `ScreenCapture`. Świat jest zamrożony, a UI ukryte na czas zrzutu. Nie uruchomiono w Unity, więc
  kadr, zapis na urządzeniu i kierunek obrotu nie są sprawdzone.
- Kamery i przełączanie widoków; HUD (prędkość, bieg, obrotomierz, asysty, tryb, pogoda, flash kolizji).
- Kontrolki dotykowe (pady, gaz, hamulec, ręczny, biegi, widok, pauza) oraz pad, klawiatura i przechył.
- Silnik audio przez `OnAudioFilterRead` z buforem przydzielonym raz.
- Dźwięki otoczenia (`AmbientAudio`): wiatr, pisk opon i deszcz z tej samej syntezy. Wyciszane w pauzie,
  trybie foto i menu. Nie sprawdzono słuchem na urządzeniu.
- Menu: start, pauza, garaż z obracającym się autem, wybór trybu/auta/lakieru/pogody/pory dnia/asyst.
- Śnieg na terenie: kolor chunków miesza się z bielą według pokrycia (`RoadChunkStreamer.SetSnowCover`).
  Nie sprawdzono w Unity.
- Woda przybrzeżna: siatka per chunk poza krawędzią drogi (`CoastWaterBuilder`), tylko tam, gdzie biom Coast
  dominuje i teren leży poniżej 0 m. Shader `Race1/Water` (Fresnel, piana z koloru wierzchołków). Gdy shadera
  nie ma w buildzie, używany jest nieprzezroczysty materiał zapasowy. Nie sprawdzono w Unity.
- Mosty i tunele w geometrii: instancjonowane filary pod mostami (co 24 m, po dwa z każdej strony)
  i obudowa tunelu z łukiem zwróconym do wnętrza (`StructureProps`). Nie sprawdzono w Unity.
- Garaż z tuningiem (TUNING): suwaki sprężyn, tłumienia, przełożenia i hamulców z procentową zmianą względem
  fabrycznej, wybór opon i RESET. Ustawienia są zapisane per auto w save i używane przy starcie przejazdu.
  Nie sprawdzono w Unity: układu, przeciągania suwaków i zapisu na urządzeniu.
- Platforma: odczyt safe area, odświeżania, baterii; termika Androida przez JNI (tylko na Androidzie).
- Wydajność: skala renderowania URP, MSAA, docelowy FPS według poziomu. Budżet na poziom
  (`PerformanceLimits` w `RaceConfig.Budgets`, sprawdzany przez `PerformanceBudget`): chunki przed autem,
  budowy na klatkę, instancje roślinności na klatkę (najbliższe chunki pierwsze, `DrawBudget`), liczba
  świateł i odległość cieni. Wartości domyślne odpowiadają dotychczasowym. Nie zmierzono kosztu na urządzeniu,
  więc limity są ostrożnymi wartościami, a nie wynikiem pomiaru.
- Latarnie: głowy instancjonowane (świecą nocą) i pula do N prawdziwych świateł punktowych
  (`StreetLampLights`, limit `RaceConfig.MaxLitStreetLamps`, domyślnie 8) przydzielanych najbliższym
  latarniom przez `LampBudget`. Nie sprawdzono w Unity, ile to kosztuje na telefonie.
- Narzędzia edytora: konfiguracja projektu (IL2CPP, ARM64, Vulkan, orientacja pozioma, liniowa
  przestrzeń barw, minSdk 30), utworzenie sceny i `RaceConfig`, budowa APK (również `-batchmode`).

## Brakujące lub uproszczone (świadome ograniczenia)

Ta lista jest ważniejsza niż lista funkcji. Wymagania projektu opisują wiele elementów, których
**nie zrobiono**.

### Grafika i świat
- **Modele samochodów nie są szczegółowe.** Karoseria to proceduralna superellipsa; nie ma wnętrza,
  PBR z teksturami ani licencjonowanych modeli. Jest LOD: pełna karoseria do 90 m, uproszczona do 300 m,
  potem auto nie jest rysowane. LOD nie był oglądany w Unity. Wymagania mówią wprost, że to nie może być wersja
  docelowa.
- **Brak budynków, znaków i barierek ze zdobieniami.** Świat ma teren, drogę, oznakowanie,
  drzewa, skały i latarnie. Latarnie nie zostały obejrzane w Unity, więc ich wygląd nocą jest nieznany.
- **Mosty i tunele są narysowane, ale nieobejrzane.** Filary pod mostami są instancjonowane co 24 m po
  dwa z każdej strony (`StructureProps`). Tunel ma obudowę z łukiem zwróconym do wnętrza. Filary pojawiają się
  w 31–45 % chunków na trasie, więc są częste. Nie sprawdzono, czy obudowa leży poprawnie względem asfaltu,
  czy nawinięcie trójkątów jest dobre z wnętrza tunelu i czy wysoki udział tuneli nie obciąża klatki. Głęboki
  wykop nadal jest zwykłą ścianą terenu.
- **Woda jest rzadka i nieobejrzana.** Pojawia się tylko w biomie Coast poniżej 0 m. Na 50 km trasy
  w pięciu ziarnach od 0 % do 2,8 % chunków ma wodę, a dla domyślnego ziarna gry (20260101) na tym odcinku
  nie ma jej wcale. Biom Coast zajmuje w świecie ok. 0,2–0,5 % powierzchni, więc to decyzja o świecie, nie błąd
  kodu. Shader `Race1/Water` nie był kompilowany ani oglądany. Nie sprawdzono, czy woda nie zalewa pobocza,
  gdy droga biegnie poniżej 0 m.
- **Efekty pogodowe są ograniczone.** Cząsteczki deszczu i śniegu istnieją (`WeatherVisuals`), ale nie
  ma kropli na szybie, rozprysków ani śladów opon. Pogoda wpływa też na przyczepność, mgłę, kolor nieba
  i nasycenie nawierzchni.
- **Śnieg na terenie jest tylko kolorem.** Pokrycie narasta w ciągu ok. 25 s i topnieje w ok. 50 s
  (`SnowCover`), a kolor terenu miesza się z bielą. Tekstura `TerrainMaterialFactory.Snow` nie jest używana:
  pokazanie jej wymagałoby mieszania tekstur w shaderze terenu, a przełączanie materiału dałoby skok.
- **Brak refleksów mokrego asfaltu** (SSR/planar); mokra nawierzchnia to niższa przyczepność i ciemniejszy kolor.
- Brak dedykowanego shadera asfaltu z połyskiem; używany jest URP Lit z teksturą proceduralną.

### Rozgrywka
- **Tryb AI Race: balans nie został zmierzony.** W testach najszybszy rywal na 3000 m jedzie 78–85 s
  w zależności od trudności, a gracz nie był mierzony.
- **Free Ride** to Endless Drive z wyborem pogody i pory dnia; brak suwaków trudności i gęstości ruchu w menu.
- **Strojenie auta w garażu:** suwaki sprężyn, tłumienia, przełożenia i hamulców są w menu, ale nie
  pokazują zmiany prowadzenia. Zawieszenie nie ma osobnej regulacji wysokości. Wpływ strojenia na balans
  nie został zmierzony.
- **Brak powtórek i nagrywania.** Kamera filmowa działa na żywo.
- **Ruch AI nie gwarantuje braku kolizji z graczem:** w testach 2-minutowych bez hamowania było
  7 kontaktów. Samochody zmieniające pas nie przewidują wszystkich sytuacji.
- **Brak uszkodzeń wizualnych** (wgnieceń). Uszkodzenie jest liczbą w HUD.
- Wyniki wyścigu AI (czasy, miejsca) nie są zapisywane. Rywale nie zderzają się fizycznie z graczem:
  jeśli dzielą pas, mogą przenikać przez auto gracza. Wyścig nie był uruchomiony w Unity.

### Fizyka
- **Sprzęgło jest uproszczone**: zablokowane powyżej obrotów jałowych, bez poślizgu sprzęgła.
- **Brak modelu kół na nierównościach**: nawierzchnia jest gładka, bez profilu wibracji.
- **ESP działa jako moment korygujący**, nie jako rzeczywiste hamowanie pojedynczych kół.
- **Brak wzajemnej fizyki zderzeń z ruchem AI** (kolizja jest zdarzeniem, bez odrzutu).
- Model opon ma zużycie i temperaturę, ale bez rozkładu temperatury po szerokości bieżnika, bez
  wpływu ciśnienia i bez kalibracji na danych z opon. Wizualnie zużycie nie jest jeszcze pokazane
  (brak zmiany wyglądu opony). Wpływ wody to współczynnik tarcia.
- Model nie zawiera sił bocznych wiatru ani aerodynamiki zależnej od pochylenia nadwozia.

### Dźwięk
- Dźwięki są syntetyczne (brak nagrań i miksu przestrzennego). Wiatr, pisk opon i deszcz są, ale nie
  były słyszane, więc poziomy i barwa są nieskalibrowane. Brak dźwięków zderzeń i przełączania biegów
  (poza zmianą obrotów).
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
- Pakiety w `Packages/manifest.json` mają wersje z linii Unity 6; nie były testowane. Projekt jest
  zapisany na wersji edytora `6000.0.64f1` (`ProjectSettings/ProjectVersion.txt`).
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

## Świadomie zostawione braki

To są braki odłożone celowo. Każdy ma powód i warunek, który by go zamknął. Nie są to błędy, o których zapomniano.

| Brak | Dlaczego zostawiony | Co by go zamknęło |
|---|---|---|
| Licencjonowane lub autorskie modele aut, wnętrze, PBR | Nie ma zasobów w repozytorium ani licencji; wymagania zakazują kopiowania cudzych zasobów | Autorskie modele od artysty albo zakupiony zasób z licencją |
| Ślady opon, dym, iskry, pył, rozpryski, krople na szybie | Efekty cząsteczkowe kosztują klatkę na telefonie, a wydajności nie zmierzono | Pomiar w profilerze na urządzeniu, potem ostrożne efekty w ramach budżetu z G9 |
| Refleksy mokrego asfaltu (SSR albo planar) | Koszt renderingu, którego nie da się sprawdzić bez urządzenia | Planarne odbicie na jednym odcinku albo tańszy odblask w shaderze drogi |
| Tekstura śniegu na terenie | Kolor terenu już miesza się z bielą; tekstura wymagałaby mieszania w shaderze | Blend tekstur w shaderze terenu, po przeglądzie wyglądu na urządzeniu |
| Częstość wybrzeża w świecie (woda jest rzadka) | To decyzja o świecie, nie o kodzie; zmiana wpływa na całą generację | Decyzja właściciela projektu: więcej biomu Coast albo osobne odcinki nadmorskie |
| Kalibracja: stałe opon, poziomy dźwięku, progi LOD, limity budżetu | Brak danych z opon i brak pomiaru na urządzeniu | Testy na urządzeniu i pomiary wzorcowe |
| Rywale bez fizycznej kolizji z graczem | Świadome uproszczenie: wyścig opiera się na odstępie IDM | Model kolizji z odrzutem, jeśli wyścig tego wymaga |
| Kontakty ruchu z graczem bez hamowania (7 w 2 min) | Na dwupasmowej drodze nie ma miejsca na unik | Dodatkowe pasy albo mocniejszy unik |
| Zapis wyników wyścigów AI i powtórki | Poza zakresem tego etapu; ROADMAP ich nie wymaga | Rozszerzenie zapisu z migracją wersji |
| Filtry i zapis zdjęć do galerii w trybie foto | Poza zakresem; zrzut zostaje w `persistentDataPath` | Osobny punkt roadmapy |
| Dźwięk przestrzenny i nagrania | Syntezator jest celowy, a nagrań brak | Nagrania od artysty albo kalibracja syntezatora |

## Następne kroki (w kolejności)

1. Zbudować APK (`docs/BUILD_AND_INSTALL.md`), uruchomić na Galaxy Z Fold 8 Ultra i zapisać wyniki
   w tabeli poniżej.
2. Zmierzyć koszt budowy chunka i klatki w Unity Profilerze, przy 60 i 120 Hz.
3. Zastąpić proceduralne samochody autorskimi modelami (Blender, LOD, PBR) lub modelami z licencją.
4. Obejrzeć w Unity filary i tuby tuneli (zrobione, nieobejrzane) oraz dodać barierki ozdobne. Woda na wybrzeżu jest zrobiona, ale rzadka (patrz
   „Woda jest rzadka” wyżej). Decyzja, czy zwiększyć wybrzeże w świecie, należy do właściciela projektu.
5. Sprawdzić w Unity tryb AI Race i Photo Mode (kadr, zrzut do `persistentDataPath`, koszt). Oba są
   zaimplementowane, ale nieuruchomione.
6. Sprawdzić w Unity panel TUNING (układ, przeciąganie suwaków, zapis, start z tuningiem) i zmierzyć,
   czy zmiany sprężyn i przełożeń dają wyczuwalną różnicę w prowadzeniu.

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
