using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Exceptions;
using Microsoft.AspNetCore.WebUtilities;

namespace GAToolAPI.Services;

public class FTCScoutApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public FTCScoutApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.ftcscout.org/rest/v1/");

        // Configure case-insensitive JSON options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<JsonObject?> GetGeneric(string path, IDictionary<string, string?>? query = null)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<JsonObject>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("FTC Scout", response.StatusCode, errorContent);
    }

    public async Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("FTC Scout", response.StatusCode, errorContent);
    }
}
