using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransportController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public TransportController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private Guid GetTargetOrgId()
    {
        return CurrentUserService.IsSuperAdmin
            ? (TenantContext.OrganizationId ?? Guid.Empty)
            : (CurrentUserService.OrganizationId ?? Guid.Empty);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var vehiclesQuery = _context.Vehicles.Where(v => v.OrganizationId == targetOrgId && !v.IsDeleted);
        var routesQuery = _context.TransportRoutes.Where(r => r.OrganizationId == targetOrgId && !r.IsDeleted);
        var allocQuery = _context.TransportAllocations.Where(a => a.OrganizationId == targetOrgId && !a.IsDeleted && a.IsActive);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            vehiclesQuery = vehiclesQuery.Where(v => v.BranchId == null || v.BranchId == CurrentUserService.BranchId.Value);
            routesQuery = routesQuery.Where(r => r.BranchId == CurrentUserService.BranchId.Value);
            allocQuery = allocQuery.Where(a => a.BranchId == CurrentUserService.BranchId.Value);
        }

        int totalVehicles = await vehiclesQuery.CountAsync(cancellationToken);
        int totalCapacity = await vehiclesQuery.SumAsync(v => (int?)v.Capacity, cancellationToken) ?? 0;
        int activeRoutes = await routesQuery.CountAsync(r => r.IsActive, cancellationToken);
        int studentsAllocated = await allocQuery.CountAsync(cancellationToken);
        decimal utilizationRate = totalCapacity > 0 ? Math.Round((decimal)studentsAllocated / totalCapacity * 100m, 1) : 0m;

        return ApiSuccess(new
        {
            totalVehicles,
            totalCapacity,
            activeRoutes,
            studentsAllocated,
            utilizationRate
        });
    }

    [HttpGet("vehicles")]
    public async Task<IActionResult> GetVehicles([FromQuery] Guid? branchId, [FromQuery] VehicleStatus? status, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.Vehicles
            .Include(v => v.Branch)
            .Include(v => v.Routes)
                .ThenInclude(r => r.Allocations)
            .Where(v => v.OrganizationId == targetOrgId && !v.IsDeleted);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(v => v.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(v => v.BranchId == branchId.Value);
        }

        if (status.HasValue)
            query = query.Where(v => v.Status == status.Value);

        var vehicles = await query
            .OrderBy(v => v.VehicleNumber)
            .Select(v => new VehicleDto(
                v.Id,
                v.OrganizationId,
                v.BranchId,
                v.Branch != null ? v.Branch.Name : "Main Campus",
                v.VehicleNumber,
                v.Model,
                v.Capacity,
                v.DriverName,
                v.DriverPhone,
                v.DriverLicenseNumber,
                v.Status,
                v.Routes.SelectMany(r => r.Allocations).Count(a => a.IsActive && !a.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(vehicles);
    }

    [HttpPost("vehicles")]
    public async Task<IActionResult> CreateVehicle([FromBody] CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.VehicleNumber))
            return ApiError("Vehicle number/registration is required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        Guid? targetBranchId = request.BranchId ?? CurrentUserService.BranchId;
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            targetBranchId = CurrentUserService.BranchId.Value;
        }

        bool exists = await _context.Vehicles
            .AnyAsync(v => v.OrganizationId == targetOrgId && v.VehicleNumber.ToLower() == request.VehicleNumber.Trim().ToLower() && !v.IsDeleted, cancellationToken);

        if (exists)
            return ApiError($"Vehicle with registration '{request.VehicleNumber}' already exists.");

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            VehicleNumber = request.VehicleNumber.Trim().ToUpperInvariant(),
            Model = request.Model.Trim(),
            Capacity = Math.Max(1, request.Capacity),
            DriverName = request.DriverName.Trim(),
            DriverPhone = request.DriverPhone.Trim(),
            DriverLicenseNumber = request.DriverLicenseNumber?.Trim(),
            Status = VehicleStatus.Active
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Transport", "Vehicle", vehicle.Id.ToString(),
            $"Created transport vehicle '{vehicle.VehicleNumber}' (Capacity: {vehicle.Capacity}, Driver: {vehicle.DriverName})",
            organizationId: targetOrgId,
            branchId: vehicle.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = vehicle.Id, vehicleNumber = vehicle.VehicleNumber }, "Vehicle created successfully.");
    }

    [HttpPut("vehicles/{id:guid}")]
    public async Task<IActionResult> UpdateVehicle(Guid id, [FromBody] UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == id && v.OrganizationId == targetOrgId && !v.IsDeleted, cancellationToken);

        if (vehicle == null) return ApiNotFound("Vehicle not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (vehicle.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot edit vehicles outside your assigned branch.");
        }

        vehicle.VehicleNumber = request.VehicleNumber.Trim().ToUpperInvariant();
        vehicle.Model = request.Model.Trim();
        vehicle.Capacity = Math.Max(1, request.Capacity);
        vehicle.DriverName = request.DriverName.Trim();
        vehicle.DriverPhone = request.DriverPhone.Trim();
        vehicle.DriverLicenseNumber = request.DriverLicenseNumber?.Trim();
        vehicle.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Transport", "Vehicle", vehicle.Id.ToString(),
            $"Updated transport vehicle '{vehicle.VehicleNumber}'",
            organizationId: targetOrgId,
            branchId: vehicle.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Vehicle updated successfully.");
    }

    [HttpDelete("vehicles/{id:guid}")]
    public async Task<IActionResult> DeleteVehicle(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var vehicle = await _context.Vehicles
            .Include(v => v.Routes)
            .FirstOrDefaultAsync(v => v.Id == id && v.OrganizationId == targetOrgId && !v.IsDeleted, cancellationToken);

        if (vehicle == null) return ApiNotFound("Vehicle not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (vehicle.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot delete vehicles outside your assigned branch.");
        }

        bool hasRoutes = vehicle.Routes.Any(r => !r.IsDeleted);
        if (hasRoutes)
            return ApiError("Cannot delete vehicle while assigned to active routes. Reassign routes first.");

        vehicle.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Transport", "Vehicle", vehicle.Id.ToString(),
            $"Deleted vehicle '{vehicle.VehicleNumber}'",
            organizationId: targetOrgId,
            branchId: vehicle.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Vehicle deleted successfully.");
    }

    [HttpGet("routes")]
    public async Task<IActionResult> GetRoutes([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.TransportRoutes
            .Include(r => r.Branch)
            .Include(r => r.Vehicle)
            .Include(r => r.Allocations)
            .Where(r => r.OrganizationId == targetOrgId && !r.IsDeleted);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(r => r.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(r => r.BranchId == branchId.Value);
        }

        var routes = await query
            .OrderBy(r => r.RouteName)
            .Select(r => new TransportRouteDto(
                r.Id,
                r.OrganizationId,
                r.BranchId,
                r.Branch != null ? r.Branch.Name : "Main Campus",
                r.VehicleId,
                r.Vehicle != null ? r.Vehicle.VehicleNumber : null,
                r.Vehicle != null ? r.Vehicle.DriverName : null,
                r.Vehicle != null ? r.Vehicle.DriverPhone : null,
                r.RouteName,
                r.StartLocation,
                r.EndLocation,
                r.StopsJson,
                r.MonthlyFare,
                r.IsActive,
                r.Allocations.Count(a => a.IsActive && !a.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(routes);
    }

    [HttpPost("routes")]
    public async Task<IActionResult> CreateRoute([FromBody] CreateTransportRouteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RouteName))
            return ApiError("Route name is required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        Guid? targetBranchId = request.BranchId ?? CurrentUserService.BranchId;
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            targetBranchId = CurrentUserService.BranchId.Value;
        }

        var route = new TransportRoute
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            VehicleId = request.VehicleId,
            RouteName = request.RouteName.Trim(),
            StartLocation = request.StartLocation.Trim(),
            EndLocation = request.EndLocation.Trim(),
            StopsJson = request.StopsJson?.Trim(),
            MonthlyFare = Math.Max(0m, request.MonthlyFare),
            IsActive = true
        };

        _context.TransportRoutes.Add(route);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Transport", "TransportRoute", route.Id.ToString(),
            $"Created transport route '{route.RouteName}' ({route.StartLocation} -> {route.EndLocation})",
            organizationId: targetOrgId,
            branchId: route.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = route.Id, routeName = route.RouteName }, "Transport route created successfully.");
    }

    [HttpPut("routes/{id:guid}")]
    public async Task<IActionResult> UpdateRoute(Guid id, [FromBody] UpdateTransportRouteRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var route = await _context.TransportRoutes
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == targetOrgId && !r.IsDeleted, cancellationToken);

        if (route == null) return ApiNotFound("Route not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (route.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot edit routes outside your assigned branch.");
        }

        route.RouteName = request.RouteName.Trim();
        route.StartLocation = request.StartLocation.Trim();
        route.EndLocation = request.EndLocation.Trim();
        route.StopsJson = request.StopsJson?.Trim();
        route.MonthlyFare = Math.Max(0m, request.MonthlyFare);
        route.VehicleId = request.VehicleId;
        route.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Transport", "TransportRoute", route.Id.ToString(),
            $"Updated transport route '{route.RouteName}'",
            organizationId: targetOrgId,
            branchId: route.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Route updated successfully.");
    }

    [HttpDelete("routes/{id:guid}")]
    public async Task<IActionResult> DeleteRoute(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var route = await _context.TransportRoutes
            .Include(r => r.Allocations)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == targetOrgId && !r.IsDeleted, cancellationToken);

        if (route == null) return ApiNotFound("Route not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (route.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot delete routes outside your assigned branch.");
        }

        bool hasActiveAllocations = route.Allocations.Any(a => a.IsActive && !a.IsDeleted);
        if (hasActiveAllocations)
            return ApiError("Cannot delete route with active student passengers. Deallocate students first.");

        route.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Transport", "TransportRoute", route.Id.ToString(),
            $"Deleted transport route '{route.RouteName}'",
            organizationId: targetOrgId,
            branchId: route.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Route deleted successfully.");
    }

    [HttpGet("allocations")]
    public async Task<IActionResult> GetAllocations([FromQuery] Guid? routeId, [FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.TransportAllocations
            .Include(a => a.Student)
            .Include(a => a.TransportRoute)
                .ThenInclude(r => r!.Vehicle)
            .Where(a => a.OrganizationId == targetOrgId && !a.IsDeleted);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(a => a.BranchId == CurrentUserService.BranchId.Value);
        }

        if (routeId.HasValue)
            query = query.Where(a => a.TransportRouteId == routeId.Value);

        if (studentId.HasValue)
            query = query.Where(a => a.StudentId == studentId.Value);

        var list = await query
            .OrderByDescending(a => a.StartDate)
            .Select(a => new TransportAllocationDto(
                a.Id,
                a.StudentId,
                a.Student != null ? a.Student.FullName : "Unknown",
                a.Student != null ? a.Student.AdmissionNumber : "-",
                a.Student != null ? a.Student.CurrentGrade : "-",
                a.Student != null ? a.Student.Section : null,
                a.TransportRouteId,
                a.TransportRoute != null ? a.TransportRoute.RouteName : "Unknown",
                a.TransportRoute != null && a.TransportRoute.Vehicle != null ? a.TransportRoute.Vehicle.VehicleNumber : null,
                a.StopName,
                a.StartDate,
                a.EndDate,
                a.IsActive
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(list);
    }

    [HttpPost("allocate")]
    public async Task<IActionResult> AllocateStudent([FromBody] AllocateStudentTransportRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (student == null) return ApiNotFound("Student not found.");

        var route = await _context.TransportRoutes
            .Include(r => r.Vehicle)
            .Include(r => r.Allocations)
            .FirstOrDefaultAsync(r => r.Id == request.TransportRouteId && r.OrganizationId == targetOrgId && !r.IsDeleted, cancellationToken);

        if (route == null) return ApiNotFound("Transport route not found.");

        if (!route.IsActive)
            return ApiError("Cannot allocate student to an inactive route.");

        // Vehicle Capacity Check
        if (route.Vehicle != null)
        {
            int currentAllocations = route.Allocations.Count(a => a.IsActive && !a.IsDeleted);
            if (currentAllocations >= route.Vehicle.Capacity)
            {
                return ApiError($"Vehicle '{route.Vehicle.VehicleNumber}' has reached maximum passenger capacity ({route.Vehicle.Capacity} seats).");
            }
        }

        // Deactivate previous active allocations for this student
        var existing = await _context.TransportAllocations
            .Where(a => a.StudentId == student.Id && a.IsActive && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var ex in existing)
        {
            ex.IsActive = false;
            ex.EndDate = DateTime.UtcNow;
        }

        var allocation = new TransportAllocation
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = student.BranchId,
            StudentId = student.Id,
            TransportRouteId = route.Id,
            StopName = request.StopName?.Trim(),
            StartDate = request.StartDate ?? DateTime.UtcNow,
            IsActive = true
        };

        _context.TransportAllocations.Add(allocation);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Transport", "TransportAllocation", allocation.Id.ToString(),
            $"Allocated student '{student.FullName}' ({student.AdmissionNumber}) to route '{route.RouteName}' (Stop: {allocation.StopName ?? "Main"})",
            organizationId: targetOrgId,
            branchId: allocation.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            allocationId = allocation.Id,
            studentName = student.FullName,
            routeName = route.RouteName,
            stop = allocation.StopName
        }, "Student transport allocated successfully.");
    }

    [HttpPost("deallocate/{id:guid}")]
    public async Task<IActionResult> DeallocateStudent(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var allocation = await _context.TransportAllocations
            .Include(a => a.Student)
            .Include(a => a.TransportRoute)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == targetOrgId && !a.IsDeleted, cancellationToken);

        if (allocation == null) return ApiNotFound("Transport allocation not found.");

        allocation.IsActive = false;
        allocation.EndDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Transport", "TransportAllocation", allocation.Id.ToString(),
            $"Deallocated student '{allocation.Student?.FullName}' from transport route '{allocation.TransportRoute?.RouteName}'",
            organizationId: targetOrgId,
            branchId: allocation.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Student transport deallocated successfully.");
    }
}
