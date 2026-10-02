using SchoolManagement.Application.Common.Models;

namespace SchoolManagement.Client.Core.Auth;

public class ClientAuthState
{
    public bool IsAuthenticated => CurrentUser != null && !string.IsNullOrWhiteSpace(AccessToken);
    public UserDto? CurrentUser { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    public event Action? OnAuthStateChanged;

    public void SetAuthenticated(UserDto user, string accessToken, string refreshToken, DateTime expiresAtUtc)
    {
        CurrentUser = user;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAtUtc = expiresAtUtc;
        OnAuthStateChanged?.Invoke();
    }

    public void SetLoggedOut()
    {
        CurrentUser = null;
        AccessToken = null;
        RefreshToken = null;
        ExpiresAtUtc = null;
        OnAuthStateChanged?.Invoke();
    }

    public bool HasPermission(string permission)
    {
        if (CurrentUser == null)
            return false;

        if (CurrentUser.RoleType == Domain.Enums.UserRoleType.SuperAdmin)
            return true;

        return CurrentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasAnyPermission(params string[] permissions)
    {
        if (CurrentUser == null)
            return false;

        if (CurrentUser.RoleType == Domain.Enums.UserRoleType.SuperAdmin)
            return true;

        return permissions.Any(p => CurrentUser.Permissions.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    public bool HasAllPermissions(params string[] permissions)
    {
        if (CurrentUser == null)
            return false;

        if (CurrentUser.RoleType == Domain.Enums.UserRoleType.SuperAdmin)
            return true;

        return permissions.All(p => CurrentUser.Permissions.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    public bool IsInRole(Domain.Enums.UserRoleType role) => CurrentUser?.RoleType == role;
    public bool IsSuperAdmin => CurrentUser?.RoleType == Domain.Enums.UserRoleType.SuperAdmin;
    public bool IsOrganizationAdmin => CurrentUser?.RoleType == Domain.Enums.UserRoleType.OrganizationAdmin;
}
