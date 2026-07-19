using System.Collections.Generic;
using System.Linq;
using KelliPhoto.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KelliPhoto.Web.Tests;

[Collection("UserManagement")]
public class UserManagementTests : IClassFixture<KelliPhotoWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserManagementTests(KelliPhotoWebApplicationFactory factory)
    {
        Environment.SetEnvironmentVariable("KELLIPHOTO_INTEGRATION_TEST", "1");

        // Unique in-memory DB per class — pass via config so parallel tests cannot clobber env.
        var dbName = "UserMgmt_" + Guid.NewGuid().ToString("N");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Testing:InMemoryDatabaseName"] = dbName
                });
            });
        });
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

        var seededAdmin = await userManager.FindByEmailAsync("admin@kelliphoto.com");
        Assert.NotNull(seededAdmin);

        var admins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
        foreach (var extraAdmin in admins.Where(a => a.Id != seededAdmin.Id))
        {
            await userManager.RemoveFromRoleAsync(extraAdmin, RoleNames.Admin);
        }

        var result = await svc.SetAdminRoleAsync(seededAdmin.Id, isAdmin: false);
        Assert.False(result.Succeeded);
        Assert.Contains("last administrator", result.Errors.First().Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetLockoutAsync_CannotLockSelf()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var admin = await userManager.FindByEmailAsync("admin@kelliphoto.com");
        Assert.NotNull(admin);

        var result = await svc.SetLockoutAsync(admin.Id, locked: true, currentUserId: admin.Id);
        Assert.False(result.Succeeded);
        Assert.Contains("your own", result.Errors.First().Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetAdminRoleAsync_CannotDemoteSelf()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var email = $"admin2-{Guid.NewGuid():N}@test.local";
        var createResult = await svc.CreateUserAsync(email, "test123", isAdmin: true);
        Assert.True(createResult.Succeeded);

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var result = await svc.SetAdminRoleAsync(user.Id, isAdmin: false, currentUserId: user.Id);
        Assert.False(result.Succeeded);
        Assert.Contains("your own", result.Errors.First().Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteUserAsync_CannotDeleteSelf()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var email = $"delete-self-{Guid.NewGuid():N}@test.local";
        var createResult = await svc.CreateUserAsync(email, "test123", isAdmin: false);
        Assert.True(createResult.Succeeded);

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var result = await svc.DeleteUserAsync(user.Id, currentUserId: user.Id);
        Assert.False(result.Succeeded);
        Assert.Contains("your own", result.Errors.First().Description, StringComparison.OrdinalIgnoreCase);
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

[CollectionDefinition("UserManagement", DisableParallelization = true)]
public class UserManagementCollection
{
}
