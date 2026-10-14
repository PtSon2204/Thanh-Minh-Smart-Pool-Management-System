using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Authorization;
using SmartPool.Domain.Enums;

namespace SmartPool.API.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.TryGetUserId(out _) && context.User.HasRole(RoleEnum.ADMIN)));
            foreach (var (module, actions) in PermissionCatalog.All)
                foreach (var action in actions)
                    options.AddPolicy(PermissionCatalog.Policy(module, action), policy => policy
                        .RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(module, action)));
        });

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
