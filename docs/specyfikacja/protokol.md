# Protokół Komunikacyjny PronAquaMulti v1.0

> Transkrypcja dokumentu `docs/zrodla/Protokol_Komunikacyjny_PronAquaMulti_v1.0_rev3821.pdf`
> (autor: INTSOL, Warszawa, 28.10.2026) do Markdown. W razie rozbieżności rozstrzyga oryginał.
> Treść przepisana bez zmian merytorycznych; oczywiste literówki w nazwach typów poprawiono.
> Uwagi transkrypcji oznaczone są jako **[Uwaga]** – to nie jest treść oryginału.
> Pełna lista parametrów konfiguracyjnych w formie danych: [`parametry.csv`](parametry.csv).

## 1. Komunikacja

Komunikacja odbywa się przy pomocy standardu UART o parametrach **115200 bps 8N1**.

### 1.1. Opis protokołu komunikacyjnego

#### 1.1.1. Opis komunikacji

Komunikacja z modułem PronAquaMulti bazuje na klasycznym odpytywaniu (tzw. „polling”) typu
zapytanie–odpowiedź. Oznacza to, że moduł jest urządzeniem pasywnym i każda komunikacja musi
zostać zainicjowana przez urządzenie nadrzędne (np. PC). Do komunikacji został wykorzystany
standardowy protokół opracowany na wewnętrzne potrzeby firmy Intsol.

#### 1.1.2. Format nagłówka protokołu

| Nazwa | magic word | size | type | id | CRC | Password | data |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Rozmiar | 3 B | 2 B | 2 B | 1 B | 2 B | 6 B | xxx |
| Offset | 0 | 3 | 5 | 7 | 8 | 10 | 16 |

- **magic word** – znacznik początku ramki, trzy znaki ASCII: `"PMU"`.
- **size** – długość ramki w bajtach, wliczając pola: type, id, CRC, Password, data.
- **type** – kod rodzaju ramki / kod ramki odpowiedzi.
- **id** – w ramce, której odbiorcą jest Slave (ramka zapytania), jest to numer sekwencyjny
  wiadomości, określany przez nadawcę ramki (może być stały, np. 0); w ramce, której odbiorcą jest
  Master (ramka odpowiedzi), wartość ta jest równa wartości pola id ramki zapytania.
- **CRC** – suma kontrolna, wartość liczona z uwzględnieniem wszystkich pól ramki z wyłączeniem
  bajtów CRC. Zastosowanie ma algorytm CRC-CCITT (0xFFFF).
- **Password** – hasło dostępu; pole występuje jedynie w przypadku komunikacji w kierunku
  Master → Slave.

W strukturze protokołu oraz we wszystkich przesyłanych komendach zastosowana jest kolejność bajtów
**Little Endian**. Pierwszy jest mniej znaczący bajt.

> **[Uwaga]** Offsety w tabeli dotyczą ramki zapytania (z polem Password). W ramce odpowiedzi
> pole Password nie występuje, więc data zaczyna się od offsetu 10. Zakres size i CRC – patrz
> pytania 2.1–2.4 w dokumencie pytań.

### 1.2. Komendy sterujące

- **Master** – urządzenie nadrzędne (komputer PC) komunikujące się z licznikiem PronAqua.
- **Slave** – licznik PronAqua.

Każda komenda przesłana do urządzenia Slave potwierdzana jest odpowiednią komendą lub komunikatem
błędu. Ramka błędu zostaje wysłana w przypadku, gdy urządzenie nie potrafi poprawnie zinterpretować
odebranej komendy lub gdy doszło do przekłamania bitów/bajtów przesyłanych danych.

Pola danych oznaczone jako ciągi znaków ASCII powinny być terminowane znakiem spoza ASCII lub 0x00,
w przypadku gdy ciąg jest krótszy niż dedykowany dla niego obszar pamięci.

#### Zestawienie komend

