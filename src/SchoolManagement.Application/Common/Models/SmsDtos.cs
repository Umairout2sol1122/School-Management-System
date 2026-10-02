namespace SchoolManagement.Application.Common.Models;

public record SmsMessage(
    string RecipientPhoneNumber,
    string Text,
    string? SenderMask = null
);

public record SmsDispatchResult(
    bool Success,
    int DeliveredCount,
    int FailedCount,
    string? MessageId,
    string? ErrorMessage
);

public record SmsBalanceResult(
    bool Success,
    decimal BalanceCredits,
    string Currency,
    bool IsLowBalance,
    string? Message
);
