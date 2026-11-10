# Plan prac (ROADMAP)

Ten plik jest źródłem prawdy dla ciągłego rozwoju gry. Każde uruchomienie cyklicznego zadania
(co godzinę) robi dokładnie to, co opisano w sekcji „Procedura”, i zapisuje wynik w „Dzienniku”.

## Procedura (dla każdego uruchomienia)

1. Przeczytaj `docs/ROADMAP.md` i `docs/STATUS.md`. Sprawdź `git status` i `git log -n 5`.
2. Wybierz **pierwszy** nieukończony punkt z listy „Kolejka”. Jeśli punkt jest zablokowany, zapisz
   blokadę w dzienniku i przejdź do następnego.
3. Zaimplementuj punkt. Zasady:
   - logika czysta (bez `UnityEngine`) trafia do `Assets/Race1/Core` i dostaje testy NUnit,
   - kod Unity trafia do `Assets/Race1/Game`, musi kompilować się w `tools/UnityCheck`,
   - nie łam istniejących kontraktów publicznych bez aktualizacji wszystkich wywołań,
   - nie twórz pull requestów, nie zmieniaj konfiguracji uprawnień ani `CLAUDE.md`.
4. Zweryfikuj:
   - `dotnet test tools/CoreTests/CoreTests.csproj` (wszystkie testy muszą przechodzić),
   - `dotnet build tools/UnityCheck/UnityCheck.csproj` (bez błędów).
   Jeśli coś zostało zepsute, napraw to przed commitem.
5. Zaktualizuj `docs/STATUS.md` (co zostało zrobione i jak zweryfikowane) i odhacz punkt tutaj.
6. Dopisz wpis do dziennika (data, punkt, wynik testów, blokady).
7. `git add -A`, commit z opisem zmian i stopką `Claude-Session`, potem `git push -u origin ccr-6f3bd08d-7ppce4`.
   Nie commituj artefaktów (`Library/`, `bin/`, `obj/`, `.apk`); `.gitignore` je ignoruje.

## Zasady jakości

- Nic nie oznaczamy jako „przetestowane”, jeśli nie uruchomiono testu. Nie udajemy wyników renderingu.
- Realizm wizualny wynika z proceduralnych materiałów, oświetlenia i geometrii. Każda zmiana
  wizualna ma mieć jasno opisany efekt i ryzyko.
- Wydajność: nowe elementy scen muszą mieć limit (liczba świateł, instancji, chunków).
- Licencja Unity jest zablokowana po stronie użytkownika. Nie próbuj jej aktywować.

## Kolejka

Punkty w kolejności wykonania. Odhaczaj `[x]` po zakończeniu.

### Rdzeń (testowalny w CoreTests)

- [x] **C1. Strojenie aut w modelu.** `VehicleTuningData` (sprężyny, tłumienie, przełożenie końcowe,
  hamulce) stosowane do `VehicleSpec` przez funkcję `VehicleTuning.Apply(spec, data)` z limitami
  zakresów. Test: zakresy są respektowane, a zapis/odczyt zachowuje wartości.
- [x] **C2. Tryb wyścigu AI.** Przeciwnicy na drodze z pozycjami w wyścigu (`RaceDirector`):
  kolejność po dystansie wzdłuż drogi, czasy okrążenia/odcinka. Testy kolejności i zakończenia.
- [x] **C3. Pogoda i pora dnia w czasie.** Harmonogram zmian pogody i godziny (`WeatherSchedule`),
  deterministyczny z seedem. Testy: płynność parametrów, brak skoków przy przejściach.
- [x] **C4. Ruch AI bez kolizji z graczem przy hamowaniu.** Reakcja AI na gracza (hamowanie przed
  pojazdem przed sobą, unik przy zbliżaniu się). Test: w scenariuszu z graczem hamującym liczba
  kontaktów spada względem obecnej wartości (7 na 2 minuty).
- [x] **C5. Zdarzenia mostów i tuneli.** Flagi tunelu (`RoadSample.Tunnel`) na odcinkach pod
  wzniesieniem, z testami ciągłości. Renderowanie w Game w późniejszym punkcie.
- [x] **C6. Model wycieku opon i temperatury.** Zużycie opony rośnie z mocy poślizgu, temperatura
  z ciepła tarcia i chłodzenia powietrzem; oba zmniejszają przyczepność (`TyreCondition`, używane
  w `VehicleDynamics`). Test: monotoniczność grip względem zużycia i temperatury, zużycie rośnie na
  łuku, świeże opony trzymają linię na łuku R=60, w pełni zużyte i zimne nie.

### Gra i grafika (weryfikowane przez UnityCheck)

- [x] **G1. Latarnie z prawdziwym światłem.** Dla najbliższych N latarni (limit 8) włączyć `Light`
  w nocy, reszta emisyjna. Limit w `RaceConfig` (`MaxLitStreetLamps`). Wybór w Core (`LampBudget`,
  z histerezą, testy), pula świateł w Game (`StreetLampLights`).
- [x] **G2. Tryb wyścigu AI w menu i w sesji.** Podpięcie `RaceDirector` z C2 do `GameSession` i menu.
  Logika w Core (`RivalRace`, testy), tryb `AiRace` w menu i w sesji (`GameSession`, `RaceConfig`).