| Zapytanie | Kod | Odpowiedź | Kod | DATA zapytania | DATA odpowiedzi | Rozdział |
| --- | --- | --- | --- | --- | --- | --- |
| REQ_HELLO | 0x0001 | RE_REQ_HELLO | 0x8001 | 0 B | 0 B | 1.2.1 |
| REQ_RESET | 0x0002 | RE_REQ_RESET | 0x8002 | 0 B | 0 B | 1.2.2 |
| REQ_GET_FW_DATE | 0x0003 | RE_REQ_GET_FW_DATE | 0x8003 | 0 B | 30 B | 1.2.3 |
| REQ_GET_PROT_REV | 0x0004 | RE_REQ_GET_PROT_REV | 0x8004 | 0 B | 2 B | 1.2.4 |
| – | – | ERROR_FRAME | 0xFFFF | – | 2 B | 1.2.5 |
| REQ_GET_CONF | 0x0011 | RE_REQ_GET_CONF | 0x8011 | 0–20 B | 0–1024 B | 1.2.6 |
| REQ_SET_CONF | 0x0012 | RE_REQ_SET_CONF | 0x8012 | 0–1024 B | 0 B | 1.2.6 |
| REQ_GET_TIME | 0x0013 | RE_REQ_GET_TIME | 0x8013 | 0 B | 4 B | 1.2.7 |
| REQ_SET_TIME | 0x0014 | RE_REQ_SET_TIME | 0x8014 | 4 B | 0 B | 1.2.7 |
| REQ_UPDATE_FW | 0x0051 | RE_REQ_UPDATE_FW | 0x8051 | 8 B | 1 B | 1.2.8 |
| REQ_GET_FW_CRC | 0x0052 | RE_REQ_GET_FW_CRC | 0x8052 | 8 B | 4 B | 1.2.9 |
| REQ_GET_FW_PARAMS | 0x0053 | RE_REQ_GET_FW_PARAMS | 0x8053 | 0 B | 8 B | 1.2.10 |
| REQ_SAVE_FW_N_PACKETS | 0x0054 | RE_REQ_SAVE_FW_N_PACKETS | 0x8054 | 5–1029 B | 0 B | 1.2.11 |
| REQ_CLEAR_ALL_FW_DATA | 0x0055 | RE_REQ_CLEAR_ALL_FW_DATA | 0x8055 | 0 B | 0 B | 1.2.12 |

> **[Uwaga]** Kod odpowiedzi = kod zapytania | 0x8000.

#### 1.2.1. Ramka HELLO

Ramka powitalna. Nie wywołuje żadnej reakcji na urządzeniu. Służy do sprawdzenia poprawnej
komunikacji pomiędzy Masterem i Slavem.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_HELLO | RE_REQ_HELLO |
| Typ (pole TYPE) | 0x0001 | 0x8001 |
| Rozmiar pola DATA | 0 B | 0 B |

#### 1.2.2. Ramka RESET

Ramka resetująca licznik PronAqua.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_RESET | RE_REQ_RESET |
| Typ (pole TYPE) | 0x0002 | 0x8002 |
| Rozmiar pola DATA | 0 B | 0 B |

#### 1.2.3. Ramka Firmware Date

Ramka odczytująca wersję firmware'u.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_GET_FW_DATE | RE_REQ_GET_FW_DATE |
| Typ (pole TYPE) | 0x0003 | 0x8003 |
| Rozmiar pola DATA | 0 B | 30 B |

Pole DATA odpowiedzi RE_REQ_GET_FW_DATE:

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| SVNRev | 2 | Numer wersji rewizji na SVN-ie (1 × unsigned short) |
| Fw Date | 14 | Data kompilacji (14 bajtów w formacie ASCII) |
| Fw Time | 14 | Czas kompilacji (14 bajtów w formacie ASCII) |
| SUMA | 30 | |

#### 1.2.4. Ramka Protocol Revision

Ramka odczytująca wersję protokołu komunikacyjnego.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_GET_PROT_REV | RE_REQ_GET_PROT_REV |
| Typ (pole TYPE) | 0x0004 | 0x8004 |
| Rozmiar pola DATA | 0 B | 2 B |

Pole DATA odpowiedzi (w oryginale opisane jako RE_REQ_GET_PROT_VER):

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| ProtocolVersion | 2 | Numer wersji protokołu (1 × unsigned short) |
| SUMA | 2 | |

