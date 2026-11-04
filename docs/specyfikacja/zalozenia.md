# Założenia programu do konfiguracji urządzeń MultiRejestrator

> Transkrypcja dokumentu `docs/zrodla/MultiRejestrator_Zalozenia_programu_v1_02.docx` do Markdown.
> W razie rozbieżności rozstrzyga oryginał. Nie edytuj treści merytorycznej – zmiany wprowadza autor dokumentu.

*Aplikacja dla systemu Windows*

| **Dokument**                        | **Wersja** | **Status**                              |
|-------------------------------------|------------|-----------------------------------------|
| Założenia programu MultiRejestrator | 1.00       | Dokument roboczy / założenia początkowe |

## 1. Cel i przeznaczenie programu

Program MultiRejestrator jest aplikacją dla systemu Windows przeznaczoną do konfiguracji, odczytu danych, diagnostyki i obsługi urządzeń MultiRejestrator poprzez interfejs komunikacyjny RS232.

Program ma umożliwiać użytkownikowi wprowadzanie danych konfiguracyjnych w formularzu, zapis tych danych w urządzeniu, odczyt konfiguracji z urządzenia oraz zapis i odczyt konfiguracji z plików na komputerze.

## 2. Zakres programu

Program powinien obejmować następujące główne obszary funkcjonalne:

- komunikację z urządzeniem MultiRejestrator poprzez RS232;
- wykrywanie i identyfikację podłączonego urządzenia;
- tworzenie nowej konfiguracji;
- edycję parametrów konfiguracji;
- odczyt konfiguracji z urządzenia;
- wysyłanie konfiguracji do urządzenia;
- zapis konfiguracji do pliku i odczyt konfiguracji z pliku;
- prezentację parametrów technicznych i diagnostycznych urządzenia;
- odczyt, prezentację i zapis bufora zdarzeń;
- konfigurację ustawień samego programu;
- obsługę błędów i komunikatów dla użytkownika;
- rejestrowanie podstawowych operacji wykonywanych przez program.

## 3. Środowisko pracy

- System operacyjny: Windows.
- Komunikacja z urządzeniem: RS232, zgodnie z odrębnym dokumentem protokołu komunikacyjnego.
- Szczegółowe wymagania sprzętowe i wersja minimalnego systemu Windows zostaną określone na etapie implementacji.
- Program powinien działać jako aplikacja desktopowa z graficznym interfejsem użytkownika.

## 4. Główna zasada działania

Program stanowi interfejs pomiędzy użytkownikiem, plikiem konfiguracji znajdującym się na komputerze oraz urządzeniem MultiRejestrator.

Podstawowy model działania:

**URZĄDZENIE ↔ PROGRAM ↔ PLIK KONFIGURACJI**

Program powinien umożliwiać niezależne odczytywanie danych z urządzenia, edytowanie ich w programie oraz zapisywanie konfiguracji do pliku. Odczyt lub zapis pliku nie powinien automatycznie powodować zapisu danych do urządzenia.

## 5. Tryby i stany pracy programu

Program powinien rozróżniać co najmniej następujące stany:

| **Stan**               | **Opis**                                                            |
|------------------------|---------------------------------------------------------------------|
| Brak połączenia        | Program działa bez połączenia z MultiRejestratorem.                 |
| Połączenie nawiązane   | Program ma aktywną komunikację z rozpoznanym urządzeniem.           |
| Odczyt danych          | Program pobiera konfigurację, parametry lub zdarzenia z urządzenia. |
| Edycja konfiguracji    | Użytkownik może zmieniać dane konfiguracyjne w formularzu.          |
| Wysyłanie konfiguracji | Program przekazuje przygotowaną konfigurację do urządzenia.         |
| Błąd komunikacji       | Wystąpił problem z komunikacją lub odpowiedzią urządzenia.          |

## 6. Obsługa komunikacji z urządzeniem

### 6.1. Wybór portu COM

- Program powinien prezentować listę dostępnych portów COM.
- Użytkownik powinien mieć możliwość wyboru portu używanego do komunikacji.
- Program powinien zapamiętywać ostatnio używany port.
- Program powinien umożliwiać odświeżenie listy portów COM.

### 6.2. Połączenie

Po wybraniu funkcji „Połącz” program powinien:

