using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Infrastructure.Services.SmsGateways;
using Xunit;

namespace SchoolManagement.UnitTests;

public class SmsGatewayUnitTests
{
    private readonly IConfiguration _config;

    public SmsGatewayUnitTests()
    {
        var settings = new Dictionary<string, string?>
        {
            {"Sms:Provider", "Jazz"},
            {"Sms:SenderMask", "CITIZENS"},
            {"Sms:LowBalanceThreshold", "200"},
            {"Sms:Jazz:ApiKey", "test_jazz_key_123"},
            {"Sms:Twilio:AccountSid", "AC_test_sid"},
            {"Sms:Twilio:AuthToken", "test_auth_token"},
            {"Sms:Twilio:FromNumber", "+12025550143"}
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Theory]
    [InlineData("03001234567", "+923001234567")]
    [InlineData("+92 300 1234567", "+923001234567")]
    [InlineData("0321-7654321", "+923217654321")]
    [InlineData("00923331234567", "+923331234567")]
    [InlineData("923451234567", "+923451234567")]
    [InlineData("(0301) 9876543", "+923019876543")]
    public void JazzSmsService_ValidatePakistaniPhoneNumber_ValidFormats_NormalizesCorrectly(string raw, string expected)
    {
        // Arrange
        var service = new JazzSmsService(_config, NullLogger<JazzSmsService>.Instance);

        // Act
        bool isValid = service.ValidatePakistaniPhoneNumber(raw, out string normalized);

        // Assert
        isValid.Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("02131234567")] // Landline Karachi (021)
    [InlineData("0512345678")]  // Landline Islamabad (051)
    [InlineData("030012345")]   // Too short (9 digits)
    [InlineData("030012345678")] // Too long (12 digits)
    [InlineData("04001234567")] // Invalid cellular code (starts with 4)
    [InlineData("ABC1234567")]  // Non-numeric
    [InlineData("")]            // Empty
    public void JazzSmsService_ValidatePakistaniPhoneNumber_InvalidFormats_ReturnsFalse(string raw)
    {
        // Arrange
        var service = new JazzSmsService(_config, NullLogger<JazzSmsService>.Instance);

        // Act
        bool isValid = service.ValidatePakistaniPhoneNumber(raw, out string normalized);

        // Assert
        isValid.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public async Task JazzSmsService_SendSmsAsync_ValidNumber_DispatchesSuccessfully()
    {
        // Arrange
        var service = new JazzSmsService(_config, NullLogger<JazzSmsService>.Instance);
        var msg = new SmsMessage("03001234567", "Fee payment received. Thank you.");

        // Act
        var result = await service.SendSmsAsync(msg);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.DeliveredCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        result.MessageId.Should().StartWith("JAZZ-");
    }

    [Fact]
    public async Task JazzSmsService_SendSmsAsync_InvalidNumber_FailsValidation()
    {
        // Arrange
        var service = new JazzSmsService(_config, NullLogger<JazzSmsService>.Instance);
        var msg = new SmsMessage("02135555555", "Absence alert");

        // Act
        var result = await service.SendSmsAsync(msg);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.DeliveredCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.ErrorMessage.Should().Contain("Invalid Pakistani cellular number format");
    }

    [Fact]
    public async Task JazzSmsService_SendBulkSmsAsync_MixOfNumbers_TracksDeliveredAndFailed()
    {
        // Arrange
        var service = new JazzSmsService(_config, NullLogger<JazzSmsService>.Instance);
        var numbers = new[] { "03001234567", "invalid_num", "03339876543", "0219999999" };

        // Act
        var result = await service.SendBulkSmsAsync(numbers, "School announcement notification");

        // Assert
        result.Should().NotBeNull();
        result.DeliveredCount.Should().Be(2);
        result.FailedCount.Should().Be(2);
        result.ErrorMessage.Should().Contain("2 message(s) failed validation or dispatch.");
    }

    [Fact]
    public async Task JazzSmsService_CheckBalanceAsync_ReturnsCreditsAndHealth()
    {
        // Arrange
        var service = new JazzSmsService(_config, NullLogger<JazzSmsService>.Instance);

        // Act
        var result = await service.CheckBalanceAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.BalanceCredits.Should().BeGreaterThan(0);
        result.Currency.Should().Be("PKR");
        result.IsLowBalance.Should().BeFalse(); // 1500 > 200
    }

    [Fact]
    public async Task TwilioSmsService_SendSmsAsync_ValidNumber_DispatchesSuccessfully()
    {
        // Arrange
        var service = new TwilioSmsService(_config, NullLogger<TwilioSmsService>.Instance);
        var msg = new SmsMessage("+923001234567", "Twilio test notification");

        // Act
        var result = await service.SendSmsAsync(msg);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.DeliveredCount.Should().Be(1);
        result.MessageId.Should().StartWith("SM");
    }
}