**Wersja protokołu opisanego w tym dokumencie ma wartość 1.**

#### 1.2.5. Ramka Error

Ramka błędu – komenda wysyłana w przypadku wykrycia błędu w transmisji.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | – | ERROR_FRAME |
| Typ (pole TYPE) | – | 0xFFFF |
| Rozmiar pola DATA | – | 2 B |

Pole DATA:

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| Error | 2 | Kod błędu (patrz niżej) |
| SUMA | 2 | |

| Kod | Znaczenie |
| --- | --- |
| 0x01 | błąd CRC |
| 0x02 | błędny rozmiar ramki |
| 0x04 | błąd dostępu do pamięci flash |
| 0x08 | błędne hasło dostępu |
| 0x10 | nieznany typ ramki |

#### 1.2.6. Ramka Configuration

Ramka wysyłająca i pobierająca parametry konfiguracyjne.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_GET_CONF | RE_REQ_GET_CONF |
| Typ (pole TYPE) | 0x0011 | 0x8011 |
| Rozmiar pola DATA | 0–20 B | 0–1024 B |

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_SET_CONF | RE_REQ_SET_CONF |
| Typ (pole TYPE) | 0x0012 | 0x8012 |
| Rozmiar pola DATA | 0–1024 B | 0 B |

Parametry konfiguracyjne przesyłane są w blokach o następującej budowie (**ConfBlock**):

| Nazwa | Rozmiar | Opis |
| --- | --- | --- |
| Id | 2 B | Numer identyfikacyjny parametru konfiguracyjnego (unsigned short) |
| Size | 2 B | Rozmiar pola „Value” (unsigned short) |
| Value | 1+ B | Wartość danego parametru. W zależności od parametru pole to może przyjmować różne wielkości i może być różnie interpretowane |

Pole DATA zapytania REQ_GET_CONF:

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| Id[0–10] | 0–20 | Tablica zawierająca numery Id parametrów żądanych w ramce odpowiedzi (tablica unsigned short) |
| SUMA | 0–20 | |

Pole DATA REQ_SET_CONF i RE_REQ_GET_CONF:

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| ConfBlock[0–10] | – | Tablica bloków konfiguracyjnych zawierających wartości parametrów. W każdej ramce może znajdować się od 0 do 10 takich bloków |
| SUMA | – | |

> **[Uwaga]** „Id[0–10]” przy rozmiarze 0–20 B oznacza maksymalnie 10 identyfikatorów.

##### Zestawienie parametrów konfiguracyjnych

N oznacza numer kanału w przedziale 0–7. Typy: unsigned char = 1 B, unsigned short = 2 B,
signed short = 2 B, unsigned long = 4 B.

**Parametry ogólne**

