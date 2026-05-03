using GAToolAPI.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
public class AuthDiscoveryController(TokenService tokens) : ControllerBase
{
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
