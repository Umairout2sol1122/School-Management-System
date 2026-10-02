using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeesController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ISmsNotificationService? _smsNotificationService;

    public FeesController(
        IApplicationDbContext context,
        IAuditService auditService,
        ISmsNotificationService? smsNotificationService = null)
    {
        _context = context;
        _auditService = auditService;
        _smsNotificationService = smsNotificationService;
    }

    private Guid GetTargetOrgId()
    {
        return CurrentUserService.IsSuperAdmin
            ? (TenantContext.OrganizationId ?? Guid.Empty)
            : (CurrentUserService.OrganizationId ?? Guid.Empty);
    }

    [HttpGet("invoices")]
    [RequirePermission(Permissions.FeesView)]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? branchId,
        [FromQuery] FeeStatus? status,
        [FromQuery] string? grade,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.FeeInvoices
            .Include(f => f.Student)
            .Include(f => f.Branch)
            .Where(f => f.OrganizationId == targetOrgId && !f.IsDeleted);

        // Branch Isolation: Principal can only view invoices within assigned branch
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(f => f.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        if (studentId.HasValue)
            query = query.Where(f => f.StudentId == studentId.Value);

        if (status.HasValue)
            query = query.Where(f => f.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(grade))
            query = query.Where(f => f.Student != null && f.Student.CurrentGrade == grade.Trim());

        if (fromDate.HasValue)
            query = query.Where(f => f.IssueDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(f => f.IssueDate <= toDate.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(f =>
                f.InvoiceNumber.ToLower().Contains(term) ||
                f.Title.ToLower().Contains(term) ||
                (f.Student != null && (
                    f.Student.FirstName.ToLower().Contains(term) ||
                    f.Student.LastName.ToLower().Contains(term) ||
                    f.Student.AdmissionNumber.ToLower().Contains(term)
                )));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var invoices = await query
            .OrderByDescending(f => f.IssueDate)
            .ThenByDescending(f => f.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        DateTime today = DateTime.UtcNow.Date;

        var items = invoices.Select(f => new FeeInvoiceSummaryDto(
            f.Id,
            f.OrganizationId,
            f.BranchId,
            f.Branch?.Name,
            f.StudentId,
            f.Student?.FullName ?? "Unknown Student",
            f.Student?.AdmissionNumber ?? "-",
            f.Student?.CurrentGrade ?? "-",
            f.Student?.Section,
            f.InvoiceNumber,
            f.Title,
            f.IssueDate,
            f.DueDate,
            f.TotalAmount,
            f.PaidAmount,
            f.Balance,
            f.Status,
            f.DueDate < today && f.Status != FeeStatus.Paid
        )).ToList();

        return ApiSuccess(new
        {
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("invoices/{id:guid}")]
    [RequirePermission(Permissions.FeesView)]
    public async Task<IActionResult> GetInvoiceById(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var invoice = await _context.FeeInvoices
            .Include(f => f.Student)
            .Include(f => f.Branch)
            .Include(f => f.Payments)
                .ThenInclude(p => p.ReceivedByUser)
            .FirstOrDefaultAsync(f => f.Id == id && f.OrganizationId == targetOrgId && !f.IsDeleted, cancellationToken);

        if (invoice == null)
            return ApiNotFound("Fee invoice not found.");

        // Branch Isolation check
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (invoice.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You do not have access to fee invoices outside your assigned branch.");
        }

        DateTime today = DateTime.UtcNow.Date;

        var payments = invoice.Payments
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new FeePaymentDetailDto(
                p.Id,
                p.FeeInvoiceId,
                p.ReceiptNumber,
                p.AmountPaid,
                p.PaymentDate,
                p.PaymentMethod,
                p.TransactionReference,
                p.ReceivedByUserId,
                p.ReceivedByUser?.FullName
            ))
            .ToList();

        var detail = new FeeInvoiceDetailDto(
            invoice.Id,
            invoice.OrganizationId,
            invoice.BranchId,
            invoice.Branch?.Name,
            invoice.StudentId,
            invoice.Student?.FullName ?? "Unknown Student",
            invoice.Student?.AdmissionNumber ?? "-",
            invoice.Student?.CurrentGrade ?? "-",
            invoice.Student?.Section,
            invoice.InvoiceNumber,
            invoice.Title,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.Balance,
            invoice.Status,
            invoice.DueDate < today && invoice.Status != FeeStatus.Paid,
            payments
        );

        return ApiSuccess(detail);
    }

    [HttpPost("invoices")]
    [RequirePermission(Permissions.FeesGenerateInvoices)]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateFeeInvoiceRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (request.TotalAmount <= 0)
            return ApiError("Invoice total amount must be greater than zero.");

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (student == null)
            return ApiNotFound("Student not found.");

        // Branch Isolation
        Guid branchId = student.BranchId ?? request.BranchId ?? Guid.Empty;
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (student.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot generate invoices for students belonging to another branch.");
            branchId = CurrentUserService.BranchId.Value;
        }

        string invoiceNum = $"INV-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var invoice = new FeeInvoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = branchId != Guid.Empty ? branchId : null,
            StudentId = request.StudentId,
            InvoiceNumber = invoiceNum,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Monthly Tuition Fee" : request.Title.Trim(),
            IssueDate = DateTime.UtcNow.Date,
            DueDate = request.DueDate.Date,
            TotalAmount = request.TotalAmount,
            PaidAmount = 0.0m,
            Status = FeeStatus.Unpaid
        };

        _context.FeeInvoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Fees", "FeeInvoice", invoice.Id.ToString(),
            $"Generated fee invoice '{invoice.InvoiceNumber}' for student '{student.FullName}' ({student.AdmissionNumber}) amount {invoice.TotalAmount:C}",
            organizationId: targetOrgId,
            branchId: invoice.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = invoice.Id, invoiceNumber = invoice.InvoiceNumber, totalAmount = invoice.TotalAmount }, "Fee invoice generated successfully.");
    }

    [HttpPost("invoices/bulk-generate")]
    [RequirePermission(Permissions.FeesGenerateInvoices)]
    public async Task<IActionResult> BulkGenerateInvoices([FromBody] BulkGenerateFeeInvoicesRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (request.Amount <= 0)
            return ApiError("Fee amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiError("Invoice title is required.");

        var query = _context.Students
            .Where(s => s.OrganizationId == targetOrgId && s.Status == StudentStatus.Active && !s.IsDeleted);

        // Branch Isolation
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(s => s.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (request.BranchId.HasValue)
        {
            query = query.Where(s => s.BranchId == request.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ClassGrade))
        {
            query = query.Where(s => s.CurrentGrade == request.ClassGrade.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Section))
        {
            query = query.Where(s => s.Section == request.Section.Trim());
        }

        var activeStudents = await query.ToListAsync(cancellationToken);
        if (activeStudents.Count == 0)
            return ApiError("No active students found matching the selected grade/section/branch criteria.");

        decimal discountRate = Math.Clamp(request.DiscountPercentage ?? 0m, 0m, 100m) / 100m;
        decimal finalAmount = Math.Round(request.Amount * (1m - discountRate), 2);

        var generatedInvoices = new List<FeeInvoice>();
        var invoiceNumbers = new List<string>();

        foreach (var student in activeStudents)
        {
            string invoiceNum = $"INV-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            invoiceNumbers.Add(invoiceNum);

            var invoice = new FeeInvoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = targetOrgId,
                BranchId = student.BranchId,
                StudentId = student.Id,
                InvoiceNumber = invoiceNum,
                Title = request.Title.Trim(),
                IssueDate = DateTime.UtcNow.Date,
                DueDate = request.DueDate.Date,
                TotalAmount = finalAmount,
                PaidAmount = 0.0m,
                Status = FeeStatus.Unpaid
            };

            generatedInvoices.Add(invoice);
            _context.FeeInvoices.Add(invoice);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Fees", "FeeInvoiceBulk", targetOrgId.ToString(),
            $"Bulk generated {generatedInvoices.Count} fee invoices for '{request.ClassGrade ?? "All Grades"}' (Amount: {finalAmount:C}, Title: '{request.Title}')",
            organizationId: targetOrgId,
            branchId: CurrentUserService.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        var result = new BulkGenerateFeeInvoicesResultDto(
            generatedInvoices.Count,
            finalAmount * generatedInvoices.Count,
            request.ClassGrade ?? "All Grades",
            request.Section,
            invoiceNumbers
        );

        return ApiSuccess(result, $"Successfully generated {generatedInvoices.Count} fee invoices.");
    }

    [HttpPost("invoices/{id:guid}/payments")]
    [RequirePermission(Permissions.FeesCollectPayment)]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordFeePaymentRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var invoice = await _context.FeeInvoices
            .Include(f => f.Student)
            .FirstOrDefaultAsync(f => f.Id == id && f.OrganizationId == targetOrgId && !f.IsDeleted, cancellationToken);

        if (invoice == null)
            return ApiNotFound("Fee invoice not found.");

        // Branch Isolation check
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (invoice.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot collect payments for invoices outside your assigned branch.");
        }

        if (request.AmountPaid <= 0)
            return ApiError("Payment amount must be greater than zero.");

        if (invoice.Status == FeeStatus.Paid)
            return ApiError("This invoice has already been fully paid.");

        decimal remainingBalance = invoice.Balance;
        if (request.AmountPaid > remainingBalance)
            return ApiError($"Payment amount ({request.AmountPaid:C}) exceeds remaining invoice balance ({remainingBalance:C}).");

        decimal newPaid = invoice.PaidAmount + request.AmountPaid;
        invoice.PaidAmount = newPaid;
        invoice.Status = newPaid >= invoice.TotalAmount ? FeeStatus.Paid : FeeStatus.PartiallyPaid;

        string receiptNum = $"REC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var payment = new FeePayment
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = invoice.BranchId,
            FeeInvoiceId = invoice.Id,
            StudentId = invoice.StudentId,
            ReceiptNumber = receiptNum,
            AmountPaid = request.AmountPaid,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Cash" : request.PaymentMethod.Trim(),
            TransactionReference = request.TransactionReference?.Trim(),
            ReceivedByUserId = CurrentUserService.UserId
        };

        _context.FeePayments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        // Auto-trigger SMS payment confirmation to guardian
        if (_smsNotificationService != null)
        {
            await _smsNotificationService.SendPaymentConfirmationAsync(targetOrgId, invoice.StudentId, receiptNum, request.AmountPaid, cancellationToken);
        }

        await _auditService.LogAsync(
            AuditAction.Create, "Fees", "FeePayment", payment.Id.ToString(),
            $"Recorded payment of {payment.AmountPaid:C} (Receipt: {receiptNum}) for invoice {invoice.InvoiceNumber}. Remaining balance: {invoice.Balance:C}",
            organizationId: targetOrgId,
            branchId: invoice.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            receiptNumber = receiptNum,
            amountPaid = payment.AmountPaid,
            remainingBalance = invoice.Balance,
            invoiceStatus = invoice.Status.ToString()
        }, "Payment collected and receipt recorded successfully.");
    }

    [HttpPost("invoices/{id:guid}/refund")]
    [RequirePermission(Permissions.FeesRefund)]
    public async Task<IActionResult> RefundPayment(Guid id, [FromBody] RefundFeePaymentRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var invoice = await _context.FeeInvoices
            .Include(f => f.Student)
            .FirstOrDefaultAsync(f => f.Id == id && f.OrganizationId == targetOrgId && !f.IsDeleted, cancellationToken);

        if (invoice == null)
            return ApiNotFound("Fee invoice not found.");

        // Branch Isolation check
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (invoice.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot refund invoices outside your assigned branch.");
        }

        if (request.RefundAmount <= 0)
            return ApiError("Refund amount must be greater than zero.");

        if (request.RefundAmount > invoice.PaidAmount)
            return ApiError($"Refund amount ({request.RefundAmount:C}) exceeds total paid amount on this invoice ({invoice.PaidAmount:C}).");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiError("Refund reason is mandatory.");

        decimal newPaid = invoice.PaidAmount - request.RefundAmount;
        invoice.PaidAmount = newPaid;
        invoice.Status = newPaid <= 0 ? FeeStatus.Unpaid : FeeStatus.PartiallyPaid;

        string refundReceipt = $"REF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var refundPaymentRecord = new FeePayment
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = invoice.BranchId,
            FeeInvoiceId = invoice.Id,
            StudentId = invoice.StudentId,
            ReceiptNumber = refundReceipt,
            AmountPaid = -request.RefundAmount, // Negative amount denotes refund
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = string.IsNullOrWhiteSpace(request.RefundMethod) ? "Cash" : request.RefundMethod.Trim(),
            TransactionReference = $"REFUND: {request.Reason.Trim()}",
            ReceivedByUserId = CurrentUserService.UserId
        };

        _context.FeePayments.Add(refundPaymentRecord);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Fees", "FeeRefund", invoice.Id.ToString(),
            $"Processed refund of {request.RefundAmount:C} (Refund ref: {refundReceipt}) on invoice {invoice.InvoiceNumber}. Reason: {request.Reason}",
            organizationId: targetOrgId,
            branchId: invoice.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            refundReceiptNumber = refundReceipt,
            refundedAmount = request.RefundAmount,
            newPaidAmount = invoice.PaidAmount,
            remainingBalance = invoice.Balance,
            invoiceStatus = invoice.Status.ToString()
        }, "Fee refund processed successfully.");
    }

    [HttpGet("defaulters")]
    [RequirePermission(Permissions.FeesView)]
    public async Task<IActionResult> GetDefaulters(
        [FromQuery] Guid? branchId,
        [FromQuery] string? grade,
        [FromQuery] int minDaysOverdue = 1,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        DateTime today = DateTime.UtcNow.Date;

        var query = _context.FeeInvoices
            .Include(f => f.Student)
                .ThenInclude(s => s!.StudentGuardians)
                    .ThenInclude(sg => sg.Guardian)
            .Include(f => f.Branch)
            .Where(f => f.OrganizationId == targetOrgId && !f.IsDeleted && f.Status != FeeStatus.Paid && f.DueDate < today);

        // Branch Isolation
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(f => f.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(grade))
        {
            query = query.Where(f => f.Student != null && f.Student.CurrentGrade == grade.Trim());
        }

        var overdueInvoices = await query.ToListAsync(cancellationToken);

        var defaultersGroup = overdueInvoices
            .GroupBy(f => f.StudentId)
            .Select(g =>
            {
                var first = g.First();
                var student = first.Student;
                var guardianContact = student?.StudentGuardians?.FirstOrDefault(sg => sg.IsPrimaryContact)?.Guardian
                    ?? student?.StudentGuardians?.FirstOrDefault()?.Guardian;

                DateTime earliestDue = g.Min(x => x.DueDate);
                int daysOverdue = (int)(today - earliestDue).TotalDays;

                return new FeeDefaulterDto(
                    g.Key,
                    student?.AdmissionNumber ?? "-",
                    student?.FullName ?? "Unknown Student",
                    student?.CurrentGrade ?? "-",
                    student?.Section,
                    first.BranchId,
                    first.Branch?.Name,
                    guardianContact?.FullName,
                    guardianContact?.Phone,
                    g.Count(),
                    g.Sum(x => x.Balance),
                    earliestDue,
                    daysOverdue
                );
            })
            .Where(d => d.DaysOverdue >= minDaysOverdue)
            .OrderByDescending(d => d.TotalOverdueAmount)
            .ToList();

        return ApiSuccess(defaultersGroup);
    }

    [HttpGet("summary")]
    [RequirePermission(Permissions.FeesView)]
    public async Task<IActionResult> GetFinancialSummary(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.FeeInvoices
            .Where(f => f.OrganizationId == targetOrgId && !f.IsDeleted);

        // Branch Isolation
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(f => f.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        var invoices = await query.ToListAsync(cancellationToken);

        DateTime today = DateTime.UtcNow.Date;

        decimal totalBilled = invoices.Sum(f => f.TotalAmount);
        decimal totalCollected = invoices.Sum(f => f.PaidAmount);
        decimal totalOutstanding = invoices.Sum(f => f.Balance);

        int totalCount = invoices.Count;
        int paidCount = invoices.Count(f => f.Status == FeeStatus.Paid);
        int partialCount = invoices.Count(f => f.Status == FeeStatus.PartiallyPaid);
        int unpaidCount = invoices.Count(f => f.Status == FeeStatus.Unpaid);

        var overdueInvoices = invoices.Where(f => f.DueDate < today && f.Status != FeeStatus.Paid).ToList();
        int defaultersCount = overdueInvoices.Select(f => f.StudentId).Distinct().Count();
        decimal overdueAmount = overdueInvoices.Sum(f => f.Balance);

        var summary = new FeeFinancialSummaryDto(
            totalBilled,
            totalCollected,
            totalOutstanding,
            totalCount,
            paidCount,
            partialCount,
            unpaidCount,
            defaultersCount,
            overdueAmount
        );

        return ApiSuccess(summary);
    }

    [HttpPost("invoices/{id:guid}/pay-online")]
    public async Task<IActionResult> PayOnline(
        Guid id,
        [FromBody] PayOnlineRequest request,
        [FromServices] IPaymentGatewayResolver gatewayResolver,
        CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var invoice = await _context.FeeInvoices
            .Include(f => f.Student)
            .FirstOrDefaultAsync(f => f.Id == id && f.OrganizationId == targetOrgId && !f.IsDeleted, cancellationToken);

        if (invoice == null)
            return ApiNotFound("Fee invoice not found.");

        decimal remainingBalance = invoice.TotalAmount - invoice.PaidAmount;
        if (remainingBalance <= 0)
            return ApiError("This invoice has already been fully paid.");

        decimal payAmount = request.Amount.HasValue && request.Amount.Value > 0
            ? Math.Min(request.Amount.Value, remainingBalance)
            : remainingBalance;

        string idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"PAY-{invoice.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : request.IdempotencyKey.Trim();

        var gatewayService = gatewayResolver.GetService(request.Gateway);

        var initRequest = new PaymentInitiationRequest(
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            Amount: payAmount,
            Currency: "PKR",
            Description: $"Online Fee Payment for {invoice.Title} (Student: {invoice.Student?.FullName})",
            CustomerName: invoice.Student?.FullName ?? "Student Parent",
            CustomerEmail: "finance@school.edu.pk",
            CustomerPhone: "+923001234567",
            ReturnUrl: request.ReturnUrl ?? "/fees",
            CancelUrl: request.CancelUrl ?? "/fees",
            IdempotencyKey: idempotencyKey
        );

        var result = await gatewayService.InitiatePaymentAsync(initRequest, cancellationToken);

        if (!result.Success)
            return ApiError(result.Message ?? "Failed to initiate payment gateway.");

        return ApiSuccess(result, "Checkout initiated successfully.");
    }

    [HttpPost("webhooks/{gatewayName}")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessGatewayWebhook(
        string gatewayName,
        [FromServices] IPaymentGatewayResolver gatewayResolver,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentGateway>(gatewayName, true, out var gateway) || gateway == PaymentGateway.Manual)
            return BadRequest(new { message = $"Invalid or unsupported gateway: {gatewayName}" });

        string payload;
        using (var reader = new StreamReader(Request.Body))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        var gatewayService = gatewayResolver.GetService(gateway);
        var result = await gatewayService.ProcessWebhookAsync(payload, headers, cancellationToken);

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage ?? "Webhook verification failed." });

        if (!result.IsPaid)
            return Ok(new { status = "acknowledged", message = "Non-payment webhook event processed." });

        // Idempotency check: if transaction already recorded, return 200 OK immediately
        if (!string.IsNullOrEmpty(result.IdempotencyKey) || !string.IsNullOrEmpty(result.TransactionId))
        {
            bool alreadyProcessed = await _context.FeePayments
                .AnyAsync(p => (p.IdempotencyKey == result.IdempotencyKey || (result.TransactionId != null && p.GatewayTransactionId == result.TransactionId)) && !p.IsDeleted, cancellationToken);

            if (alreadyProcessed)
                return Ok(new { status = "duplicate", message = "Payment already processed." });
        }

        // Try extracting invoiceId from idempotency key format: PAY-{invoiceId}-{timestamp}
        Guid invoiceId = Guid.Empty;
        if (!string.IsNullOrEmpty(result.IdempotencyKey) && result.IdempotencyKey.StartsWith("PAY-"))
        {
            var parts = result.IdempotencyKey.Split('-');
            if (parts.Length >= 2 && Guid.TryParse(parts[1], out var parsedId))
            {
                invoiceId = parsedId;
            }
        }

        FeeInvoice? invoice = null;
        if (invoiceId != Guid.Empty)
        {
            invoice = await _context.FeeInvoices.FirstOrDefaultAsync(f => f.Id == invoiceId && !f.IsDeleted, cancellationToken);
        }

        if (invoice == null)
        {
            return Ok(new { status = "unmapped", message = "Invoice ID could not be resolved from payload." });
        }

        decimal amountPaid = result.AmountPaid > 0 ? result.AmountPaid : (invoice.TotalAmount - invoice.PaidAmount);
        invoice.PaidAmount += amountPaid;
        invoice.Status = invoice.PaidAmount >= invoice.TotalAmount ? FeeStatus.Paid : FeeStatus.PartiallyPaid;

        string receiptNo = $"REC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N[..6].ToUpper()}";

        var payment = new FeePayment
        {
            OrganizationId = invoice.OrganizationId,
            BranchId = invoice.BranchId,
            FeeInvoiceId = invoice.Id,
            StudentId = invoice.StudentId,
            ReceiptNumber = receiptNo,
            AmountPaid = amountPaid,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = gateway.ToString(),
            TransactionReference = result.TransactionId,
            Gateway = gateway,
            GatewayTransactionId = result.TransactionId,
            GatewayResponse = result.RawResponse,
            IdempotencyKey = result.IdempotencyKey
        };

        _context.FeePayments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        // Auto-trigger SMS payment confirmation
        if (_smsNotificationService != null)
        {
            await _smsNotificationService.SendPaymentConfirmationAsync(invoice.OrganizationId, invoice.StudentId, receiptNo, amountPaid, cancellationToken);
        }

        await _auditService.LogAsync(
            AuditAction.Create, "Finance", "FeePayment", payment.Id.ToString(),
            $"Online gateway payment of Rs. {amountPaid} recorded via {gateway} for invoice {invoice.InvoiceNumber}.",
            organizationId: invoice.OrganizationId,
            branchId: invoice.BranchId,
            cancellationToken: cancellationToken);

        return Ok(new { status = "success", receiptNumber = receiptNo, amount = amountPaid });
    }

    [HttpPost("send-reminders")]
    [RequirePermission(Permissions.FeesView)]
    public async Task<IActionResult> SendFeeReminders(
        [FromQuery] Guid? invoiceId,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var today = DateTime.UtcNow.Date;
        var query = _context.FeeInvoices
            .Include(f => f.Student)
            .Where(f => f.OrganizationId == targetOrgId && f.Status != FeeStatus.Paid && !f.IsDeleted);

        if (invoiceId.HasValue)
        {
            query = query.Where(f => f.Id == invoiceId.Value);
        }
        else
        {
            // Auto reminders: 3 days before, on due date, or 7 days overdue
            var dueIn3Days = today.AddDays(3);
            var overdue7Days = today.AddDays(-7);
            query = query.Where(f => f.DueDate.Date == dueIn3Days || f.DueDate.Date == today || f.DueDate.Date == overdue7Days);
        }

        if (branchId.HasValue)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        var invoices = await query.ToListAsync(cancellationToken);
        int sent = 0;
        if (_smsNotificationService != null)
        {
            foreach (var inv in invoices)
            {
                await _smsNotificationService.SendFeeReminderAsync(targetOrgId, inv.Id, cancellationToken);
                sent++;
            }
        }

        return ApiSuccess(new { remindersSent = sent }, $"Fee reminder SMS notifications queued/dispatched to {sent} guardian(s).");
    }
}
