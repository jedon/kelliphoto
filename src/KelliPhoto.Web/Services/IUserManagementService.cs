using Microsoft.AspNetCore.Identity;

namespace KelliPhoto.Web.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync();
    Task<IdentityResult> CreateUserAsync(string email, string password, bool isAdmin);
    Task<IdentityResult> SetAdminRoleAsync(string userId, bool isAdmin, string? currentUserId = null);
    Task<IdentityResult> SetLockoutAsync(string userId, bool locked);
    Task<IdentityResult> DeleteUserAsync(string userId, string? currentUserId = null);
}

public class UserSummaryDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string UserName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public bool IsLockedOut { get; set; }
    public bool EmailConfirmed { get; set; }
}

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string User = "User";
}
