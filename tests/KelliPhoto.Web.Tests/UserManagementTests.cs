using KelliPhoto.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KelliPhoto.Web.Tests;

public class UserManagementTests : IClassFixture<KelliPhotoWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserManagementTests(KelliPhotoWebApplicationFactory factory)
    {
        Environment.SetEnvironmentVariable("KELLIPHOTO_INTEGRATION_TEST", "1");

        // Unique in-memory DB per class — must be set before the host is built (parallel CI tests share env otherwise).
        var dbName = "UserMgmt_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("KELLIPHOTO_INMEMORY_DB", dbName);

        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task CreateUser_AssignsUserRoleOnly()
    {
        var email = $"user-{Guid.NewGuid():N}@test.local";
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var result = await svc.CreateUserAsync(email, "test123", isAdmin: false);
        Assert.True(result.Succeeded);

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, RoleNames.User));
        Assert.False(await userManager.IsInRoleAsync(user, RoleNames.Admin));
    }

    [Fact]
    public async Task CreateUser_WithAdminFlag_AssignsAdminAndUserRoles()
    {
        var email = $"admin-{Guid.NewGuid():N}@test.local";
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var result = await svc.CreateUserAsync(email, "test123", isAdmin: true);
        Assert.True(result.Succeeded);

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, RoleNames.User));
        Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Admin));
    }

    [Fact]
    public async Task SetAdminRole_CannotRemoveLastAdministrator()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var admins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
        var soleAdmin = admins.Single();

        var result = await svc.SetAdminRoleAsync(soleAdmin.Id, isAdmin: false);
        Assert.False(result.Succeeded);
        Assert.Contains("last administrator", result.Errors.First().Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterPage_WhenRegistrationDisabled_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Identity/Account/Register");
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
