using System.Security.Cryptography;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace GAToolAPI.Services.Auth;

/// <summary>
///     Loads (and lazily creates) the ECDSA P-256 key used to sign self-issued JWTs.
///     The PEM-encoded private key is stored in Secrets Manager under "AuthSigningKey".
///     If the secret does not exist on first startup, a key is generated and persisted.
/// </summary>
public class AuthSigningKeyProvider(IAmazonSecretsManager sm, ILogger<AuthSigningKeyProvider> logger)
{
    private const string SecretName = "AuthSigningKey";

    private ECDsa? _key;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task<ECDsa> GetKeyAsync(CancellationToken ct = default)
    {
        if (_key != null) return _key;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_key != null) return _key;

            string pem;
            try
            {
                var resp = await sm.GetSecretValueAsync(
                    new GetSecretValueRequest { SecretId = SecretName }, ct);
                pem = resp.SecretString;
                logger.LogInformation("Loaded auth signing key from Secrets Manager");
            }
            catch (ResourceNotFoundException)
            {
                logger.LogWarning("Auth signing key not found in Secrets Manager — generating a new one");
                pem = GenerateAndStoreKeyAsync(ct).GetAwaiter().GetResult();
            }

            var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            _key = ec;
            return _key;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<string> GenerateAndStoreKeyAsync(CancellationToken ct)
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ec.ExportPkcs8PrivateKeyPem();
        try
        {
            await sm.CreateSecretAsync(new CreateSecretRequest
            {
                Name = SecretName,
                Description = "ECDSA P-256 private key for signing gatool API access tokens",
                SecretString = pem
            }, ct);
            logger.LogInformation("Stored newly-generated auth signing key in Secrets Manager");
            return pem;
        }
        catch (ResourceExistsException)
        {
            // Another pod beat us to creating the key — fetch and use theirs so all
            // pods sign with the same key (otherwise tokens issued by one pod won't
            // validate on another).
            logger.LogInformation("Auth signing key was created concurrently by another pod; reusing it");
            var resp = await sm.GetSecretValueAsync(
                new GetSecretValueRequest { SecretId = SecretName }, ct);
            return resp.SecretString;
        }
    }
}