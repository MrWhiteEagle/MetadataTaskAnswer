namespace Import.ConnectionSupport;

// !Ta klasa nie podlega code review!
public static class ConnectionSupportFactory
{
    public static IConnectionSupport GetConnectionSupport(string connectorTypeCode)
    {
        return connectorTypeCode switch
        {
            FivetranConnectionSupport.ConnectorTypeCode => new FivetranConnectionSupport(),
            _ => throw new NotSupportedException($"Connector type '{connectorTypeCode}' is not supported.")
        };
    }
}