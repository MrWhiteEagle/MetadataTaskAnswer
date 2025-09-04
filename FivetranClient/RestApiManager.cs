using FivetranClient.Fetchers;
using FivetranClient.Infrastructure;
using FivetranClient.Models;
using System.Net;

namespace FivetranClient;

public class RestApiManager(HttpRequestHandler requestHandler) : IDisposable, IRestApiManager
{
    private readonly PaginatedFetcher _paginatedFetcher = new(requestHandler);
    private readonly NonPaginatedFetcher _nonPaginatedFetcher = new(requestHandler);
    // Indicates whether this instance owns the HttpClient and should dispose it.
    internal readonly HttpClient? _createdClient;

    public static readonly Uri ApiBaseUrl = new("https://api.fivetran.com/v1/");

    // DB - Jeśli w obecnym przykładzie kożystamy z tylko tego konstruktora, to po co nam inne?
    // Jeśli w prawdziwym kodzie są używane do innych przykładów inicjalizacji klasy to nie było tematu.
    // Jeśli nie to możnaby kożystać tylko z jednego, bo ciężko się je czyta.
    public RestApiManager(string apiKey, string apiSecret, TimeSpan timeout) // <-- o tego
        : this(ApiBaseUrl, apiKey, apiSecret, timeout)
    {
        // na przykład:
        // :this(new HttpRequestHandler(new FiveTranHttpClient(ApiBaseUrl, apiKey, apiSecret, timeout))
    }

    public RestApiManager(Uri baseUrl, string apiKey, string apiSecret, TimeSpan timeout)
        : this(new FivetranHttpClient(baseUrl, apiKey, apiSecret, timeout), true)
    {
    }

    private RestApiManager(HttpClient client, bool _) : this(new HttpRequestHandler(client)) => this._createdClient = client;

    public RestApiManager(HttpClient client) : this(new HttpRequestHandler(client))
    {
    }

    public IAsyncEnumerable<Group> GetGroupsAsync(CancellationToken cancellationToken)
    {
        var endpointPath = "groups";
        return this._paginatedFetcher.FetchItemsAsync<Group>(endpointPath, cancellationToken);
    }

    public IAsyncEnumerable<Connector> GetConnectorsAsync(string groupId, CancellationToken cancellationToken)
    {
        var endpointPath = $"groups/{WebUtility.UrlEncode(groupId)}/connectors";
        return this._paginatedFetcher.FetchItemsAsync<Connector>(endpointPath, cancellationToken);
    }

    public async Task<DataSchemas?> GetConnectorSchemasAsync(
        string connectorId,
        CancellationToken cancellationToken)
    {
        var endpointPath = $"connectors/{WebUtility.UrlEncode(connectorId)}/schemas";
        return await this._nonPaginatedFetcher.FetchAsync<DataSchemas>(endpointPath, cancellationToken);
    }

    public void Dispose()
    {
        _createdClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}