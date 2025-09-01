using System.Text.Json;

namespace FivetranClient.Fetchers;

public abstract class BaseFetcher(HttpRequestHandler requestHandler)
{
    protected readonly HttpRequestHandler RequestHandler = requestHandler;
    protected static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    // Skoro w obu klasach dziedziczących deserializujemy w ten sam sposób, to może lepiej jest robić to tutaj?
    // Oba fetchery mają dostęp do RequestHandler i przetwarzają dane w ten sam sposób.
    protected async Task<TResult?> FetchAndDeserializeAsync<TResult>(string url, CancellationToken cancellationToken)
    {
        var response = await this.RequestHandler.GetAsync(url, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResult>(content, SerializerOptions);
    }
}