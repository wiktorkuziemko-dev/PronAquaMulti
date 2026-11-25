# Rejestr decyzji

Najnowsze na górze. Odwołanie decyzji to nowy wpis. Numery w nawiasach odnoszą się do pytań
z [`specyfikacja/odpowiedzi.md`](specyfikacja/odpowiedzi.md).

## Założenia wykonawcy (do potwierdzenia)

Luki w Protokole rev3892, dla których przyjęto rozwiązanie robocze. W kodzie oznaczone
komentarzem `// ZAŁOŻENIE:`.

| Temat | Przyjęte rozwiązanie |
| --- | --- |
| Bufor zdarzeń (pkt 13 Założeń) | Protokół rev3871 nie ma ramki bufora zdarzeń. Widok „Zdarzenia” z makiety zastąpiono widokiem „Dane pomiarowe” opartym na ramce Measure Report (0x0018). Do wyjaśnienia, czy to ta sama funkcja. |
| Measure Report – kanały | Zapytanie nie zawiera numeru kanału. Odpowiedź jest dekodowana jako ciąg sekcji (nagłówek 18 B + bloki), dopóki starcza danych – obsługuje zarówno jeden, jak i kilka kanałów w ramce. |
| Measure Report – koniec danych | Program pobiera kolejne porcje od minuty następnej po ostatnio otrzymanej, aż odpowiedź nie zawiera bloków albo minuta przekroczy 1439. |
| Measure Report – pole Day Index w odpowiedzi | Traktowane jako unsigned long (4 B) – numer indeksu dnia (opis w oryginale jest skopiowany z innej ramki). |
| Monitor GSM | Start ramką 0x0017, potem co 2 s Device State (Id 1, 3). Protokół nie ma ramki zatrzymania – wyłączenie monitora w programie kończy tylko odpytywanie. |
| Odczyt pamięci (6.1.14) | Brak ramki w Protokole. Funkcja w interfejsie widoczna jako niedostępna. |
| Stan wejść/wyjść, operator, tryb pracy (6.1.5, 6.1.10) | Brak w ramce Device State. Nie są wyświetlane. Liczniki (6.1.11) są dostępne od rev3892 – ramki Device Counters i Measure Counters. |
| Zapis liczników (0x001A, 0x001C) | Program pozwala zmienić wartości liczników i zapisuje je po potwierdzeniu, tak jak konfigurację. Protokół nie opisuje ograniczeń zapisu. |
| Device State Id 3 | W oryginale „unsigned short” przy Size 1 – wartości liczbowe dekodowane zawsze według pola Size z bloku. |

## Decyzje

