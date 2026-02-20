using Microsoft.AspNetCore.Authorization;

namespace GAToolAPI.AuthExtensions;

public class HasAnyRoleRequirement(params string[] roles) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Roles { get; } = roles?.Length > 0 ? roles : throw new ArgumentException("At least one role required.", nameof(roles));
}
