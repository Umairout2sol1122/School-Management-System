using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.Services.PaymentGateways;

public class StripePaymentService : IPaymentGatewayService
{
    private readonly IConfiguration _config;
    private readonly ILogger<StripePaymentService> _logger;

    public PaymentGateway GatewayType => PaymentGateway.Stripe;

    public StripePaymentService(IConfiguration config, ILogger<StripePaymentService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            string secretKey = _config["PaymentGateways:Stripe:SecretKey"] ?? "sk_test_mock_123456789";
            string checkoutUrl = _config["PaymentGateways:Stripe:CheckoutUrl"] ?? "https://checkout.stripe.com/pay/";

            string sessionId = $"cs_test_{Guid.NewGuid():N}";
            string clientSecret = $"pi_{Guid.NewGuid():N}_secret_{Guid.NewGuid():N}";
            string redirectUrl = $"{checkoutUrl}{sessionId}";

            return Task.FromResult(new PaymentInitiationResult(
                Success: true,
                CheckoutUrl: redirectUrl,
                TransactionId: sessionId,
                ClientSecret: clientSecret,
                Message: "Stripe checkout session initialized successfully."
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe payment initiation failed.");
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
            _logger.LogInformation("Processing Stripe webhook event.");
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            string? eventType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            string? transactionId = null;
            decimal amountPaid = 0;
            string? idempotencyKey = null;
            bool isPaid = false;

            if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("object", out var objProp))
            {
                transactionId = objProp.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if ((objProp.TryGetProperty("amount_total", out var totProp) && totProp.TryGetInt64(out long totalCents)) ||
                    (objProp.TryGetProperty("amount", out var amtProp) && amtProp.TryGetInt64(out totalCents)))
                {
                    amountPaid = totalCents / 100m;
                }
                if (objProp.TryGetProperty("client_reference_id", out var clientRef))
                {
                    idempotencyKey = clientRef.GetString();
                }
                else if (objProp.TryGetProperty("metadata", out var metaProp) && metaProp.TryGetProperty("idempotencyKey", out var idempMeta))
                {
                    idempotencyKey = idempMeta.GetString();
                }
            }

            if (eventType == "checkout.session.completed" || eventType == "payment_intent.succeeded")
            {
                isPaid = true;
            }

            return Task.FromResult(new WebhookProcessingResult(
                Success: true,
                IsPaid: isPaid,
                TransactionId: transactionId,
                IdempotencyKey: idempotencyKey ?? transactionId,
                AmountPaid: amountPaid,
                RawResponse: payload,
                ErrorMessage: isPaid ? null : $"Stripe event '{eventType}' does not indicate payment success."
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook.");
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