- [x] **G3. Tryb fotograficzny.** (Orbita i ogniskowa w Core, `PhotoOrbit`; tryb w pauzie w Game.) Pauza > „Photo”: swobodna kamera orbitalna, zmiana FOV i ogniskowej,
  zapis zrzutu (`ScreenCapture`) do `persistentDataPath`.
- [x] **G4. Garaż z tuningiem.** Suwaki sprężyn, tłumienia, przełożeń w menu, zapis do `VehicleTuningData`
  (po C1). Logika w Core (`TuningControls`, `VehicleTuningStore`, testy), panel TUNING w garażu w Game.
- [x] **G5. Woda na wybrzeżu.** Płaszczyzna wody poniżej poziomu morza w biomie Coast, prosty shader
  z odbiciem Fresnela, pianka na linii brzegu. Logika w Core (`Shoreline`, testy), siatka per chunk
  w Game (`CoastWaterBuilder`), shader `Race1/Water`. Woda jest rzadka: patrz dziennik.
- [x] **G6. Mosty i tunele w geometrii.** Filary pod mostami (flaga `Bridge`), tuba tunelu dla flagi C5.
  Geometria w Core (`StructureGeometry`, testy), filary i obudowa w Game (`StructureProps`). Nieobejrzane.
- [x] **G7. Dźwięki otoczenia.** Szum opon na poślizgu, wiatr zależny od prędkości, deszcz (pętla)
  generowane przez `EngineSynth`-podobne syntezatory w Core, odtwarzane w Game. Poziomy w `AmbientSoundModel`,
  synteza w `AmbientSynth` (Core, testy), odtwarzanie w `AmbientAudio` (Game). Nie słyszane.
- [x] **G8. Poziomy szczegółów aut.** LOD (`LODGroup`) z uproszczoną karoserią dla dalekich aut.
  Progi w Core (`CarLod`, testy), uproszczona karoseria w `CarMeshFactory`, `LODGroup` w `CarView`. Nie oglądane.
- [x] **G9. Budżet wydajności.** Limity: chunki, instancje roślinności, liczba świateł, cienie.
  Zapisane w `RaceConfig` i wymuszane przez `PerformanceDirector`. Typ i sprawdzanie w Core
  (`PerformanceBudget`, `DrawBudget`, testy); egzekucja w `VegetationStreamer`, `StreetLampLights`, `PerformanceDirector`.
- [x] **G10. Przegląd jakości.** Uzupełnienie `docs/STATUS.md` o listę wizualną i świadomie
  zostawione braki (bez udawania wyników renderingu). Lista wizualna i tabela braków są w STATUS.

### Rozszerzenie po G10 (propozycje; właściciel projektu może je przestawić)

Kolejka G1–G10 jest zrobiona. Poniższe punkty wynikają z listy „Świadomie zostawione braki” w `docs/STATUS.md`
i da się je zrobić oraz przetestować w Core bez urządzenia. Wykonuje się je w kolejności.

- [x] **H1. Śnieg na nawierzchni i terenie.** Core: `SnowCover` (cel z ilości śniegu, narastanie i topnienie
  z ograniczoną szybkością), testy w SnowCoverTests. Game: kolor chunków miesza się z bielą według pokrycia.
  Odstępstwo od planu: tekstura `TerrainMaterialFactory.Snow` nie jest podpinana, bo przełączenie materiału dałoby
  skok na wszystkich chunkach naraz. Mieszanie tekstur w shaderze zostaje jako osobny krok.
- [x] **H2. Krople na szybie w deszczu.** Core: `WindscreenWetness` (rośnie z deszczem, maleje z prędkością,
  dokładne rozwiązanie równania), `WindscreenDroplets` (układ kropli z ziarna), testy w WindscreenTests.
  Game: `WindscreenOverlay` pokazuje krople w widoku z kokpitu. Nieobejrzane.
- [x] **H3. Ślady opon na poślizgu.** Core: `SkidMarkModel` (intensywność z poziomu pisku, zanik) i `SkidMarks`
  (bufor o stałej pojemności, odstęp 0,5 m, koła niezależne), testy w SkidMarksTests. Game: `SkidMarkRenderer`
  rysuje ślady w zasięgu 150 m jedną siatką. Odstępstwo: limit to stała 400 śladów, a nie wartość z budżetu G9,
  bo dodanie pola do serializowanej struktury zmieniłoby domyślne wartości istniejących assetów.
- [x] **H4. Refleksy mokrego asfaltu.** Core: `WetReflection.Strength(wetness, sunElevation, night01)`, zero na
  suchym asfalcie, najmocniejsze przy niskim słońcu i mokrej nawierzchni, testy w WetReflectionTests. Game: siła
  odbicia steruje gładkością asfaltu (`RoadMaterialSet.SetReflection`), zamiast parametru własnego shadera.
  Odstępstwo: brak własnego shadera drogi; prawdziwe odbicia (SSR/planar) pozostają w braku.

## Dziennik

<!-- Każde uruchomienie dopisuje wpis na górze tej sekcji. -->