| Data | Decyzja | Uzasadnienie |
| --- | --- | --- |
| 2026-11-25 | Uwagi z testów z 10.11.2026 (`zrodla/Uwagi_z_testow_2026-11-10.txt`), wprowadzone w wersji 0.2: | |
| | 1. Poziom przełączania wejścia binarnego (Id 1006) wpisywany w V z jedną cyfrą po przecinku; w ramce nadal krok 100 mV. | Uwaga 1. |
| | 2. Przycisk „Rozłącz” zielony. | Uwaga 2. |
| | 3. Zegar urządzenia: pola „Czas komputera” i „Czas urządzenia”, przyciski „Odczytaj z urządzenia” i „Ustaw wg komputera” (ustawia, potem odczytuje oba czasy). Ręczne wpisywanie czasu usunięte. | Uwaga 3 (zastępuje odpowiedź 3.15 w części „ręczne”). |
| | 4. Przycisk diagnostyki „Odśwież” → „Pobierz stan”. | Uwaga 4. |
| | 5. Numer odbiorcy (Id 3) w transmisji w odwrotnej kolejności bajtów: „0W” → 0x57 0x30. | Uwaga 5 (zastępuje odpowiedź 4.2.3). |
| | 6. Pola obowiązkowe: Numer odbiorcy, APN nazwa, URL podstawowy, URL rezerwowy – czerwona obwódka; puste pole blokuje wysyłanie z komunikatem, którego pola brakuje. | Uwaga 6. |
| | 7. Brak odpowiedzi urządzenia: połączenie zamykane, „Rozłącz” nieaktywny, „Połącz” aktywny. | Uwaga 7. |
| | 8. Błędna odpowiedź lub ramka błędu nie zrywają połączenia – tylko komunikat z przyczyną. Za brak odpowiedzi uznaje się wyłącznie brak jakichkolwiek bajtów w czasie oczekiwania. | Uwaga 8. |
| | 9. „Monitorowanie GSM” → „Monitorowanie sygnału GSM (co 2 s)”. | Uwaga 9. |
| | 10. Nowa konfiguracja: pasmo CAT-M1 = B20, NB = B8, protokół TCP, format Bin (V12); bez opcji „nie wybrano” w Id 24 i 25. | Uwaga 10 (zastępuje część odpowiedzi 4.1.2). |
| | 11. Bez przycisku „Zapisz jako…”; „Zapisz” zawsze pokazuje okno wyboru pliku. | Uwaga 11. |
| | 12. Przed każdym wysłaniem konfiguracji – zapis do pliku (okno wyboru pliku); anulowanie zapisu anuluje wysyłanie. | Uwaga 12. |
| 2026-11-25 | Protokół rev3892: liczniki impulsów kanałów i liczniki urządzenia w widoku Diagnostyka – odczyt i zapis po potwierdzeniu. | Nowe ramki 1.2.12–1.2.13; pkt 12 Założeń („liczniki i inne dane diagnostyczne”). |
| 2026-11-06 | Tryb demonstracyjny `--symulator` w programie (port „SYMULATOR”, urządzenie w pamięci). Niewidoczny bez parametru. | Testy i szkolenia bez sprzętu. |
| 2026-11-06 | Widok „Zdarzenia” z makiety nazwany „Dane pomiarowe” (ramka Measure Report). | Protokół rev3871 nie ma bufora zdarzeń – patrz założenia wyżej. |
| 2026-11-06 | Numeracja wersji programu: `MAJOR.MINOR.PATCH` w `Directory.Build.props`. Tag `vX.Y.Z` na GitHubie buduje wydanie z plikami `.exe` (x64, x86). | Propozycja wykonawcy (9.9). |
| 2026-11-06 | Plik konfiguracji: tekst UTF-8 `.txt`, jeden parametr w wierszu `Id = wartość  # nazwa`, sekcje jak w interfejsie, nagłówek z wersją protokołu, numerem seryjnym, firmware i datą. Bez osobnej wersji formatu. | 7.1, 7.4, 11.3 Założeń. |
| 2026-11-05 | Log zapisywany wyłącznie w folderze programu, nadpisywany przy każdym połączeniu. Gdy system zablokuje zapis – tylko komunikat. | Ustalenie z 5.11.2026, 7.10. |
| 2026-11-05 | Inny numer seryjny w konfiguracji niż w urządzeniu – tylko ostrzeżenie; numer seryjny (Id 1) jest wysyłany z konfiguracją. | Ustalenie z 5.11.2026, 4.1.6, 5.10. |
| 2026-11-05 | Walidacja zależności tylko dla: kolejności progów alarmów (zależnie od kierunku) i co najmniej jednego pasma CAT-M1 / NB. Pozostałe ograniczenia wynikają z typów i rozmiarów pól. | Ustalenie z 5.11.2026, 4.1.3, 4.2.7, 4.3.8. |
| 2026-11-05 | Dwa pliki wykonywalne: Windows x64 i x86 (32-bit). | 1.4. |
| 2026-11-05 | Zostaje .NET 10 (mimo odpowiedzi 1.5 „.NET 8”). | Ustalenie z 5.11.2026; .NET 8 kończy wsparcie 10.11.2026. |
| 2026-11-05 | Kod dostępu należy do konfiguracji; połączenie bez pytania używa kodu z bieżącej konfiguracji. Zmiana kodu w urządzeniu to osobna operacja; Id 2 nie jest wysyłany z konfiguracją. Domyślny kod dla nowej konfiguracji w pliku `.ini`. | 5.3, 5.4, 5.7. |
| 2026-11-05 | Bez komendy RESET i bez aktualizacji firmware. | 1.2, 1.3. |
| 2026-11-05 | Komunikacja: timeout 1 s, 3 s dla zapisu, bez ponawiania; HELLO co 5 s w bezczynności. | 2.6, 2.13. |
| 2026-11-05 | Interfejs wg makiety „MultiRejestrator – makieta UI” zaakceptowanej 6.11.2026, z logo PRONAL na niebieskim tle. | Akceptacja Zamawiającego. |
| 2026-11-04 | Dokumenty źródłowe i ich transkrypcje przechowywane w repozytorium (`docs/`). | Jedno źródło prawdy. |
| 2026-11-04 | Wydanie jako pojedynczy plik `.exe` (self-contained), budowany w GitHub Actions. | 1.7. |
| 2026-11-04 | Interfejs: Avalonia 12 (Fluent), MVVM z CommunityToolkit.Mvvm. | Weryfikacja wyglądu poza Windows. |
| 2026-11-04 | Platforma: C# / .NET 10 (LTS). | Wsparcie do 2028. |
