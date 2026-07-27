using Microsoft.AspNetCore.Authorization;

namespace GAToolAPI.AuthExtensions;

public class HasRoleHandler : AuthorizationHandler<HasRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HasRoleRequirement requirement)
    {
        if (context.User.Claims.Any(c => c.Type == AuthRoles.ClaimType && c.Value == requirement.Role))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
