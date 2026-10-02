using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.Services.PaymentGateways;

public class JazzCashPaymentService : IPaymentGatewayService
{
    private readonly IConfiguration _config;
    private readonly ILogger<JazzCashPaymentService> _logger;

    public PaymentGateway GatewayType => PaymentGateway.JazzCash;

    public JazzCashPaymentService(IConfiguration config, ILogger<JazzCashPaymentService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            string merchantId = _config["PaymentGateways:JazzCash:MerchantId"] ?? "MC12345";
            string password = _config["PaymentGateways:JazzCash:Password"] ?? "pass123";
            string integritySalt = _config["PaymentGateways:JazzCash:IntegritySalt"] ?? "salt123456";
            string checkoutUrl = _config["PaymentGateways:JazzCash:CheckoutUrl"] ?? "https://sandbox.jazzcash.com.pk/CustomerPortal/transactionmanagement/merchantform/";

            string txnRef = $"JC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
            string expiry = DateTime.UtcNow.AddHours(2).ToString("yyyyMMdd HHmmss");

            // Format amount in paisas (e.g. 5000 PKR = 500000 paisas)
            string amountFormatted = ((int)(request.Amount * 100)).ToString();

            // Sorted string for integrity hash calculation (Standard JazzCash HMAC-SHA256)
            string rawData = $"{integritySalt}&{amountFormatted}&PKR&{expiry}&{merchantId}&{password}&{txnRef}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(integritySalt));
            string secureHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData)));

            string redirectCheckout = $"{checkoutUrl}?pp_MerchantID={merchantId}&pp_TxnRefNo={txnRef}&pp_Amount={amountFormatted}&pp_TxnCurrency=PKR&pp_SecureHash={secureHash}&pp_ReturnURL={Uri.EscapeDataString(request.ReturnUrl)}";

            return Task.FromResult(new PaymentInitiationResult(
                Success: true,
                CheckoutUrl: redirectCheckout,
                TransactionId: txnRef,
                ClientSecret: secureHash,
                Message: "JazzCash payment checkout generated successfully."
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JazzCash checkout initiation failed.");
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
            _logger.LogInformation("Processing JazzCash webhook callback.");
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            string? responseCode = root.TryGetProperty("pp_ResponseCode", out var code) ? code.GetString() : null;
            string? txnRef = root.TryGetProperty("pp_TxnRefNo", out var refProp) ? refProp.GetString() : null;
            string? amountStr = root.TryGetProperty("pp_Amount", out var amtProp) ? amtProp.GetString() : "0";
            string? idempotencyKey = root.TryGetProperty("idempotencyKey", out var idemp) ? idemp.GetString() : txnRef;

            bool isPaid = responseCode == "000" || responseCode == "121"; // 000 is JazzCash Success
            decimal amountPaid = decimal.TryParse(amountStr, out decimal paisas) ? paisas / 100m : 0m;

            return Task.FromResult(new WebhookProcessingResult(
                Success: true,
                IsPaid: isPaid,
                TransactionId: txnRef,
                IdempotencyKey: idempotencyKey,
                AmountPaid: amountPaid,
                RawResponse: payload,
                ErrorMessage: isPaid ? null : $"JazzCash transaction rejected with response code: {responseCode}"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing JazzCash webhook payload.");
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