| Id | Size | Value |
| --- | --- | --- |
| 1 | 4 | Numer seryjny urządzenia (unsigned long) |
| 2 | 6 | Kod dostępu (6 × unsigned char) |
| 3 | 2 | Numer odbiorcy (unsigned short) |
| 4 | 1 | Rodzaj anteny (unsigned char) |
| 5 | 1 | Wybór modułu i tryb jego pracy (unsigned char): 1 – SIM7080G CAT-M1; 2 – SIM7080G NB; 3 – SIM7080G CAT-M1+NB; 4 – BG770A-GL CAT-M1; 5 – BG770A-GL NB; 6 – BG770A-GL CAT-M1+NB; 7 – SIM7082G CAT-M1; 8 – SIM7082G NB; 9 – SIM7082G CAT-M1+NB |
| 6 | 4 | Kod PIN (4 × unsigned char) |
| 7 | 18 | APN nazwa (18 × ASCII) |
| 8 | 18 | APN użytkownik (18 × ASCII) |
| 9 | 18 | APN hasło (18 × ASCII) |
| 10 | 4 | Wybór pasma dla CAT-M1 w postaci mapy bitów (unsigned long): 0x01 – B3; 0x02 – B8; 0x04 – B20 |
| 11 | 4 | Wybór pasma dla NB w postaci mapy bitów (unsigned long): 0x01 – B3; 0x02 – B8; 0x04 – B20 |
| 12 | 1 | Zezwolenie na roaming (unsigned char): 0 – brak zezwolenia; 1 – zezwolenie |
| 13 | 4 | Maksymalny czas trwania pojedynczej sesji [s] (unsigned long) |
| 14 | 4 | Interwał wybudzania w trybie PSM [s] (unsigned long) |
| 15 | 4 | Interwał aktywności w trybie eDRX [s] (unsigned long) |
| 16 | 32 | URL podstawowy (32 × ASCII) |
| 17 | 32 | URL rezerwowy (32 × ASCII) |
| 18 | 32 | MQTT URL (32 × ASCII) |
| 19 | 2 | MQTT Port (unsigned short) |
| 20 | 32 | MQTT Client ID (32 × ASCII) |
| 21 | 32 | MQTT Username (32 × ASCII) |
| 22 | 32 | MQTT Password (32 × ASCII) |
| 23 | 32 | MQTT Sent Topic (32 × ASCII) |
| 24 | 1 | Wybór protokołu (unsigned char): 1 – TCP; 2 – HTTPS; 3 – MQTT; 4 – FTP; 5 – SFTP |
| 25 | 1 | Wybór formatu danych w raporcie (unsigned char): 1 – Bin (protokół własny V12); 2 – JSON; 3 – CSV; 4 – XML |
| 26 | 32 | Klucz szyfrowania AES (32 × unsigned char) |
| 27 | 4 | Czas na stabilizację wejścia sabotażu otwarcia pokrywy [s] (unsigned long) |
| 28 | 2 | Wartość kalibracyjna temperatury [°C] (signed short) |
| 29 | 50 | Symbol (50 × ASCII) |

**Parametry kanału N (Id = podstawa + N × 1000, N = 0–7)**

Wspólne listy wartości używane niżej:

- *Aktywność sygnalizacji alarmu:* 0 – brak aktywności; 1 – sygnalizacja wywołania alarmu;
  2 – sygnalizacja odwołania alarmu; 3 – sygnalizacja wywołania i odwołania alarmu.
- *Kierunek sygnalizacji alarmu:* 0 – wywołanie alarmu przy narastaniu, odwołanie przy opadaniu;
  1 – wywołanie alarmu przy opadaniu, odwołanie przy narastaniu.

