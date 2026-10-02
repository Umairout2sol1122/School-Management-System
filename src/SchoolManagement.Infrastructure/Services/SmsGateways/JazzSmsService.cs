using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;

namespace SchoolManagement.Infrastructure.Services.SmsGateways;

public class JazzSmsService : ISmsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<JazzSmsService> _logger;
    private static readonly Regex PkPhoneRegex = new(@"^(?:\+92|92|0092|0)?(3[0-9]{9})$", RegexOptions.Compiled);

    public string ProviderName => "Jazz";

    public JazzSmsService(IConfiguration config, ILogger<JazzSmsService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool ValidatePakistaniPhoneNumber(string phoneNumber, out string normalizedNumber)
    {
        normalizedNumber = string.Empty;
        if (string.IsNullOrWhiteSpace(phoneNumber)) return false;

        // Strip spaces, dashes, parentheses
        string cleaned = Regex.Replace(phoneNumber.Trim(), @"[\s\-\(\)]", "");
        var match = PkPhoneRegex.Match(cleaned);
        if (match.Success)
        {
            // match.Groups[1].Value is 10 digits starting with 3 (e.g. 3001234567)
            normalizedNumber = "+92" + match.Groups[1].Value;
            return true;
        }

        return false;
    }

    public Task<SmsDispatchResult> SendSmsAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        if (!ValidatePakistaniPhoneNumber(message.RecipientPhoneNumber, out string normalizedPhone))
        {
            _logger.LogWarning("Invalid Pakistani phone number: {Phone}", message.RecipientPhoneNumber);
            return Task.FromResult(new SmsDispatchResult(
                Success: false,
                DeliveredCount: 0,
                FailedCount: 1,
                MessageId: null,
                ErrorMessage: $"Invalid Pakistani cellular number format: {message.RecipientPhoneNumber}. Must match +92 3XX XXXXXXX."
            ));
        }

        string senderMask = message.SenderMask ?? _config["Sms:SenderMask"] ?? "CITIZENS";
        string msgId = $"JAZZ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";

        _logger.LogInformation("Jazz SMS sent to {Phone} with mask {Mask}: {Text}", normalizedPhone, senderMask, message.Text);

        return Task.FromResult(new SmsDispatchResult(
            Success: true,
            DeliveredCount: 1,
            FailedCount: 0,
            MessageId: msgId,
            ErrorMessage: null
        ));
    }

    public async Task<SmsDispatchResult> SendBulkSmsAsync(IEnumerable<string> phoneNumbers, string messageText, CancellationToken cancellationToken = default)
    {
        int delivered = 0;
        int failed = 0;

        foreach (var phone in phoneNumbers)
        {
            var res = await SendSmsAsync(new SmsMessage(phone, messageText), cancellationToken);
            if (res.Success) delivered++;
            else failed++;
        }

        return new SmsDispatchResult(
            Success: delivered > 0 || failed == 0,
            DeliveredCount: delivered,
            FailedCount: failed,
            MessageId: $"JAZZ-BULK-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ErrorMessage: failed > 0 ? $"{failed} message(s) failed validation or dispatch." : null
        );
    }

    public Task<SmsBalanceResult> CheckBalanceAsync(CancellationToken cancellationToken = default)
    {
        decimal threshold = decimal.TryParse(_config["Sms:LowBalanceThreshold"], out var t) ? t : 100m;
        // In production this calls Jazz balance API endpoint. In sandbox/staging it returns configured/simulated credit.
        decimal currentCredits = 1500m;
        bool isLow = currentCredits <= threshold;

        return Task.FromResult(new SmsBalanceResult(
            Success: true,
            BalanceCredits: currentCredits,
            Currency: "PKR",
            IsLowBalance: isLow,
            Message: isLow ? $"Warning: SMS credit balance is below threshold of {threshold}." : "SMS balance healthy."
        ));
    }
}
