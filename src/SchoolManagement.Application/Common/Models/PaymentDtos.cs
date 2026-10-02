using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record PaymentInitiationRequest(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal Amount,
    string Currency,
    string Description,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ReturnUrl,
    string CancelUrl,
    string IdempotencyKey
);

public record PaymentInitiationResult(
    bool Success,
    string? CheckoutUrl,
    string? TransactionId,
    string? ClientSecret,
    string? Message
);

public record WebhookProcessingResult(
    bool Success,
    bool IsPaid,
    string? TransactionId,
    string? IdempotencyKey,
    decimal AmountPaid,
    string? RawResponse,
    string? ErrorMessage
);

public record PayOnlineRequest(
    PaymentGateway Gateway,
    decimal? Amount,
    string? IdempotencyKey,
    string? ReturnUrl,
    string? CancelUrl
);
