using Microsoft.AspNetCore.Authorization;

namespace GAToolAPI.AuthExtensions;

public class HasRoleRequirement(string role) : IAuthorizationRequirement
{
    public string Role { get; } = role ?? throw new ArgumentNullException(nameof(role));
}