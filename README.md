# Horizon Drive — wyścigowa gra 3D na Androida (Unity 6, URP)

Projekt gry wyścigowej z nieskończoną, proceduralnie generowaną drogą asfaltową, realistycznym
prowadzeniem (model simcade) i trybami: Endless Drive, Free Ride, Time Attack, Traffic Challenge.

> **Stan w skrócie:** rdzeń gry (generator drogi, fizyka, ruch drogowy, zapis, jakość, layout Folda)
> jest zaimplementowany i przetestowany (53 testy przechodzą). Warstwa Unity jest napisana i
> **kompiluje się semantycznie** na referencjach UnityEngine 2021.3, ale **nie była uruchomiona w
> Unity Editorze ani na telefonie**. **Plik APK nie został wygenerowany** — w tym środowisku nie ma
> Unity Editora ani licencji. Szczegóły: [docs/STATUS.md](docs/STATUS.md).

## Co jest w repozytorium

| Ścieżka | Zawartość |
|---|---|
| `Assets/Race1/Core/` | Czysta logika C# bez UnityEngine (asmdef `Race1.Core`): droga, fizyka, opony, ruch AI, kamera, jakość, wejście, layout, niebo, dźwięk silnika, zapis, garaż |
| `Assets/Race1/Game/` | Warstwa Unity: punkt wejścia `GameSession`, strumieniowanie chunków, roślinność instancjonowana, samochody, kamery, HUD, menu, wejście dotykowe/pad/klawiatura/tilt, audio, platforma Android |
| `Assets/Race1/Editor/` | Konfiguracja projektu i budowa APK (`Race1 > Setup Project`, `Race1 > Build Android APK`) |
| `Assets/Race1/Tests/Core/` | Testy NUnit rdzenia (uruchamiane w Unity Test Runner i w `dotnet test`) |
| `tools/CoreTests/` | Projekt `dotnet test` kompilujący `Core` i testy na zwykłym .NET 8 |
| `tools/UnityCheck/` | Kompilacja semantyczna warstwy Unity przeciw referencjom `UnityEngine.Modules` 2021.3 (+ stuby) |
| `docs/` | Architektura, stan prac, instrukcja budowy i instalacji |

## Szybki start

1. Zainstaluj **Unity 6** (6000.0.x LTS) z modułem **Android Build Support** (w pakiecie są SDK, NDK i OpenJDK).
2. Otwórz folder repozytorium w Unity Hub. Unity pobierze pakiety z `Packages/manifest.json`.
3. W menu: **Race1 › Setup Project**. Następnie **Race1 › Build Android APK** (APK w `Builds/Android/HorizonDrive.apk`).
4. Szczegóły, w tym ustawienie *Active Input Handling = Both* i testy na urządzeniu: [docs/BUILD_AND_INSTALL.md](docs/BUILD_AND_INSTALL.md).

Testy rdzenia bez Unity:

```bash
dotnet test tools/CoreTests/CoreTests.csproj
```

## Dokumentacja

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — moduły, układy współrzędnych, determinizm, strumieniowanie, wydajność.
- [docs/STATUS.md](docs/STATUS.md) — co zaimplementowano, co zweryfikowano, wyniki testów i znane ograniczenia.
- [docs/BUILD_AND_INSTALL.md](docs/BUILD_AND_INSTALL.md) — budowa APK, instalacja, lista testów urządzeniowych.

## Licencja i zasoby

Kod i proceduralne zasoby są oryginalne. Nazwy samochodów (Aurora Veloce, Tempesta GTX, Kestrel SR)
są autorskie i nie odpowiadają żadnemu producentowi. Projekt nie zawiera modeli, dźwięków ani tekstur
z zewnętrznych źródeł.