- 2026-11-10 (uruchomienie 33, cykl godzinny) — kolejka nadal pusta: wszystkie punkty C, G i H są `[x]`, a na gałęzi
  nie ma nowych commitów od origin. Nic nie wdrożono. Propozycje z uruchomienia 21 wymagają Unity Editora, urządzenia
  albo decyzji właściciela. Weryfikacja: CoreTests 232/232, UnityCheck bez błędów, drzewo czyste. Blokady bez zmian:
  brak Unity Editora i urządzenia; licencja Unity po stronie użytkownika.


- 2026-11-10 (uruchomienie 32, cykl godzinny) — kolejka nadal pusta: wszystkie punkty C, G i H są `[x]`, a na gałęzi
  nie ma nowych commitów od origin. Nic nie wdrożono. Propozycje z uruchomienia 21 wymagają Unity Editora, urządzenia
  albo decyzji właściciela. Weryfikacja: CoreTests 232/232, UnityCheck bez błędów, drzewo czyste. Blokady bez zmian:
  brak Unity Editora i urządzenia; licencja Unity po stronie użytkownika.


- 2026-11-10 (uruchomienie 31, cykl godzinny) — kolejka nadal pusta: wszystkie punkty C, G i H są `[x]`, a na gałęzi
  nie ma nowych commitów od origin. Nic nie wdrożono. Propozycje z uruchomienia 21 wymagają Unity Editora, urządzenia
  albo decyzji właściciela. Weryfikacja: CoreTests 232/232, UnityCheck bez błędów, drzewo czyste. Blokady bez zmian:
  brak Unity Editora i urządzenia; licencja Unity po stronie użytkownika.


- 2026-11-10 (uruchomienie 30, cykl godzinny) — kolejka nadal pusta: wszystkie punkty C, G i H są `[x]`, a na gałęzi
  nie ma nowych commitów od origin. Nic nie wdrożono. Propozycje z uruchomienia 21 wymagają Unity Editora, urządzenia
  albo decyzji właściciela. Weryfikacja: CoreTests 232/232, UnityCheck bez błędów, drzewo czyste. Blokady bez zmian:
  brak Unity Editora i urządzenia; licencja Unity po stronie użytkownika.


- 2026-11-10 (uruchomienie 29, cykl godzinny) — kolejka nadal pusta: wszystkie punkty C, G i H są `[x]`, a na gałęzi
  nie ma nowych commitów od origin. Nic nie wdrożono. Propozycje z uruchomienia 21 wymagają Unity Editora, urządzenia
  albo decyzji właściciela. Weryfikacja: CoreTests 232/232, UnityCheck bez błędów, drzewo czyste. Blokady bez zmian:
  brak Unity Editora i urządzenia; licencja Unity po stronie użytkownika.


- 2026-11-10 (uruchomienie 28, cykl godzinny) — kolejka nadal pusta: wszystkie punkty C, G i H są `[x]`, a na gałęzi
  nie ma nowych commitów od origin. Nic nie wdrożono. Propozycje z uruchomienia 21 wymagają Unity Editora, urządzenia
  albo decyzji właściciela. Weryfikacja: CoreTests 232/232, UnityCheck bez błędów, drzewo czyste. Blokady bez zmian:
  brak Unity Editora i urządzenia; licencja Unity po stronie użytkownika.


- 2026-11-10 (uruchomienie 27, cykl godzinny) — kolejka nadal pusta: wszystkie punkty C, G i H są `[x]`, a na gałęzi
  nie ma nowych commitów od origin. Nic nie wdrożono. Propozycje z uruchomienia 21 wymagają Unity Editora, urządzenia
  albo decyzji właściciela, więc nie da się ich bezpiecznie wykonać w cyklu. Weryfikacja: CoreTests 232/232, UnityCheck
  bez błędów, drzewo czyste. Blokady bez zmian: brak Unity Editora i urządzenia; licencja Unity po stronie użytkownika.


- 2026-11-09 (uruchomienie 26, cykl godzinny) — kolejka nadal pusta: wszystkie punkty C, G i H są `[x]`, więc nic nie
  wdrożono. Nie dodano nowego zakresu, bo wybór należy do właściciela (propozycje w uruchomieniu 21). Weryfikacja:
  CoreTests 232/232, UnityCheck bez błędów, drzewo czyste, gałąź zgodna z origin. Wpis z poprzedniego przebiegu
  został pominięty, więc ten dopisuje go zbiorczo. Blokady bez zmian: brak Unity Editora i urządzenia; licencja
  Unity po stronie użytkownika.


- 2026-11-09 (uruchomienie 25, cykl godzinny) — kolejka nadal pusta, bez nowych commitów na gałęzi. Weryfikacja:
  CoreTests 232/232, UnityCheck bez błędów. Blokady bez zmian (brak Unity Editora i urządzenia). Uwaga: od kilku
  przebiegów wpisy są identyczne; decyzja o dalszym zadaniu cyklicznym należy do właściciela.


- 2026-11-09 (uruchomienie 24, cykl godzinny) — kolejka nadal pusta, bez nowych commitów na gałęzi. Weryfikacja:
  CoreTests 232/232, UnityCheck bez błędów. Blokady bez zmian (brak Unity Editora i urządzenia).


- 2026-11-09 (uruchomienie 23, cykl godzinny) — kolejka nadal pusta, bez nowych commitów na gałęzi. Weryfikacja:
  CoreTests 232/232, UnityCheck bez błędów. Blokady bez zmian (brak Unity Editora i urządzenia).


