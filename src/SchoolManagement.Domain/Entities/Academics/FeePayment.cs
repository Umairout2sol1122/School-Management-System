using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class FeePayment : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid FeeInvoiceId { get; set; }
    public Guid StudentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string PaymentMethod { get; set; } = "BankTransfer"; // Cash, Online, BankTransfer, Cheque
    public string? TransactionReference { get; set; }
    public Guid? ReceivedByUserId { get; set; }

    public PaymentGateway Gateway { get; set; } = PaymentGateway.Manual;
    public string? GatewayTransactionId { get; set; }
    public string? GatewayResponse { get; set; }
    public string? IdempotencyKey { get; set; }

    public Organization? Organization { get; set; }
    public FeeInvoice? FeeInvoice { get; set; }
    public Student.Student? Student { get; set; }
    public User? ReceivedByUser { get; set; }
}
