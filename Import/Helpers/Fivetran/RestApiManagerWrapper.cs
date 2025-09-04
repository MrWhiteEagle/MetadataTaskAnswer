using FivetranClient;

namespace Import.Helpers.Fivetran;

public class RestApiManagerWrapper(IRestApiManager restApiManager, string groupId) : IDisposable
{
    // DB - Wątpię że ta klasa jest potrzebna.
    public IRestApiManager RestApiManager { get; } = restApiManager;
    public string GroupId { get; } = groupId;

    public void Dispose()
    {
        this.RestApiManager.Dispose();
        GC.SuppressFinalize(this);
    }
}