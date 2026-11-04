# Dokumentacja projektu

| Plik | Zawartość |
| --- | --- |
| [`zrodla/`](zrodla/) | Oryginały dokumentów (DOCX, PDF). **Rozstrzygają w razie rozbieżności.** |
| [`specyfikacja/zalozenia.md`](specyfikacja/zalozenia.md) | Założenia programu – transkrypcja do Markdown. |
| [`specyfikacja/protokol.md`](specyfikacja/protokol.md) | Protokół komunikacyjny – transkrypcja z tabelami, zestawienie komend. |
| [`specyfikacja/parametry.csv`](specyfikacja/parametry.csv) | Wszystkie parametry konfiguracyjne jako dane (90 definicji = 517 parametrów). |
| [`decyzje.md`](decyzje.md) | Rejestr decyzji projektowych. |

Transkrypcje służą do szybkiego przeszukiwania i cytowania. Treść merytoryczną zmienia tylko autor
oryginału; po nowej wersji dokumentu źródłowego transkrypcję należy odświeżyć.

## Otwarte pytania

Pytania i niejasności do specyfikacji są zbierane w osobnym dokumencie zespołu
(„MultiRejestrator – pytania i niejasności przed rozpoczęciem prac”). Numery pytań
(np. `4.3.7`) są przywoływane w kolumnie `pytania` pliku `parametry.csv` i w komentarzach
`// ZAŁOŻENIE:` w kodzie.

## Format `parametry.csv`

Separator `;`, kodowanie UTF-8. Jeden wiersz na definicję parametru.

| Kolumna | Znaczenie |
| --- | --- |
| `id` | Id parametru. Dla parametrów kanału – podstawa; rzeczywiste Id = `id + N × 1000`, N = 0–7. |
| `kanal` | `tak` – parametr powtarzany dla 8 kanałów. |
| `rozmiar_B` | Rozmiar pola Value w bajtach. |
| `typ` | `u8`, `u16`, `i16`, `u32` (little endian), `ascii` (tekst o stałej długości), `bytes` (surowe bajty). |
| `nazwa` | Nazwa z Protokołu. |
| `jednostka` | Jednostka z Protokołu (bez skali, jeśli jej nie podano). |
| `wartosci` | Lista dozwolonych wartości `kod=opis`, rozdzielona `; `. Pusta – wartość liczbowa lub tekst. |
| `maska_bitowa` | `tak` – wartości z kolumny `wartosci` można łączyć sumą bitową. |
| `grupa_wejscia` | Dla parametrów kanału: przy jakim typie wejścia (Id 1001) parametr ma znaczenie. |
| `min`, `max`, `domyslna` | **Puste – do uzupełnienia przez INTSOL** (pytania 4.1.1, 4.1.2). |
| `pytania` | Numery powiązanych pytań z dokumentu pytań. |
| `uwagi` | Uwagi transkrypcji (np. podejrzane jednostki). |

Mapowanie `grupa_wejscia` → wartość Id 1001: `impulsowe` = 1, `binarne` = 2,
`analog_2_5V` = 3, `analog_10V` = 4, `analog_4_20mA` = 5, `wszystkie` = zawsze.
