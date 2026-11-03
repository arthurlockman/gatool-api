using GAToolAPI.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

/// <summary>
///     Minimal OIDC discovery + JWKS endpoints so external tools (jwt.io, debuggers,
///     future relying parties) can resolve the public key used to sign our access
///     tokens directly from the issuer URL.
///
///     The token's <c>iss</c> claim is <c>https://api.gatool.org/auth</c>, so these
///     endpoints intentionally live at <c>/auth/.well-known/...</c> (no /v3 prefix)
///     to match the standard discovery convention of <c>{iss}/.well-known/...</c>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("auth/.well-known")]
[OpenApiTag("Authentication")]
public class AuthDiscoveryController(TokenService tokens) : ControllerBase
{
    /// <summary>Gets discovery metadata for the GATool access-token issuer. No authentication is required.</summary>
    /// <remarks>
    ///     Describes the issuer, JWKS URL, and supported signing algorithm. This is minimal discovery metadata,
    ///     not a full OpenID Connect authorization server.
    /// </remarks>
    /// <returns>Issuer metadata including jwks_uri and the ES256 signing algorithm.</returns>
    /// <response code="200">Returns the issuer discovery document.</response>
    [HttpGet("openid-configuration")]
    public object OpenIdConfiguration()
    {
        var issuer = TokenService.Issuer;
        return new
        {
            issuer,
            jwks_uri = $"{issuer}/.well-known/jwks.json",
            id_token_signing_alg_values_supported = new[] { "ES256" },
            subject_types_supported = new[] { "public" },
            response_types_supported = new[] { "token" }
        };
    }

    /// <summary>Gets the public JSON Web Key Set used to verify GATool access tokens. No authentication is required.</summary>
    /// <param name="ct">Request cancellation token; not a client-supplied parameter.</param>
    /// <returns>A keys array containing the public EC P-256 signing key; no private key material.</returns>
    /// <response code="200">Returns the public signing key set.</response>
    [HttpGet("jwks.json")]
    public async Task<object> Jwks(CancellationToken ct)
    {
        var jwk = await tokens.GetPublicJwkAsync(ct);
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = jwk.Kty,
                    use = jwk.Use,
                    alg = jwk.Alg,
                    kid = jwk.KeyId,
                    crv = jwk.Crv,
                    x = jwk.X,
                    y = jwk.Y
                }
            }
        };
    }
}