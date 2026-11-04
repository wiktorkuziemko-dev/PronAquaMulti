# MultiRejestrator – konfigurator

Aplikacja desktopowa (Windows) do konfiguracji urządzeń MultiRejestrator / PronAquaMulti przez RS232/UART.
Specyfikacja: „Założenia programu MultiRejestrator” v1.00 oraz „Protokół Komunikacyjny PronAquaMulti” v1.0 rev3821.
Otwarte pytania do specyfikacji są prowadzone w osobnym dokumencie zespołu, poza repozytorium.

## Dokumentacja (`docs/`)

- `docs/specyfikacja/zalozenia.md`, `docs/specyfikacja/protokol.md` – transkrypcje; czytaj je zamiast PDF/DOCX.
  Oryginały w `docs/zrodla/` rozstrzygają w razie rozbieżności.
- `docs/specyfikacja/parametry.csv` – parametry konfiguracyjne jako dane; format opisany w `docs/README.md`.
- `docs/decyzje.md` – rejestr decyzji; dopisz wpis przy każdej istotnej decyzji projektowej.

## Technologia

- C# / .NET 10, Avalonia 12 (Fluent), CommunityToolkit.Mvvm, System.IO.Ports, xUnit.
- Wspólne ustawienia w `Directory.Build.props` (TargetFramework, Nullable, ImplicitUsings, wersja programu).
  Ostrzeżenia są traktowane jak błędy.

## Architektura

- `Protocol` – ramki, CRC, transport. Nie zna GUI ani modelu konfiguracji.
- `Core` – model konfiguracji, specyfikacja parametrów (docelowo jako dane, nie kod), pliki. Nie zna GUI.
- `App` – wyłącznie widoki i modele widoków; logika w `Core` / `Protocol`.
- `Simulator` – implementacja urządzenia po stronie Slave, używana w testach.

## Zasady

- Identyfikatory w kodzie po angielsku; komentarze, teksty interfejsu i komunikaty po polsku.
- Każde założenie, którego nie potwierdza dokumentacja, oznaczaj komentarzem `// ZAŁOŻENIE:` z opisem,
  żeby dało się je łatwo odnaleźć i zweryfikować.
- Zapis do urządzenia zawsze wymaga świadomego potwierdzenia użytkownika (pkt 9, 15 Założeń).
- Logika protokołu musi być testowalna bez portu COM (transport za interfejsem, testy na symulatorze).

## Polecenia

- Budowanie: `dotnet build`
- Testy: `dotnet test`
- Uruchomienie: `dotnet run --project src/MultiRejestrator.App`
