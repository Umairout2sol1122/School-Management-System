namespace SchoolManagement.Application.Common.Interfaces;

public interface ISmsNotificationService
{
    Task SendAbsenceAlertAsync(Guid organizationId, Guid studentId, DateTime date, CancellationToken cancellationToken = default);
    Task SendPaymentConfirmationAsync(Guid organizationId, Guid studentId, string receiptNumber, decimal amount, CancellationToken cancellationToken = default);
    Task SendFeeReminderAsync(Guid organizationId, Guid invoiceId, CancellationToken cancellationToken = default);
    Task SendResultPublishedAsync(Guid organizationId, Guid studentId, string examTitle, CancellationToken cancellationToken = default);
    Task SendAdmissionStatusChangeAsync(Guid organizationId, Guid studentId, string newStatus, CancellationToken cancellationToken = default);
}
