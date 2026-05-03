using System.Security.Cryptography;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace GAToolAPI.Services.Auth;

/// <summary>
///     Provides a server-side pepper used to HMAC-hash OTP codes before storage.
///     Without the pepper, a 6-digit OTP could be brute-forced trivially from the
///     stored hash; with it, the attacker also needs Secrets Manager read access.
///
///     Lazily creates a 32-byte random pepper on first startup and stores it under
///     the secret name "AuthOtpPepper".
/// </summary>
public class OtpPepperProvider
{
    private const string SecretName = "AuthOtpPepper";

    private readonly IAmazonSecretsManager _sm;
    private readonly ILogger<OtpPepperProvider> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private byte[]? _pepper;

    public OtpPepperProvider(IAmazonSecretsManager sm, ILogger<OtpPepperProvider> logger)
    {
        _sm = sm;
        _logger = logger;
    }

    public async Task<byte[]> GetAsync(CancellationToken ct = default)
    {
        if (_pepper != null) return _pepper;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_pepper != null) return _pepper;

            string b64;
            try
            {
                var resp = await _sm.GetSecretValueAsync(
                    new GetSecretValueRequest { SecretId = SecretName }, ct);
                b64 = resp.SecretString;
            }
            catch (ResourceNotFoundException)
            {
                _logger.LogWarning("OTP pepper not found in Secrets Manager — generating a new one");
                b64 = await GenerateAndStoreAsync(ct);
            }

            _pepper = Convert.FromBase64String(b64);
            return _pepper;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<string> GenerateAndStoreAsync(CancellationToken ct)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var b64 = Convert.ToBase64String(bytes);
        try
        {
            await _sm.CreateSecretAsync(new CreateSecretRequest
            {
                Name = SecretName,
                Description = "Server-side pepper for HMAC-SHA256 of OTP login codes",
                SecretString = b64
            }, ct);
            _logger.LogInformation("Stored newly-generated OTP pepper in Secrets Manager");
            return b64;
        }
        catch (ResourceExistsException)
        {
            // Another pod beat us to it — read what they wrote.
            var resp = await _sm.GetSecretValueAsync(
                new GetSecretValueRequest { SecretId = SecretName }, ct);
            return resp.SecretString;
        }
    }
}
