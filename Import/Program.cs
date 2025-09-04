using Import.ConnectionSupport;

namespace Import;

// !Ta klasa nie podlega code review!
// Kodu nie zmieniłem, chociaż moim zdaniem SelectToImport i RunImport powinny być asynchroniczne.
class Program
{
    static void Main(string[] args) // <-- powino być async Task Main
    {
        while (true)
        {
            var connectionSupport = RetryUntilSuccess(SelectConnectorToImport);
            var connectionDetails = RetryUntilSuccess(connectionSupport.GetConnectionDetailsForSelection);
            var selectedToImport = RetryUntilSuccess(() =>
                connectionSupport.SelectToImport(connectionDetails)
            // async () => await conectionSupport.SelectoToImportNew(connectionDetails) <-- alternatywna implementacja z async Task<string?>
            );
            var connection = connectionSupport.GetConnection(connectionDetails, selectedToImport);
            Console.Clear();
            try
            {
                connectionSupport.RunImport(connection);
                // await connectionSupport.RunImportNew(connection); <-- alternatywna implementacja z async Task RunImportNew(object? connection)
                Console.WriteLine("Import completed successfully.");
            }
            finally
            {
                connectionSupport.CloseConnection(connection);
            }
            Console.WriteLine("Press any key to continue, or 'q' to exit...");
            var key = Console.ReadKey(true);
            if (key.KeyChar is 'q' or 'Q')
            {
                Environment.Exit(0);
            }
        }
    }

    private static T RetryUntilSuccess<T>(Func<T> action)
    {
        string? errorMessage = null;

        // DB - Trochę bez sensu łapać wyjątki, wypisywać je i próbować ponownie w nieskończoność dodatkowo czyszcząc konsolę.
        // Testując łapanie nieprawidłowego API key, nie widziałem nawet błedu który został wypisany.
        // Można byłoby albo zatrzymać program zapętlając while(errorMessage = null), albo dodać limit prób. Lub chociaż nie czyścić konsoli.
        // Nie każdfy błąd powinien być zapętlany. Chociażby właśnie 401 Unauthorized.
        while (true)
        {
            Console.Clear();
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine($"Error: {errorMessage}");
            }

            try
            {
                return action();
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
        }
    }

    private static IConnectionSupport SelectConnectorToImport()
    {
        Console.WriteLine("Select a connector to import from:");
        Console.WriteLine("1. Fivetran");
        Console.Write("Enter the number of your choice: ");

        return Console.ReadLine() switch
        {
            "1" => ConnectionSupportFactory.GetConnectionSupport(FivetranConnectionSupport.ConnectorTypeCode),
            _ => throw new NotSupportedException("This connector is not supported yet.")
        };
    }
}