- 2026-11-09 (uruchomienie 22, cykl godzinny) — kolejka nadal pusta. Gałąź nie ma nowych commitów ani nowych punktów
  od poprzedniego przebiegu, więc nic nie wykonano. Weryfikacja: CoreTests 232/232, UnityCheck bez błędów. Blokady
  bez zmian: brak Unity Editora i urządzenia; licencja Unity po stronie użytkownika. Czeka na decyzję właściciela
  (propozycje w dzienniku uruchomienia 21).


- 2026-11-09 (uruchomienie 21, cykl godzinny) — kolejka pusta. Nie ma nieukończonego punktu: G1–G10 i H1–H4 są
  zrobione. Nie dopisano nowego zakresu, bo wybór kolejnych punktów należy do właściciela projektu. Weryfikacja:
  CoreTests 232/232, UnityCheck bez błędów, drzewo czyste. Propozycje do decyzji: (1) prawdziwe odbicia planarne
  na jednym odcinku, po pomiarze kosztu; (2) kalibracja stałych na urządzeniu (opony, poziomy dźwięku, progi LOD,
  limity budżetu, siła śladów i kropli); (3) decyzja o częstości wybrzeża w świecie, bo woda jest rzadka; (4)
  przegląd w Unity wszystkich pozycji „nieobejrzane” z listy wizualnej w `docs/STATUS.md`. Blokady: brak Unity
  Editora i urządzenia; licencja Unity po stronie użytkownika.


- 2026-11-09 (uruchomienie 20, cykl godzinny) — H4 zrobione, z odstępstwem. Core: `WetReflection` (film od mokrości
  0,1 do 0,6, odbicie słabsze przy wysokim słońcu, od 5° do 50° w dół o 60 %, w nocy o 20 % mniej; zero na suchym;
  testy w WetReflectionTests). Game: `RoadMaterialSet.SetReflection` steruje gładkością asfaltu; `GameSession` podaje
  siłę odbicia co sekundę z pogody i wysokości słońca. Zmiana zachowania: `SetWetness` już nie nabłyszcza asfaltu,
  bo robi to odbicie. Dla suchej drogi wynik jest taki sam. Odstępstwo: nie napisano własnego shadera drogi, więc
  prawdziwe odbicia (SSR/planar) zostają w braku. Testy: CoreTests 232/232 (7 nowych). UnityCheck bez błędów.
  Blokady: brak Unity Editora i urządzenia; połysk nieobejrzany. Kolejka H1–H4 jest zrobiona. Następny: decyzja
  właściciela, czy dopisać kolejne punkty (np. prawdziwe odbicia planarne, kalibrację na urządzeniu).

- 2026-11-09 (uruchomienie 19, cykl godzinny) — H3 zrobione, z odstępstwem od budżetu. Core: `SkidMarkModel`
  (intensywność rośnie od poziomu pisku 0,2 do 0,8, zanik liniowy w czasie życia 6 s) i `SkidMarks` (bufor o stałej
  pojemności; co 0,5 m na każde koło, które piszczy; pierwszy ślad poślizgu od razu; najstarsze znikają przy pełnym
  buforze). Game: `SkidMarkRenderer` buduje jedną siatkę ciemnych czworokątów dla śladów w zasięgu 150 m, przebudowuje
  ją co 0,1 s i nie próbkuje drogi dalej, bo to cofałoby generator. Odstępstwo: limit 400 śladów jest stałą, a nie
  wartością z budżetu G9, bo nowe pole w serializowanej strukturze dałoby zero w istniejących assetach. Testy: CoreTests
  225/225 (10 nowych w SkidMarksTests). UnityCheck bez błędów. Test rozstawu używa kroku 1/600 s, bo przy 1/60 s
  odstępy zaokrąglają się do klatek. Blokady: brak Unity Editora i urządzenia; ślady nieobejrzane. Następny: H4
  (refleksy mokrego asfaltu).

- 2026-11-09 (uruchomienie 18, cykl godzinny) — H2 zrobione. Core: `WindscreenWetness` (dW/dt = A·deszcz −
  (A·deszcz + C(prędkość))·W, rozwiązane dokładnie, więc krok nie przekracza stanu ustalonego i zostaje w 0–1;
  C rośnie z prędkością do pełnego przy 25 m/s, a na postoju tylko powoli paruje) oraz `WindscreenDroplets` (układ
  kropli z ziarna, większość małych). Game: `WindscreenOverlay` maluje krople raz do tekstury i ustawia
  przezroczystość według wilgotności; `GameSession` pokazuje je tylko w widoku z kokpitu, a w pauzie, trybie foto
  i menu je wyłącza. Stub `Image` w `tools/UnityCheck` dostał pole `sprite`, bo to zgodne z prawdziwym API.
  Testy: CoreTests 215/215 (9 nowych w WindscreenTests). UnityCheck bez błędów. Blokady: brak Unity Editora
  i urządzenia; krople nieobejrzane. Następny: H3 (ślady opon na poślizgu).

