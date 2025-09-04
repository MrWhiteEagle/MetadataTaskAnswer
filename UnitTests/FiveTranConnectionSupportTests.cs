namespace UnitTests;

using FivetranClient;
using FivetranClient.Models;
using Import.ConnectionSupport;
using Import.Helpers.Fivetran;
using Moq;
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
        var consolemock = new Mock<IConsoleIO>();
        consolemock.SetupSequence(c => c.ReadLine())
            .Returns(testkey)
            .Returns(testsecret);

        // Inicjalizacja FivetranConnectionSupport z zamockowanym IConsoleIO
        var support = new FivetranConnectionSupport(console: consolemock.Object);

        // Wywołanie
        var details = support.GetConnectionDetailsForSelection();

        // Wyniki
        Assert.NotNull(details);
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
    public void SelectToImport_ReturnsGroupIdAsString(string pickedNo)
    {
        // Setup
        var managerMock = new Mock<IRestApiManager>();
        var consoleMock = new Mock<IConsoleIO>();
        managerMock.Setup(m => m.GetGroupsAsync(CancellationToken.None)).Returns(GetTestGroups());
        consoleMock.Setup(c => c.ReadLine()).Returns(pickedNo);

        // Wywołanie
        var support = new FivetranConnectionSupport(managerMock.Object, consoleMock.Object);
        var details = new FivetranConnectionSupport.FivetranConnectionDetailsForSelection(testkey, testsecret);

        // Pobieranie listy testowych grup do porównania
        var testlist = GetTestGroups().ToBlockingEnumerable().ToList();
        var result = support.SelectToImport(details);

        Assert.NotNull(result);
        Assert.IsType<string>(result);
        Assert.Equal(result, testlist[int.Parse(pickedNo) - 1].Id);
    }

    // Test metody RunImport (starej) - zwykle zwraca błędny wynik, ale czasem dobry - dowód na to że moja implementacja jest poprawna
    [Fact]
    public void RunImport_WritesToConsole()
    {
        //Setup
        var managerMock = new Mock<IRestApiManager>();
        var wrapperMock = new RestApiManagerWrapper(managerMock.Object, testgroupid);
        managerMock.Setup(m => m.GetConnectorsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(GetTestConnectors());

        // Mock schematów dla każdego connectora
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetTestSchemas().Result);
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetTestSchemas().Result);
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetTestSchemas().Result);

        var support = new FivetranConnectionSupport();
        // Tymczasowe przekierowanie Console.Out do StringWriter w celu ewaluacji wyjścia
        var writer = new StringWriter();
        Console.SetOut(writer);
        support.RunImport(wrapperMock);
        var output = writer.ToString();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true }); // Przywrócenie standardowego wyjścia konsoli
        _output.WriteLine(output); // Wypisanie przechwyconego wyjścia do testowego wyjścia
        Assert.NotNull(output);
        Assert.Contains("con1:", output);
        Assert.Contains("con2:", output);
        Assert.Contains("con3:", output);
    }

    // Test metody RunImportNew - asynchronicznej wersji RunImport
    [Fact]
    public async Task RunImportNew_WritesMappingsToConsole()
    {
        //Setup
        var managerMock = new Mock<IRestApiManager>();
        var wrapperMock = new RestApiManagerWrapper(managerMock.Object, testgroupid);

        managerMock.Setup(m => m.GetConnectorsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(GetTestConnectors());


        //Mock schematów
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetTestSchemas().Result);
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetTestSchemas().Result);
        managerMock.Setup(m => m.GetConnectorSchemasAsync("con3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetTestSchemas().Result);

        var support = new FivetranConnectionSupport();

        // Przechwycenie wyjścia
        var writer = new StringWriter();
        var ogOut = Console.Out;
        Console.SetOut(writer);

        // Wywołanie
        await support.RunImportNew(wrapperMock);

        // Przywrócenie konsoli
        Console.SetOut(ogOut);

        var output = writer.ToString();
        _output.WriteLine(output); // Wypisanie przechwyconego wyjścia do testowego wyjścia

        // Wyniki
        Assert.NotNull(output);
        Assert.Contains("Lineage mappings:", output);
        Assert.Contains("con1:", output);
        Assert.Contains("con2:", output);
        Assert.Contains("con3:", output);
    }

}