| Podstawa Id | Size | Value |
| --- | --- | --- |
| 1001 | 1 | Typ wejścia kanału N (unsigned char): 0 – nieaktywne; 1 – cyfrowe impulsowe; 2 – cyfrowe binarne; 3 – analogowe 2,5 V; 4 – analogowe 10 V; 5 – analogowe 4–20 mA |
| 1002 | 1 | Tryb pracy dla wejścia impulsowego kanału N (unsigned char): 0 – podstawowy; 1 – sprzężony zliczanie: impulsy do przodu, impulsy do tyłu; 2 – sprzężony zliczanie: impulsy, kierunek poziom wysoki inkrementacja; 3 – sprzężony zliczanie: impulsy, kierunek poziom niski inkrementacja |
| 1003 | 2 | Minimalna długość impulsu dla wejścia impulsowego kanału N [ms] (unsigned short) |
| 1004 | 1 | Dziesiętny mnożnik wagi impulsu dla wejścia impulsowego kanału N (unsigned char): 0 – ×1; 1 – ×10; 2 – ×100; 3 – ×1000 |
| 1005 | 2 | Czas braku impulsów dla wejścia impulsowego kanału N [min] (unsigned short) |
| 1006 | 1 | Poziom przełączania wejścia binarnego kanału N [100 mV] (unsigned char) |
| 1007 | 1 | Aktywność sygnalizacji przejścia wejścia binarnego kanału N: 0 – brak; 1 – na stan wysoki; 2 – na stan niski; 3 – na stan wysoki i niski |
| 1008 | 2 | Czas na stabilizację nowego stanu wejścia binarnego kanału N [ms] (unsigned short) |
| 1009 | 2 | Czas martwy od momentu włączenia czujnika do rozpoczęcia odczytu dla wejścia analogowego 2,5 V kanału N [ms] (unsigned short) |
| 1010 | 2 | Wartość progu wywołania 1 alarmu dla wejścia analogowego 2,5 V kanału N [mV] (unsigned short) |
| 1011 | 2 | Wartość progu odwołania 1 alarmu dla wejścia analogowego 2,5 V kanału N [mV] (unsigned short) |
| 1012 | 2 | Wartość progu wywołania 2 alarmu dla wejścia analogowego 2,5 V kanału N [mV] (unsigned short) |
| 1013 | 2 | Wartość progu odwołania 2 alarmu dla wejścia analogowego 2,5 V kanału N **[ms]** (unsigned short) **[Uwaga: w oryginale „ms”, prawdopodobnie „mV”]** |
| 1014 | 2 | Wartość progu wywołania 3 alarmu dla wejścia analogowego 2,5 V kanału N [mV] (unsigned short) |
| 1015 | 2 | Wartość progu odwołania 3 alarmu dla wejścia analogowego 2,5 V kanału N [mV] (unsigned short) |
| 1016 | 2 | Wartość progu wywołania 4 alarmu dla wejścia analogowego 2,5 V kanału N [mV] (unsigned short) |
| 1017 | 2 | Wartość progu odwołania 4 alarmu dla wejścia analogowego 2,5 V kanału N [mV] (unsigned short) |
| 1018 | 1 | Aktywność sygnalizacji alarmu 1 dla wejścia analogowego 2,5 V kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1019 | 1 | Aktywność sygnalizacji alarmu 2 dla wejścia analogowego 2,5 V kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1020 | 1 | Aktywność sygnalizacji alarmu 3 dla wejścia analogowego 2,5 V kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1021 | 1 | Aktywność sygnalizacji alarmu 4 dla wejścia analogowego 2,5 V kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1022 | 1 | Kierunek sygnalizacji alarmu 1 dla wejścia analogowego 2,5 V kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1023 | 1 | Kierunek sygnalizacji alarmu 2 dla wejścia analogowego 2,5 V kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1024 | 1 | Kierunek sygnalizacji alarmu 3 dla wejścia analogowego 2,5 V kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1025 | 1 | Kierunek sygnalizacji alarmu 4 dla wejścia analogowego 2,5 V kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1026 | 2 | Czas martwy od momentu włączenia czujnika do rozpoczęcia odczytu dla wejścia analogowego 10 V kanału N [ms] (unsigned short) |
| 1027 | 2 | Wartość progu wywołania 1 alarmu dla wejścia analogowego 10 V kanału N [mV] (unsigned short) |
| 1028 | 2 | Wartość progu odwołania 1 alarmu dla wejścia analogowego 10 V kanału N [mV] (unsigned short) |
| 1029 | 2 | Wartość progu wywołania 2 alarmu dla wejścia analogowego 10 V kanału N [mV] (unsigned short) |
| 1030 | 2 | Wartość progu odwołania 2 alarmu dla wejścia analogowego 10 V kanału N [mV] (unsigned short) |
| 1031 | 2 | Wartość progu wywołania 3 alarmu dla wejścia analogowego 10 V kanału N [mV] (unsigned short) |
| 1032 | 2 | Wartość progu odwołania 3 alarmu dla wejścia analogowego 10 V kanału N [mV] (unsigned short) |
| 1033 | 2 | Wartość progu wywołania 4 alarmu dla wejścia analogowego 10 V kanału N [mV] (unsigned short) |
| 1034 | 2 | Wartość progu odwołania 4 alarmu dla wejścia analogowego 10 V kanału N [mV] (unsigned short) |
| 1035 | 1 | Aktywność sygnalizacji alarmu 1 dla wejścia analogowego 10 V kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1036 | 1 | Aktywność sygnalizacji alarmu 2 dla wejścia analogowego 10 V kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1037 | 1 | Aktywność sygnalizacji alarmu 3 dla wejścia analogowego 10 V kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1038 | 1 | Aktywność sygnalizacji alarmu 4 dla wejścia analogowego 10 V kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1039 | 1 | Kierunek sygnalizacji alarmu 1 dla wejścia analogowego 10 V kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1040 | 1 | Kierunek sygnalizacji alarmu 2 dla wejścia analogowego 10 V kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1041 | 1 | Kierunek sygnalizacji alarmu 3 dla wejścia analogowego 10 V kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1042 | 1 | Kierunek sygnalizacji alarmu 4 dla wejścia analogowego 10 V kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1043 | 2 | Czas martwy od momentu włączenia czujnika do rozpoczęcia odczytu dla wejścia analogowego 4–20 mA kanału N [ms] (unsigned short) |
| 1044 | 2 | Maksymalny czas stabilizacji prądu dla wejścia analogowego 4–20 mA kanału N [ms] (unsigned short) |
| 1045 | 1 | Procentowa granica wahań prądu dla wejścia analogowego 4–20 mA kanału N [%] (unsigned char) |
| 1046 | 2 | Wartość progu wywołania 1 alarmu dla wejścia analogowego 4–20 mA kanału N [mA] (unsigned short) |
| 1047 | 2 | Wartość progu odwołania 1 alarmu dla wejścia analogowego 4–20 mA kanału N [mA] (unsigned short) |
| 1048 | 2 | Wartość progu wywołania 2 alarmu dla wejścia analogowego 4–20 mA kanału N [mA] (unsigned short) |
| 1049 | 2 | Wartość progu odwołania 2 alarmu dla wejścia analogowego 4–20 mA kanału N [mA] (unsigned short) |
| 1050 | 2 | Wartość progu wywołania 3 alarmu dla wejścia analogowego 4–20 mA kanału N [mA] (unsigned short) |
| 1051 | 2 | Wartość progu odwołania 3 alarmu dla wejścia analogowego 4–20 mA kanału N [mA] (unsigned short) |
| 1052 | 2 | Wartość progu wywołania 4 alarmu dla wejścia analogowego 4–20 mA kanału N [mA] (unsigned short) |
| 1053 | 2 | Wartość progu odwołania 4 alarmu dla wejścia analogowego 4–20 mA kanału N [mA] (unsigned short) |
| 1054 | 1 | Aktywność sygnalizacji alarmu 1 dla wejścia analogowego 4–20 mA kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1055 | 1 | Aktywność sygnalizacji alarmu 2 dla wejścia analogowego 4–20 mA kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1056 | 1 | Aktywność sygnalizacji alarmu 3 dla wejścia analogowego 4–20 mA kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1057 | 1 | Aktywność sygnalizacji alarmu 4 dla wejścia analogowego 4–20 mA kanału N (unsigned char) – *aktywność sygnalizacji alarmu* |
| 1058 | 1 | Kierunek sygnalizacji alarmu 1 dla wejścia analogowego 4–20 mA kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1059 | 1 | Kierunek sygnalizacji alarmu 2 dla wejścia analogowego 4–20 mA kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1060 | 1 | Kierunek sygnalizacji alarmu 3 dla wejścia analogowego 4–20 mA kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |
| 1061 | 1 | Kierunek sygnalizacji alarmu 4 dla wejścia analogowego 4–20 mA kanału N (unsigned char) – *kierunek sygnalizacji alarmu* |

