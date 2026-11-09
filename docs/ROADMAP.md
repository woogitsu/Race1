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
- [ ] **G2. Tryb wyścigu AI w menu i w sesji.** Podpięcie `RaceDirector` z C2 do `GameSession` i menu.
- [ ] **G3. Tryb fotograficzny.** Pauza > „Photo”: swobodna kamera orbitalna, zmiana FOV i ogniskowej,
  zapis zrzutu (`ScreenCapture`) do `persistentDataPath`.
- [ ] **G4. Garaż z tuningiem.** Suwaki sprężyn, tłumienia, przełożeń w menu, zapis do `VehicleTuningData`
  (po C1).
- [ ] **G5. Woda na wybrzeżu.** Płaszczyzna wody poniżej poziomu morza w biomie Coast, prosty shader
  z odbiciem Fresnela, pianka na linii brzegu.
- [ ] **G6. Mosty i tunele w geometrii.** Filary pod mostami (flaga `Bridge`), tuba tunelu dla flagi C5.
- [ ] **G7. Dźwięki otoczenia.** Szum opon na poślizgu, wiatr zależny od prędkości, deszcz (pętla)
  generowane przez `EngineSynth`-podobne syntezatory w Core, odtwarzane w Game.
- [ ] **G8. Poziomy szczegółów aut.** LOD (`LODGroup`) z uproszczoną karoserią dla dalekich aut.
- [ ] **G9. Budżet wydajności.** Limity: chunki, instancje roślinności, liczba świateł, cienie.
  Zapisane w `RaceConfig` i wymuszane przez `PerformanceDirector`.
- [ ] **G10. Przegląd jakości.** Uzupełnienie `docs/STATUS.md` o listę wizualną i świadomie
  zostawione braki (bez udawania wyników renderingu).

## Dziennik

<!-- Każde uruchomienie dopisuje wpis na górze tej sekcji. -->

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
