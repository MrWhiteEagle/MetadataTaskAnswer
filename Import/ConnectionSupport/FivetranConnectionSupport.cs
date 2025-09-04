using FivetranClient;
using FivetranClient.Models;
using Import.Helpers.Fivetran;
using System.Collections.Concurrent;
using System.Text;

namespace Import.ConnectionSupport;

// equivalent of database is group in Fivetran terminology
public class FivetranConnectionSupport : IConnectionSupport
{
    private readonly IConsoleIO _consoleIO;
    private readonly IRestApiManager? _restApiManager;
    public const string ConnectorTypeCode = "FIVETRAN";
    internal record FivetranConnectionDetailsForSelection(string ApiKey, string ApiSecret);

    // Używanie opcjonalnych restapimanager i konsoli w przypadku użycia testów jednostkowych
    public FivetranConnectionSupport(IRestApiManager? restApiManager = null, IConsoleIO? console = null)
    {
        this._consoleIO = console ?? new ConsoleIO();
        // restapimanager może mieć null - w metodach i tak jest on tworzony i disposowany przez "using"
        this._restApiManager = restApiManager;
    }



    public object? GetConnectionDetailsForSelection()
    {
        _consoleIO.Write("Provide your Fivetran API Key: ");
        var apiKey = _consoleIO.ReadLine() ?? throw new ArgumentNullException();
        _consoleIO.Write("Provide your Fivetran API Secret: ");
        var apiSecret = _consoleIO.ReadLine() ?? throw new ArgumentNullException();

        return new FivetranConnectionDetailsForSelection(apiKey, apiSecret);
    }

    public object GetConnection(object? connectionDetails, string? selectedToImport)
    {
        if (connectionDetails is not FivetranConnectionDetailsForSelection details)
        {
            throw new ArgumentException("Invalid connection details provided.");
        }

        // DB Ta klasa ma jedno użycie, na pewno jest potrzebna?
        // Jeśli planowana jest dodatkowa funkjonalność to ok, ale w obecnym stanie lepiej albo zignorować tą metodę i przejść od razu do RunImport
        // Albo stworzyć structurę która przechowuje apiKey, Secret i selectedToImport i korzystać z niej.
        return new RestApiManagerWrapper(
            new RestApiManager(
                details.ApiKey,
                details.ApiSecret,
                TimeSpan.FromSeconds(40)),
            selectedToImport ?? throw new ArgumentNullException($"Selected group was null: {nameof(selectedToImport)}"));
        // Na razie zostawię, ale możnaby całkowicie pominąć tą metodę i używać RestApiManager bezpośrednio.
        // Skoro w Program.cs mamy już dostęp do apiKey i apiSecret w connectionDetails to możnaby do
        // RunImport przekazać je bezpośrednio i tam stworzyć RestApiManager.
        // Dodatkowo nie rozumiem po co tworzyć jednorazowy RestApiManager w metodzie SelectToImport skoro i tak tworzymy go ponownie tutaj?
    }

    public void CloseConnection(object? connection)
    {
        switch (connection)
        {
            case RestApiManager restApiManager:
                restApiManager.Dispose();
                break;
            case RestApiManagerWrapper restApiManagerWrapper:
                restApiManagerWrapper.Dispose();
                break;
            default:
                throw new ArgumentException("Invalid connection type provided.");
        }
    }

    public string SelectToImport(object? connectionDetails)
    {
        if (connectionDetails is not FivetranConnectionDetailsForSelection details)
        {
            throw new ArgumentException("Invalid connection details provided.");
        }
        // Sprawdzamy czy jest dostępny RestrApiManager z testu, jeśli nie - tworzymy nowy.
        using var restApiManager = _restApiManager ?? new RestApiManager(details.ApiKey, details.ApiSecret, TimeSpan.FromSeconds(40));
        var groups = restApiManager
            .GetGroupsAsync(CancellationToken.None)
            .ToBlockingEnumerable();
        if (!groups.Any())
        {
            throw new Exception("No groups found in Fivetran account.");
        }

        // bufforing for performance
        var consoleOutputBuffer = "";
        consoleOutputBuffer += "Available groups in Fivetran account:\n";
        var elementIndex = 1;
        foreach (var group in groups)
        {
            consoleOutputBuffer += $"{elementIndex++}. {group.Name} (ID: {group.Id})\n";
        }
        consoleOutputBuffer += "Please select a group to import from (by number): ";
        _consoleIO.Write(consoleOutputBuffer);
        var input = _consoleIO.ReadLine();
        if (string.IsNullOrWhiteSpace(input)
            || !int.TryParse(input, out var selectedIndex)
            || selectedIndex < 1
            || selectedIndex > groups.Count())
        {
            throw new ArgumentException("Invalid group selection.");
        }

        var selectedGroup = groups.ElementAt(selectedIndex - 1);
        return selectedGroup.Id ?? throw new ArgumentNullException("Selected group was null or it's id was null.");
    }