- 2026-11-09 (uruchomienie 17, cykl godzinny) — H1 zrobione, z odstępstwem. Core: `SnowCover` (cel to łagodna rampa
  od 0,15 do 0,6 z ilości śniegu pogodowego; pokrycie narasta z szybkością 0,04/s, czyli pełne w ok. 25 s, i topnieje
  wolniej, 0,02/s, czyli w ok. 50 s; nigdy nie przekracza celu; złe dane nie zmieniają stanu; testy w SnowCoverTests).
  Game: `RoadChunkStreamer.SetSnowCover` miesza kolor chunków z bielą (`Color.Lerp`, przez istniejący
  `ApplyBiomeTint`); `GameSession` co klatkę przesuwa pokrycie w stronę celu i przekazuje zmiany powyżej 0,001.
  Odstępstwo: tekstury `TerrainMaterialFactory.Snow` nie używam, bo przełączenie materiału zmieniłoby wszystkie
  chunki w jednej klatce. Testy: CoreTests 206/206 (6 nowych). UnityCheck: po poprawce nazwy `System.Math` bez
  błędów. Blokady: brak Unity Editora i urządzenia; wygląd śniegu nieobejrzany. Następny: H2 (krople na szybie).

- 2026-11-09 (uruchomienie 16, cykl godzinny) — G10 zrobione: przegląd jakości w `docs/STATUS.md`. Dodano listę
  wizualną (każdy element: plik w kodzie, stan „napisane / nieobejrzane / brak”, co sprawdzić na urządzeniu),
  sekcję „Świadomie zostawione braki” (powód i warunek zamknięcia) oraz datę przeglądu. Sprawdzono każdą nazwę
  klasy w tabeli, aby nie wpisać czegoś, czego nie ma. Poprawiono dwa nieaktualne wpisy: wersję Unity (projekt
  jest na 6000.0.64f1, a nie 6.0.23) i zdanie „brak cząsteczek” (deszcz i śnieg mają cząsteczki). Dopisano brak,
  którego wcześniej nie było w dokumencie: materiał śniegu istnieje, ale nie jest podpięty do terenu. Wynik testów: CoreTests 200/200, UnityCheck bez
  błędów. Blokady: brak Unity Editora i urządzenia, więc nic z listy wizualnej nie zostało obejrzane. Kolejka
  G1–G10 jest zrobiona. Dopisano propozycje H1–H4 z listy braków, do wykonania w kolejnych przebiegach.
  Następny: H1 (śnieg na nawierzchni).

- 2026-11-09 (uruchomienie 15, cykl godzinny) — G9 zrobione. Core: `PerformanceLimits` (chunki przed autem, budowy na
  klatkę, instancje roślinności, światła, odległość cieni; [Serializable], żeby dało się je edytować w `RaceConfig`),
  `PerformanceBudget` (tabela domyślna zgodna z dotychczasowymi wartościami, zakresy bezpieczeństwa, wybór wiersza
  z awaryjnym powrotem na domyślne, sprawdzenie monotoniczności) i `DrawBudget` (przydział limitu instancji od
  najbliższych grup). Game: `RaceConfig.Budgets`; `PerformanceDirector` czyta chunki, budowy i cienie z tabeli
  zamiast z `switch`; `VegetationStreamer.Draw` ma limit instancji i rysuje najbliższe chunki najpierw; `StreetLampLights`
  dostaje limit świateł na klatkę, ograniczony rozmiarem puli. Pole `ChunksAheadMax` w `RaceConfig` nie jest już
  używane, zostawiono je ze względu na zgodność. Testy: CoreTests 200/200 (11 nowych w PerformanceBudgetTests).
  UnityCheck bez błędów. Nie zmierzono kosztu na urządzeniu: limity są ostrożnymi wartościami. Następny: G10
  (przegląd jakości i listy braków w docs/STATUS.md).

- 2026-11-09 (uruchomienie 14, cykl godzinny) — G8 zrobione. Core: `CarLod` (pełny model do 90 m, uproszczony do
  300 m, potem wycięcie; zamiana odległości na udział wysokości ekranu przy polu widzenia 70°, z funkcją odwrotną;
  testy w CarLodTests). Game: `CarMeshFactory.BuildBody` ma przeciążenie z liczbą pierścieni i próbek; uproszczona
  karoseria używa 16 pierścieni i 18 próbek i tych samych materiałów. `CarView` dodaje ją jako drugi renderer
  i buduje `LODGroup`: pierwszy poziom to wszystkie renderery pełnego auta, drugi to uproszczona karoseria. Poza
  progiem wycięcia auto nie jest rysowane. Domyślne wartości przeciążenia są stałymi, które były już w kodzie,
  więc pełny model nie powinien się zmienić, ale nie porównałem siatek. Testy: CoreTests 189/189 (7 nowych
  w CarLodTests). UnityCheck bez błędów. Nie sprawdzono w Unity: przełączania poziomów, wyglądu uproszczonej
  karoserii, czy światła reflektorów nie świecą na wycięte auta i kosztu. Następny: G9 (budżet wydajności).

