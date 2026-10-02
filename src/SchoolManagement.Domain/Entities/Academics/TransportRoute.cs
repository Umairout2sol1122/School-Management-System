using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Domain.Entities.Academics;

public class TransportRoute : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? VehicleId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public string StartLocation { get; set; } = string.Empty;
    public string EndLocation { get; set; } = string.Empty;
    public string? StopsJson { get; set; }
    public decimal MonthlyFare { get; set; } = 0m;
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public Vehicle? Vehicle { get; set; }
    public ICollection<TransportAllocation> Allocations { get; set; } = new List<TransportAllocation>();
}
