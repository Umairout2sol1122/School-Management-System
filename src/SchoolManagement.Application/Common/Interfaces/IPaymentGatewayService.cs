using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Interfaces;

public interface IPaymentGatewayService
{
    PaymentGateway GatewayType { get; }
    Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default);
    Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default);
}

public interface IPaymentGatewayResolver
{
    IPaymentGatewayService GetService(PaymentGateway gateway);
}