    public void RunImport(object? connection)
    {
        if (connection is not RestApiManagerWrapper restApiManagerWrapper)
        {
            throw new ArgumentException("Invalid connection type provided.");
        }

        var restApiManager = _restApiManager ?? restApiManagerWrapper.RestApiManager;
        var groupId = restApiManagerWrapper.GroupId;

        var connectors = restApiManager
            .GetConnectorsAsync(groupId, CancellationToken.None)
            .ToBlockingEnumerable();
        if (!connectors.Any())
        {
            throw new Exception("No connectors found in the selected group.");
        }

        var allMappingsBuffer = "Lineage mappings:\n";
        Parallel.ForEach(connectors, connector =>
        {
            var connectorSchemas = restApiManager
                .GetConnectorSchemasAsync(connector.Id, CancellationToken.None)
                .Result;

            foreach (var schema in connectorSchemas?.Schemas ?? [])
            {
                foreach (var table in schema.Value?.Tables ?? [])
                {
                    allMappingsBuffer += $"  {connector.Id}: {schema.Key}.{table.Key} -> {schema.Value?.NameInDestination}.{table.Value.NameInDestination}\n";
                }
            }
        });

        _consoleIO.WriteLine(allMappingsBuffer);
    }

    #region Poprawki
    // DB - Ta metoda powinna być asynchroniczna by nie blokować wątku
    public async Task<string> SelectToImportNew(object? connectionDetails)
    {
        // Db - nie lepiej zmienić sygnatury z object? na konkretny typ? Skoro chcemy tylko 1 konkretny typ obiektu, to lepiej to wymusić.
        if (connectionDetails is not FivetranConnectionDetailsForSelection details)
        {
            throw new ArgumentException("Invalid connection details provided.");
        }
        using IRestApiManager restApiManager = new RestApiManager(details.ApiKey, details.ApiSecret, TimeSpan.FromSeconds(40));

        Console.WriteLine("Fetching groups, please wait...\n");

        // DB - Tymczasowy mock, bo nie mam dostępu do API
        //var groups = new List<FivetranClient.Models.Group>
        //{
        //    new FivetranClient.Models.Group { Id = "g1", Name = "group 1" },
        //    new FivetranClient.Models.Group { Id = "g2", Name = "group 2" },
        //};
        // Dla efektu przy testach bez API :)
        //await Task.Delay(2000);


        // DB - w produkcji raczej lepiej nie blokować wątku, asynchronicznie - to asynchronicznie.
        // Zapisujemy groups jako "blueprint" listy, a potem używamy go w pętli asynchronicznie zapisując każdy element do listy w pamięci, bez zatrzymywania wątku.
        var groups = restApiManager.GetGroupsAsync(CancellationToken.None);
        Console.Clear();

        // bufforing for performance
        // DB - Zmiana stringu przez konkatenację nie wpływa na wydajność, string w C# jest niemutowalny, każda zmiana tworzy nowy obiekt w pamięci, lepszy stringbuilder.
        //var consoleOutputBuffer = "";
        //consoleOutputBuffer += "Available groups in Fivetran account:\n";
        var consoleOutputBuffer = new StringBuilder();
        var fetched = new List<Group>();
        consoleOutputBuffer.AppendLine("Available groups in Fivetran account:");
        var elementIndex = 1;
        await foreach (var group in groups)
        {
            consoleOutputBuffer.AppendLine($"{elementIndex++}. {group.Name} (ID: {group.Id})");
            fetched.Add(group);
        }
        consoleOutputBuffer.AppendLine("Please select a group to import from (by number): ");
        Console.Write(consoleOutputBuffer.ToString());
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)
            || !int.TryParse(input, out var selectedIndex)
            || selectedIndex < 1
            || selectedIndex > fetched.Count())
        {
            throw new ArgumentException("Invalid group selection.");
        }

        // DB - niepotrzebna zmienna.
        //var selectedGroup = fetched.ElementAt(selectedIndex - 1);
        return (fetched.ElementAt(selectedIndex - 1).Id ?? throw new ArgumentNullException("Selected group was null or it's id was null."));
    }

    // DB - To też powinno być asynchroniczne, mamy operacje IO, nie blokujemy wątków, zamiast tego można wyświetlać info o postępie/statusie.
    public async Task RunImportNew(object? connection)
    {
        if (connection is not RestApiManagerWrapper restApiManagerWrapper)
        {
            throw new ArgumentException("Invalid connection type provided.");
        }

        var restApiManager = restApiManagerWrapper.RestApiManager;
        var groupId = restApiManagerWrapper.GroupId;

        #region Old code
        // DB - Ponownie, konkatenacja stringów w pętli - ponownie StringBuilder
        // Używanie Parallel.ForEach miałoby sens gdyby wykonywać zadania zależne od CPU, a nie IO/HTTP.
        // Dodatkowo konkatenacja stringów w pętli wielowątkowej może prowadzić do problemów z synchronizacją/korupcji.

        //var allMappingsBuffer = "Lineage mappings:\n";
        //Parallel.ForEach(connectors, connector =>
        //{
        //    //var connectorSchemas = restApiManager
        //    //    .GetConnectorSchemasAsync(connector.Id, CancellationToken.None)
        //    //    .Result;

        //    foreach (var schema in connectorSchemas?.Schemas ?? [])
        //    {
        //        foreach (var table in schema.Value?.Tables ?? [])
        //        {
        //            allMappingsBuffer += $"  {connector.Id}: {schema.Key}.{table.Key} -> {schema.Value?.NameInDestination}.{table.Value.NameInDestination}\n";
        //        }
        //    }
        //});
        #endregion

        // DB - Żeby zapytania Http faktycznie działały równolegle, możemy zlistować każde wywołanie jako Task i odpalić je wszystkie naraz.
        // ALE wywołanie XXXXXX zadań na raz to słaby pomysł przy takiej ilości danych.
        // Używamy semaphoreslim do ograniczania liczby jednoczesnych zapytań. Może to być wolniejsze, ale jest bezpieczniejsze
        // i nie ogranicza ilości jednoczesnych zapytań do wątków CPU, a raczej do limitów API i karty sieciowej.
        // Używamy ConcurrentQueue do bezpiecznego zapisywania outputu z wielu zadań jednocześnie.
        // Każde zadanie ma własny bufor do którego zapisuje wyniki, a po wykonaniu się zadanie dodaje swoje wyniki do bufora głównego.

        //DB - Zapisuję przyszły fetched jako "przepis" na listę.
        var fetched = restApiManager
            .GetConnectorsAsync(groupId, CancellationToken.None);

        var semaphore = new SemaphoreSlim(5);
        ConcurrentQueue<string> allMappingsBuffer = new();
        allMappingsBuffer.Enqueue("Lineage mappings:\n");
        var tasks = new List<Task>();
        _consoleIO.Clear();
        // Jako że użycie (jak poprzednio) .ToBlockingEnumerable() blokuje wątek - wolę asynchronicznie.
        _consoleIO.WriteLine("Fetching connectors, please wait...\n"); // <-- Przynajmniej można coś pokazać.
        // await foreach zwraca elementy sekwencyjnie, więc tutaj nic nie przyśpieszę, ale uważam że tak jest lepiej.
        await foreach (var connector in fetched)
        {
            // Dla każdego connectora tworzymy Task który pobiera schematy.
            // Task.Run uruchamia zadania od razu gdy się pojawią, więc nie musimy czekać na koniec pętli.
            // Schematy są pobierane jak tylko pojawi się connector, nie czekamy na koniec pętli.
            tasks.Add(Task.Run(async () =>
            {
                StringBuilder connectorBuffer = new();
                try
                {
                    // SemaphoreSlim ogranicza jednoczesne zapytania do 5. Można to dostosować do API.
                    await semaphore.WaitAsync();
                    var schemas = await restApiManager.GetConnectorSchemasAsync(connector.Id, CancellationToken.None);
                    foreach (var schema in schemas?.Schemas ?? [])
                    {
                        foreach (var table in schema.Value?.Tables ?? [])
                        {
                            connectorBuffer.AppendLine($"  {connector.Id}: {schema.Key}.{table.Key} -> {schema.Value?.NameInDestination}.{table.Value.NameInDestination}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    connectorBuffer.AppendLine($"  {connector.Id}: Error fetching schemas - {ex.Message}");
                }
                finally
                {
                    allMappingsBuffer.Enqueue(connectorBuffer.ToString());
                    // Po wykonaniu zadania (niezależnie od wyniku) zwalniamy semaphore dla następnego Taska.
                    semaphore.Release();
                }
            }));
        }
        _consoleIO.WriteLine($"Found {tasks.Count} connectors.");
        _consoleIO.WriteLine("Fetching schemas for connectors, please wait...\n");
        // Czekamy na ukończenie wszytkich mapowań.
        await Task.WhenAll(tasks);
        _consoleIO.Clear();

        foreach (var buffer in allMappingsBuffer)
        {
            _consoleIO.Write(buffer);
        }
    }
    #endregion

    // Głowiłem się czy jednak nagiąć zasadę nie zmieniania kodu w Program.cs który mimo wszystko działa, ale uznałem że po prostu napiszę swoje propozycje metod osobno.
    // Swoje uwagi zapisałem w komentarzach.
}