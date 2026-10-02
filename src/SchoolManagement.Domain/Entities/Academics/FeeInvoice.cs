using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class FeeInvoice : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid StudentId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty; // e.g. "Tuition Fee - October 2026"
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(15);
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; } = 0.0m;
    public FeeStatus Status { get; set; } = FeeStatus.Unpaid;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public Student.Student? Student { get; set; }
    public ICollection<FeePayment> Payments { get; set; } = new List<FeePayment>();

    public decimal Balance => TotalAmount - PaidAmount;
}
