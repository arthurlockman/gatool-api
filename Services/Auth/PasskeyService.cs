using Fido2NetLib;
using Fido2NetLib.Objects;
using GAToolAPI.Models;
using Microsoft.IdentityModel.Tokens;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services.Auth;

/// <summary>
///     WebAuthn (passkey) registration and authentication on the server side.
///     Uses fido2-net-lib for protocol details.
///
///     Challenges are held briefly in FusionCache (Redis-backed) keyed by an opaque session
///     id returned to the client. The client echoes that id back when completing the ceremony.
/// </summary>
public class PasskeyService(
    IFido2 fido2,
    IMetadataService metadataService,
    CommunityAaguidService communityAaguids,
    AuthRepository repo,
    IFusionCache cache,
    ILogger<PasskeyService> logger)
{
    private const string CachePrefix = "webauthn:challenge:";
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    // ── Registration ────────────────────────────────────────────────────────

    public record RegisterOptionsResult(string SessionId, CredentialCreateOptions Options);

    public async Task<RegisterOptionsResult> BeginRegistrationAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await repo.ListPasskeysAsync(normalized, ct);
        var exclude = existing
            .Select(p => new PublicKeyCredentialDescriptor(Base64UrlEncoder.DecodeBytes(p.CredentialId)))
            .ToList();

        var user = new Fido2User
        {
            DisplayName = normalized,
            Name = normalized,
            Id = System.Text.Encoding.UTF8.GetBytes(normalized)
        };

        var authenticatorSelection = new AuthenticatorSelection
        {
            // Discoverable credentials so users can sign in without typing their email
            ResidentKey = ResidentKeyRequirement.Required,
            // Required: passkeys replace passwords/OTP, so we always want UV (biometric/PIN)
            UserVerification = UserVerificationRequirement.Required
        };

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            ExcludeCredentials = exclude,
            AuthenticatorSelection = authenticatorSelection,
            AttestationPreference = AttestationConveyancePreference.None,
            Extensions = new AuthenticationExtensionsClientInputs()
        });

        var sessionId = NewSessionId();
        await cache.SetAsync(CachePrefix + sessionId,
            new ChallengeBlob { Email = normalized, OptionsJson = options.ToJson() },
            ChallengeLifetime, ct);

        return new RegisterOptionsResult(sessionId, options);
    }

    public async Task<PasskeyRecord?> CompleteRegistrationAsync(
        string email,
        string sessionId,
        AuthenticatorAttestationRawResponse attestation,
        string? nickname,
        CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        var blob = await cache.TryGetAsync<ChallengeBlob>(CachePrefix + sessionId, token: ct);
        if (!blob.HasValue || blob.Value.Email != normalized)
        {
            logger.LogWarning("Passkey registration session not found / mismatched email for {Email}", normalized);
            return null;
        }

        await cache.RemoveAsync(CachePrefix + sessionId, token: ct);

        var origOptions = CredentialCreateOptions.FromJson(blob.Value.OptionsJson);

        var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestation,
            OriginalOptions = origOptions,
            IsCredentialIdUniqueToUserCallback = async (args, innerCt) =>
            {
                var existing = await repo.GetPasskeyByCredentialIdAsync(
                    Base64UrlEncoder.Encode(args.CredentialId), innerCt);
                return existing == null;
            }
        }, ct);

        var credentialIdB64 = Base64UrlEncoder.Encode(result.Id);
        var resolvedNickname = !string.IsNullOrWhiteSpace(nickname)
            ? nickname.Trim()
            : await ResolveAuthenticatorNameAsync(result.AaGuid, ct) ?? "Passkey";
        var record = new PasskeyRecord
        {
            Email = normalized,
            CredentialId = credentialIdB64,
            PublicKey = result.PublicKey,
            SignCount = result.SignCount,
            AaGuid = result.AaGuid,
            Nickname = resolvedNickname,
            CreatedAt = DateTimeOffset.UtcNow,
            // Transports aren't surfaced by Fido2's RegisteredPublicKeyCredential.
            // The browser may send them at registration time but we don't persist here;
            // they'd primarily be used to populate allowCredentials during authentication.
            Transports = []
        };
        await repo.SavePasskeyAsync(record, ct);
        logger.LogInformation("Registered passkey {CredentialId} for {Email} ({Authenticator})",
            credentialIdB64, normalized, resolvedNickname);
        return record;
    }

    /// <summary>
    /// Resolves an authenticator's human-readable name using a two-tier lookup:
    /// (1) the FIDO Metadata Service (covers most certified hardware authenticators)
    /// and (2) the passkeydeveloper community list (covers the major passkey
    /// providers like iCloud Keychain, 1Password, Bitwarden). Returns null if
    /// the AAGUID is empty or unknown to both sources.
    /// </summary>
    private async Task<string?> ResolveAuthenticatorNameAsync(Guid aaguid, CancellationToken ct)
    {
        if (aaguid == Guid.Empty) return null;
        try
        {
            var entry = await metadataService.GetEntryAsync(aaguid, ct);
            var description = entry?.MetadataStatement?.Description;
            if (!string.IsNullOrWhiteSpace(description)) return description;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FIDO MDS lookup failed for AAGUID {Aaguid}", aaguid);
        }

        return communityAaguids.Lookup(aaguid);
    }

    // ── Authentication ─────────────────────────────────────────────────────

    public record AuthOptionsResult(string SessionId, AssertionOptions Options);

    public async Task<AuthOptionsResult> BeginAuthenticationAsync(string? email, CancellationToken ct = default)
    {
        // For username-less / discoverable credential flow, allowedCredentials is empty.
        // For username-first flow, restrict to that user's credentials.
        var allowed = new List<PublicKeyCredentialDescriptor>();
        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            normalized = email.Trim().ToLowerInvariant();
            var creds = await repo.ListPasskeysAsync(normalized, ct);
            allowed.AddRange(creds.Select(c =>
                new PublicKeyCredentialDescriptor(Base64UrlEncoder.DecodeBytes(c.CredentialId))));
        }

        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowed,
            UserVerification = UserVerificationRequirement.Required,
            Extensions = new AuthenticationExtensionsClientInputs()
        });

        var sessionId = NewSessionId();
        await cache.SetAsync(CachePrefix + sessionId,
            new ChallengeBlob { Email = normalized, OptionsJson = options.ToJson() },
            ChallengeLifetime, ct);

        return new AuthOptionsResult(sessionId, options);
    }

    public async Task<UserRecord?> CompleteAuthenticationAsync(
        string sessionId,
        AuthenticatorAssertionRawResponse assertion,
        CancellationToken ct = default)
    {
        var blob = await cache.TryGetAsync<ChallengeBlob>(CachePrefix + sessionId, token: ct);
        if (!blob.HasValue)
        {
            logger.LogWarning("Passkey auth session not found");
            return null;
        }

        await cache.RemoveAsync(CachePrefix + sessionId, token: ct);

        var origOptions = AssertionOptions.FromJson(blob.Value.OptionsJson);

        var credentialIdB64 = Base64UrlEncoder.Encode(assertion.RawId);
        var stored = await repo.GetPasskeyByCredentialIdAsync(credentialIdB64, ct);
        if (stored == null)
        {
            logger.LogWarning("Passkey {CredentialId} not registered", credentialIdB64);
            return null;
        }

        // If the user supplied an email when starting the ceremony, enforce that the
        // selected credential actually belongs to that user. This prevents the confusing
        // case where "sign in as alice" ends up authenticating bob because the browser
        // chose Bob's discoverable credential.
        if (!string.IsNullOrEmpty(blob.Value.Email) &&
            !string.Equals(blob.Value.Email, stored.Email, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Passkey owner {Owner} does not match requested email {Requested}",
                stored.Email, blob.Value.Email);
            return null;
        }

        var verifyResult = await fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = assertion,
            OriginalOptions = origOptions,
            StoredPublicKey = stored.PublicKey,
            StoredSignatureCounter = stored.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = (args, innerCt) =>
            {
                // userHandle is the email bytes we set during registration
                if (args.UserHandle == null) return Task.FromResult(true); // username-first flow has no userHandle
                var handleEmail = System.Text.Encoding.UTF8.GetString(args.UserHandle);
                return Task.FromResult(string.Equals(handleEmail, stored.Email, StringComparison.OrdinalIgnoreCase));
            }
        }, ct);

        await repo.UpdatePasskeyCounterAsync(stored.Email, credentialIdB64, verifyResult.SignCount, ct);

        var user = await repo.GetUserAsync(stored.Email, ct);
        if (user == null)
        {
            logger.LogWarning("Passkey {CredentialId} references missing user {Email}",
                credentialIdB64, stored.Email);
            return null;
        }

        return user;
    }

    private static string NewSessionId()
    {
        Span<byte> bytes = stackalloc byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode([.. bytes]);
    }

    private class ChallengeBlob
    {
        public string? Email { get; set; }
        public string OptionsJson { get; set; } = "";
    }
}