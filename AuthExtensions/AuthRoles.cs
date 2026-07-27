namespace GAToolAPI.AuthExtensions;

public static class AuthRoles
{
    public const string User = "user";
    public const string Admin = "admin";
    public const string FirstGlobalWrite = "firstglobal-write";
    public const string ClaimType = "https://gatool.org/roles";
}

public static class AuthPolicies
{
    public const string User = AuthRoles.User;
    public const string Admin = AuthRoles.Admin;
    public const string FirstGlobalWrite = AuthRoles.FirstGlobalWrite;
}

public record AssignableRoleMetadata(string Name, string Label, string Description);

public static class AuthRoleCatalog
{
    private static readonly AssignableRoleMetadata[] AssignableRoles =
    [
        new(
            AuthRoles.FirstGlobalWrite,
            "FIRST Global Write",
            "Create and update shared FIRST Global team data")
    ];

    public static IReadOnlyList<AssignableRoleMetadata> ManuallyAssignable => AssignableRoles;

    public static bool TryGetManuallyAssignable(string role, out AssignableRoleMetadata metadata)
    {
        var match = AssignableRoles.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, role?.Trim(), StringComparison.OrdinalIgnoreCase));
        metadata = match!;
        return match != null;
    }

    public static string[] Canonicalize(IEnumerable<string> roles) => roles
        .Where(role => !string.IsNullOrWhiteSpace(role))
        .Select(role => CanonicalizeKnownRole(role.Trim()))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(role => role, StringComparer.Ordinal)
        .ToArray();

    private static string CanonicalizeKnownRole(string role)
    {
        if (string.Equals(role, AuthRoles.User, StringComparison.OrdinalIgnoreCase)) return AuthRoles.User;
        if (string.Equals(role, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase)) return AuthRoles.Admin;
        if (string.Equals(role, AuthRoles.FirstGlobalWrite, StringComparison.OrdinalIgnoreCase))
            return AuthRoles.FirstGlobalWrite;
        return role;
    }
}
