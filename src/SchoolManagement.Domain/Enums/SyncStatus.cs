namespace SchoolManagement.Domain.Enums;

public enum SyncStatus
{
    Pending = 0,
    InFlight = 1,
    Synced = 2,
    Conflict = 3,
    Failed = 4
}
