using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.Services.PaymentGateways;

public class PaymentGatewayResolver : IPaymentGatewayResolver
{
    private readonly IEnumerable<IPaymentGatewayService> _gateways;

    public PaymentGatewayResolver(IEnumerable<IPaymentGatewayService> gateways)
    {
        _gateways = gateways;
    }

    public IPaymentGatewayService GetService(PaymentGateway gateway)
    {
        var service = _gateways.FirstOrDefault(g => g.GatewayType == gateway);
        if (service == null)
            throw new NotSupportedException($"Payment gateway '{gateway}' is not supported or configured.");

        return service;
    }
}
