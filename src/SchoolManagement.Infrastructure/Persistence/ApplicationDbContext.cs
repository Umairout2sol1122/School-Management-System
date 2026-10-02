using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Entities.Audit;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncQueueItem> SyncQueue => Set<SyncQueueItem>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<OrganizationModule> OrganizationModules => Set<OrganizationModule>();
    public DbSet<AcademicSession> AcademicSessions => Set<AcademicSession>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ClassGrade> ClassGrades => Set<ClassGrade>();
    public DbSet<ClassSection> ClassSections => Set<ClassSection>();
    public DbSet<SubjectCourse> SubjectCourses => Set<SubjectCourse>();
    public DbSet<CourseAssignment> CourseAssignments => Set<CourseAssignment>();
    public DbSet<OrganizationSetting> OrganizationSettings => Set<OrganizationSetting>();
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<TimetableSlot> TimetableSlots => Set<TimetableSlot>();
    public DbSet<HomeworkAssignment> HomeworkAssignments => Set<HomeworkAssignment>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();
    public DbSet<ExamSchedule> ExamSchedules => Set<ExamSchedule>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<FeeInvoice> FeeInvoices => Set<FeeInvoice>();
    public DbSet<FeePayment> FeePayments => Set<FeePayment>();
    public DbSet<StudentDocument> StudentDocuments => Set<StudentDocument>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookIssue> BookIssues => Set<BookIssue>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<TransportRoute> TransportRoutes => Set<TransportRoute>();
    public DbSet<TransportAllocation> TransportAllocations => Set<TransportAllocation>();
    public DbSet<AdmissionApplication> AdmissionApplications => Set<AdmissionApplication>();
    public DbSet<CommunicationLog> CommunicationLogs => Set<CommunicationLog>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PaySlip> PaySlips => Set<PaySlip>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filter for multi-tenant isolation and soft deletion
        modelBuilder.Entity<Organization>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<Branch>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<OrganizationModule>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<User>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<Role>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (e.OrganizationId == null || _tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<Permission>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<UserRole>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RolePermission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SyncQueueItem>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<Student>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<Guardian>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<StudentGuardian>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<StaffProfile>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<AcademicSession>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<Department>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<ClassGrade>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<ClassSection>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<SubjectCourse>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<CourseAssignment>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<OrganizationSetting>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<NotificationSetting>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<Announcement>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<AttendanceRecord>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<TimetableSlot>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<HomeworkAssignment>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<AssignmentSubmission>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<ExamSchedule>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<ExamResult>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<LeaveRequest>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<FeeInvoice>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<FeePayment>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<StudentDocument>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<Book>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<BookIssue>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<Vehicle>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<TransportRoute>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<TransportAllocation>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<AdmissionApplication>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<CommunicationLog>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<Certificate>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<SalaryStructure>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<PayrollRun>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        modelBuilder.Entity<PaySlip>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenantContext.IsSuperAdmin || _tenantContext.OrganizationId == null || e.OrganizationId == _tenantContext.OrganizationId));

        // Organization configuration
        modelBuilder.Entity<Organization>(b =>
        {
            b.ToTable("Organizations", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Code).IsUnique();
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.Property(e => e.Code).HasMaxLength(50).IsRequired();
            b.Property(e => e.ThemePrimaryColor).HasMaxLength(20);
            b.Property(e => e.ThemeSecondaryColor).HasMaxLength(20);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        // Branch configuration
        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("Branches", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.Code }).IsUnique();
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.Property(e => e.Code).HasMaxLength(50).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany(o => o.Branches)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // OrganizationModule configuration
        modelBuilder.Entity<OrganizationModule>(b =>
        {
            b.ToTable("OrganizationModules", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.ModuleName }).IsUnique();
            b.Property(e => e.ModuleName).HasMaxLength(100).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany(o => o.Modules)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // User configuration
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users", "Security");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.Email }).IsUnique();
            b.HasIndex(e => new { e.OrganizationId, e.Username }).IsUnique();
            b.Property(e => e.Email).HasMaxLength(256).IsRequired();
            b.Property(e => e.Username).HasMaxLength(100).IsRequired();
            b.Property(e => e.PasswordHash).IsRequired();
            b.Property(e => e.PasswordSalt).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Role configuration
        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("Roles", "Security");
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).HasMaxLength(100).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        // Permission configuration
        modelBuilder.Entity<Permission>(b =>
        {
            b.ToTable("Permissions", "Security");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Code).IsUnique();
            b.Property(e => e.Code).HasMaxLength(100).IsRequired();
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.Property(e => e.Module).HasMaxLength(100).IsRequired();
        });

        // UserRole configuration
        modelBuilder.Entity<UserRole>(b =>
        {
            b.ToTable("UserRoles", "Security");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();

            b.HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RolePermission configuration
        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("RolePermissions", "Security");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();

            b.HasOne(e => e.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RefreshToken configuration
        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens", "Security");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Token).IsUnique();

            b.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLogs", "Audit");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.OrganizationId);
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => e.TimestampUtc);
            b.Property(e => e.Module).HasMaxLength(100).IsRequired();
            b.Property(e => e.EntityName).HasMaxLength(100).IsRequired();
        });

        // SyncQueueItem configuration
        modelBuilder.Entity<SyncQueueItem>(b =>
        {
            b.ToTable("SyncQueue", "Sync");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.ClientSyncId).IsUnique();
            b.HasIndex(e => new { e.OrganizationId, e.SyncStatus });
            b.Property(e => e.EntityName).HasMaxLength(100).IsRequired();
        });

        // Student configuration
        modelBuilder.Entity<Student>(b =>
        {
            b.ToTable("Students", "Student");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.AdmissionNumber }).IsUnique();
            b.Property(e => e.AdmissionNumber).HasMaxLength(50).IsRequired();
            b.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            b.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            b.Property(e => e.CurrentGrade).HasMaxLength(100).IsRequired();
            b.Property(e => e.Section).HasMaxLength(50);
            b.Property(e => e.RollNumber).HasMaxLength(50);
            b.Property(e => e.NationalIdOrBForm).HasMaxLength(50);
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Guardian configuration
        modelBuilder.Entity<Guardian>(b =>
        {
            b.ToTable("Guardians", "Student");
            b.HasKey(e => e.Id);
            b.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            b.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            b.Property(e => e.Phone).HasMaxLength(30).IsRequired();
            b.Property(e => e.NationalId).HasMaxLength(50);
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // StudentGuardian linking configuration
        modelBuilder.Entity<StudentGuardian>(b =>
        {
            b.ToTable("StudentGuardians", "Student");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.StudentId, e.GuardianId }).IsUnique();
            b.Property(e => e.Relationship).HasMaxLength(50).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Student)
                .WithMany(s => s.StudentGuardians)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Guardian)
                .WithMany(g => g.StudentGuardians)
                .HasForeignKey(e => e.GuardianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // StaffProfile configuration
        modelBuilder.Entity<StaffProfile>(b =>
        {
            b.ToTable("StaffProfiles", "Staff");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.EmployeeCode }).IsUnique();
            b.HasIndex(e => e.UserId).IsUnique();
            b.Property(e => e.EmployeeCode).HasMaxLength(50).IsRequired();
            b.Property(e => e.Designation).HasMaxLength(100).IsRequired();
            b.Property(e => e.Department).HasMaxLength(100).IsRequired();
            b.Property(e => e.Qualification).HasMaxLength(200);
            b.Property(e => e.NationalId).HasMaxLength(50);
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AcademicSession configuration
        modelBuilder.Entity<AcademicSession>(b =>
        {
            b.ToTable("AcademicSessions", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.Code }).IsUnique();
            b.Property(e => e.Name).HasMaxLength(100).IsRequired();
            b.Property(e => e.Code).HasMaxLength(50).IsRequired();
            b.Property(e => e.Type).HasMaxLength(50).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany(o => o.AcademicSessions)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Department configuration
        modelBuilder.Entity<Department>(b =>
        {
            b.ToTable("Departments", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.Code }).IsUnique();
            b.Property(e => e.Name).HasMaxLength(150).IsRequired();
            b.Property(e => e.Code).HasMaxLength(50).IsRequired();
            b.Property(e => e.Description).HasMaxLength(500);
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany(o => o.Departments)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.HeadOfDepartmentUser)
                .WithMany()
                .HasForeignKey(e => e.HeadOfDepartmentUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ClassGrade configuration
        modelBuilder.Entity<ClassGrade>(b =>
        {
            b.ToTable("ClassGrades", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.Code }).IsUnique();
            b.Property(e => e.Name).HasMaxLength(150).IsRequired();
            b.Property(e => e.Code).HasMaxLength(50).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany(o => o.ClassGrades)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Department)
                .WithMany()
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ClassSection configuration
        modelBuilder.Entity<ClassSection>(b =>
        {
            b.ToTable("ClassSections", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.ClassGradeId, e.Name }).IsUnique();
            b.Property(e => e.Name).HasMaxLength(100).IsRequired();
            b.Property(e => e.RoomNumber).HasMaxLength(50);
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.ClassGrade)
                .WithMany(g => g.Sections)
                .HasForeignKey(e => e.ClassGradeId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.ClassTeacherUser)
                .WithMany()
                .HasForeignKey(e => e.ClassTeacherUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // SubjectCourse configuration
        modelBuilder.Entity<SubjectCourse>(b =>
        {
            b.ToTable("SubjectCourses", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.Code }).IsUnique();
            b.Property(e => e.Name).HasMaxLength(150).IsRequired();
            b.Property(e => e.Code).HasMaxLength(50).IsRequired();
            b.Property(e => e.Type).HasMaxLength(50).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany(o => o.SubjectCourses)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Department)
                .WithMany()
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(e => e.ClassGrade)
                .WithMany()
                .HasForeignKey(e => e.ClassGradeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CourseAssignment configuration
        modelBuilder.Entity<CourseAssignment>(b =>
        {
            b.ToTable("CourseAssignments", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.ClassSection)
                .WithMany()
                .HasForeignKey(e => e.ClassSectionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.SubjectCourse)
                .WithMany()
                .HasForeignKey(e => e.SubjectCourseId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.TeacherUser)
                .WithMany()
                .HasForeignKey(e => e.TeacherUserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.AcademicSession)
                .WithMany()
                .HasForeignKey(e => e.AcademicSessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // OrganizationSetting configuration
        modelBuilder.Entity<OrganizationSetting>(b =>
        {
            b.ToTable("OrganizationSettings", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.OrganizationId).IsUnique();
            b.Property(e => e.Currency).HasMaxLength(10).IsRequired();
            b.Property(e => e.DateFormat).HasMaxLength(30).IsRequired();
            b.Property(e => e.TimeFormat).HasMaxLength(30).IsRequired();
            b.Property(e => e.TimeZone).HasMaxLength(100).IsRequired();
            b.Property(e => e.Language).HasMaxLength(10).IsRequired();
            b.Property(e => e.GradingSystem).HasMaxLength(50).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // NotificationSetting configuration
        modelBuilder.Entity<NotificationSetting>(b =>
        {
            b.ToTable("NotificationSettings", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.OrganizationId).IsUnique();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Announcement configuration
        modelBuilder.Entity<Announcement>(b =>
        {
            b.ToTable("Announcements", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.Title).HasMaxLength(250).IsRequired();
            b.Property(e => e.TargetAudience).HasMaxLength(50).IsRequired();
            b.Property(e => e.Priority).HasMaxLength(20).IsRequired();
            b.Property(e => e.RowVersion).IsRowVersion();

            b.HasOne(e => e.Organization)
                .WithMany(o => o.Announcements)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(e => e.ClassGrade)
                .WithMany()
                .HasForeignKey(e => e.ClassGradeId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(e => e.Department)
                .WithMany()
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AttendanceRecord>(b =>
        {
            b.ToTable("AttendanceRecords", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<TimetableSlot>(b =>
        {
            b.ToTable("TimetableSlots", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<HomeworkAssignment>(b =>
        {
            b.ToTable("HomeworkAssignments", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<AssignmentSubmission>(b =>
        {
            b.ToTable("AssignmentSubmissions", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.MarksAwarded).HasPrecision(18, 2);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ExamSchedule>(b =>
        {
            b.ToTable("ExamSchedules", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.TotalMarks).HasPrecision(18, 2);
            b.Property(e => e.PassingMarks).HasPrecision(18, 2);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ExamResult>(b =>
        {
            b.ToTable("ExamResults", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.MarksObtained).HasPrecision(18, 2);
            b.Property(e => e.Percentage).HasPrecision(18, 2);
            b.Property(e => e.Gpa).HasPrecision(18, 2);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<LeaveRequest>(b =>
        {
            b.ToTable("LeaveRequests", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<FeeInvoice>(b =>
        {
            b.ToTable("FeeInvoices", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.TotalAmount).HasPrecision(18, 2);
            b.Property(e => e.PaidAmount).HasPrecision(18, 2);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<FeePayment>(b =>
        {
            b.ToTable("FeePayments", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.IdempotencyKey);
            b.Property(e => e.AmountPaid).HasPrecision(18, 2);
            b.Property(e => e.GatewayTransactionId).HasMaxLength(100);
            b.Property(e => e.IdempotencyKey).HasMaxLength(100);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<StudentDocument>(b =>
        {
            b.ToTable("StudentDocuments", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Book>(b =>
        {
            b.ToTable("Books", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<BookIssue>(b =>
        {
            b.ToTable("BookIssues", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.FineAmount).HasPrecision(18, 2);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Vehicle>(b =>
        {
            b.ToTable("Vehicles", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<TransportRoute>(b =>
        {
            b.ToTable("TransportRoutes", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.MonthlyFare).HasPrecision(18, 2);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<TransportAllocation>(b =>
        {
            b.ToTable("TransportAllocations", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<AdmissionApplication>(b =>
        {
            b.ToTable("AdmissionApplications", "Student");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<CommunicationLog>(b =>
        {
            b.ToTable("CommunicationLogs", "Tenant");
            b.HasKey(e => e.Id);
            b.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Certificate>(b =>
        {
            b.ToTable("Certificates", "Tenant");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.SerialNumber }).IsUnique();
            b.HasIndex(e => e.VerificationCode);
            b.Property(e => e.SerialNumber).HasMaxLength(100).IsRequired();
            b.Property(e => e.VerificationCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.IssuedByName).HasMaxLength(200);
            b.Property(e => e.Conduct).HasMaxLength(100);
            b.Property(e => e.LeavingReason).HasMaxLength(500);
            b.Property(e => e.PromotedToClass).HasMaxLength(100);
            b.Property(e => e.Remarks).HasMaxLength(1000);
            b.Property(e => e.RevocationReason).HasMaxLength(500);
            b.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.IssuedByUser).WithMany().HasForeignKey(e => e.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalaryStructure>(b =>
        {
            b.ToTable("SalaryStructures", "Staff");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.StaffProfileId, e.IsActive });
            b.Property(e => e.BasicSalary).HasPrecision(18, 2);
            b.Property(e => e.HouseRentAllowance).HasPrecision(18, 2);
            b.Property(e => e.MedicalAllowance).HasPrecision(18, 2);
            b.Property(e => e.ConveyanceAllowance).HasPrecision(18, 2);
            b.Property(e => e.SpecialAllowance).HasPrecision(18, 2);
            b.Property(e => e.BankName).HasMaxLength(150);
            b.Property(e => e.BankAccountTitle).HasMaxLength(150);
            b.Property(e => e.BankAccountNumber).HasMaxLength(100);
            b.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.StaffProfile).WithMany().HasForeignKey(e => e.StaffProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PayrollRun>(b =>
        {
            b.ToTable("PayrollRuns", "Staff");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.Year, e.Month });
            b.Property(e => e.Title).HasMaxLength(200).IsRequired();
            b.Property(e => e.TotalGrossSalary).HasPrecision(18, 2);
            b.Property(e => e.TotalTaxDeduction).HasPrecision(18, 2);
            b.Property(e => e.TotalEobiDeduction).HasPrecision(18, 2);
            b.Property(e => e.TotalAbsenceDeduction).HasPrecision(18, 2);
            b.Property(e => e.TotalOtherDeductions).HasPrecision(18, 2);
            b.Property(e => e.TotalNetSalary).HasPrecision(18, 2);
            b.Property(e => e.ProcessedByName).HasMaxLength(200);
            b.Property(e => e.ApprovedByName).HasMaxLength(200);
            b.Property(e => e.DisbursedByName).HasMaxLength(200);
            b.Property(e => e.Remarks).HasMaxLength(500);
            b.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(e => e.PaySlips).WithOne(e => e.PayrollRun).HasForeignKey(e => e.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaySlip>(b =>
        {
            b.ToTable("PaySlips", "Staff");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.OrganizationId, e.SlipNumber }).IsUnique();
            b.HasIndex(e => new { e.PayrollRunId, e.StaffProfileId }).IsUnique();
            b.Property(e => e.SlipNumber).HasMaxLength(100).IsRequired();
            b.Property(e => e.BasicSalary).HasPrecision(18, 2);
            b.Property(e => e.HouseRentAllowance).HasPrecision(18, 2);
            b.Property(e => e.MedicalAllowance).HasPrecision(18, 2);
            b.Property(e => e.ConveyanceAllowance).HasPrecision(18, 2);
            b.Property(e => e.SpecialAllowance).HasPrecision(18, 2);
            b.Property(e => e.GrossSalary).HasPrecision(18, 2);
            b.Property(e => e.TaxDeduction).HasPrecision(18, 2);
            b.Property(e => e.EobiEmployeeContribution).HasPrecision(18, 2);
            b.Property(e => e.EobiEmployerContribution).HasPrecision(18, 2);
            b.Property(e => e.AbsenceDeduction).HasPrecision(18, 2);
            b.Property(e => e.AdvanceRecovery).HasPrecision(18, 2);
            b.Property(e => e.OtherDeductions).HasPrecision(18, 2);
            b.Property(e => e.TotalDeductions).HasPrecision(18, 2);
            b.Property(e => e.NetSalary).HasPrecision(18, 2);
            b.Property(e => e.BankName).HasMaxLength(150);
            b.Property(e => e.BankAccountNumber).HasMaxLength(100);
            b.Property(e => e.PaymentReference).HasMaxLength(100);
            b.Property(e => e.Remarks).HasMaxLength(500);
            b.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.StaffProfile).WithMany().HasForeignKey(e => e.StaffProfileId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        string currentUser = _currentUserService?.Email ?? "System";

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = DateTime.UtcNow;
                    if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy) || entry.Entity.CreatedBy == "System")
                        entry.Entity.CreatedBy = currentUser;
                    entry.Entity.SyncVersion = 1;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = currentUser;
                    entry.Entity.SyncVersion++;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
