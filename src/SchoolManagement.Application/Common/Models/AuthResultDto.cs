namespace SchoolManagement.Application.Common.Models;

public record AuthResultDto(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAtUtc,
    UserDto? User,
    string? ErrorMessage
)
{
    public static AuthResultDto Succeeded(string accessToken, string refreshToken, DateTime expiresAtUtc, UserDto user) =>
        new(true, accessToken, refreshToken, expiresAtUtc, user, null);

    public static AuthResultDto Failed(string errorMessage) =>
        new(false, null, null, null, null, errorMessage);
}
