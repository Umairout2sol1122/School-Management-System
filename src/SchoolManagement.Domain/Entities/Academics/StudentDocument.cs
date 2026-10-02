using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class StudentDocument : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid StudentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public StudentDocumentType DocumentType { get; set; } = StudentDocumentType.Other;
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; } = 0;
    public string? Description { get; set; }
    public Guid? UploadedByUserId { get; set; }

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public Student.Student? Student { get; set; }
    public User? UploadedByUser { get; set; }
}