- 2026-11-09 (uruchomienie 13, cykl godzinny) — G7 zrobione. Core: `AmbientSoundModel` (poziom wiatru rośnie od
  15 do 90 m/s; pisk opon od sumy kąta i współczynnika poślizgu 0,10 do 0,30; poziom deszczu z zakresu 0–1) oraz
  `AmbientSynth` (wiatr: szum przez filtr dolnoprzepustowy, którego częstotliwość graniczna rośnie z poziomem; pisk:
  szum przez pasmowy filtr 2 kHz, Q 6; deszcz: szum powyżej 1,2 kHz i kropelki jako pochodna impulsu). Pierwsza wersja
  kropelek była jaśniejsza od szumu tylko w niskich tonach, więc test pasma odrzucił ją. Kropelki zmieniono na
  pochodną o zaniku 0,2 ms. Game: `AmbientAudio` (wzorzec jak w `EngineAudio`, wartości przez `volatile`), zasilany
  co klatkę z prędkości, najgorszego poślizgu czterech opon i opadów. Wyciszany w pauzie, trybie foto i menu.
  Testy: CoreTests 182/182 (11 nowych w AmbientSoundTests, w tym pasmo mierzone algorytmem Goertzela). UnityCheck bez
  błędów. Nie sprawdzono słuchem na urządzeniu: poziomów, barwy ani tego, czy pisk nie jest zbyt ostry. Następny:
  G8 (poziomy szczegółów aut).

- 2026-11-09 (uruchomienie 12, cykl godzinny) — G6 zrobione, z zastrzeżeniami. Core: `StructureGeometry` (rozstaw
  filarów co 24 m, wysokość filara od gruntu do spodu pomostu z grubością 0,9 m, minimum 2 m, profil obudowy tunelu
  jako półelipsa o półosiach szerokość drogi + 1,2 m i 5 m; testy w StructureGeometryTests). Game: `StructureProps`
  buduje instancjonowane filary pod mostami (dwa z każdej strony, z gruntu naturalnego) i łukową obudowę tunelu
  z normalnymi do wnętrza. Nawinięcie trójkątów zależy od normalnych. Pomiar na 50 km trasy (test tymczasowy,
  usunięty przed commitem): mosty to 30–43 % próbek, tunele 10–25 %, filary w 31–45 % chunków. Stan ten odpowiada
  uwadze z C5 o wysokim udziale mostów i tuneli w alpejskim terenie. Testy: CoreTests 171/171. UnityCheck bez
  błędów. Nie sprawdzono w Unity: położenia obudowy względem asfaltu, kierunku nawinięcia z wnętrza tunelu,
  wyglądu filarów ani kosztu klatki przy takim udziale tuneli. Następny: G7 (dźwięki otoczenia).

- 2026-11-09 (uruchomienie 11, cykl godzinny) — G5 zrobione, z zastrzeżeniem o zasięgu. Core: `Shoreline` (poziom
  morza 0 m, próg biomu Coast 0,5, minimalna głębokość 0,2 m, pas piany 3 m; testy w ShorelineTests). Game:
  `CoastWaterBuilder` buduje wodę per chunk poza krawędzią drogi, tylko z czworokątów, których wszystkie narożniki
  leżą poniżej poziomu morza w biomie Coast. Piana jest w kolorze wierzchołków. Shader `Race1/Water` daje Fresnel
  i piankę. Materiał zapasowy jest używany, gdy shadera nie ma w buildzie. Bramka: woda tylko w chunkach, w których
  biom Coast ma wagę ≥ 0,5 na zewnętrznym pierścieniu. Pomiar na 50 km trasy (test tymczasowy, usunięty przed
  commitem): od 0 do 2,8 % chunków z wodą zależnie od ziarna. Dla domyślnego ziarna 20260101 nie ma wody wcale.
  Przyczyną jest rzadkość biomu Coast (ok. 0,2–0,5 % powierzchni), a nie błąd kodu. Decyzja o częstości wybrzeża
  należy do świata. Testy: CoreTests 164/164. UnityCheck bez błędów. Nie sprawdzono w Unity: shadera (nie był
  kompilowany), wyglądu, kosztu budowy siatki na urządzeniu oraz tego, czy woda nie zalewa pobocza, gdy droga biegnie
  poniżej 0 m. Następny: G6 (mosty i tunele w geometrii).

- 2026-11-09 (uruchomienie 10, cykl godzinny) — G4 zrobione. Core: `TuningControls` (suwak 0–1 ↔ wartość w zakresie
  z C1, procentowa zmiana od fabrycznej, odczyt i zapis jednego pokrętła w rekordzie, cykl opon) oraz
  `VehicleTuningStore` (jeden rekord na auto w `SaveData.Tunings`, tworzony przy pierwszej edycji; odczyt nie zmienia
  zapisu). Game: w garażu przycisk TUNING otwiera panel z czterema suwakami, przełącznikiem opon, RESET i BACK.
  Start przejazdu używa strojonego auta. Naprawa przy okazji: garaż nie był ukrywany w pauzie ani w trybie foto,
  więc był widoczny i klikalny nad ekranem pauzy i zdjęcia. Teraz jest ukrywany w obu tych stanach.
  Testy: CoreTests 158/158 (13 nowych w GarageTuningTests, w tym losowe edycje, które nigdy nie wychodzą poza
  zakresy). UnityCheck bez błędów. Nie sprawdzono w Unity: układu panelu, przeciągania suwaków, zapisu do pliku
  i tego, czy tuning zmienia prowadzenie. Zmiany trafiają na dysk przy najbliższym `SaveNow` (autosave co 30 s,
  pauza, wyjście). Następny: G5 (woda na wybrzeżu).

