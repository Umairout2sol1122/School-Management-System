using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record VehicleDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string VehicleNumber,
    string Model,
    int Capacity,
    string DriverName,
    string DriverPhone,
    string? DriverLicenseNumber,
    VehicleStatus Status,
    int ActiveAllocationsCount
);

public record CreateVehicleRequest(
    string VehicleNumber,
    string Model,
    int Capacity = 30,
    string DriverName = "",
    string DriverPhone = "",
    string? DriverLicenseNumber = null,
    Guid? BranchId = null
);

public record UpdateVehicleRequest(
    string VehicleNumber,
    string Model,
    int Capacity,
    string DriverName,
    string DriverPhone,
    string? DriverLicenseNumber,
    VehicleStatus Status
);

public record TransportRouteDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    Guid? VehicleId,
    string? VehicleNumber,
    string? DriverName,
    string? DriverPhone,
    string RouteName,
    string StartLocation,
    string EndLocation,
    string? StopsJson,
    decimal MonthlyFare,
    bool IsActive,
    int TotalStudentsAllocated
);

public record CreateTransportRouteRequest(
    string RouteName,
    string StartLocation,
    string EndLocation,
    string? StopsJson = null,
    decimal MonthlyFare = 0m,
    Guid? VehicleId = null,
    Guid? BranchId = null
);

public record UpdateTransportRouteRequest(
    string RouteName,
    string StartLocation,
    string EndLocation,
    string? StopsJson,
    decimal MonthlyFare,
    Guid? VehicleId,
    bool IsActive
);

public record TransportAllocationDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    string CurrentGrade,
    string? Section,
    Guid TransportRouteId,
    string RouteName,
    string? VehicleNumber,
    string? StopName,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive
);

public record AllocateStudentTransportRequest(
    Guid StudentId,
    Guid TransportRouteId,
    string? StopName = null,
    DateTime? StartDate = null
);
