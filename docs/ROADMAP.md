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
- [ ] **C2. Tryb wyścigu AI.** Przeciwnicy na drodze z pozycjami w wyścigu (`RaceDirector`):
  kolejność po dystansie wzdłuż drogi, czasy okrążenia/odcinka. Testy kolejności i zakończenia.
- [ ] **C3. Pogoda i pora dnia w czasie.** Harmonogram zmian pogody i godziny (`WeatherSchedule`),
  deterministyczny z seedem. Testy: płynność parametrów, brak skoków przy przejściach.
- [ ] **C4. Ruch AI bez kolizji z graczem przy hamowaniu.** Reakcja AI na gracza (hamowanie przed
  pojazdem przed sobą, unik przy zbliżaniu się). Test: w scenariuszu z graczem hamującym liczba
  kontaktów spada względem obecnej wartości (7 na 2 minuty).
- [ ] **C5. Zdarzenia mostów i tuneli.** Flagi tunelu (`RoadSample.Tunnel`) na odcinkach pod
  wzniesieniem, z testami ciągłości. Renderowanie w Game w późniejszym punkcie.
- [ ] **C6. Model wycieku opon i temperatury.** Opcjonalny parametr zużycia wpływający na grip.
  Test: monotoniczność grip względem zużycia.

### Gra i grafika (weryfikowane przez UnityCheck)

- [ ] **G1. Latarnie z prawdziwym światłem.** Dla najbliższych N latarni (limit 8) włączyć `Light`
  w nocy, reszta emisyjna. Limit w `RaceConfig`.
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

- 2026-11-08 (uruchomienie 1) — C1 zrobione: `VehicleTuning.Apply` (Core/Garage) z limitami zakresów, bez zmiany bazowego auta, normalizacja rekordu zapisu. Testy: CoreTests 80/80 (9 nowych w VehicleTuningTests), UnityCheck bez błędów. Następny: C2 (tryb wyścigu AI).
- 2026-11-08 — Utworzono plan prac i cykliczne uruchamianie co godzinę. Stan wyjściowy: testy 71/71,
  UnityCheck bez błędów. Licencja Unity zablokowana po stronie użytkownika (szczegóły w BUILD_AND_INSTALL).
