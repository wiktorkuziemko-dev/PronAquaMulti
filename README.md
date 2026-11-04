# MultiRejestrator – konfigurator

Aplikacja okienkowa dla Windows do konfiguracji, odczytu danych i diagnostyki urządzeń
MultiRejestrator (PronAquaMulti) przez port szeregowy RS232/UART.

> **Stan:** szkielet projektu. Implementacja ruszy po uzgodnieniu otwartych pytań
> do dokumentacji (protokół, parametry, kod dostępu).

## Dokumentacja

- *Założenia programu do konfiguracji urządzeń MultiRejestrator* v1.00 – [`docs/specyfikacja/zalozenia.md`](docs/specyfikacja/zalozenia.md)
- *Protokół Komunikacyjny PronAquaMulti* v1.0 rev3821 (INTSOL) – [`docs/specyfikacja/protokol.md`](docs/specyfikacja/protokol.md)
- Parametry konfiguracyjne jako dane – [`docs/specyfikacja/parametry.csv`](docs/specyfikacja/parametry.csv)
- Indeks i opis formatów – [`docs/README.md`](docs/README.md), decyzje – [`docs/decyzje.md`](docs/decyzje.md)

## Struktura

| Katalog | Zawartość |
| --- | --- |
| `src/MultiRejestrator.Protocol` | Ramki protokołu, CRC, transport szeregowy. Bez zależności od GUI. |
| `src/MultiRejestrator.Core` | Model konfiguracji, specyfikacja parametrów, pliki konfiguracji. |
| `src/MultiRejestrator.App` | Aplikacja okienkowa (Avalonia, MVVM z CommunityToolkit.Mvvm). |
| `src/MultiRejestrator.Simulator` | Symulator urządzenia do testów bez sprzętu. |
| `tests/MultiRejestrator.Tests` | Testy jednostkowe i integracyjne (xUnit). |

## Wymagania

- .NET SDK 10 (wersja ustalona w `global.json`)
- Docelowo: Windows 10 x64 lub nowszy. Projekt buduje się i uruchamia także na Linuksie i macOS.

## Budowanie i uruchamianie

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/MultiRejestrator.App
```

Pojedynczy plik `.exe` dla Windows:

```bash
dotnet publish src/MultiRejestrator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

## Gotowy program z GitHub Actions

Każdy push na `main` i każdy pull request uruchamia workflow **Build** (Windows):
budowanie, testy i publikację. Plik `MultiRejestrator.exe` można pobrać z zakładki
**Actions** → wybrany przebieg → sekcja **Artifacts** → `MultiRejestrator-win-x64`.
Program nie wymaga instalacji .NET.
