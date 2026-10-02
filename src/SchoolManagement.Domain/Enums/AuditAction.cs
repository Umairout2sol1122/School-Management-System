namespace SchoolManagement.Domain.Enums;

public enum AuditAction
{
    Login = 1,
    Logout = 2,
    FailedLogin = 3,
    PasswordReset = 4,
    Create = 5,
    Update = 6,
    Delete = 7,
    SyncPush = 8,
    SyncPull = 9,
    PermissionChange = 10,
    RoleChange = 11,
    SecurityViolation = 12,
    View = 13,
    Export = 14
}
