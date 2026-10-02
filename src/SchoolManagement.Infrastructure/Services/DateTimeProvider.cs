using SchoolManagement.Application.Common.Interfaces;

namespace SchoolManagement.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
