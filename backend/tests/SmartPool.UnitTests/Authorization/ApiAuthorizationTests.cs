using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartPool.API.Authorization;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Authorization;
using SmartPool.Application.Features.Notifications;
using SmartPool.Application.Features.Notifications.Queries.GetNotifications;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.UnitTests.Authorization;

public sealed class ApiAuthorizationTests
{
    private static readonly string[] OwnOnboardingActions =
    [
        nameof(StaffsController.MyOnboarding), nameof(StaffsController.SaveMyOnboarding),
        nameof(StaffsController.SubmitMyOnboarding), nameof(StaffsController.RequestMyCorrection),
        nameof(StaffsController.UploadMyOnboarding), nameof(StaffsController.ReadOwnDocument)
    ];
    private static readonly string[] OwnNotificationActions =
    [
        nameof(NotificationsController.GetMine), nameof(NotificationsController.MarkMineAsRead)
    ];

    [Theory]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "ADMIN", AuthorizationPolicies.AdminOnly, true)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "admin", AuthorizationPolicies.AdminOnly, true)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "SALES", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "LIFEGUARD", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "COACH", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "not-a-guid", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "CUSTOMER", AuthorizationPolicies.AdminOnly, false)]
    [InlineData(false, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "ADMIN", AuthorizationPolicies.AdminOnly, false)]
    public async Task AuthorizeAsync_RequiresAuthenticatedCallerWithValidIdAndPermittedRole(
        bool isAuthenticated,
        string userId,
        string role,
        string policy,
        bool expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IRolePermissionReader>());
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity(isAuthenticated ? "test" : null);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));

        var result = await authorization.AuthorizeAsync(new ClaimsPrincipal(identity), null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData("ADMIN", RoleEnum.ADMIN, true)]
    [InlineData("admin", RoleEnum.ADMIN, true)]
    [InlineData("Sales", RoleEnum.SALES, true)]
    [InlineData("Lifeguard", RoleEnum.LIFEGUARD, true)]
    [InlineData("Coach", RoleEnum.COACH, true)]
    [InlineData("CUSTOMER", RoleEnum.ADMIN, false)]
    public void HasRole_UsesCaseInsensitiveRoleClaims(string role, RoleEnum expectedRole, bool expected)
    {
        var principal = CreatePrincipal(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", role);

        Assert.Equal(expected, principal.HasRole(expectedRole));
    }

    [Fact]
    public void TryGetUserId_RejectsEmptyGuid()
    {
        var principal = CreatePrincipal(true, Guid.Empty.ToString(), "ADMIN");

        Assert.False(principal.TryGetUserId(out _));
    }

    [Fact]
    public void HasRole_IgnoresClaimsOutsideTheRoleClaimType()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("role", "ADMIN"));
        var principal = new ClaimsPrincipal(identity);

        Assert.False(principal.HasRole(RoleEnum.ADMIN));
    }

    [Fact]
    public void OperationalEndpoints_RequirePermissions()
    {
        var controllers = new[]
        {
            typeof(ServicesController), typeof(StaffsController), typeof(ShiftsController),
            typeof(RentalsController), typeof(PoolAccessController), typeof(TicketController),
            typeof(NotificationsController), typeof(PaymentsController), typeof(ReportsController),
            typeof(ProfilesController), typeof(SwimClassesController)
        };
        var actions = controllers.SelectMany(controller => controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any() && !method.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Select(method => (Controller: controller, Method: method)))
            .ToArray();

        foreach (var action in actions)
        {
            if (action.Controller == typeof(StaffsController) && OwnOnboardingActions.Contains(action.Method.Name))
                continue;
            if (action.Controller == typeof(NotificationsController) && OwnNotificationActions.Contains(action.Method.Name))
                continue;
            var policies = action.Method.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy);
            Assert.True(policies.Any(policy => policy?.StartsWith("permission:", StringComparison.Ordinal) == true ||
                policy == AuthorizationPolicies.AdminOnly), $"{action.Controller.Name}.{action.Method.Name} must require an operational permission.");
        }
    }

    [Fact]
    public async Task OwnOnboardingEndpointsRequireAuthentication()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IRolePermissionReader>());
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        foreach (var name in OwnOnboardingActions)
        {
            var method = typeof(StaffsController).GetMethod(name)!;
            var attributes = method.GetCustomAttributes<AuthorizeAttribute>().ToArray();
            Assert.NotEmpty(attributes);
            Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
            var policy = await AuthorizationPolicy.CombineAsync(policies, attributes);
            Assert.NotNull(policy);
            Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(CreatePrincipal(true, Guid.NewGuid().ToString(), "SALES"), null, policy)).Succeeded);
        }
    }

    [Fact]
    public async Task OwnOnboardingActionsDeriveActorFromClaimsInsteadOfRequest()
    {
        var actor = Guid.NewGuid();
        OnboardingCommand? sent = null;
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(It.IsAny<OnboardingCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<OnboardingWorkspace>, CancellationToken>((command, _) => sent = (OnboardingCommand)command)
            .ReturnsAsync(new OnboardingWorkspace("OK", null, null, null, null));
        var controller = new StaffsController(sender.Object) { ControllerContext = new()
        {
            HttpContext = new DefaultHttpContext { User = CreatePrincipal(true, actor.ToString(), "SALES") }
        } };
        foreach (var action in new Func<OnboardingCommand, Task<IActionResult>>[]
        {
            command => controller.SaveMyOnboarding(command, default),
            command => controller.SubmitMyOnboarding(command, default),
            command => controller.RequestMyCorrection(command, default)
        })
        {
            await action(new() { ActorId = Guid.NewGuid(), EmployeeId = Guid.NewGuid(), InvitationId = Guid.NewGuid(), Token = "foreign-invitation" });
            Assert.NotNull(sent);
            Assert.Equal(actor, sent.ActorId);
            Assert.Null(sent.InvitationId);
            Assert.Null(sent.Token);
        }
        await controller.MyOnboarding(default);
        Assert.Equal(actor, sent!.ActorId);
        using var stream = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        await controller.UploadMyOnboarding(new() { InvitationId = Guid.NewGuid(), Token = "foreign-invitation", Purpose = "FRONT",
            File = new FormFile(stream, 0, stream.Length, "file", "synthetic.png") { Headers = new HeaderDictionary(), ContentType = "image/png" } }, default);
        Assert.Equal(actor, sent!.ActorId);
        Assert.Null(sent.InvitationId);
        Assert.Null(sent.Token);
        sender.Setup(item => item.Send(It.IsAny<ReadStaffDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffDocumentContent([], "image/png"));
        await controller.ReadOwnDocument(Guid.NewGuid(), default);
        sender.Verify(item => item.Send(It.Is<ReadStaffDocumentQuery>(query => query.ActorId == actor && !query.IsAdmin && query.InvitationId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OwnNotificationEndpointsRequireAuthenticationAndDeriveOwnerFromClaims()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IRolePermissionReader>());
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        foreach (var name in OwnNotificationActions)
        {
            var method = typeof(NotificationsController).GetMethod(name)!;
            var attributes = method.GetCustomAttributes<AuthorizeAttribute>().ToArray();
            Assert.NotEmpty(attributes);
            Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
            var policy = await AuthorizationPolicy.CombineAsync(policies, attributes);
            Assert.NotNull(policy);
            Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(CreatePrincipal(true, Guid.NewGuid().ToString(), "CUSTOMER"), null, policy)).Succeeded);
        }

        var actor = Guid.NewGuid();
        GetMyNotificationsQuery? sentQuery = null;
        MarkMyNotificationReadCommand? sentCommand = null;
        var sender = new Mock<ISender>();
        sender.Setup(item => item.Send(It.IsAny<GetMyNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<GetNotificationsResult>, CancellationToken>((request, _) => sentQuery = (GetMyNotificationsQuery)request)
            .ReturnsAsync(new GetNotificationsResult());
        sender.Setup(item => item.Send(It.IsAny<MarkMyNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<bool>, CancellationToken>((request, _) => sentCommand = (MarkMyNotificationReadCommand)request)
            .ReturnsAsync(true);
        var controller = new NotificationsController(sender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(true, actor.ToString(), "CUSTOMER") }
            }
        };

        await controller.GetMine(2, 10, default);
        Assert.NotNull(sentQuery);
        Assert.Equal(actor, sentQuery.UserId);
        Assert.Equal(2, sentQuery.Page);
        Assert.Equal(10, sentQuery.PageSize);

        var notificationId = Guid.NewGuid();
        await controller.MarkMineAsRead(notificationId, default);
        Assert.NotNull(sentCommand);
        Assert.Equal(actor, sentCommand.UserId);
        Assert.Equal(notificationId, sentCommand.NotificationId);
    }

    [Fact]
    public void TicketLookupGrant_DoesNotImplyEntryConfirmationOrHistory()
    {
        Assert.Equal(PermissionCatalog.Policy("ticket_lookup", "read"),
            typeof(PoolAccessController).GetMethod(nameof(PoolAccessController.Lookup))!
                .GetCustomAttribute<RequirePermissionAttribute>()!.Policy);
        Assert.Equal(PermissionCatalog.Policy("pool_entry", "create"),
            typeof(PoolAccessController).GetMethod(nameof(PoolAccessController.Confirm))!
                .GetCustomAttribute<RequirePermissionAttribute>()!.Policy);
        Assert.Equal(PermissionCatalog.Policy("pool_entry", "read"),
            typeof(PoolAccessController).GetMethod(nameof(PoolAccessController.GetHistory))!
                .GetCustomAttribute<RequirePermissionAttribute>()!.Policy);
    }

    [Fact]
    public void PermissionManagement_RequiresAdmin()
    {
        var policy = Assert.Single(typeof(RolesController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.AdminOnly, policy.Policy);
        Assert.False(PermissionCatalog.IsValid("users", "create"));
        Assert.True(PermissionCatalog.IsValid("ticket_lookup", "read"));
    }

    [Theory]
    [InlineData("ADMIN", true, true)]
    [InlineData("Admin", true, true)]
    [InlineData("admin", true, true)]
    [InlineData("SALES", true, false)]
    [InlineData("LIFEGUARD", true, false)]
    [InlineData("ADMIN", false, false)]
    public async Task ShiftListing_UsesAdminPolicyAndStillRequiresReadPermission(string role, bool hasPermission, bool expected)
    {
        var reader = new Mock<IRolePermissionReader>();
        reader.Setup(item => item.ReadCurrentPermissionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPermission ? "{\"shifts\":[\"read\"]}" : "{}");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(reader.Object);
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var metadata = typeof(ShiftsController).GetMethod(nameof(ShiftsController.GetShifts))!
            .GetCustomAttributes<AuthorizeAttribute>();
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        Assert.NotNull(policy);

        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            CreatePrincipal(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", role), null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData("COACH", true, true)]
    [InlineData("COACH", false, false)]
    [InlineData("ADMIN", true, false)]
    public async Task CreateSwimClass_RequiresAdminAndCreatePermission(
        string role, bool hasPermission, bool expected)
    {
        var reader = new Mock<IRolePermissionReader>();
        reader.Setup(item => item.ReadCurrentPermissionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPermission ? "{\"swim_classes\":[\"create\"]}" : "{}");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(reader.Object);
        services.AddApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var metadata = typeof(SwimClassesController).GetMethod(nameof(SwimClassesController.Create))!
            .GetCustomAttributes<AuthorizeAttribute>();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        Assert.NotNull(policy);

        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            CreatePrincipal(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", role), null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
[InlineData(nameof(SwimClassesController.CreateSessions), "class_sessions", "create")]
[InlineData(nameof(SwimClassesController.Activate), "swim_classes", "update")]
public async Task ClassSchedulingEndpoints_RequireCoachAndTheirPermission(
    string action, string module, string permission)
{
    var reader = new Mock<IRolePermissionReader>();

    reader.Setup(item => item.ReadCurrentPermissionsAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync($"{{\"{module}\":[\"{permission}\"]}}");

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(reader.Object);
    services.AddApiAuthorization();

    using var provider = services.BuildServiceProvider();

    var metadata = typeof(SwimClassesController)
        .GetMethod(action)!
        .GetCustomAttributes<AuthorizeAttribute>();

    var policy = await AuthorizationPolicy.CombineAsync(
        provider.GetRequiredService<IAuthorizationPolicyProvider>(),
        metadata);

    Assert.NotNull(policy);

    var authorization = provider.GetRequiredService<IAuthorizationService>();

    // COACH có permission tương ứng => được phép.
    Assert.True((await authorization.AuthorizeAsync(
        CreatePrincipal(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "COACH"),
        null,
        policy)).Succeeded);

    // ADMIN không thuộc role được endpoint cho phép => bị từ chối.
    Assert.False((await authorization.AuthorizeAsync(
        CreatePrincipal(true, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "ADMIN"),
        null,
        policy)).Succeeded);
}

    private static ClaimsPrincipal CreatePrincipal(bool isAuthenticated, string userId, string role)
    {
        var identity = new ClaimsIdentity(isAuthenticated ? "test" : null);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }
}
