using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Domain.Entities.Academics;

public class Book : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? Isbn { get; set; }
    public string? Publisher { get; set; }
    public string Category { get; set; } = "General";
    public int TotalCopies { get; set; } = 1;
    public int AvailableCopies { get; set; } = 1;
    public string? RackNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<BookIssue> BookIssues { get; set; } = new List<BookIssue>();
}
