using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Security.KeyVault.Secrets;
using GAToolAPI.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class TOAApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public TOAApiService(HttpClient httpClient, SecretClient keyVaultClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://theorangealliance.org/api/");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add(
            "X-TOA-Key", keyVaultClient.GetSecret("TOAApiKey").Value.Value);
        _httpClient.DefaultRequestHeaders.Add(
            "X-Application-Origin", keyVaultClient.GetSecret("TOAApiKey").Value.Value);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public async Task<JsonArray?> GetGeneric(string path, IDictionary<string, string?>? query = null)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<JsonArray>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("The Orange Alliance", response.StatusCode, errorContent);
    }

    public async Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("The Orange Alliance", response.StatusCode, errorContent);
    }
}