1.  otworzyć wybrany port komunikacyjny;
2.  nawiązać komunikację zgodnie z protokołem;
3.  sprawdzić, czy podłączone urządzenie jest obsługiwanym MultiRejestratorem;
4.  odczytać podstawowe informacje identyfikacyjne urządzenia;
5.  poinformować użytkownika o wyniku operacji.

### 6.3. Identyfikacja urządzenia

Po nawiązaniu komunikacji program powinien odczytać i prezentować w dolnej linijce okna:

- numer seryjny – parameter konfiguracyjny Id == 1 z ramki RE_REQ_GET_CONF (kod 0x8011)
- wersję firmware – pola z ramki RE_REQ_GET_FW_DATE (kod 0x8003):
    - “SVNRev” - numer rewizji
    - “Fw Date” - data
    - “Fw Time” - czas kompilacji
- nr wersji protokołu komunikacyjnego – pole “ProtocolVersion” z ramki RE_REQ_GET_PROT_VER (kod 0x8004)

### 6.4. Rozłączenie i utrata komunikacji

- Program powinien posiadać funkcję „Rozłącz”.
- W przypadku utraty komunikacji program powinien zmienić stan na „brak połączenia” lub „błąd komunikacji”.
- Program powinien poinformować użytkownika o utracie komunikacji.
- Operacje wymagające aktywnego połączenia powinny być niedostępne lub odpowiednio blokowane.

## 7. Konfiguracja urządzenia

Parametry konfiguracyjne powinny być prezentowane w głównym oknie programu i pogrupowane w logiczne, tematyczne sekcje. Konkretna lista parametrów oraz ich właściwości będzie określona na podstawie odrębnej specyfikacji parametrów i protokołu komunikacyjnego.

### 7.1. Walidacja parametrów

- Dla parametrów liczbowych powinien być określony dopuszczalny zakres.
- Program powinien sprawdzać poprawność danych przed wysłaniem konfiguracji do urządzenia.
- Program powinien uniemożliwiać lub odpowiednio sygnalizować wprowadzenie wartości niedopuszczalnej.
- Jeżeli pomiędzy parametrami występują zależności, program powinien je kontrolować.

### 7.2. Wartości domyślne

Program powinien umożliwiać utworzenie nowej, pustej/domyślnej konfiguracji urządzenia. Dla parametrów, dla których zostaną określone wartości domyślne, program powinien automatycznie je zastosować.

## 8. Odczyt konfiguracji z urządzenia

- Funkcja „Pobierz dane z urządzenia” powinna odczytywać konfigurację zgodnie z protokołem.
- Po poprawnym odczycie dane powinny zostać przedstawione w odpowiednich polach formularza.
- Odczyt konfiguracji nie powinien automatycznie modyfikować konfiguracji zapisanej wcześniej na dysku.
- Program powinien sygnalizować zakończenie operacji oraz ewentualny błąd.

## 9. Wysyłanie konfiguracji do urządzenia

- Funkcja „Wyślij dane do urządzenia” powinna przekazywać przygotowaną konfigurację do MultiRejestratora.
- Przed wysłaniem program powinien wykonać walidację danych.
- Program powinien jednoznacznie wskazać użytkownikowi urządzenie, do którego dane będą wysyłane, w szczególności jego numer seryjny, jeżeli jest dostępny.
- Operacja zapisu do urządzenia powinna wymagać świadomego potwierdzenia użytkownika.
- Po zakończeniu zapisu program powinien poinformować o wyniku operacji.
- W przypadku błędu program powinien wskazać, że konfiguracja nie została prawidłowo zapisana lub że stan zapisu jest niepewny.

## 10. Porównanie konfiguracji

Program powinien przewidywać możliwość porównania konfiguracji znajdującej się w pamięci programu z konfiguracją odczytaną z urządzenia. Funkcja może być wdrażana jako osobny moduł, jednak architektura programu powinna umożliwiać jej późniejsze dodanie.

- Różnice powinny być prezentowane w sposób jednoznaczny, najlepiej parametr po parametrze.
- Porównanie nie powinno samo w sobie powodować zapisu żadnych danych do urządzenia.

## 11. Pliki konfiguracji

### 11.1. Zapis

- Program powinien umożliwiać zapis aktualnej konfiguracji na dysku.
- Przy pierwszym zapisie program powinien proponować nazwę pliku odpowiadającą numerowi seryjnemu urządzenia, jeżeli numer seryjny jest dostępny.
- Użytkownik powinien mieć możliwość wskazania lokalizacji i zmiany nazwy pliku.

