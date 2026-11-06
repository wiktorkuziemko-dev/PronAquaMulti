# Dokumentacja projektu

| Plik | Zawartość |
| --- | --- |
| [`zrodla/`](zrodla/) | Oryginały dokumentów (DOCX, PDF). **Rozstrzygają w razie rozbieżności.** |
| [`specyfikacja/zalozenia.md`](specyfikacja/zalozenia.md) | Założenia programu – transkrypcja do Markdown. |
| [`specyfikacja/protokol.md`](specyfikacja/protokol.md) | Protokół komunikacyjny rev3871 – transkrypcja z tabelami, zestawienie komend. |
| [`specyfikacja/odpowiedzi.md`](specyfikacja/odpowiedzi.md) | Pytania do specyfikacji z odpowiedziami INTSOL i Zamawiającego (5.11.2026). |
| [`specyfikacja/stan-urzadzenia.csv`](specyfikacja/stan-urzadzenia.csv) | Parametry ramki Device State (0x0015) jako dane. |
| [`specyfikacja/parametry.csv`](specyfikacja/parametry.csv) | Wszystkie parametry konfiguracyjne jako dane (90 definicji = 517 parametrów). |
| [`decyzje.md`](decyzje.md) | Rejestr decyzji projektowych. |

Transkrypcje służą do szybkiego przeszukiwania i cytowania. Treść merytoryczną zmienia tylko autor
oryginału; po nowej wersji dokumentu źródłowego transkrypcję należy odświeżyć.

## Pytania i założenia

Odpowiedzi na pytania do specyfikacji: [`specyfikacja/odpowiedzi.md`](specyfikacja/odpowiedzi.md).
Rozwiązania robocze dla luk w Protokole: sekcja „Założenia wykonawcy” w [`decyzje.md`](decyzje.md).

## Format `parametry.csv`

Separator `;`, kodowanie UTF-8. Jeden wiersz na definicję parametru.

| Kolumna | Znaczenie |
| --- | --- |
| `id` | Id parametru. Dla parametrów kanału – podstawa; rzeczywiste Id = `id + N × 1000`, N = 0–7. |
| `kanal` | `tak` – parametr powtarzany dla 8 kanałów. |
| `rozmiar_B` | Rozmiar pola Value w bajtach. |
| `typ` | Kodowanie w ramce: `u8`, `u16`, `i16`, `u32` (little endian), `ascii` (tekst o stałej długości, dopełniany 0x00), `bytes` (surowe bajty). |
| `edycja` | Sposób edycji i walidacji: `number`, `enum`, `bitmask`, `text`, `digits` (tylko cyfry), `bcd` (8 cyfr BCD), `hex` (bajty jako znaki hex). |
| `nazwa`, `jednostka` | Z Protokołu (z poprawkami potwierdzonymi przez INTSOL). |
| `wartosci` | Lista dozwolonych wartości `kod=opis`, rozdzielona `; `. Dla `bitmask` – bity. |
| `grupa_wejscia` | Dla parametrów kanału: przy jakim typie wejścia (Id 1001) parametr ma znaczenie. |
| `sekcja` | Sekcja interfejsu: `ogolne`, `modem`, `apn`, `serwery`, `transmisja`, `kanal`. |
| `min`, `max` | Dla liczb – zakres wartości; dla `text`, `digits`, `hex` – zakres długości w znakach. |
| `domyslna` | Wartość w „Nowej konfiguracji” (odpowiedź 4.1.2). |
| `uwagi` | Ustalenia i założenia dotyczące parametru. |

Mapowanie `grupa_wejscia` → wartość Id 1001: `impulsowe` = 1, `binarne` = 2,
`analog_2_5V` = 3, `analog_10V` = 4, `analog_4_20mA` = 5, `wszystkie` = zawsze.

Plik jest wbudowany w program (`MultiRejestrator.Core`) – zmiana w CSV zmienia formularz i walidację
bez zmian w kodzie.
