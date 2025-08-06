using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class TBAApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public TBAApiService(HttpClient httpClient, SecretClient keyVaultClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://www.thebluealliance.com/api/v3/");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add(
            "X-TBA-Auth-Key", keyVaultClient.GetSecret("TBAApiKey").Value.Value);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public async Task<JsonArray?> GetGeneric(string path, IDictionary<string, string?>? query = null)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        return await _httpClient.GetFromJsonAsync<JsonArray>(requestUrl);
    }

    public async Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        return await _httpClient.GetFromJsonAsync<T>(requestUrl, _jsonOptions);
    }
}