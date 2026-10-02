using SchoolManagement.Application.Common.Models;

namespace SchoolManagement.Application.Common.Interfaces;

public interface ISmsService
{
    string ProviderName { get; }
    Task<SmsDispatchResult> SendSmsAsync(SmsMessage message, CancellationToken cancellationToken = default);
    Task<SmsDispatchResult> SendBulkSmsAsync(IEnumerable<string> phoneNumbers, string messageText, CancellationToken cancellationToken = default);
    Task<SmsBalanceResult> CheckBalanceAsync(CancellationToken cancellationToken = default);
    bool ValidatePakistaniPhoneNumber(string phoneNumber, out string normalizedNumber);
}