### 11.2. Odczyt

- Program powinien umożliwiać otwarcie wcześniej zapisanej konfiguracji.
- Po otwarciu dane powinny zostać przedstawione w odpowiednich polach programu.
- Otworzenie pliku nie powinno automatycznie wysyłać jego zawartości do urządzenia.

### 11.3. Informacje w pliku

Format pliku powinien umożliwiać zapisanie, w zakresie dostępnym na etapie implementacji:

- konfiguracji urządzenia;
- wersji firmware lub informacji o wersji urządzenia;
- daty utworzenia lub modyfikacji konfiguracji;
- wersji formatu pliku konfiguracji. – proponuję, żeby wyróżnikiem format był numer wersji protokołu komunikacyjnego

## 12. Parametry techniczne i diagnostyka

Program powinien posiadać wydzielony obszar prezentacji parametrów technicznych i diagnostycznych urządzenia. Konkretna lista parametrów będzie wynikała z dokumentacji urządzenia.

- czas rzeczywisty RTC – z możliwością ustawienia
- wersja firmware
- bieżące wartości diagnostyczne udostępniane przez urządzenie:
    - poziom sygnału GSM
    - nr błędu działania modułu komórkowego
    - stan modułu komórkowego
        - wyłączony
        - włączony niezarejestrowany w sieci
        - włączony zarejestrowanyw sieci
    - ICCID
    - IMSI
    - IMEI
    - Pasmo pracy modułu komórkowego
    - Cell ID
    - Czas od ostatniego resetu uP
    - Przyczyna ostatniego resetu uP
    - Napięcie baterii zasilającej przy obciążeniu
    - Źródło zasilania – bateria wewnętrzna / BatteryPack / PowerPack – zasilanie z AKU / PowerPack zasilanie siecowe
    - Napięcie stabilizowane
    - Stan sabotażu (otwarcia pokrywki)
    - Temperatura
- stan wejść/wyjść
- stan komunikacji
- liczniki i inne dane diagnostyczne dostępne w protokole
- obsługa wysyłania raportu “Na żądanie”
- obsługa trybu monitorowania sygnału GSM
- Odczyt zawartości pamięci

## 13. Bufor zdarzeń

Program powinien umożliwiać odczyt, prezentację i zapis na dysku zdarzeń zbuforowanych w urządzeniu.

- Zdarzenia powinny być prezentowane w formie tabeli.
- W miarę dostępności danych tabela powinna zawierać datę/czas, typ lub kod zdarzenia oraz opis.
- Program powinien umożliwiać sortowanie i filtrowanie zdarzeń, jeżeli liczba i charakter danych tego wymagają.
- Program powinien umożliwiać zapis odczytanych zdarzeń do pliku.
- Odczyt bufora zdarzeń nie powinien automatycznie oznaczać jego skasowania.
- Jeżeli protokół umożliwia kasowanie zdarzeń, funkcja kasowania powinna być oddzielona od funkcji odczytu i wymagać potwierdzenia użytkownika.

## 14. Konfiguracja programu

Program powinien posiadać własne ustawienia, niezależne od konfiguracji MultiRejestratora.

- ostatnio używany port COM;
- kod dostępu wykorzystywany przy tworzeniu nowej konfiguracji, zgodnie z przyjętym sposobem zabezpieczenia;
- domyślna lokalizacja plików konfiguracji, jeżeli zostanie przewidziana;
- domyślna lokalizacja zapisu zdarzeń, jeżeli zostanie przewidziana;
- inne ustawienia użytkownika określone na etapie implementacji.

## 15. Kod dostępu i bezpieczeństwo operacji

- Program powinien obsługiwać kod dostępu wymagany przez urządzenie zgodnie z protokołem.
- Operacje zapisu konfiguracji do urządzenia powinny być jednoznacznie potwierdzane przez użytkownika.
- Program powinien minimalizować możliwość wysłania konfiguracji do niewłaściwego urządzenia.
- Szczegółowy mechanizm autoryzacji będzie określony w dokumentacji protokołu.

## 16. Obsługa niezapisanych zmian

Jeżeli użytkownik zmieni konfigurację znajdującą się w pamięci programu, a następnie wykona operację mogącą spowodować utratę tych zmian, program powinien poinformować użytkownika.