- 2026-11-09 (uruchomienie 9, cykl godzinny) — G3 zrobione. Core: `PhotoOrbit` (orbita wokół auta, obrót
  przeciąganiem z kątem ograniczonym do −2°…70°, zoom 3–40 m, ogniskowa 14–200 mm, FOV pionowy z 24 mm
  sensora: 50 mm daje 27°) i `PhotoShots` (nazwy zrzutów w stałym formacie daty). Game: w pauzie przycisk PHOTO.
  Świat jest zamrożony, a kamera krąży po orbicie wokół auta. Przeciąganie obraca kamerę, przyciski CLOSER,
  FARTHER, TELE, WIDE i SHOOT działają, BACK wraca do pauzy. Zrzut: UI jest ukryte przez jedną klatkę, potem
  `ScreenCapture.CaptureScreenshot` zapisuje PNG do `persistentDataPath`. Testy: CoreTests 145/145 (11 nowych
  w PhotoOrbitTests). Pierwsza wersja testu zakładała ścisły spadek FOV także przy ogniskowej przyciętej do
  maksimum. Poprawiono to na oczekiwanie niemalejące. UnityCheck bez błędów. Nie sprawdzono w Unity: kadru,
  kierunku obrotu, zapisu zrzutu na urządzeniu ani układu przycisków przy innych proporcjach ekranu (Fold
  składany). Następny: G4 (garaż z tuningiem).

- 2026-11-09 (uruchomienie 8, cykl godzinny) — G2 zrobione. Core: `RivalRace` składa wyścig z gracza (Id 0)
  i rywali na siatce za startem (odstęp 12 m, pasy naprzemiennie), odlicza start (3 s, nikt się nie rusza)
  i prowadzi rywali przez `AiRacerModel` oraz `RaceDirector`. Funkcje pasów `LaneCentre` i `LaneOf` są w Core.
  Game: tryb `AiRace` w menu (przycisk po TRAFFIC CHALLENGE). `GameSession` tworzy rywali i ich wizualne auta,
  podczas odliczania hamulec trzyma auto, HUD pokazuje miejsce, dystans i czas, a po mecie po 6 s wraca do menu.
  Rywale poza strumieniowanym odcinkiem drogi są ukryci i nie są próbkowane, bo próbkowanie daleko za autem
  cofałoby generator drogi. Ustawienia w `RaceConfig`: 3000 m, 5 rywali, 50 m/s przy tempie 1, trudność Normal.
  Testy: CoreTests 134/134 (8 nowych w RivalRaceTests). Pomiar w testach: najszybszy rywal na 3000 m jedzie
  łatwy 85.2 s, normalny 80.7 s, trudny 77.9 s. UnityCheck bez błędów. Nie sprawdzono w Unity: wyglądu rywali,
  HUD, odliczania ani balansu z graczem. Ograniczenia: brak fizycznej kolizji z rywalami, wyniki nie są zapisywane.
  Następny: G3 (tryb fotograficzny).

- 2026-11-09 (uruchomienie 7, cykl godzinny) — G1 zrobione. `LampBudget` (Core/World): wybiera najbliższe N
  latarni; lampa już zapalona zostaje, dopóki niezapalona nie jest o 15% bliżej (histerezja), więc światła nie
  migoczą. Wybór deterministyczny, bez alokacji w klatce. `StreetLampLights` (Game/Road): pula N świateł
  reflektorowych pod światem, przydział stabilny (światło trzyma latarnię, dopóki ona świeci), wyłączona w dzień.
  Limit `RaceConfig.MaxLitStreetLamps` = 8. W każdej klatce najpierw zwalniane są światła gasnących latarni, potem
  przydzielane nowe. Ten porządek poprawiłem przy przeglądzie kodu, zanim cokolwiek uruchomiłem. Testy: CoreTests
  126/126 (6 nowych w LampBudgetTests; przebieg wzdłuż rzędu 60 latarni: najwyżej 2 przełączenia na latarnię,
  zawsze dokładnie 8 zapalonych). UnityCheck bez błędów. Nie sprawdzono w Unity: wyglądu nocą, kosztu ośmiu świateł
  na Androidzie i współdziałania z limitem świateł URP oraz z `CarLights`. Następny: G2 (tryb wyścigu AI w menu i
  sesji).

