using FivetranClient.Models;

namespace FivetranClient.Fetchers;

public sealed class NonPaginatedFetcher(HttpRequestHandler requestHandler) : BaseFetcher(requestHandler)
{
    public async Task<T?> FetchAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        // Zgeneralizowana deserializacja i fetchowanie z klasy bazowej
        var root = await FetchAndDeserializeAsync<NonPaginatedRoot<T>>(endpoint, cancellationToken);
        return root is null ? default(T) : root.Data;
    }
}