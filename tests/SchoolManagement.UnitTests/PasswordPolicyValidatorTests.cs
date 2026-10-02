using SchoolManagement.Application.Common.Security;
using Xunit;

namespace SchoolManagement.UnitTests;

public class PasswordPolicyValidatorTests
{
    [Theory]
    [InlineData("Pass@2026!")]
    [InlineData("SuperAdmin@2026!")]
    [InlineData("Strong#P4ssw0rd$")]
    [InlineData("Pak2Source#Safe1")]
    public void Validate_ValidStrongPasswords_ShouldSucceed(string password)
    {
        var (isValid, errorMessage) = PasswordPolicyValidator.Validate(password);

        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrWhitespace_ShouldFail(string? password)
    {
        var (isValid, errorMessage) = PasswordPolicyValidator.Validate(password);

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("cannot be empty", errorMessage);
    }

    [Theory]
    [InlineData("Short1!")]
    [InlineData("Ab1!")]
    public void Validate_LessThan8Characters_ShouldFail(string password)
    {
        var (isValid, errorMessage) = PasswordPolicyValidator.Validate(password);

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("at least 8 characters", errorMessage);
    }

    [Fact]
    public void Validate_MissingUppercase_ShouldFail()
    {
        var (isValid, errorMessage) = PasswordPolicyValidator.Validate("password123!");

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("uppercase", errorMessage);
    }

    [Fact]
    public void Validate_MissingLowercase_ShouldFail()
    {
        var (isValid, errorMessage) = PasswordPolicyValidator.Validate("PASSWORD123!");

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("lowercase", errorMessage);
    }

    [Fact]
    public void Validate_MissingDigit_ShouldFail()
    {
        var (isValid, errorMessage) = PasswordPolicyValidator.Validate("Password!@#");

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("digit", errorMessage);
    }

    [Fact]
    public void Validate_MissingSpecialCharacter_ShouldFail()
    {
        var (isValid, errorMessage) = PasswordPolicyValidator.Validate("Password123");

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("special character", errorMessage);
    }
}