- 2026-11-09 (uruchomienie 6, cykl godzinny) — C6 zrobione. `TyreCondition` (czyste funkcje): zużycie
  rośnie proporcjonalnie do mocy poślizgu i nigdy nie maleje; grip zużycia spada monotonicznie do 0.78
  przy pełnym zużyciu; temperatura ma okno optymalne 85–105 °C, poza nim grip spada do 0.65 przy
  skrajnych wartościach; ciepło z poślizgu i chłodzenie powietrzem (stan ustalony 20 + 0.5·P/15).
  `VehicleDynamics` mnoży przyczepność przez stan każdej opony, a `VehicleTelemetry` raportuje
  `TyreWear` i `TyreTemperature`. Pierwsza wersja testów używała stałego skrętu bez gazu: auto wpadało
  w bąk i stawało przy barierze, więc pomiar był bezużyteczny. Testy oparte na `TestWorld.Autopilot`
  (stabilnym prowadzeniu po łuku). Wyniki pomiarów: zużycie tylnej lewej opony na łuku R=100 m
  wynosi 5.1e-4 po 5 s, 8.0e-4 po 10 s, 1.39e-3 po 20 s; na łuku R=60 m maksymalne odchylenie od osi
  pasa wynosi 0.048 m dla świeżych opon w 90 °C, a 7.0 m (wypadek) dla pełnego zużycia i dla
  temperatury 25 °C. Ograniczenie: stałe modelu (`WearPerJoule`, przewodność chłodzenia, progi grip)
  są dobrane z rozsądku, nie skalibrowane na danych z opon. Testy: CoreTests 120/120 (9 nowych w
  TyreConditionTests), UnityCheck bez błędów. Następny: G1 (latarnie z prawdziwym światłem).

- 2026-11-09 (uruchomienie 5, cykl godzinny) — C5 zrobione. `RoadSample.Tunnel` (natural ground ponad drogą) obok `Bridge`. Flagi przełączają się dopiero po utrzymaniu warunku przez 60 m (`StructureMinLength`), z histerezą wejścia i wyjścia; liczniki są w `RoadState`, więc punkty kontrolne i cofanie dają ten sam wynik. Pomiar pierwszej wersji: flaga migotała ok. 250 razy na 50 km, po zmianach 26–46 razy. Uwaga: tuneli i mostów jest dużo (ok. 12–25% i 30–40% próbek) z powodu amplitudy terenu alpejskiego, a droga nie może za nim nadążyć przy ograniczeniu nachylenia. To punkt do strojenia biomu, nie błąd mechaniki flag. Testy: CoreTests 111/111 (5 nowych w RoadStructureTests), UnityCheck bez błędów. Renderowanie flag to punkt G6. Następny: C6 (zużycie opon).
- 2026-11-09 (uruchomienie 4, cykl godzinny) — C4 zrobione z zastrzeżeniem. Pomiar pokazał, że 7 kontaktów w poprzednim teście wynikało z gracza jadącego stałe 20 m/s w wolniejsze auto na dwupasmowej drodze, bez hamowania i bez miejsca na ucieczkę (jeden pas na kierunek). Zmiany: unik auta z tego samego kierunku, gdy szybszy gracz zbliża się z tyłu (`ShouldEvade`, sąsiedni pas), bezpieczna luka w obu kierunkach (`IsGapSafe`, uwzględnia prędkość względną), auto wjeżdżające na pas liczy się jako lider, `AddCar` do scenariuszy. Wyniki: hamujący gracz 2 min na wygenerowanej drodze — 0 kontaktów (także przed zmianą; kryterium spadku z 7 dotyczy gracza bez hamowania); czteropasmowa prosta — auto ustępuje, 0 kontaktów; dwupasmowa prosta z graczem bez hamowania — kontakt nieunikniony (test dokumentuje ograniczenie). CoreTests 106/106 (3 nowe w TrafficEvasionTests), UnityCheck bez błędów. Następny: G1 (latarnie z prawdziwym światłem) — to punkt warstwy Unity, a C5 (tunele) wymaga najpierw danych o tunelu w rdzeniu; wybieram C5.
- 2026-11-08 (uruchomienie 3, cykl godzinny) — C3 zrobione: `WeatherSchedule` (łańcuch Markowa pogody z segmentami 4–10 min gry, deterministyczny z ziarnem, śnieg tylko w zimie, bez przejść do siebie) i `HourAt` (godzina doby z zawijaniem). Test płynności: parametry pogody nie zmieniają się szybciej niż pozwala stała czasowa mieszania. Testy: CoreTests 103/103 (10 nowych w WeatherScheduleTests), UnityCheck bez błędów. Harmonogram nie jest jeszcze podpięty do GameSession (zrobi to punkt G2 lub osobny wpis). Następny: C4 (ruch AI bez kolizji z graczem przy hamowaniu).
- 2026-11-08 (uruchomienie 2, cykl godzinny) — C2 zrobione: `RaceDirector` (kolejność po dystansie, kolejność na mecie z interpolacją czasu przekroczenia linii, odstępy do lidera, remisy po Id) i `AiRacerModel` (IDM za najbliższym autem w tym samym pasie, trzy poziomy tempa). Pasy zapobiegają kolejkowaniu się wszystkich AI za jednym autem. Testy: CoreTests 93/93 (13 nowych w RaceDirectorTests), UnityCheck bez błędów. Następny: C3 (harmonogram pogody i pory dnia).
- 2026-11-08 (uruchomienie 1) — C1 zrobione: `VehicleTuning.Apply` (Core/Garage) z limitami zakresów, bez zmiany bazowego auta, normalizacja rekordu zapisu. Testy: CoreTests 80/80 (9 nowych w VehicleTuningTests), UnityCheck bez błędów. Następny: C2 (tryb wyścigu AI).
- 2026-11-08 — Utworzono plan prac i cykliczne uruchamianie co godzinę. Stan wyjściowy: testy 71/71,
  UnityCheck bez błędów. Licencja Unity zablokowana po stronie użytkownika (szczegóły w BUILD_AND_INSTALL).
