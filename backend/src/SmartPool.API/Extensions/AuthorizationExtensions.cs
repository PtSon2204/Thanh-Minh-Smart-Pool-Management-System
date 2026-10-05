using Microsoft.Extensions.DependencyInjection;
using SmartPool.API.Authorization;
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
            options.AddPolicy(AuthorizationPolicies.AdminOrStaff, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.TryGetUserId(out _) &&
                    (context.User.HasRole(RoleEnum.ADMIN) || context.User.HasRole(RoleEnum.STAFF))));
        });

        return services;
    }
}
