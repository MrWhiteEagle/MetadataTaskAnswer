# Indeks zmian
> Repozytorium podzielone jest na 2 gałęzie - ***master***, ***original*** i ***unit-tests***
> - Zaproponowane zmiany  --> master
> - Oryginał dla porónania --> original
> - Testy jednostkowe --> unit-tests

Miłej lektury!
## Projekt "Import"
> Zdecydowana większość zmian dokonanych w tym projekcie to komentarze z propozycjami zmian.
> By zastosować się do warunków zadania w poleceniu postanowiłem zachować funkcjonalności w plikach niepodlegających zadaniu.
### Program.cs
 - Metody odpowiadające za "fetchowanie" danych powinny być wykonywane asynchronicznie by nie blokować wątku, a ich sygraturą powinno być **Task**. Tak samo z metodą *main*.
 - Pętla ***RetryUntilSuccess()*** powinna obsługiwać błędy związane z połączeniem lub błędnymi danymi logowania, zamiast zapętlać się w nieskończoność. 
 > Proponowane zmiany znajdują się w komentarzach w kodzie.

### FiveTranConnectionSupport.cs
- W metodzie ***GetConnectionDetailsForSelection()*** Pobierane są klucze i sekrety API za pomocą konsoli. Jest to możliwie niebezpieczna praktyka. Jednak jest to kwestia bezpieczeństwa danych, dlatego tylko o tym wspominam. Zamiast tego możnaby wykorzystać zaszyfrowany plik, zmienną środowiskową lub algorytm oszukujący keyloggery.
- Metoda ***SelectToImport()*** pobiera dane za pomocą *RESTApi* i *HttpClient*, zatem powinna być asynchroniczna. Podczas pobierania wyników możnaby wyświetlić komunikat o postępach. Proponowane rozwiązanie znajduje się w regionie ***Poprawki*** z dopieskiem ***new***.
> Tutaj poraz pierwszy spotkałem się z konkatenacją *string* podpisaną jako bufor, by zwiększyć wydajność. W praktyce nie zmienia to wydajności ponieważ obiekty *string* w C# są niemutowalne, co oznacza tworzenie nowego obiektu w pamięci przy każdej operacji przypisania. W proponowanym rozwiązaniu zamieniłem to na *StringBuilder*.
- Metoda ***RunImport*** ponownie pobiera dane, więc też powinna być asynchroniczna. Metoda implementuje równoległe wywołania na wątkach za pomocą *Parallel.ForEach()*. Ze względu na operacje I/O a nie zależne od CPU jest to dość ograniczające, dlatego w proponowanym rozwiązaniu (region ***Poprawki*** z dopieskiem ***new***) zaimplementowałem tworzenie osobnego zadania dla każdego connectora. Jednoczesne z pobieraniem konektorów. W efekcie uzyskujemy efekt "fabryki" gdzie każdy connector zaczyna wyszukiwać swoje schematy zaraz jak tylko zostanie znaleziony, podczas gdy reszta dalej jest pobierana. Liczbę jednoczesnych wywołań ograniczamy za pomocą *SemaphoreSlim*. Pozwala to dostosować liczbę jednoczesnych zapytań HTTP do możliwości API, a nie liczby wątków procesora. Oczekujemy na zakończenie wszystkioch zadań, a każde z nich ma swój własny bufor, który na koniec wykonania dodaje swoją zawartość do *ConcurrentQueue*, która jest bezpieczna w użyciu wielowątkowym.

### RestApiManagerWrapper.cs
Uznałem że jako że ta klasa ma WYŁĄCZNIE jedno użycie jest ona raczej zbędna - choć nie znam prawdziwej i pełnej struktury kodu i kontekstu użycia. W przypadku z zadania lepiej utworzyć strukturę, lub przekazywać dane logowania API prosto do metody ***RunImport()*** a klienta RestApi tworzyć tam.

