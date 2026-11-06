# MultiRejestrator – konfigurator

Aplikacja okienkowa dla Windows do konfiguracji, odczytu danych i diagnostyki urządzeń
MultiRejestrator (PronAquaMulti) przez port szeregowy RS232/UART.

> **Stan:** wersja robocza 0.1 – pełna konfiguracja, pliki, porównanie, diagnostyka i dane pomiarowe
> zgodnie z Protokołem rev3871. Luki w Protokole i przyjęte założenia: [`docs/decyzje.md`](docs/decyzje.md).

## Dokumentacja

- *Założenia programu do konfiguracji urządzeń MultiRejestrator* v1.00 – [`docs/specyfikacja/zalozenia.md`](docs/specyfikacja/zalozenia.md)
- *Protokół Komunikacyjny PronAquaMulti* v1.0 rev3871 (INTSOL) – [`docs/specyfikacja/protokol.md`](docs/specyfikacja/protokol.md)
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

Pojedynczy plik `.exe` dla Windows (dla 32-bit: `-r win-x86`):

```bash
dotnet publish src/MultiRejestrator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

## Tryb demonstracyjny (bez urządzenia)

Uruchomienie z parametrem `--symulator` dodaje do listy portów pozycję **SYMULATOR** – urządzenie
symulowane w pamięci (kod dostępu `000000`). Pozwala przećwiczyć wszystkie funkcje bez sprzętu.

```bash
dotnet run --project src/MultiRejestrator.App -- --symulator
```

Symulator można też wystawić na porcie szeregowym (np. jednym z pary wirtualnych portów com0com)
i łączyć się z nim programem przez drugi port pary:

```bash
dotnet run --project src/MultiRejestrator.Simulator -- COM10
```

## Pliki programu

| Plik | Miejsce | Zawartość |
| --- | --- | --- |
| `MultiRejestrator.ini` | obok programu | ustawienia: ostatni port, domyślny kod dostępu, opcje |
| `MultiRejestrator.log` | obok programu | log komunikacji (pełne ramki), nadpisywany przy każdym połączeniu |
| `*.txt` | wybrane przez użytkownika | konfiguracje urządzeń – czytelny tekst, jeden parametr w wierszu |

## Gotowy program z GitHub Actions

Każdy push na `main` i każdy pull request uruchamia workflow **Build** (Windows):
budowanie, testy i publikację dwóch plików – dla Windows 64- i 32-bitowego. Pliki można pobrać
z zakładki **Actions** → wybrany przebieg → sekcja **Artifacts** (`MultiRejestrator-win-x64`,
`MultiRejestrator-win-x86`). Program nie wymaga instalacji .NET.

Wydanie: tag `vX.Y.Z` (zgodny z wersją w `Directory.Build.props`) tworzy GitHub Release z oboma plikami.
