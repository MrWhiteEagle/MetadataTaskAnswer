namespace Import.ConnectionSupport;

// !Ten interfejs nie podlega CR, symyluje on istniejące poziomy abstrakcji w aplikacji!
public interface IConnectionSupport
{
    // Gets all information needed to be able to list databases (or equivalent in ETL/BI)
    public object? GetConnectionDetailsForSelection();
    // Lists databases (or equivalent in ETL/BI) to import and returns the selected one
    public string? SelectToImport(object? connectionDetails);
    // Gets the connection object based on the provided details and selected database (or equivalent in ETL/BI)
    public object? GetConnection(object? connectionDetails, string? selectedToImport);
    public void CloseConnection(object? connection);
    public void RunImport(object? connection);
}