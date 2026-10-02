using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class BookIssue : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid BookId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? StaffProfileId { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime? ReturnDate { get; set; }
    public decimal FineAmount { get; set; } = 0m;
    public BookIssueStatus Status { get; set; } = BookIssueStatus.Issued;
    public string? Remarks { get; set; }

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public Book? Book { get; set; }
    public Student.Student? Student { get; set; }
    public StaffProfile? StaffProfile { get; set; }
}