### IConnectionSupport.cs
Tak jak poprzednio, aby zastosować zaproponowane zmiany należałoby zmienić sygnatury metod interfejsu. Zachowany został oryginał.

## Projekt FiveTranClient

> W tym projekcie nie znajdowało się w moim odczuciu wiele do poprawy, a większość zmian skupia się raczej na dobrych (według mnie) praktykach w kodzie.

### Fetchery
Jako że zarówno ***PaginatedFetcher*** i ***NonPaginatedFetcher*** dziedziczą po ***BaseFetcher*** metody służące pobieraniu i deserializacji danych w obu przypadkach zostały oparte o metodę zaimplementowaną w klasie bazowej. Dane wyjściowe nie ulegają zmianie. 

### Modele

Większość modeli używa typów jako *non-nullable*. Dodano *"?"* by to zmienić. Nie ma to zastosowania praktycznego, a większość implementacji już sprawdza je w przypadku wartości null.  Po prostu mniej ostrzeżeń w VS.

### HttpRequestHandler.cs
- Metoda ***GetAsync()*** ma sygnaturę *Task*, lecz nie korzysta z asynchroniczności, poprawiłem to zapisując rezultat funkcji ***_GetASync()*** w zmiennej i przekazując go do ***GetOrAdd()***
- Metoda ***_GetAsync()*** korzysta z *.EnsureSuccessStatusCode()* zanim dojdzie do sprawdzenia statusu zapytania (nawet tego już zaimplementowanego w oryginale). Właśnie tutaj powinny być zaimplementowane inne reakcje programu na błędne statusy jak Unauthorized, i być obsługiwane w pętli ***RetryUntilSuccess()*** w *Program.cs*. Przeniesiono to poniżej bloku *if*, jak i zaimplementowano dodatkowy check sprawdzający błąd 401 - Unauthorized.

### RestApiManager.cs
Ależ tam jest konstruktorów!
Czy jeśli w live-code i prawdziwym przypadku użycia nie korzystamy z innych konstruktorów i sposobów inicjalizacji obiektu a tylko tego w zadaniu, nie lepiej zachować jeden lub dwa konstruktory?

## Projekt UnitTests
> W tym projekcie skupiłem się głównie na testowaniu metod z klasy ***FivetranClientSupport***
- W klasie ***FiveTranConnectionSupportTests*** zostały zaimplementowane testy jednostkowe za pomocą xUnit i Moq. Klasa zawiera statyczne metody i zmienne mające usprawnić proces testowania.
- Metody statyczne odpowiadają sygnaturą metodom z **RestApiManager** aby symulować wynik ich działania.
- Dodatkowo w tej gałęzi razem z projektem zostały zaimplementowane interfejsy **IConsoleIO** oraz **IRestApiManager** by łatwo móc mockować odpowiadające im klasy za pomocą *Moq*.
### IConsoleIO
Standardowe wykorzystanie metod z **Console**.
Dodatkowo w klasie **ConsoleIO** używanej zamiast **Console** zaimplementowano mechanizm chroniący przed wyjątkami w przypadku użycia metody *Clear()* sprawdzając czy wyjście zostało przejęte.
### IRestApiManager
Interfejs deklarujący metody użyte w klasie **RestApiManager**
### RunImport

Kilka słów o metodzie **RunImport**:
- Stara metoda zwraca niepewny i niedokładny wynik (czego się spodziewałem i opisałem wyżej), w wyniku często brakuje wszystkich connectorów lub schematów, czasem jednak wynik testu jest pozytywny (zdecydowanie jednak częściej - nie)
- W nowej metodzie (**RunImportNew**) korzystającej z asynchroniczności w pełni i nieblokującej wątku w żaden sposób, wyniki były takie same za każdym razem co zostało sprawdzone w obu przypadkach przechwytywaniem wyjścia konsoli za pomocą użycia **ITestOutputHelper** z *xUnit*.