- Przykład: „Konfiguracja została zmieniona. Czy zapisać zmiany?”
- Program powinien rozróżniać konfigurację zapisaną na dysku od konfiguracji aktualnie edytowanej.

## 17. Obsługa błędów i komunikaty

Program powinien prezentować jednoznaczne komunikaty w przypadku co najmniej:

- braku wybranego portu COM;
- braku możliwości otwarcia portu;
- braku odpowiedzi urządzenia;
- utraty komunikacji;
- podłączenia nieobsługiwanego urządzenia;
- błędnego kodu dostępu;
- nieprawidłowych danych konfiguracji;
- błędu odczytu lub zapisu konfiguracji;
- błędu odczytu lub zapisu pliku.

Komunikaty powinny, w miarę możliwości, informować użytkownika o przyczynie problemu oraz o możliwym dalszym działaniu.

## 18. Rejestr operacji programu

Program powinien posiadać mechanizm wewnętrznego logowania istotnych operacji, przydatny do diagnostyki problemów.

- pełne logowanie zawartości pakietów w czasie komunikacji
- błędy operacji na plikach.

Zakres, sposób przechowywania oraz dostęp użytkownika do logu zostaną określone na etapie implementacji.

## 19. Interfejs użytkownika

Główne okno programu powinno być podzielone na pasek funkcji, obszar informacji o urządzeniu, obszar konfiguracji oraz pasek statusu.

W górnej części okna powinny znajdować się funkcje:

- Nowa konfiguracja
- Otwórz konfigurację z dysku
- Zapisz konfigurację na dysku
- Pobierz dane z urządzenia
- Wyślij dane do urządzenia
- Połącz
- Rozłącz
- Parametry techniczne / diagnostyka
- Bufor zdarzeń

Główna część okna:

- pola parametrów konfiguracyjnych pogrupowane w tematyczne sekcje;
- obszar informacji identyfikacyjnych urządzenia;
- ewentualny obszar diagnostyki lub zakładki dla funkcji dodatkowych.

Dolna część okna powinna prezentować co najmniej:

- aktualny port COM
- stan połączenia
- numer seryjny
- wersję firmware
- nr wersji protokołu komunikacyjnego

## 20. Wersjonowanie i kompatybilność

- Program powinien posiadać własny numer wersji.
- Format pliku konfiguracji powinien posiadać informację o wersji formatu.
- Program powinien rozpoznawać, jeżeli konfiguracja pochodzi z innej wersji urządzenia lub innego formatu.
- Nie należy automatycznie wysyłać konfiguracji do urządzenia, jeżeli program nie może potwierdzić jej zgodności z obsługiwanym urządzeniem.

## 21. Podział dokumentacji projektowej

Niniejszy dokument opisuje funkcjonalne założenia programu. Szczegóły techniczne powinny zostać rozdzielone na odrębne dokumenty:

| **Dokument**                                                        | **Zakres**                                                                                                 |
|---------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------|
| Założenia programu MultiRejestrator                                 | Co program ma umożliwiać i jak ma zachowywać się wobec użytkownika.                                        |
| Protokół komunikacyjny ze specyfikacja parametrów MultiRejestratora | Sposób wymiany danych pomiędzy programem a urządzeniem, lista parametrów, typy, zakresy, wartości domyślne |
| Lista zdarzeń                                                       | Kody, typy i opisy zdarzeń dostępnych w buforze urządzenia.                                                |

## 22. Elementy do doprecyzowania w kolejnych etapach

Przed rozpoczęciem implementacji należy doprecyzować co najmniej:

- wartości domyślne;
- zależności pomiędzy parametrami;
- format pliku konfiguracji;
- listę parametrów diagnostycznych;
- format i sposób eksportu bufora zdarzeń;
- minimalną wersję systemu Windows;
- ostateczny układ interfejsu użytkownika.

## 23. Uwagi końcowe

Niniejszy dokument stanowi funkcjonalne założenia programu MultiRejestrator i powinien być traktowany jako dokument nadrzędny dla wykonawcy aplikacji w zakresie zachowania programu i jego podstawowych funkcji. Szczegółowe rozwiązania techniczne, format danych oraz sposób komunikacji z urządzeniem powinny wynikać z odrębnej dokumentacji.

Lista parametrów konfiguracji oraz szczegółowe właściwości pól nie są w tym dokumencie powielane, ponieważ powinny zostać określone w odrębnej specyfikacji parametrów i protokole komunikacyjnym.
