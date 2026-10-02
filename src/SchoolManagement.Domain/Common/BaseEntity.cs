using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
    public long SyncVersion { get; set; } = 1;
    public byte[]? RowVersion { get; set; }

    public void MarkUpdated(string updatedBy)
    {
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        SyncVersion++;
    }

    public void MarkDeleted(string deletedBy)
    {
        IsDeleted = true;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = deletedBy;
        SyncVersion++;
    }
}
