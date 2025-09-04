using FivetranClient.Models;

namespace FivetranClient;
public interface IRestApiManager : IDisposable
{
    public IAsyncEnumerable<Group> GetGroupsAsync(CancellationToken cancellationToken);
    public IAsyncEnumerable<Connector> GetConnectorsAsync(string groupId, CancellationToken cancellationToken);
    public Task<DataSchemas?> GetConnectorSchemasAsync(
        string connectorId,
        CancellationToken cancellationToken);
}
