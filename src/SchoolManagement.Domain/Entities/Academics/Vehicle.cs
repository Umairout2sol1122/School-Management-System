using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class Vehicle : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Capacity { get; set; } = 30;
    public string DriverName { get; set; } = string.Empty;
    public string DriverPhone { get; set; } = string.Empty;
    public string? DriverLicenseNumber { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Active;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<TransportRoute> Routes { get; set; } = new List<TransportRoute>();
}
