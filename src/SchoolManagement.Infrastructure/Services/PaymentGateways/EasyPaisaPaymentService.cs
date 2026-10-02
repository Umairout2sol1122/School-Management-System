using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.Services.PaymentGateways;

public class EasyPaisaPaymentService : IPaymentGatewayService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EasyPaisaPaymentService> _logger;

    public PaymentGateway GatewayType => PaymentGateway.EasyPaisa;

    public EasyPaisaPaymentService(IConfiguration config, ILogger<EasyPaisaPaymentService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            string storeId = _config["PaymentGateways:EasyPaisa:StoreId"] ?? "12345";
            string checkoutUrl = _config["PaymentGateways:EasyPaisa:CheckoutUrl"] ?? "https://easypaisa.com.pk/easypay/Index.jsf";

            string orderId = $"EP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
            string redirectCheckout = $"{checkoutUrl}?storeId={storeId}&orderId={orderId}&transactionAmount={request.Amount:F2}&mobileAccountNo={request.CustomerPhone}&emailAddress={request.CustomerEmail}&postBackURL={Uri.EscapeDataString(request.ReturnUrl)}";

            return Task.FromResult(new PaymentInitiationResult(
                Success: true,
                CheckoutUrl: redirectCheckout,
                TransactionId: orderId,
                ClientSecret: orderId,
                Message: "EasyPaisa checkout initiated successfully."
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EasyPaisa initiation failed.");
            return Task.FromResult(new PaymentInitiationResult(
                Success: false,
                CheckoutUrl: null,
                TransactionId: null,
                ClientSecret: null,
                Message: ex.Message
            ));
        }
    }

    public Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing EasyPaisa webhook callback.");
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            string? status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            string? orderId = root.TryGetProperty("orderId", out var ord) ? ord.GetString() : null;
            string? transactionId = root.TryGetProperty("transactionId", out var txn) ? txn.GetString() : orderId;
            decimal amountPaid = 0m;
            if (root.TryGetProperty("transactionAmount", out var amt))
            {
                if (amt.ValueKind == JsonValueKind.Number) amountPaid = amt.GetDecimal();
                else if (decimal.TryParse(amt.GetString(), out decimal p)) amountPaid = p;
            }
            else if (root.TryGetProperty("amount", out var amt2))
            {
                if (amt2.ValueKind == JsonValueKind.Number) amountPaid = amt2.GetDecimal();
                else if (decimal.TryParse(amt2.GetString(), out decimal p2)) amountPaid = p2;
            }

            string? idempotencyKey = root.TryGetProperty("idempotencyKey", out var idemp) ? idemp.GetString() : orderId;

            bool isPaid = string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(status, "0000", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new WebhookProcessingResult(
                Success: true,
                IsPaid: isPaid,
                TransactionId: transactionId,
                IdempotencyKey: idempotencyKey,
                AmountPaid: amountPaid,
                RawResponse: payload,
                ErrorMessage: isPaid ? null : $"EasyPaisa status indicates incomplete payment: {status}"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing EasyPaisa webhook.");
            return Task.FromResult(new WebhookProcessingResult(
                Success: false,
                IsPaid: false,
                TransactionId: null,
                IdempotencyKey: null,
                AmountPaid: 0,
                RawResponse: payload,
                ErrorMessage: ex.Message
            ));
        }
    }
}
