using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Infrastructure.Services.SmsGateways;

public class SmsNotificationService : ISmsNotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ILogger<SmsNotificationService> _logger;

    public SmsNotificationService(
        IApplicationDbContext context,
        ISmsService smsService,
        ILogger<SmsNotificationService> logger)
    {
        _context = context;
        _smsService = smsService;
        _logger = logger;
    }

    private async Task<string?> GetPrimaryGuardianOrStudentPhoneAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var sg = await _context.StudentGuardians
            .Include(x => x.Guardian)
            .Where(x => x.StudentId == studentId && x.Guardian != null && !x.Guardian.IsDeleted)
            .OrderByDescending(x => x.IsPrimaryContact)
            .FirstOrDefaultAsync(cancellationToken);

        if (sg?.Guardian != null && !string.IsNullOrWhiteSpace(sg.Guardian.Phone))
        {
            return sg.Guardian.Phone.Trim();
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId && !s.IsDeleted, cancellationToken);

        return student?.EmergencyContactPhone?.Trim();
    }

    public async Task SendAbsenceAlertAsync(Guid organizationId, Guid studentId, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == studentId && s.OrganizationId == organizationId && !s.IsDeleted, cancellationToken);
            if (student == null) return;

            string? phone = await GetPrimaryGuardianOrStudentPhoneAsync(studentId, cancellationToken);
            if (string.IsNullOrWhiteSpace(phone))
            {
                _logger.LogWarning("No phone number found for student {StudentId} to send absence alert.", studentId);
                return;
            }

            string text = $"Dear Parent, your ward {student.FullName} (Roll/Adm: {student.RollNumber ?? student.AdmissionNumber}) was marked ABSENT on {date:dd-MMM-yyyy}. The Citizens Academy.";
            var dispatchResult = await _smsService.SendSmsAsync(new SmsMessage(phone, text), cancellationToken);

            var log = new CommunicationLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = student.BranchId,
                Title = $"Absence Alert: {student.FullName}",
                Message = text,
                Channel = "SMS",
                TargetAudience = "Parents",
                RecipientCount = 1,
                Status = dispatchResult.Success ? "Delivered" : "Failed",
                FailureReason = dispatchResult.ErrorMessage,
                SentByUserEmail = "system.sms@school.edu"
            };

            _context.CommunicationLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send absence alert SMS for student {StudentId}.", studentId);
        }
    }

    public async Task SendPaymentConfirmationAsync(Guid organizationId, Guid studentId, string receiptNumber, decimal amount, CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == studentId && s.OrganizationId == organizationId && !s.IsDeleted, cancellationToken);
            if (student == null) return;

            string? phone = await GetPrimaryGuardianOrStudentPhoneAsync(studentId, cancellationToken);
            if (string.IsNullOrWhiteSpace(phone)) return;

            string text = $"Dear Parent, Fee payment of PKR {amount:N0} for student {student.FullName} was received successfully under Receipt #{receiptNumber}. Thank you.";
            var dispatchResult = await _smsService.SendSmsAsync(new SmsMessage(phone, text), cancellationToken);

            var log = new CommunicationLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = student.BranchId,
                Title = $"Fee Payment Confirmation: {receiptNumber}",
                Message = text,
                Channel = "SMS",
                TargetAudience = "Parents",
                RecipientCount = 1,
                Status = dispatchResult.Success ? "Delivered" : "Failed",
                FailureReason = dispatchResult.ErrorMessage,
                SentByUserEmail = "system.sms@school.edu"
            };

            _context.CommunicationLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send fee payment confirmation SMS for student {StudentId}.", studentId);
        }
    }

    public async Task SendFeeReminderAsync(Guid organizationId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var invoice = await _context.FeeInvoices
                .Include(i => i.Student)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.OrganizationId == organizationId && !i.IsDeleted, cancellationToken);
            if (invoice == null || invoice.Student == null) return;

            string? phone = await GetPrimaryGuardianOrStudentPhoneAsync(invoice.StudentId, cancellationToken);
            if (string.IsNullOrWhiteSpace(phone)) return;

            decimal balance = invoice.TotalAmount - invoice.PaidAmount;
            if (balance <= 0) return;

            string dueStatus = invoice.DueDate.Date < DateTime.UtcNow.Date ? "OVERDUE" : $"Due Date: {invoice.DueDate:dd-MMM-yyyy}";
            string text = $"Dear Parent, Fee Reminder: Invoice #{invoice.InvoiceNumber} for {invoice.Student.FullName} has a pending balance of PKR {balance:N0}. ({dueStatus}). Kindly settle promptly.";

            var dispatchResult = await _smsService.SendSmsAsync(new SmsMessage(phone, text), cancellationToken);

            var log = new CommunicationLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = invoice.BranchId ?? invoice.Student.BranchId,
                Title = $"Fee Reminder: {invoice.InvoiceNumber}",
                Message = text,
                Channel = "SMS",
                TargetAudience = "Parents",
                RecipientCount = 1,
                Status = dispatchResult.Success ? "Delivered" : "Failed",
                FailureReason = dispatchResult.ErrorMessage,
                SentByUserEmail = "system.sms@school.edu"
            };

            _context.CommunicationLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send fee reminder SMS for invoice {InvoiceId}.", invoiceId);
        }
    }

    public async Task SendResultPublishedAsync(Guid organizationId, Guid studentId, string examTitle, CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == studentId && s.OrganizationId == organizationId && !s.IsDeleted, cancellationToken);
            if (student == null) return;

            string? phone = await GetPrimaryGuardianOrStudentPhoneAsync(studentId, cancellationToken);
            if (string.IsNullOrWhiteSpace(phone)) return;

            string text = $"Dear Parent, Examination Results for '{examTitle}' have been published for {student.FullName}. Please login to Parent Portal to view detailed report card.";
            var dispatchResult = await _smsService.SendSmsAsync(new SmsMessage(phone, text), cancellationToken);

            var log = new CommunicationLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = student.BranchId,
                Title = $"Result Published: {examTitle}",
                Message = text,
                Channel = "SMS",
                TargetAudience = "Parents",
                RecipientCount = 1,
                Status = dispatchResult.Success ? "Delivered" : "Failed",
                FailureReason = dispatchResult.ErrorMessage,
                SentByUserEmail = "system.sms@school.edu"
            };

            _context.CommunicationLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send result published SMS for student {StudentId}.", studentId);
        }
    }

    public async Task SendAdmissionStatusChangeAsync(Guid organizationId, Guid studentId, string newStatus, CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == studentId && s.OrganizationId == organizationId && !s.IsDeleted, cancellationToken);
            if (student == null) return;

            string? phone = await GetPrimaryGuardianOrStudentPhoneAsync(studentId, cancellationToken);
            if (string.IsNullOrWhiteSpace(phone)) return;

            string text = $"Dear Applicant, admission status for candidate {student.FullName} (App #{student.AdmissionNumber}) has been updated to: {newStatus}. Citizens School Administration.";
            var dispatchResult = await _smsService.SendSmsAsync(new SmsMessage(phone, text), cancellationToken);

            var log = new CommunicationLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = student.BranchId,
                Title = $"Admission Status: {newStatus}",
                Message = text,
                Channel = "SMS",
                TargetAudience = "Parents",
                RecipientCount = 1,
                Status = dispatchResult.Success ? "Delivered" : "Failed",
                FailureReason = dispatchResult.ErrorMessage,
                SentByUserEmail = "system.sms@school.edu"
            };

            _context.CommunicationLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admission status SMS for student {StudentId}.", studentId);
        }
    }
}
