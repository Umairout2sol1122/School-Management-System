namespace SchoolManagement.Application.Common.Exceptions;

public class CrossTenantAccessException : Exception
{
    public CrossTenantAccessException(Guid userTenantId, Guid attemptedTenantId)
        : base($"Security Violation: User from tenant '{userTenantId}' attempted unauthorized access to data belonging to tenant '{attemptedTenantId}'.")
    {
    }

    public CrossTenantAccessException(string message) : base(message)
    {
    }
}
