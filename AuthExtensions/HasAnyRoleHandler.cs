using Microsoft.AspNetCore.Authorization;

namespace GAToolAPI.AuthExtensions;

public class HasAnyRoleHandler : AuthorizationHandler<HasAnyRoleRequirement>
{
    private const string RolesClaimType = "https://gatool.org/roles";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HasAnyRoleRequirement requirement)
    {
        if (context.User.Claims.Any(c => c.Type == RolesClaimType && requirement.Roles.Contains(c.Value)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
