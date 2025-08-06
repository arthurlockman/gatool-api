using System.Text.Json.Nodes;

namespace GAToolAPI.Services;

public interface IApiService
{
    public Task<JsonObject?> GetGeneric(string path, IDictionary<string, string?>? query = null);
    public Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null);
}