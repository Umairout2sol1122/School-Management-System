namespace SchoolManagement.Application.Common.Models;

public record ChangePasswordRequestDto(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);

public record ForgotPasswordRequestDto(
    string Email
);

public record ResetPasswordRequestDto(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword
);

public record UpdateProfileRequestDto(
    string FirstName,
    string LastName,
    string? PhoneNumber
);
