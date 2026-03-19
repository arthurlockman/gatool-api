using System.Text.Json;
using GAToolAPI.Exceptions;
using GAToolAPI.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace GAToolAPI.Services;

public class CasterstoolApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public CasterstoolApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://casterstool.com/api/");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public async Task<List<TeamConnection>?> GetConnections(string year, string eventCode, string teamNumbers)
    {
        var path = $"events/{year}{eventCode}/summary/connections";
        var query = new Dictionary<string, string?>
        {
            ["teams"] = teamNumbers,
            ["all_time"] = "true"
        };
        var requestUrl = QueryHelpers.AddQueryString(path, query);
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<List<TeamConnection>>(_jsonOptions);

        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("Casterstool", response.StatusCode, errorContent);
    }
}
