using Microsoft.AspNetCore.Authorization;

namespace Play.Inventory.Service.Policies
{
    public class InventoryReadOrAdminRequirement : IAuthorizationRequirement
    {

    }

    public class InventoryReadOrAdminHandler : AuthorizationHandler<InventoryReadOrAdminRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            InventoryReadOrAdminRequirement requirement)
        {
            var scopes = context.User.FindAll("scope").Select(c => c.Value);
            var hasScope = scopes.Contains("inventory.readaccess");

            var isAdmin = context.User.IsInRole("Admin");

            if (hasScope || isAdmin)
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
