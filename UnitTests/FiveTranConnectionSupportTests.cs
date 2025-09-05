namespace UnitTests;

using FivetranClient;
using FivetranClient.Models;
using Import.ConnectionSupport;
using Import.Helpers.Fivetran;
using Moq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class FiveTranConnectionSupportTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    public static async IAsyncEnumerable<Group> GetTestGroups()
    {
        yield return new Group { Id = "group1", Name = "Group 1" };
        await Task.Yield(); // <- Może być zamienione na Task.Delay() w przypadku symulowania oczekiwania na dane
        yield return new Group { Id = "group2", Name = "Group 2" };
        await Task.Yield();
        yield return new Group { Id = "group3", Name = "Group 3" };
    }

    // Przykładowe Connectory o indentycznej sygnaturze co w RestApiManager
    public static async IAsyncEnumerable<Connector> GetTestConnectors()
    {
        yield return new Connector
        {
            Id = "con1",
            Paused = true,
            Schema = "con1schema",
            Service = "con1service"
        };
        await Task.Yield();
        yield return new Connector
        {
            Id = "con2",
            Paused = true,
            Schema = "con2schema",
            Service = "con2service"
        };
        await Task.Yield();
        yield return new Connector
        {
            Id = "con3",
            Paused = true,
            Schema = "con3schema",
            Service = "con3service"
        };
    }
    // Przykładowe schematy o indentycznej sygnaturze co Metoda z RestApiManager
    public static async Task<DataSchemas?> GetTestSchemas()
    {
        await Task.Delay(1);
        return new DataSchemas
        {
            Schemas = new Dictionary<string, Schema?>
            {
                {"schema1",
                new Schema
                {
                    NameInDestination = "schema1dest",
                    Tables = new Dictionary<string, Table>
                    {
                        { "S1table1", new Table { NameInDestination = "table1" } },
                        { "S1table2", new Table { NameInDestination = "table2" } }
                    }
                } },
                {"schema2",
                new Schema
                {
                    NameInDestination = "schema2dest",
                    Tables = new Dictionary<string, Table>
                    {
                        { "S2table1", new Table { NameInDestination = "table1" } },
                        { "S2table2", new Table { NameInDestination = "table2" } }
                    }
                } }
            }
        };
    }
    // Stałe testowe
    private static readonly string testkey = "test_key";
    private static readonly string testsecret = "test_secret";
    private static readonly string testconnectorid = "test_connector_id";
    private static readonly string testgroupid = "test_group_id";

    // Test metody GetConnectionDetailsForSelection
    [Fact]
    public void GetConnectionDetailsForSelection_ReturnsFivetranDetails()
    {
        // Setup
        var outputbuffer = new StringBuilder();
        var consolemock = CreateConsoleMock(outputbuffer);
        consolemock.SetupSequence(c => c.ReadLine())
            .Returns(testkey)
            .Returns(testsecret);

        // Inicjalizacja FivetranConnectionSupport z zamockowanym IConsoleIO
        var support = new FivetranConnectionSupport(console: consolemock.Object);

        // Wywołanie
        var details = support.GetConnectionDetailsForSelection();

        _output.WriteLine(outputbuffer.ToString());

        // Wyniki
        var result = Assert.IsType<FivetranConnectionSupport.FivetranConnectionDetailsForSelection>(details);
        Assert.Equal(testkey, result.ApiKey);
        Assert.Equal(testsecret, result.ApiSecret);
    }

    // Test metody GetConnection
    [Fact]
    public void GetConnection_ReturnsRestApiManagerWrapperWithGroupID()
    {
        // Setup
        var support = new FivetranConnectionSupport();
        var details = new FivetranConnectionSupport.FivetranConnectionDetailsForSelection(testkey, testsecret);

        // Wywołanie
        var connection = support.GetConnection(details, testconnectorid);

        // Wyniki
        Assert.NotNull(connection);
        var result = Assert.IsType<RestApiManagerWrapper>(connection);
        Assert.Equal(result.GroupId, testconnectorid);
        Assert.NotNull(result.RestApiManager);
        var manager = Assert.IsType<RestApiManager>(result.RestApiManager);
    }

    // Test metody SelectToImport
    [Theory]
    [InlineData("1")]
    public async Task SelectToImport_ReturnsGroupIdAsString(string pickedNo)
    {
        // Setup
        var outputbuffer = new StringBuilder();
        var managerMock = new Mock<IRestApiManager>();
        var consoleMock = CreateConsoleMock(outputbuffer);
        managerMock.Setup(m => m.GetGroupsAsync(It.IsAny<CancellationToken>())).Returns(GetTestGroups());
        consoleMock.Setup(c => c.ReadLine()).Returns(pickedNo);

        // Wywołanie
        var support = new FivetranConnectionSupport(managerMock.Object, consoleMock.Object);
        var details = new FivetranConnectionSupport.FivetranConnectionDetailsForSelection(testkey, testsecret);

        // Tworzenie wyniku porównawczego (od razu wyciągamy grupę)
        var expected = Task.Run(async () =>
        {
            var groups = new List<Group>();
            await foreach (var group in GetTestGroups())
                groups.Add(group);
            return groups[int.Parse(pickedNo) - 1].Id;
        });
        var result = support.SelectToImport(details);

        _output.WriteLine(outputbuffer.ToString());

        // Wyniki
        Assert.IsType<string>(result);
        Assert.Equal(result, await expected);
    }

    // Test metody RunImport (starej) - zwykle zwraca błędny wynik, ale czasem dobry - dowód na to że moja implementacja jest poprawna
    [Fact]
    public async Task RunImport_WritesMappingsToConsole()
    {
        //Setup
        var outputbuffer = new StringBuilder();
        var managerMock = new Mock<IRestApiManager>();
        var wrapperMock = new RestApiManagerWrapper(managerMock.Object, testgroupid);
        var consoleMock = CreateConsoleMock(outputbuffer);

        managerMock.Setup(m => m.GetConnectorsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(GetTestConnectors());
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con1", It.IsAny<CancellationToken>()))
            .Returns(() => GetTestSchemas());
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con2", It.IsAny<CancellationToken>()))
            .Returns(() => GetTestSchemas());
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con3", It.IsAny<CancellationToken>()))
            .Returns(() => GetTestSchemas());

        var support = new FivetranConnectionSupport(console: consoleMock.Object);

        // Wywołanie
        support.RunImport(wrapperMock);
        var output = outputbuffer.ToString();

        _output.WriteLine(output); // <-- Wypisanie przechwyconego wyjścia do testowego wyjścia

        // Wuniki
        Assert.True(!string.IsNullOrEmpty(output)); // <-- mamy wyjście
        Assert.Contains("Lineage mappings:", output); // <-- mamy nagłówek
        Assert.True(await CheckImportOutput(output)); // <-- mamy wszystkie oczekiwane mapowania
    }

    // Test metody RunImportNew - asynchronicznej wersji RunImport
    [Fact]
    public async Task RunImportNew_WritesMappingsToConsole()
    {
        //Setup
        var outputbuffer = new StringBuilder();
        var managerMock = new Mock<IRestApiManager>();
        var consoleMock = CreateConsoleMock(outputbuffer);
        var wrapperMock = new RestApiManagerWrapper(managerMock.Object, testgroupid);

        managerMock.Setup(m => m.GetConnectorsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(GetTestConnectors());
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con1", It.IsAny<CancellationToken>()))
            .Returns(() => GetTestSchemas());
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con2", It.IsAny<CancellationToken>()))
            .Returns(() => GetTestSchemas());
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con3", It.IsAny<CancellationToken>()))
            .Returns(() => GetTestSchemas());

        var support = new FivetranConnectionSupport(console: consoleMock.Object);

        // Wywołanie
        await support.RunImportNew(wrapperMock);

        var output = outputbuffer.ToString();
        _output.WriteLine(output);

        // Wyniki
        Assert.True(!string.IsNullOrEmpty(output)); // <-- mamy wyjście
        Assert.Contains("Lineage mappings:", output); // <-- mamy nagłówek
        Assert.True(await CheckImportOutput(output)); // <-- mamy wszystkie oczekiwane mapowania
    }


    // Metoda dodatkowa do sprawdzenie czy w wyjściu konsoli znajdują się oczekiwane mapowania
    private async Task<bool> CheckImportOutput(string output)
    {
        List<string> expectedMappings = new();
        var schemas = await GetTestSchemas();
        await foreach (var connector in GetTestConnectors()) // <-- Tworzymy każdą możliwą kombinację i sprawdzamy czy została zwrócona (charakterystyka testu)
        {
            foreach (var schema in schemas!.Schemas!)
            {
                foreach (var table in schema.Value!.Tables!)
                {
                    var mapping = $"{connector.Id}: {schema.Key}.{table.Key} -> {schema.Value.NameInDestination}.{table.Value.NameInDestination}";
                    expectedMappings.Add(mapping);
                }
            }
        }

        for (int i = expectedMappings.Count - 1; i >= 0; i--)
        {
            if (output.Contains(expectedMappings[i]))
            {
                expectedMappings.RemoveAt(i);
            }
        }

        return expectedMappings.Count == 0;

    }

    private static Mock<IConsoleIO> CreateConsoleMock(StringBuilder outputbuffer) // <-- Pomocnicza metoda do tworzenia zamockowanego IConsoleIO z przechwytywaniem wyjścia
    {
        var consoleMock = new Mock<IConsoleIO>();
        consoleMock.Setup(c => c.WriteLine(It.IsAny<string>()))
            .Callback<string>(msg => outputbuffer.AppendLine(msg));
        consoleMock.Setup(c => c.Write(It.IsAny<string>()))
            .Callback<string>(msg => outputbuffer.Append(msg));
        consoleMock.Setup(c => c.Clear())
            .Callback(() => outputbuffer.AppendLine("--- Console cleared ---"));

        return consoleMock;
    }
}
