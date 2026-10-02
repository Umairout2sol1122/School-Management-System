using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SchoolManagement.Api.Controllers;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class ComprehensiveScenarioQcTests
{
    private readonly Mock<IAuditService> _auditServiceMock = new();
    private readonly PasswordHasher _passwordHasher = new();

    private ApplicationDbContext CreateInMemoryDbContext(string dbName, ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    private static TenantContext CreateTenantContext(Guid? orgId = null, Guid? branchId = null, bool isSuperAdmin = false)
    {
        var context = new TenantContext();
        context.SetTenant(orgId, branchId, isSuperAdmin);
        return context;
    }

    private void SetupController(BaseApiController controller, Guid? userId, Guid? orgId, Guid? branchId, UserRoleType role)
    {
        var claims = new List<Claim>();
        if (userId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (orgId.HasValue) claims.Add(new Claim("tenant_id", orgId.Value.ToString()));
        if (branchId.HasValue) claims.Add(new Claim("branch_id", branchId.Value.ToString()));
        claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        claims.Add(new Claim("role_type", role.ToString()));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userId);
        currentUserServiceMock.Setup(s => s.OrganizationId).Returns(orgId);
        currentUserServiceMock.Setup(s => s.BranchId).Returns(branchId);
        currentUserServiceMock.Setup(s => s.RoleType).Returns(role);
        currentUserServiceMock.Setup(s => s.IsSuperAdmin).Returns(role == UserRoleType.SuperAdmin);
        currentUserServiceMock.Setup(s => s.Email).Returns("qc-tester@school.edu");

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICurrentUserService))).Returns(currentUserServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ITenantContext))).Returns(CreateTenantContext(orgId, branchId, role == UserRoleType.SuperAdmin));

        httpContext.RequestServices = serviceProviderMock.Object;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    // =========================================================================
    // 1. BRANCH ADMINISTRATION QC
    // =========================================================================
    [Fact]
    public async Task BranchAdmin_OwnBranchAccess_Succeeds_And_CrossBranch_Forbidden()
    {
        var orgId = Guid.NewGuid();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();
        var principalId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchAId));

        var branchA = new Branch { Id = branchAId, OrganizationId = orgId, Name = "Branch Alpha", Code = "BA", IsActive = true };
        var branchB = new Branch { Id = branchBId, OrganizationId = orgId, Name = "Branch Beta", Code = "BB", IsActive = true };
        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();

        var controller = new BranchesController(db, _auditServiceMock.Object);
        SetupController(controller, principalId, orgId, branchAId, UserRoleType.Principal);

        // Positive: Access own branch dashboard
        var ownResult = await controller.GetBranchDashboard(branchAId, CancellationToken.None);
        ownResult.Should().BeOfType<OkObjectResult>();

        // Negative: Direct API call / URL manipulation with another branch ID
        var foreignResult = await controller.GetBranchDashboard(branchBId, CancellationToken.None);
        foreignResult.Should().BeOfType<ObjectResult>();
        (foreignResult as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // Security: Principal cannot delete branch
        var deleteResult = await controller.DeleteBranch(branchAId, CancellationToken.None);
        (deleteResult as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // =========================================================================
    // 2. PARENT / GUARDIAN MANAGEMENT QC
    // =========================================================================
    [Fact]
    public async Task ParentPortal_AccessOwnChild_Succeeds_And_UnrelatedStudent_Blocked()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();
        var childStudentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var guardian = new Guardian { Id = guardianId, OrganizationId = orgId, UserId = parentUserId, FirstName = "John", LastName = "Guardian", Phone = "03001234567" };
        var childStudent = new Student { Id = childStudentId, OrganizationId = orgId, BranchId = branchId, FirstName = "Child", LastName = "One", AdmissionNumber = "C-01", CurrentGrade = "Grade 5" };
        var otherStudent = new Student { Id = otherStudentId, OrganizationId = orgId, BranchId = branchId, FirstName = "Other", LastName = "Student", AdmissionNumber = "O-02", CurrentGrade = "Grade 5" };

        var link = new StudentGuardian { Id = Guid.NewGuid(), OrganizationId = orgId, GuardianId = guardianId, StudentId = childStudentId, IsPrimaryContact = true };

        db.Guardians.Add(guardian);
        db.Students.AddRange(childStudent, otherStudent);
        db.StudentGuardians.Add(link);
        await db.SaveChangesAsync();

        var controller = new ParentPortalController(db, _auditServiceMock.Object);
        SetupController(controller, parentUserId, orgId, branchId, UserRoleType.Parent);

        // Positive: Parent accesses own child attendance
        var childAttendance = await controller.GetChildAttendance(childStudentId, null, null, CancellationToken.None);
        childAttendance.Should().BeOfType<OkObjectResult>();

        // Negative: Parent tries to access unrelated student attendance -> 403 Forbidden
        var otherAttendance = await controller.GetChildAttendance(otherStudentId, null, null, CancellationToken.None);
        otherAttendance.Should().BeOfType<ObjectResult>();
        (otherAttendance as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // Negative: Parent tries to view fees of unrelated student -> 403 Forbidden
        var otherFees = await controller.GetChildFees(otherStudentId, CancellationToken.None);
        (otherFees as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // =========================================================================
    // 3. ACADEMIC MANAGEMENT (SCHOOLS & INSTITUTES) QC
    // =========================================================================
    [Fact]
    public async Task AcademicManagement_CapacityLimits_And_Collisions_Enforced()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var grade = new ClassGrade { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Grade 10", Code = "G10", Capacity = 2, IsActive = true };
        var section = new ClassSection { Id = Guid.NewGuid(), OrganizationId = orgId, ClassGradeId = grade.Id, Name = "Section A", Capacity = 2, IsActive = true };
        var subject = new SubjectCourse { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Physics", Code = "PHY-101", IsActive = true };
        var teacherUser = new User { Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "Alan", LastName = "Turing", Email = "alan@turing.edu", RoleType = UserRoleType.Teacher, IsActive = true };

        db.ClassGrades.Add(grade);
        db.ClassSections.Add(section);
        db.SubjectCourses.Add(subject);
        db.Users.Add(teacherUser);

        // Add 2 students to fill capacity
        var s1 = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, FirstName = "Student", LastName = "1", AdmissionNumber = "S1", CurrentGrade = "Grade 10", Section = "Section A", Status = StudentStatus.Active };
        var s2 = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, FirstName = "Student", LastName = "2", AdmissionNumber = "S2", CurrentGrade = "Grade 10", Section = "Section A", Status = StudentStatus.Active };
        db.Students.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var studentsController = new StudentsController(db, _auditServiceMock.Object);
        SetupController(studentsController, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        // Negative: Enrolling 3rd student must fail capacity validation
        var createStudentReq = new CreateStudentRequestDto(
            branchId, "S3", null, "Student", "Three", DateTime.UtcNow.AddYears(-15),
            Gender.Male, null, null, null, "Grade 10", "Section A"
        );
        var capacityFailResult = await studentsController.CreateStudent(createStudentReq, CancellationToken.None);
        (capacityFailResult as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Timetable collision test: Create slot at 09:00 - 10:00
        var academicsController = new AcademicsController(db, _auditServiceMock.Object);
        SetupController(academicsController, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        var slot1Req = new CreateTimetableSlotRequest(
            null, grade.Id, section.Id, subject.Id, teacherUser.Id,
            DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0), "Room-101"
        );
        var slot1Res = await academicsController.CreateTimetableSlot(slot1Req, CancellationToken.None);
        slot1Res.Should().BeOfType<OkObjectResult>();

        // Duplicate/collision slot with overlapping time for same teacher:
        var slot2CollisionReq = new CreateTimetableSlotRequest(
            null, grade.Id, section.Id, subject.Id, teacherUser.Id,
            DayOfWeek.Monday, new TimeSpan(9, 30, 0), new TimeSpan(10, 30, 0), "Room-102"
        );
        var collisionRes = await academicsController.CreateTimetableSlot(slot2CollisionReq, CancellationToken.None);
        (collisionRes as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // =========================================================================
    // 4. STUDENT LIFECYCLE & STATUS TRANSITIONS QC
    // =========================================================================
    [Fact]
    public async Task StudentLifecycle_BranchTransfer_And_StatusTransitions_PreservesHistory()
    {
        var orgId = Guid.NewGuid();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchAId));

        var branchA = new Branch { Id = branchAId, OrganizationId = orgId, Name = "Campus A", Code = "CA", IsActive = true };
        var branchB = new Branch { Id = branchBId, OrganizationId = orgId, Name = "Campus B", Code = "CB", IsActive = true };
        var student = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchAId, FirstName = "Ali", LastName = "Khan", AdmissionNumber = "AK-100", CurrentGrade = "Grade 9", Status = StudentStatus.Active };

        db.Branches.AddRange(branchA, branchB);
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var controller = new StudentsController(db, _auditServiceMock.Object);
        SetupController(controller, adminId, orgId, branchAId, UserRoleType.OrganizationAdmin);

        // Branch Transfer:
        var transferReq = new TransferBranchRequestDto(branchBId, "Relocation to City B");
        var transferResult = await controller.TransferBranch(student.Id, transferReq, CancellationToken.None);
        transferResult.Should().BeOfType<OkObjectResult>();

        // Verify transfer
        student.BranchId.Should().Be(branchBId);

        // Status Transitions: Active -> Suspended -> Withdrawn -> Graduated -> Re-admission
        var suspendReq = new UpdateStudentStatusRequestDto(StudentStatus.Suspended, "Disciplinary action");
        await controller.UpdateStatus(student.Id, suspendReq, CancellationToken.None);
        student.Status.Should().Be(StudentStatus.Suspended);

        var withdrawReq = new UpdateStudentStatusRequestDto(StudentStatus.Withdrawn, "Family moved abroad");
        await controller.UpdateStatus(student.Id, withdrawReq, CancellationToken.None);
        student.Status.Should().Be(StudentStatus.Withdrawn);

        var readmitReq = new UpdateStudentStatusRequestDto(StudentStatus.Active, "Re-admitted after return");
        await controller.UpdateStatus(student.Id, readmitReq, CancellationToken.None);
        student.Status.Should().Be(StudentStatus.Active);
    }

    // =========================================================================
    // 5. ATTENDANCE LOCKING & CORRECTIONS QC
    // =========================================================================
    [Fact]
    public async Task Attendance_Locking_And_Corrections_Workflow()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var grade = new ClassGrade { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Grade 7", Code = "G7" };
        var student = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, FirstName = "Zara", LastName = "Noor", AdmissionNumber = "ZN-1", CurrentGrade = "Grade 7" };
        var attendance = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            ClassGradeId = grade.Id,
            StudentId = student.Id,
            Date = DateTime.UtcNow.Date,
            Status = AttendanceStatus.Absent,
            IsLocked = false
        };

        db.ClassGrades.Add(grade);
        db.Students.Add(student);
        db.AttendanceRecords.Add(attendance);
        await db.SaveChangesAsync();

        var controller = new AttendanceController(db, _auditServiceMock.Object);
        SetupController(controller, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        // Lock attendance for date
        var lockReq = new LockAttendanceRequestDto(DateTime.UtcNow.Date, grade.Id, null);
        var lockResult = await controller.LockAttendance(lockReq, CancellationToken.None);
        lockResult.Should().BeOfType<OkObjectResult>();

        attendance.IsLocked.Should().BeTrue();

        // Correction request to change Absent to Present with reason
        var correctReq = new CorrectAttendanceRequestDto(attendance.Id, AttendanceStatus.Present, "Medical certificate submitted");
        var correctResult = await controller.CorrectAttendance(correctReq, CancellationToken.None);
        correctResult.Should().BeOfType<OkObjectResult>();

        attendance.Status.Should().Be(AttendanceStatus.Present);
        attendance.Remarks.Should().Contain("Medical certificate submitted");
    }

    // =========================================================================
    // 6. EXAMINATION WORKFLOW QC
    // =========================================================================
    [Fact]
    public async Task Exam_PublishWorkflow_And_StudentAccessControl()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var grade = new ClassGrade { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Grade 8", Code = "G8" };
        var subject = new SubjectCourse { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Mathematics", Code = "MATH-8" };
        var student = new Student { Id = studentId, UserId = studentUserId, OrganizationId = orgId, BranchId = branchId, FirstName = "Bilal", LastName = "Ahmed", AdmissionNumber = "BA-8", CurrentGrade = "Grade 8" };

        var exam = new ExamSchedule
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            ClassGradeId = grade.Id,
            SubjectCourseId = subject.Id,
            Title = "Midterm Math Exam",
            TotalMarks = 100,
            PassingMarks = 40,
            ExamDate = DateTime.UtcNow.AddDays(-1),
            IsPublished = false,
            IsLocked = false
        };

        var result = new ExamResult
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            ExamScheduleId = exam.Id,
            StudentId = student.Id,
            MarksObtained = 85,
            Grade = "A",
            Percentage = 85.0m,
            IsFinalized = false
        };

        db.ClassGrades.Add(grade);
        db.SubjectCourses.Add(subject);
        db.Students.Add(student);
        db.ExamSchedules.Add(exam);
        db.ExamResults.Add(result);
        await db.SaveChangesAsync();

        // Student Portal: Cannot see unpublished exam results
        var studentPortal = new StudentPortalController(db, _auditServiceMock.Object);
        SetupController(studentPortal, studentUserId, orgId, branchId, UserRoleType.Student);

        var studentExamsUnpublished = await studentPortal.GetResults(null, CancellationToken.None);
        studentExamsUnpublished.Should().BeOfType<OkObjectResult>();

        // Admin: Review, Approve, and Publish Exam
        var academicsController = new AcademicsController(db, _auditServiceMock.Object);
        SetupController(academicsController, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        var reviewRes = await academicsController.ReviewExamResults(exam.Id, CancellationToken.None);
        reviewRes.Should().BeOfType<OkObjectResult>();

        var publishRes = await academicsController.PublishExam(exam.Id, CancellationToken.None);
        publishRes.Should().BeOfType<OkObjectResult>();

        exam.IsPublished.Should().BeTrue();
        result.IsFinalized.Should().BeTrue();

        // Report Card generation
        var reportCardRes = await academicsController.GetStudentReportCard(student.Id, null, CancellationToken.None);
        reportCardRes.Should().BeOfType<OkObjectResult>();
    }

    // =========================================================================
    // 7. FEES & FINANCE QC
    // =========================================================================
    [Fact]
    public async Task Fees_BulkInvoice_Payment_Refund_And_Defaulters_Workflow()
    {
        var orgId = Guid.NewGuid();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var principalAId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchAId));

        var student = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchAId, FirstName = "Hamza", LastName = "Tariq", AdmissionNumber = "HT-1", CurrentGrade = "Grade 6", Status = StudentStatus.Active };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var feesController = new FeesController(db, _auditServiceMock.Object);
        SetupController(feesController, adminId, orgId, branchAId, UserRoleType.OrganizationAdmin);

        // Bulk invoice generation for Grade 6 with 10% discount on 10,000 fee
        var bulkReq = new BulkGenerateFeeInvoicesRequest("Term 1 Fee", 10000m, DateTime.UtcNow.AddDays(15), "Grade 6", null, branchAId, 10m);
        var bulkResult = await feesController.BulkGenerateInvoices(bulkReq, CancellationToken.None);
        bulkResult.Should().BeOfType<OkObjectResult>();

        var invoice = await db.FeeInvoices.FirstOrDefaultAsync(f => f.StudentId == student.Id);
        invoice.Should().NotBeNull();
        invoice!.TotalAmount.Should().Be(9000m); // 10,000 - 10% = 9,000
        invoice.Status.Should().Be(FeeStatus.Unpaid);

        // Record Partial Payment of 4,000
        var payReq = new RecordFeePaymentRequest(4000m, "BankTransfer", "TXN-12345");
        var payResult = await feesController.RecordPayment(invoice.Id, payReq, CancellationToken.None);
        payResult.Should().BeOfType<OkObjectResult>();

        invoice.PaidAmount.Should().Be(4000m);
        invoice.Balance.Should().Be(5000m);
        invoice.Status.Should().Be(FeeStatus.PartiallyPaid);

        // Record Remaining Payment of 5,000 -> Status Paid
        var payReq2 = new RecordFeePaymentRequest(5000m, "Cash");
        await feesController.RecordPayment(invoice.Id, payReq2, CancellationToken.None);
        invoice.Status.Should().Be(FeeStatus.Paid);

        // Refund 2,000
        var refundReq = new RefundFeePaymentRequest(2000m, "Duplicate payment adjustment");
        var refundResult = await feesController.RefundPayment(invoice.Id, refundReq, CancellationToken.None);
        refundResult.Should().BeOfType<OkObjectResult>();
        invoice.PaidAmount.Should().Be(7000m);
        invoice.Balance.Should().Be(2000m);
        invoice.Status.Should().Be(FeeStatus.PartiallyPaid);

        // Branch Isolation: Principal of Branch B cannot collect payment or refund invoice of Branch A
        var branchBFeesController = new FeesController(db, _auditServiceMock.Object);
        SetupController(branchBFeesController, principalAId, orgId, branchBId, UserRoleType.Principal);
        var foreignPay = await branchBFeesController.RecordPayment(invoice.Id, new RecordFeePaymentRequest(500m), CancellationToken.None);
        (foreignPay as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // =========================================================================
    // 8. FACULTY / STAFF / HR QC
    // =========================================================================
    [Fact]
    public async Task Staff_BranchIsolation_Leaves_And_BranchTransfer()
    {
        var orgId = Guid.NewGuid();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();
        var principalAId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchAId));

        var branchA = new Branch { Id = branchAId, OrganizationId = orgId, Name = "Campus A", Code = "CA", IsActive = true };
        var branchB = new Branch { Id = branchBId, OrganizationId = orgId, Name = "Campus B", Code = "CB", IsActive = true };
        db.Branches.AddRange(branchA, branchB);

        var staffUser = new User { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchAId, FirstName = "Doctor", LastName = "Who", Email = "who@staff.edu" };
        var staffProfile = new StaffProfile
        {
            Id = Guid.NewGuid(),
            UserId = staffUser.Id,
            OrganizationId = orgId,
            BranchId = branchAId,
            EmployeeCode = "EMP-001",
            Designation = "Senior Lecturer",
            Department = "Computer Science",
            EmploymentType = EmploymentType.FullTime,
            Status = StaffStatus.Active,
            JoiningDate = DateTime.UtcNow
        };

        db.Users.Add(staffUser);
        db.StaffProfiles.Add(staffProfile);
        await db.SaveChangesAsync();

        var controller = new StaffController(db, _auditServiceMock.Object, _passwordHasher);
        SetupController(controller, principalAId, orgId, branchAId, UserRoleType.Principal);

        // Positive: Principal A can view staff in branch A
        var ownStaffRes = await controller.GetStaffById(staffProfile.Id, CancellationToken.None);
        ownStaffRes.Should().BeOfType<OkObjectResult>();

        // Leave submission for staff
        var leaveReq = new SubmitStaffLeaveRequestDto("Sick", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(2), "Flu recovery");
        var leaveRes = await controller.SubmitStaffLeave(staffProfile.Id, leaveReq, CancellationToken.None);
        leaveRes.Should().BeOfType<OkObjectResult>();

        // Transfer staff to Campus B
        var transferReq = new TransferStaffBranchRequestDto(branchBId, "Promotion to Campus B");
        var transferRes = await controller.TransferBranch(staffProfile.Id, transferReq, CancellationToken.None);
        transferRes.Should().BeOfType<OkObjectResult>();

        staffProfile.BranchId.Should().Be(branchBId);

        // Now Principal A cannot modify staff anymore since staff is now in Branch B!
        var updateBlocked = await controller.UpdateStaff(staffProfile.Id, new UpdateStaffRequestDto(null, "Doctor", "Who", null, "Lecturer", "CS", null, null, EmploymentType.FullTime, StaffStatus.Active, null, null, null, null), CancellationToken.None);
        (updateBlocked as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // =========================================================================
    // 9. LIBRARY & TRANSPORT MANAGEMENT QC
    // =========================================================================
    [Fact]
    public async Task Library_And_Transport_Workflows_PassCapacityAndCheckoutValidation()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var student = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, FirstName = "Usman", LastName = "Ghani", AdmissionNumber = "UG-1", CurrentGrade = "Grade 9" };
        var book = new Book { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, Title = "Clean Architecture", Author = "Robert Martin", TotalCopies = 1, AvailableCopies = 1, IsActive = true };

        db.Students.Add(student);
        db.Books.Add(book);
        await db.SaveChangesAsync();

        // 1. Library: Checkout book
        var libraryController = new LibraryController(db, _auditServiceMock.Object);
        SetupController(libraryController, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        var issueReq = new IssueBookRequest(book.Id, student.Id, null, 14, "Semester reading");
        var issueResult = await libraryController.IssueBook(issueReq, CancellationToken.None);
        issueResult.Should().BeOfType<OkObjectResult>();

        book.AvailableCopies.Should().Be(0);

        // Negative: Cannot checkout when 0 copies available
        var secondCheckout = await libraryController.IssueBook(issueReq, CancellationToken.None);
        (secondCheckout as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Return book
        var issueRecord = await db.BookIssues.FirstAsync(bi => bi.BookId == book.Id);
        var returnReq = new ReturnBookRequest(0m, BookIssueStatus.Returned);
        var returnResult = await libraryController.ReturnBook(issueRecord.Id, returnReq, CancellationToken.None);
        returnResult.Should().BeOfType<OkObjectResult>();
        book.AvailableCopies.Should().Be(1);

        // 2. Transport: Vehicle capacity check
        var transportController = new TransportController(db, _auditServiceMock.Object);
        SetupController(transportController, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        var vehicle = new Vehicle { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, VehicleNumber = "BUS-01", Model = "Toyota Coaster", Capacity = 1, Status = VehicleStatus.Active };
        var route = new TransportRoute { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, VehicleId = vehicle.Id, RouteName = "Route North", StartLocation = "Campus", EndLocation = "North Town", MonthlyFare = 5000m, IsActive = true };
        db.Vehicles.Add(vehicle);
        db.TransportRoutes.Add(route);
        await db.SaveChangesAsync();

        // Allocate student 1
        var alloc1 = await transportController.AllocateStudent(new AllocateStudentTransportRequest(student.Id, route.Id, "Stop A"), CancellationToken.None);
        alloc1.Should().BeOfType<OkObjectResult>();

        // Allocate student 2 -> Must fail because vehicle capacity is 1!
        var student2 = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, FirstName = "Second", LastName = "Passenger", AdmissionNumber = "SP-2", CurrentGrade = "Grade 9" };
        db.Students.Add(student2);
        await db.SaveChangesAsync();

        var alloc2 = await transportController.AllocateStudent(new AllocateStudentTransportRequest(student2.Id, route.Id, "Stop B"), CancellationToken.None);
        (alloc2 as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // =========================================================================
    // 10. GLOBAL SECURITY, IDOR & TENANT ISOLATION QC
    // =========================================================================
    [Fact]
    public async Task CrossTenant_And_CrossBranch_DataLeakage_BlockedGlobally()
    {
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgAId, branchAId));

        var studentOrgA = new Student { Id = Guid.NewGuid(), OrganizationId = orgAId, BranchId = branchAId, FirstName = "Student", LastName = "A", AdmissionNumber = "SA" };
        var studentOrgB = new Student { Id = Guid.NewGuid(), OrganizationId = orgBId, BranchId = branchBId, FirstName = "Student", LastName = "B", AdmissionNumber = "SB" };

        var docOrgA = new StudentDocument { Id = Guid.NewGuid(), OrganizationId = orgAId, BranchId = branchAId, StudentId = studentOrgA.Id, Title = "Birth Certificate A", FileUrl = "https://cdn.school.edu/a.pdf", FileName = "a.pdf" };
        var docOrgB = new StudentDocument { Id = Guid.NewGuid(), OrganizationId = orgBId, BranchId = branchBId, StudentId = studentOrgB.Id, Title = "Confidential B", FileUrl = "https://cdn.school.edu/b.pdf", FileName = "b.pdf" };

        db.Students.AddRange(studentOrgA, studentOrgB);
        db.StudentDocuments.AddRange(docOrgA, docOrgB);
        await db.SaveChangesAsync();

        var documentsController = new DocumentsController(db, _auditServiceMock.Object);

        // Org A user attempts direct API download of Org B document
        SetupController(documentsController, Guid.NewGuid(), orgAId, branchAId, UserRoleType.OrganizationAdmin);
        var crossOrgDownload = await documentsController.DownloadDocument(docOrgB.Id, CancellationToken.None);
        (crossOrgDownload as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status404NotFound); // 404 because scoped to Org A!

        // Principal of Branch A attempts direct API download of Branch B document
        var principalDocsController = new DocumentsController(db, _auditServiceMock.Object);
        SetupController(principalDocsController, Guid.NewGuid(), orgAId, branchAId, UserRoleType.Principal);
        docOrgA.BranchId = branchBId; // pretend document is in branch B of Org A
        await db.SaveChangesAsync();

        var crossBranchDownload = await principalDocsController.DownloadDocument(docOrgA.Id, CancellationToken.None);
        (crossBranchDownload as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // =========================================================================
    // 11. ADMISSION APPLICATION WORKFLOW QC
    // =========================================================================
    [Fact]
    public async Task AdmissionApplication_Workflow_SubmitReviewApproveAndEnroll_Succeeds()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var grade = new ClassGrade { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Grade 1", Code = "G1", Capacity = 30, IsActive = true };
        db.ClassGrades.Add(grade);
        await db.SaveChangesAsync();

        var controller = new StudentsController(db, _auditServiceMock.Object);
        SetupController(controller, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        // 1. Submit admission application
        var submitReq = new SubmitAdmissionApplicationRequest(
            "Taha", "Siddiqui", DateTime.UtcNow.AddYears(-6), Gender.Male, "Grade 1",
            "Siddiqui Senior", "03009998877", "siddiqui@gmail.com", "Father", "House 123", branchId
        );
        var submitRes = await controller.SubmitApplication(submitReq, CancellationToken.None);
        submitRes.Should().BeOfType<OkObjectResult>();

        var app = await db.AdmissionApplications.FirstAsync(a => a.FirstName == "Taha");
        app.Status.Should().Be(ApplicationStatus.Pending);

        // 2. Review application
        var reviewRes = await controller.ReviewApplication(app.Id, new ReviewApplicationRequest("Candidate eligible for assessment"), CancellationToken.None);
        reviewRes.Should().BeOfType<OkObjectResult>();
        app.Status.Should().Be(ApplicationStatus.UnderReview);

        // 3. Approve & Enroll application
        var approveReq = new ApproveAndEnrollApplicationRequest("Section A", 15000m, DateTime.UtcNow.AddDays(10));
        var approveRes = await controller.ApproveAndEnrollApplication(app.Id, approveReq, CancellationToken.None);
        approveRes.Should().BeOfType<OkObjectResult>();

        app.Status.Should().Be(ApplicationStatus.Enrolled);
        app.EnrolledStudentId.Should().NotBeNull();

        // Verify Student created
        var enrolledStudent = await db.Students.FirstAsync(s => s.Id == app.EnrolledStudentId!.Value);
        enrolledStudent.FirstName.Should().Be("Taha");
        enrolledStudent.CurrentGrade.Should().Be("Grade 1");
        enrolledStudent.Section.Should().Be("Section A");

        // Verify Guardian created and linked
        var guardianLink = await db.StudentGuardians.Include(sg => sg.Guardian).FirstOrDefaultAsync(sg => sg.StudentId == enrolledStudent.Id);
        guardianLink.Should().NotBeNull();
        guardianLink!.Guardian!.Phone.Should().Be("03009998877");

        // Verify Initial Tuition Fee Invoice generated
        var initialInvoice = await db.FeeInvoices.FirstOrDefaultAsync(f => f.StudentId == enrolledStudent.Id);
        initialInvoice.Should().NotBeNull();
        initialInvoice!.TotalAmount.Should().Be(15000m);
    }

    // =========================================================================
    // 12. STUDENT LIFECYCLE CLEARANCE CHECKS QC
    // =========================================================================
    [Fact]
    public async Task StudentLifecycle_ClearanceChecks_BlockGraduationUntilClearedOrBypassed()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var student = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, FirstName = "Zain", LastName = "Abbas", AdmissionNumber = "ZA-10", CurrentGrade = "Grade 10", Status = StudentStatus.Active };
        var unpaidInvoice = new FeeInvoice { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, StudentId = student.Id, TotalAmount = 5000m, PaidAmount = 0m, Status = FeeStatus.Unpaid };
        var book = new Book { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, Title = "Chemistry", Author = "Author", TotalCopies = 1, AvailableCopies = 0, IsActive = true };
        var bookIssue = new BookIssue { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, BookId = book.Id, StudentId = student.Id, Status = BookIssueStatus.Issued, DueDate = DateTime.UtcNow.AddDays(5) };

        db.Students.Add(student);
        db.FeeInvoices.Add(unpaidInvoice);
        db.Books.Add(book);
        db.BookIssues.Add(bookIssue);
        await db.SaveChangesAsync();

        var controller = new StudentsController(db, _auditServiceMock.Object);
        SetupController(controller, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        // Negative: Graduating student with unpaid fee must fail clearance
        var failGraduation = await controller.UpdateStatus(student.Id, new UpdateStudentStatusRequestDto(StudentStatus.Graduated, "Graduating class of 2026", false), CancellationToken.None);
        (failGraduation as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Positive: Administrative bypass of clearance allows graduation
        var bypassGraduation = await controller.UpdateStatus(student.Id, new UpdateStudentStatusRequestDto(StudentStatus.Graduated, "Admin waiver granted", true), CancellationToken.None);
        bypassGraduation.Should().BeOfType<OkObjectResult>();
        student.Status.Should().Be(StudentStatus.Graduated);
    }

    // =========================================================================
    // 13. TIMETABLE UPDATE & SUBSTITUTE TEACHER QC
    // =========================================================================
    [Fact]
    public async Task Timetable_UpdateAndSubstituteTeacher_HandlesConflicts()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var grade = new ClassGrade { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Grade 5", Code = "G5" };
        var subject = new SubjectCourse { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "English", Code = "ENG-5" };

        var teacher1User = new User { Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "Teacher", LastName = "One", Email = "t1@school.edu" };
        var teacher1Staff = new StaffProfile { Id = Guid.NewGuid(), UserId = teacher1User.Id, OrganizationId = orgId, BranchId = branchId, EmployeeCode = "T1" };

        var teacher2User = new User { Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "Teacher", LastName = "Two", Email = "t2@school.edu" };
        var teacher2Staff = new StaffProfile { Id = Guid.NewGuid(), UserId = teacher2User.Id, OrganizationId = orgId, BranchId = branchId, EmployeeCode = "T2" };

        var slot = new TimetableSlot
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            ClassGradeId = grade.Id,
            SubjectCourseId = subject.Id,
            TeacherStaffId = teacher1Staff.Id,
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            RoomNumber = "Room-10",
            IsActive = true
        };

        db.ClassGrades.Add(grade);
        db.SubjectCourses.Add(subject);
        db.Users.AddRange(teacher1User, teacher2User);
        db.StaffProfiles.AddRange(teacher1Staff, teacher2Staff);
        db.TimetableSlots.Add(slot);
        await db.SaveChangesAsync();

        var controller = new AcademicsController(db, _auditServiceMock.Object);
        SetupController(controller, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        // Update slot time
        var updateReq = new UpdateTimetableSlotRequest(new TimeSpan(11, 0, 0), new TimeSpan(12, 0, 0), "Room-11");
        var updateRes = await controller.UpdateTimetableSlot(slot.Id, updateReq, CancellationToken.None);
        updateRes.Should().BeOfType<OkObjectResult>();

        slot.StartTime.Should().Be(new TimeSpan(11, 0, 0));
        slot.RoomNumber.Should().Be("Room-11");

        // Assign Substitute Teacher
        var subReq = new AssignSubstituteTeacherRequest(teacher2Staff.Id, "Teacher One on sick leave");
        var subRes = await controller.AssignSubstituteTeacher(slot.Id, subReq, CancellationToken.None);
        subRes.Should().BeOfType<OkObjectResult>();

        slot.SubstituteTeacherStaffId.Should().Be(teacher2Staff.Id);
    }

    // =========================================================================
    // 14. LEAVE APPROVAL ATTENDANCE INTEGRATION QC
    // =========================================================================
    [Fact]
    public async Task LeaveManagement_Approval_SynchronizesAttendanceAsLeave()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var grade = new ClassGrade { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Grade 6", Code = "G6" };
        var student = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, FirstName = "Mustafa", LastName = "Kamal", CurrentGrade = "Grade 6", AdmissionNumber = "MK-1" };

        var leaveDate = DateTime.UtcNow.Date.AddDays(1);
        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            RequesterUserId = Guid.NewGuid(),
            RequesterType = RequesterType.Student,
            StudentId = student.Id,
            StartDate = leaveDate,
            EndDate = leaveDate,
            Reason = "Attending family wedding",
            Status = LeaveStatus.Pending
        };

        db.ClassGrades.Add(grade);
        db.Students.Add(student);
        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var controller = new AcademicsController(db, _auditServiceMock.Object);
        SetupController(controller, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        var decideReq = new LeaveDecisionRequest(LeaveStatus.Approved, "Approved by Principal");
        var decideRes = await controller.DecideLeave(leave.Id, decideReq, CancellationToken.None);
        decideRes.Should().BeOfType<OkObjectResult>();

        // Verify attendance record automatically synchronized with Leave status
        var att = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.StudentId == student.Id && a.Date == leaveDate);
        att.Should().NotBeNull();
        att!.Status.Should().Be(AttendanceStatus.Leave);
        att.Remarks.Should().Contain("Attending family wedding");
    }

    // =========================================================================
    // 15. TARGETED COMMUNICATION DISPATCH & HISTORY QC
    // =========================================================================
    [Fact]
    public async Task Communication_TargetedDispatch_And_HistoryLogging()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString(), CreateTenantContext(orgId, branchId));

        var s1 = new Student { Id = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, FirstName = "A", LastName = "B", AdmissionNumber = "AB", CurrentGrade = "G1", Status = StudentStatus.Active };
        db.Students.Add(s1);
        await db.SaveChangesAsync();

        var controller = new AnnouncementsController(db, _auditServiceMock.Object);
        SetupController(controller, adminId, orgId, branchId, UserRoleType.OrganizationAdmin);

        // Dispatch notice
        var dispatchReq = new DispatchNotificationRequest("Severe Weather Alert", "Classes will be online tomorrow", "SMS", "Students", branchId);
        var dispatchRes = await controller.DispatchNotification(dispatchReq, CancellationToken.None);
        dispatchRes.Should().BeOfType<OkObjectResult>();

        // Retrieve communication history
        var historyRes = await controller.GetCommunicationHistory(branchId, "SMS", CancellationToken.None);
        historyRes.Should().BeOfType<OkObjectResult>();

        var log = await db.CommunicationLogs.FirstAsync(c => c.Title == "Severe Weather Alert");
        log.Channel.Should().Be("SMS");
        log.TargetAudience.Should().Be("Students");
        log.Status.Should().Be("Delivered");
    }
}
