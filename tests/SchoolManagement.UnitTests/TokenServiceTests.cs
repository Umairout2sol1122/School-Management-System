using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Configuration;
using SchoolManagement.Infrastructure.Security;
using Xunit;

namespace SchoolManagement.UnitTests;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly JwtSettings _jwtSettings;

    public TokenServiceTests()
    {
        _jwtSettings = new JwtSettings
        {
            Secret = "Unit_Testing_Secret_Key_At_Least_32_Bytes_Long_2026!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 30,
            RefreshTokenExpiryDays = 7
        };
        _tokenService = new TokenService(_jwtSettings);
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeUserAndTenantClaims()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            Username = "schooladmin",
            Email = "admin@alpha.edu",
            RoleType = UserRoleType.OrganizationAdmin
        };

        var permissions = new[] { Permissions.StudentsView, Permissions.StudentsCreate };

        // Act
        string tokenString = _tokenService.GenerateAccessToken(user, permissions);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        jwt.Issuer.Should().Be(_jwtSettings.Issuer);
        jwt.Audiences.Should().Contain(_jwtSettings.Audience);

        jwt.Claims.Should().Contain(c => (c.Type == "nameid" || c.Type == ClaimTypes.NameIdentifier) && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => (c.Type == "email" || c.Type == ClaimTypes.Email) && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => (c.Type == "unique_name" || c.Type == ClaimTypes.Name) && c.Value == user.Username);
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == user.OrganizationId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "branch_id" && c.Value == user.BranchId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "role_type" && c.Value == user.RoleType.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "permission" && c.Value == Permissions.StudentsView);
        jwt.Claims.Should().Contain(c => c.Type == "permission" && c.Value == Permissions.StudentsCreate);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldProduceActiveTokenWithCorrectExpiry()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var token = _tokenService.GenerateRefreshToken(userId, "127.0.0.1");

        // Assert
        token.UserId.Should().Be(userId);
        token.Token.Should().NotBeNullOrWhiteSpace();
        token.IsActiveToken.Should().BeTrue();
        token.IsRevoked.Should().BeFalse();
        token.IsExpired.Should().BeFalse();
        token.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays), TimeSpan.FromSeconds(5));
    }
}
