# Budowa APK i instalacja

Ten dokument opisuje kroki, które wykonuje się na komputerze z Unity Hub. **Nie zostały one wykonane
w środowisku, w którym powstał kod** (brak Unity Editora). Są napisane tak, żeby dało się je przejść
bez zgadywania. Jeśli któryś krok nie działa, zapisz dokładny komunikat i wróć do `docs/STATUS.md`.

## 1. Wymagania

- Unity Hub i **Unity 6 (6000.0.x LTS)** z modułem **Android Build Support**. Moduł zawiera
  OpenJDK, Android SDK i NDK, więc nie trzeba instalować ich osobno.
- Komputer z 16 GB RAM i ok. 20 GB wolnego miejsca.
- Telefon Samsung Galaxy Z Fold 8 Ultra (lub inny z Androidem 11+) z włączonym trybem dewelopera
  i debugowaniem USB.

> **Aktywacja licencji.** Konto Personal nie może aktywować licencji offline (ograniczenie Unity dla miejsc
> Enterprise i Industry), a logowanie Unity ID wymaga przeglądarki na tym samym komputerze, co edytor.
> Dlatego budowę najprościej wykonać na własnym komputerze z Unity Hub, zalogowanym na Unity ID.
> Edytor z kontenera uruchamia się poprawnie i czeka tylko na licencję.

## 2. Otwarcie projektu

1. W Unity Hub wybierz **Open** i wskaż folder repozytorium.
2. Unity pobierze pakiety z `Packages/manifest.json` (Input System, URP, ugui, Test Framework).
   Przy pytaniu o przełączenie backendu Input System wybierz **Yes**.
3. Po imporcie sprawdź konsolę. Błędy kompilacji nie powinny się pojawić.

## 3. Konfiguracja

1. Menu **Race1 › Setup Project**. Skrypt:
   - tworzy `Assets/Race1/Scenes/Main.unity` i dodaje ją do ustawień budowy,
   - tworzy `Assets/Race1/Resources/RaceConfig.asset` (jeśli go nie ma),
   - ustawia IL2CPP, ARM64, Vulkan (z OpenGL ES 3 jako zapasowym), orientację poziomą,
     liniową przestrzeń barw i minimalne SDK 30,
   - przypisuje pierwszy znaleziony **URP Asset** do ustawień grafiki.
2. Jeśli konsola pokazuje „no URP pipeline asset found”: **Assets › Create › Rendering › URP Asset
   (with Universal Renderer)**, zapisz go w `Assets/Race1/Settings/` i uruchom *Setup Project* ponownie.
3. Ręcznie: **Edit › Project Settings › Player › Other Settings › Active Input Handling = Both**.
   Bez tego nie zadziała gamepad, klawiatura ani przechył, bo kod wejścia jest włączany dyrektywą
   `ENABLE_INPUT_SYSTEM`.

## 4. Test w edytorze

Otwórz dowolną scenę (np. `Main.unity`) i wciśnij **Play**. Gra sama tworzy kamerę, światło, świat
i menu. W edytorze działa klawiatura: **A/D** lub strzałki (kierunek), **W/S** (gaz/hamulec),
**Spacja** (ręczny), **Q/E** (bieg), **C** (widok), **Esc** (pauza).

## 5. Budowa APK

**Z edytora:** menu **Race1 › Build Android APK**. Plik trafi do `Builds/Android/HorizonDrive.apk`.

**Z wiersza poleceń** (przykład dla Linuksa/macOS, dostosuj ścieżkę do instalacji Unity):

```bash
/ścieżka/do/Unity -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod Race1.Editor.Race1BuildTool.BuildApkBatch -logFile build.log
echo "exit code: $?"
```

Kod wyjścia `0` oznacza sukces. W przypadku błędu sprawdź `build.log`.

Budowa jest podpisywana domyślnym kluczem debug. Do publikacji w sklepie potrzebny jest własny keystore
(**Edit › Project Settings › Player › Android › Publishing Settings**).

## 6. Instalacja na telefonie

```bash
adb devices                         # telefon musi być na liście jako "device"
adb install -r Builds/Android/HorizonDrive.apk
adb shell monkey -p com.race1.horizondrive -c android.intent.category.LAUNCHER 1
```

Logi z urządzenia: `adb logcat -s Unity` (filtr na błędy: `adb logcat *:E`).

## 7. Lista kontrolna przed uznaniem wersji za działającą

Wyniki zapisz w tabeli w `docs/STATUS.md`. Każdy punkt ma być zaliczony na **rzeczywistym urządzeniu**,
nie na symulatorze.

1. Gra startuje do menu bez zawieszenia; showroom się obraca.
2. Endless Drive: po starcie widać drogę, nie ma pustego pasa przez pierwsze klatki.
3. Gaz, hamulec, kierownica dotykowa reagują; pedały rosną wolniej niż opadają.
4. Zakręty przy 50, 100, 200 i 300 km/h: auto zostaje na drodze, ABS i TC działają w trybie Sport.
5. 100 km jazdy bez zatrzymań i bez zauważalnych przeskoków drogi; pamięć (Profiler) nie rośnie stale.
6. Złożenie i rozłożenie Folda: HUD i kontrolki przebudowują się bez restartu.
7. Pauza, powrót do gry, wyjście z aplikacji i ponowne uruchomienie: zapis wczytuje pozycję i auto.
8. Po 20 minutach jazdy: brak krytycznego throttlingu; jakość obniża się w razie potrzeby.
9. Kontroler Bluetooth (pad) i przechył (po kalibracji).

## 8. Diagnostyka

| Objaw | Co sprawdzić |
|---|---|
| Czarny ekran zamiast drogi | Czy URP Asset jest przypisany (Setup Project), czy kamera ma `MainCamera` |
| Brak sterowania z pada | *Active Input Handling = Both* |
| Różowe materiały | Shader URP nie został włączony w buildzie: dodaj `Universal Render Pipeline/Lit` do *Always Included Shaders* |
| Build kończy się błędem SDK | Moduł Android Build Support w Unity Hub; minimalne SDK 30 |
| Gra zamula na zakrętach | Profiler: koszt `RoadMeshBuilder.Build` i `VegetationStreamer.Populate`; spadek budżetu do 1 chunka |
