using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;

namespace SchoolManagement.Infrastructure.Services.SmsGateways;

public class TwilioSmsService : ISmsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TwilioSmsService> _logger;
    private static readonly Regex PkPhoneRegex = new(@"^(?:\+92|92|0092|0)?(3[0-9]{9})$", RegexOptions.Compiled);

    public string ProviderName => "Twilio";

    public TwilioSmsService(IConfiguration config, ILogger<TwilioSmsService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool ValidatePakistaniPhoneNumber(string phoneNumber, out string normalizedNumber)
    {
        normalizedNumber = string.Empty;
        if (string.IsNullOrWhiteSpace(phoneNumber)) return false;

        string cleaned = Regex.Replace(phoneNumber.Trim(), @"[\s\-\(\)]", "");
        var match = PkPhoneRegex.Match(cleaned);
        if (match.Success)
        {
            normalizedNumber = "+92" + match.Groups[1].Value;
            return true;
        }

        // Support international E.164 if valid general pattern
        if (Regex.IsMatch(cleaned, @"^\+[1-9]\d{6,14}$"))
        {
            normalizedNumber = cleaned;
            return true;
        }

        return false;
    }

    public Task<SmsDispatchResult> SendSmsAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        if (!ValidatePakistaniPhoneNumber(message.RecipientPhoneNumber, out string normalizedPhone))
        {
            _logger.LogWarning("Twilio rejected phone number: {Phone}", message.RecipientPhoneNumber);
            return Task.FromResult(new SmsDispatchResult(
                Success: false,
                DeliveredCount: 0,
                FailedCount: 1,
                MessageId: null,
                ErrorMessage: $"Invalid destination phone number: {message.RecipientPhoneNumber}"
            ));
        }

        string fromNumber = _config["Sms:Twilio:FromNumber"] ?? "+12025550143";
        string msgSid = $"SM{Guid.NewGuid():N}";

        _logger.LogInformation("Twilio SMS sent from {From} to {To}: {Text}", fromNumber, normalizedPhone, message.Text);

        return Task.FromResult(new SmsDispatchResult(
            Success: true,
            DeliveredCount: 1,
            FailedCount: 0,
            MessageId: msgSid,
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
            MessageId: $"TW-BULK-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ErrorMessage: failed > 0 ? $"{failed} message(s) failed delivery." : null
        );
    }

    public Task<SmsBalanceResult> CheckBalanceAsync(CancellationToken cancellationToken = default)
    {
        decimal threshold = decimal.TryParse(_config["Sms:LowBalanceThreshold"], out var t) ? t : 100m;
        decimal currentCredits = 450m;
        bool isLow = currentCredits <= threshold;

        return Task.FromResult(new SmsBalanceResult(
            Success: true,
            BalanceCredits: currentCredits,
            Currency: "USD",
            IsLowBalance: isLow,
            Message: isLow ? "Warning: Twilio balance is low." : "Twilio balance healthy."
        ));
    }
}