> **[Uwaga]** Razem: 29 parametrów ogólnych + 8 × 61 parametrów kanałów = 517 parametrów.
> Przykład: typ wejścia kanału 0 ma Id 1001, kanału 7 – Id 8001.

#### 1.2.7. Ramka Time

Ramka wysyłająca i pobierająca aktualny czas.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_GET_TIME | RE_REQ_GET_TIME |
| Typ (pole TYPE) | 0x0013 | 0x8013 |
| Rozmiar pola DATA | 0 B | 4 B |

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_SET_TIME | RE_REQ_SET_TIME |
| Typ (pole TYPE) | 0x0014 | 0x8014 |
| Rozmiar pola DATA | 4 B | 0 B |

Pole DATA RE_REQ_GET_TIME i REQ_SET_TIME:

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| Time | 4 | Czas – timestamp (1 × unsigned long) |
| SUMA | 4 | |

#### 1.2.8. Ramka Firmware Update

Ramka wysyłająca rozkaz rozpoczęcia wymiany firmware'u.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_UPDATE_FW | RE_REQ_UPDATE_FW |
| Typ (pole TYPE) | 0x0051 | 0x8051 |
| Rozmiar pola DATA | 8 B | 1 B |

Pole DATA REQ_UPDATE_FW:

| Parametr | Rozmiar [B] | Jednostka | Opis |
| --- | --- | --- | --- |
| Size | 4 | B | Rozmiar firmware'u do zaktualizowania (1 × unsigned long) |
| CRC | 4 | – | Suma kontrolna wyliczona z firmware'u do zaktualizowania algorytmem CRC-32 (wielomian 0x04C11DB7) (1 × unsigned long) |
| SUMA | 8 | | |

