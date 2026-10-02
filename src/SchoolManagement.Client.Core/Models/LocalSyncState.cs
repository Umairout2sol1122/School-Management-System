namespace SchoolManagement.Client.Core.Models;

public class LocalSyncState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public long LastSyncVersion { get; set; } = 0;
    public DateTime LastSyncAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsSyncing { get; set; } = false;
    public string? LastError { get; set; }
}
