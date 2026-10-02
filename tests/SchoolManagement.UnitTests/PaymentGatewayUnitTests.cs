using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Services.PaymentGateways;
using Xunit;

namespace SchoolManagement.UnitTests;

public class PaymentGatewayUnitTests
{
    private readonly IConfiguration _configuration;

    public PaymentGatewayUnitTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"PaymentGateways:JazzCash:MerchantId", "TEST_JAZZ_MERCHANT"},
            {"PaymentGateways:JazzCash:Password", "TEST_JAZZ_PW"},
            {"PaymentGateways:JazzCash:IntegritySalt", "TEST_SALT_XYZ123"},
            {"PaymentGateways:JazzCash:CheckoutUrl", "https://sandbox.jazzcash.com.pk/CustomerPortal/transactionmanagement/merchantform/"},
            {"PaymentGateways:EasyPaisa:StoreId", "TEST_EP_STORE"},
            {"PaymentGateways:EasyPaisa:CheckoutUrl", "https://easypaisa.com.pk/easypay/Index.jsf"},
            {"PaymentGateways:Stripe:SecretKey", "sk_test_fake_stripe_key_123"},
            {"PaymentGateways:Stripe:CheckoutUrl", "https://checkout.stripe.com/pay/"},
            {"PaymentGateways:Stripe:WebhookSecret", "whsec_fake_webhook_secret_123"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task JazzCashPaymentService_InitiatePayment_GeneratesCheckoutUrlAndTransactionId()
    {
        // Arrange
        var service = new JazzCashPaymentService(_configuration, NullLogger<JazzCashPaymentService>.Instance);
        var request = new PaymentInitiationRequest(
            InvoiceId: Guid.NewGuid(),
            InvoiceNumber: "INV-2026-001",
            Amount: 15000.50m,
            Currency: "PKR",
            Description: "Tuition Fee",
            CustomerName: "Ali Khan",
            CustomerEmail: "parent@school.pk",
            CustomerPhone: "03001234567",
            ReturnUrl: "https://school.local/fees",
            CancelUrl: "https://school.local/fees",
            IdempotencyKey: "IDEM-001"
        );

        // Act
        var result = await service.InitiatePaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TransactionId.Should().StartWith("JC-");
        result.CheckoutUrl.Should().StartWith("https://sandbox.jazzcash.com.pk/");
        result.ClientSecret.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task JazzCashPaymentService_ProcessWebhook_SuccessfulPayload_ReturnsIsPaidTrue()
    {
        // Arrange
        var service = new JazzCashPaymentService(_configuration, NullLogger<JazzCashPaymentService>.Instance);
        string payload = """
        {
            "pp_ResponseCode": "000",
            "pp_ResponseMessage": "Transaction successful",
            "pp_TxnRefNo": "T20261002010101",
            "pp_BillReference": "JC-INV-2026-001-1234",
            "pp_Amount": "1500050",
            "idempotencyKey": "IDEM-001"
        }
        """;

        // Act
        var result = await service.ProcessWebhookAsync(payload, new Dictionary<string, string>());

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.IsPaid.Should().BeTrue();
        result.TransactionId.Should().Be("T20261002010101");
        result.AmountPaid.Should().Be(15000.50m);
        result.IdempotencyKey.Should().Be("IDEM-001");
    }

    [Fact]
    public async Task EasyPaisaPaymentService_InitiatePayment_GeneratesCheckoutUrlAndOrderId()
    {
        // Arrange
        var service = new EasyPaisaPaymentService(_configuration, NullLogger<EasyPaisaPaymentService>.Instance);
        var request = new PaymentInitiationRequest(
            InvoiceId: Guid.NewGuid(),
            InvoiceNumber: "INV-2026-002",
            Amount: 8500m,
            Currency: "PKR",
            Description: "Lab Fee",
            CustomerName: "Fatima Ahmed",
            CustomerEmail: "fatima@school.pk",
            CustomerPhone: "03451234567",
            ReturnUrl: "https://school.local/fees",
            CancelUrl: "https://school.local/fees",
            IdempotencyKey: "IDEM-002"
        );

        // Act
        var result = await service.InitiatePaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TransactionId.Should().StartWith("EP-");
        result.CheckoutUrl.Should().Contain("storeId=TEST_EP_STORE");
        result.CheckoutUrl.Should().Contain("transactionAmount=8500.00");
    }

    [Fact]
    public async Task EasyPaisaPaymentService_ProcessWebhook_SuccessPayload_ReturnsIsPaidTrue()
    {
        // Arrange
        var service = new EasyPaisaPaymentService(_configuration, NullLogger<EasyPaisaPaymentService>.Instance);
        string payload = """
        {
            "orderRefNum": "EP-INV-2026-002-999",
            "transactionId": "TXN-EP-887766",
            "status": "0000",
            "amount": "8500.00",
            "idempotencyKey": "IDEM-002"
        }
        """;

        // Act
        var result = await service.ProcessWebhookAsync(payload, new Dictionary<string, string>());

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.IsPaid.Should().BeTrue();
        result.TransactionId.Should().Be("TXN-EP-887766");
        result.AmountPaid.Should().Be(8500m);
    }

    [Fact]
    public async Task StripePaymentService_InitiatePayment_ReturnsCheckoutUrl()
    {
        // Arrange
        var service = new StripePaymentService(_configuration, NullLogger<StripePaymentService>.Instance);
        var request = new PaymentInitiationRequest(
            InvoiceId: Guid.NewGuid(),
            InvoiceNumber: "INV-2026-003",
            Amount: 25000m,
            Currency: "PKR",
            Description: "Tuition Fee",
            CustomerName: "Zainab Bibi",
            CustomerEmail: "parent@citizenscholars.edu.pk",
            CustomerPhone: "03211234567",
            ReturnUrl: "https://school.local/fees?status=success",
            CancelUrl: "https://school.local/fees?status=cancel",
            IdempotencyKey: "IDEM-003"
        );

        // Act
        var result = await service.InitiatePaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CheckoutUrl.Should().NotBeNullOrWhiteSpace();
        result.CheckoutUrl.Should().Contain("stripe.com");
        result.TransactionId.Should().StartWith("cs_test_");
    }

    [Fact]
    public async Task StripePaymentService_ProcessWebhook_CheckoutSessionCompleted_ReturnsIsPaidTrue()
    {
        // Arrange
        var service = new StripePaymentService(_configuration, NullLogger<StripePaymentService>.Instance);
        string payload = """
        {
            "type": "checkout.session.completed",
            "data": {
                "object": {
                    "id": "cs_test_stripe_abc123",
                    "payment_status": "paid",
                    "amount_total": 2500000,
                    "metadata": {
                        "idempotencyKey": "IDEM-003"
                    }
                }
            }
        }
        """;

        // Act
        var result = await service.ProcessWebhookAsync(payload, new Dictionary<string, string>());

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.IsPaid.Should().BeTrue();
        result.TransactionId.Should().Be("cs_test_stripe_abc123");
        result.AmountPaid.Should().Be(25000m);
        result.IdempotencyKey.Should().Be("IDEM-003");
    }

    [Fact]
    public void PaymentGatewayResolver_ResolvesMatchingServiceForEachGateway()
    {
        // Arrange
        var jazzService = new JazzCashPaymentService(_configuration, NullLogger<JazzCashPaymentService>.Instance);
        var epService = new EasyPaisaPaymentService(_configuration, NullLogger<EasyPaisaPaymentService>.Instance);
        var stripeService = new StripePaymentService(_configuration, NullLogger<StripePaymentService>.Instance);

        var resolver = new PaymentGatewayResolver(new IPaymentGatewayService[]
        {
            jazzService,
            epService,
            stripeService
        });

        // Act & Assert
        resolver.GetService(PaymentGateway.JazzCash).Should().BeSameAs(jazzService);
        resolver.GetService(PaymentGateway.EasyPaisa).Should().BeSameAs(epService);
        resolver.GetService(PaymentGateway.Stripe).Should().BeSameAs(stripeService);
    }
}