Pole DATA RE_REQ_UPDATE_FW:

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| Error | 1 | Status poprawności danych: 0 – dane poprawne; 1 – niepoprawne dane |
| SUMA | 1 | |

#### 1.2.9. Ramka Firmware CRC

Ramka pobierająca sumę kontrolną z przesłanych danych nowego firmware'u. Suma kontrolna liczona
jest z danych umieszczonych w pamięci flash w zadanym rozmiarze.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_GET_FW_CRC | RE_REQ_GET_FW_CRC |
| Typ (pole TYPE) | 0x0052 | 0x8052 |
| Rozmiar pola DATA | 8 B | 4 B |

Pole DATA REQ_GET_FW_CRC:

| Parametr | Rozmiar [B] | Jednostka | Opis |
| --- | --- | --- | --- |
| Start Address | 4 | B | Adres, od którego ma być liczona suma kontrolna (1 × unsigned long) |
| Stop Address | 4 | B | Adres, do którego ma być liczona suma kontrolna (1 × unsigned long) |
| SUMA | 8 | | |

Pole DATA RE_REQ_GET_FW_CRC:

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| CRC | 4 | Suma kontrolna wyliczona algorytmem CRC-32 (wielomian 0x04C11DB7) (1 × unsigned long) |
| SUMA | 4 | |

#### 1.2.10. Ramka Firmware Params

Ramka pobierająca informację o parametrach firmware'u możliwego do przesłania do urządzenia.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_GET_FW_PARAMS | RE_REQ_GET_FW_PARAMS |
| Typ (pole TYPE) | 0x0053 | 0x8053 |
| Rozmiar pola DATA | 0 B | 8 B |

Pole DATA RE_REQ_GET_FW_PARAMS (w oryginale opisane jako RE_REQ_GET_FW_CRC):

| Parametr | Rozmiar [B] | Jednostka | Opis |
| --- | --- | --- | --- |
| Packet Size | 4 | B | Rozmiar pakietu danych (1 × unsigned long) |
| Packet Number | 4 | – | Liczba pakietów danych możliwa do przesłania (1 × unsigned long) |
| SUMA | 8 | | |

#### 1.2.11. Ramka Firmware Packet N-size

Ramka wysyłająca większą liczbę pakietów danych firmware'u.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_SAVE_FW_N_PACKETS | RE_REQ_SAVE_FW_N_PACKETS |
| Typ (pole TYPE) | 0x0054 | 0x8054 |
| Rozmiar pola DATA | 5–1029 B | 0 B |

Pole DATA REQ_SAVE_FW_N_PACKETS:

| Parametr | Rozmiar [B] | Opis |
| --- | --- | --- |
| PacketNb | 4 | Numer pakietu danych (pierwszy pakiet oznaczony jest numerem 0) (1 × unsigned long) |
| PacketAmount | 1 | Liczba pakietów przesyłanych w ramce, w zakresie 0–16 (1 × unsigned char) |
| Fw Data[0–192] | 0–1024 | Dane pakietu |
| SUMA | 5–1029 | |

#### 1.2.12. Ramka Clear All Firmware Data

Ramka czyszcząca bufor tymczasowy bootloadera.

| | Zapytanie | Odpowiedź |
| --- | --- | --- |
| Nazwa komendy | REQ_CLEAR_ALL_FW_DATA | RE_REQ_CLEAR_ALL_FW_DATA |
| Typ (pole TYPE) | 0x0055 | 0x8055 |
| Rozmiar pola DATA | 0 B | 0 B |
