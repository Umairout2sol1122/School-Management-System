using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Entities.Audit;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<Branch> Branches { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SyncQueueItem> SyncQueue { get; }
    DbSet<Student> Students { get; }
    DbSet<Guardian> Guardians { get; }
    DbSet<StudentGuardian> StudentGuardians { get; }
    DbSet<StaffProfile> StaffProfiles { get; }
    DbSet<OrganizationModule> OrganizationModules { get; }
    DbSet<AcademicSession> AcademicSessions { get; }
    DbSet<Department> Departments { get; }
    DbSet<ClassGrade> ClassGrades { get; }
    DbSet<ClassSection> ClassSections { get; }
    DbSet<SubjectCourse> SubjectCourses { get; }
    DbSet<CourseAssignment> CourseAssignments { get; }
    DbSet<OrganizationSetting> OrganizationSettings { get; }
    DbSet<NotificationSetting> NotificationSettings { get; }
    DbSet<Announcement> Announcements { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<TimetableSlot> TimetableSlots { get; }
    DbSet<HomeworkAssignment> HomeworkAssignments { get; }
    DbSet<AssignmentSubmission> AssignmentSubmissions { get; }
    DbSet<ExamSchedule> ExamSchedules { get; }
    DbSet<ExamResult> ExamResults { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<FeeInvoice> FeeInvoices { get; }
    DbSet<FeePayment> FeePayments { get; }
    DbSet<StudentDocument> StudentDocuments { get; }
    DbSet<Book> Books { get; }
    DbSet<BookIssue> BookIssues { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<TransportRoute> TransportRoutes { get; }
    DbSet<TransportAllocation> TransportAllocations { get; }
    DbSet<AdmissionApplication> AdmissionApplications { get; }
    DbSet<CommunicationLog> CommunicationLogs { get; }
    DbSet<Certificate> Certificates { get; }
    DbSet<SalaryStructure> SalaryStructures { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PaySlip> PaySlips { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
