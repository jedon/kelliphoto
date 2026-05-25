using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KelliPhoto.Web.Services;

public class UserManagementService : IUserManagementService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserManagementService(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync()
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        var summaries = new List<UserSummaryDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            summaries.Add(new UserSummaryDto
            {
                Id = user.Id,
                Email = user.Email ?? "",
                UserName = user.UserName ?? "",
                IsAdmin = roles.Contains(RoleNames.Admin),
                IsLockedOut = await _userManager.IsLockedOutAsync(user),
                EmailConfirmed = user.EmailConfirmed
            });
        }

        return summaries;
    }

    public async Task<IdentityResult> CreateUserAsync(string email, string password, bool isAdmin)
    {
        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return result;

        result = await _userManager.AddToRoleAsync(user, RoleNames.User);
        if (!result.Succeeded)
            return result;

        if (isAdmin)
        {
            result = await _userManager.AddToRoleAsync(user, RoleNames.Admin);
            if (!result.Succeeded)
                return result;
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetAdminRoleAsync(string userId, bool isAdmin, string? currentUserId = null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, RoleNames.Admin);

        if (!isAdmin && isCurrentlyAdmin)
        {
            var adminCount = await CountUsersInRoleAsync(RoleNames.Admin);
            if (adminCount <= 1)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Description = "Cannot remove the last administrator."
                });
            }
        }

        if (isAdmin && !isCurrentlyAdmin)
            return await _userManager.AddToRoleAsync(user, RoleNames.Admin);

        if (!isAdmin && isCurrentlyAdmin)
            return await _userManager.RemoveFromRoleAsync(user, RoleNames.Admin);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetLockoutAsync(string userId, bool locked)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        if (locked)
        {
            return await _userManager.SetLockoutEndDateAsync(
                user, DateTimeOffset.UtcNow.AddYears(100));
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        return await _userManager.SetLockoutEndDateAsync(user, null);
    }

    public async Task<IdentityResult> DeleteUserAsync(string userId, string? currentUserId = null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        if (await _userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            var adminCount = await CountUsersInRoleAsync(RoleNames.Admin);
            if (adminCount <= 1)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Description = "Cannot delete the last administrator."
                });
            }
        }

        return await _userManager.DeleteAsync(user);
    }

    private async Task<int> CountUsersInRoleAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            return 0;

        var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
        return usersInRole.Count;
    }
}
