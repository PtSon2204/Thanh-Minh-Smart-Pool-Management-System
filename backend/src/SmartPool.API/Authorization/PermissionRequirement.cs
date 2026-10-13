using Microsoft.AspNetCore.Authorization;

namespace SmartPool.API.Authorization;

public sealed record PermissionRequirement(string Module, string Action) : IAuthorizationRequirement;
