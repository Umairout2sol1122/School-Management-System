using System.Security.Claims;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            string? id = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(id, out var parsedId) ? parsedId : null;
        }
    }

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public Guid? OrganizationId
    {
        get
        {
            string? tenant = User?.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(tenant, out var parsedId) ? parsedId : null;
        }
    }

    public Guid? BranchId
    {
        get
        {
            string? branch = User?.FindFirst("branch_id")?.Value;
            return Guid.TryParse(branch, out var parsedId) ? parsedId : null;
        }
    }

    public UserRoleType? RoleType
    {
        get
        {
            string? role = User?.FindFirst("role_type")?.Value;
            return Enum.TryParse<UserRoleType>(role, out var parsedRole) ? parsedRole : null;
        }
    }

    public bool IsSuperAdmin => RoleType == UserRoleType.SuperAdmin;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IEnumerable<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value) ?? Enumerable.Empty<string>();

    public bool HasPermission(string permission) =>
        IsSuperAdmin || Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
