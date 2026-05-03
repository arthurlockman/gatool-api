using System.Text.Json;
using System.Text.Json.Serialization;

namespace GAToolAPI.Services.Auth;

/// <summary>
///     Loads and periodically refreshes the passkeydeveloper community AAGUID list
///     (https://github.com/passkeydeveloper/passkey-authenticator-aaguids).
///
///     The official FIDO Metadata Service primarily covers FIDO-certified hardware
///     authenticators (YubiKey, Titan, Feitian, etc.). The major *passkey* providers
///     — iCloud Keychain, Google Password Manager, 1Password, Bitwarden, Dashlane,
///     etc. — register their AAGUIDs on this community list instead. We use it as
///     a second-tier lookup behind MDS so newly-registered passkey providers start
///     resolving to friendly names automatically as the upstream list updates.
///
///     Refreshes once a day. If the initial fetch fails the service starts empty
///     and retries on the next interval — passkey registration still succeeds and
///     just falls back to "Passkey".
/// </summary>
public class CommunityAaguidService : BackgroundService
{
    private const string SourceUrl =
        "https://raw.githubusercontent.com/passkeydeveloper/passkey-authenticator-aaguids/main/aaguid.json";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CommunityAaguidService> _logger;
    private volatile IReadOnlyDictionary<Guid, string> _names = new Dictionary<Guid, string>();

    public CommunityAaguidService(IHttpClientFactory httpClientFactory,
        ILogger<CommunityAaguidService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string? Lookup(Guid aaguid)
    {
        if (aaguid == Guid.Empty) return null;
        return _names.TryGetValue(aaguid, out var name) ? name : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kick off the first refresh immediately so the names are available shortly
        // after startup. Don't block app startup on it — passkey registration is
        // tolerant of an empty map.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Community AAGUID refresh failed; will retry in {Interval}", RefreshInterval);
            }
            try { await Task.Delay(RefreshInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient(nameof(CommunityAaguidService));
        http.Timeout = TimeSpan.FromSeconds(15);
        using var resp = await http.GetAsync(SourceUrl, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var raw = await JsonSerializer.DeserializeAsync<Dictionary<string, Entry>>(stream,
            cancellationToken: ct);
        if (raw == null)
        {
            _logger.LogWarning("Community AAGUID feed returned null payload");
            return;
        }

        var map = new Dictionary<Guid, string>(raw.Count);
        foreach (var (key, entry) in raw)
        {
            if (Guid.TryParse(key, out var aaguid) && !string.IsNullOrWhiteSpace(entry.Name))
                map[aaguid] = entry.Name;
        }
        _names = map;
        _logger.LogInformation("Loaded {Count} community AAGUID entries", map.Count);
    }

    private class Entry
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
