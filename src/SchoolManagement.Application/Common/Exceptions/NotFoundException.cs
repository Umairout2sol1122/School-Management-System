namespace SchoolManagement.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key ({key}) was not found.")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}
