using FluentAssertions;
using SchoolManagement.Infrastructure.Security;
using Xunit;

namespace SchoolManagement.UnitTests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ShouldReturnNonEmptyHashAndSalt()
    {
        // Act
        var (hash, salt) = _hasher.HashPassword("SuperSecret2026!");

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        salt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void HashPassword_SamePasswordHashedTwice_ShouldProduceDifferentSaltsAndHashes()
    {
        // Act
        var result1 = _hasher.HashPassword("SamePassword123!");
        var result2 = _hasher.HashPassword("SamePassword123!");

        // Assert
        result1.Salt.Should().NotBe(result2.Salt);
        result1.Hash.Should().NotBe(result2.Hash);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        const string password = "MyP@ssword9988!";
        var (hash, salt) = _hasher.HashPassword(password);

        // Act
        bool isValid = _hasher.VerifyPassword(password, hash, salt);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var (hash, salt) = _hasher.HashPassword("CorrectPassword!");

        // Act
        bool isValid = _hasher.VerifyPassword("WrongPassword!", hash, salt);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "somehash", "somesalt")]
    [InlineData("password", "", "somesalt")]
    [InlineData("password", "somehash", "")]
    [InlineData("password", "invalid_base64_hash", "invalid_base64_salt")]
    public void VerifyPassword_WithInvalidInputs_ShouldReturnFalse(string password, string hash, string salt)
    {
        // Act
        bool isValid = _hasher.VerifyPassword(password, hash, salt);

        // Assert
        isValid.Should().BeFalse();
    }
}
