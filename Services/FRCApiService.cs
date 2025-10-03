using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Security.KeyVault.Secrets;
using GAToolAPI.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class FRCApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public FRCApiService(HttpClient httpClient, SecretClient keyVaultClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://frc-api.firstinspires.org/v3.0/");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Authorization, keyVaultClient.GetSecret("FRCApiKey").Value.Value);

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
        throw new ExternalApiException("FRC API", response.StatusCode, errorContent);
    }

    public async Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("FRC API", response.StatusCode, errorContent);
    }
}