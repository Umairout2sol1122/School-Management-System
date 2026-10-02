namespace SchoolManagement.Application.Common.Models;

public record LoginRequestDto(
    string EmailOrUsername,
    string Password,
    string? OrganizationCode = null
);
