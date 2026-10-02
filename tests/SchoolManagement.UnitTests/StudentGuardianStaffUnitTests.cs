using FluentAssertions;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests;

public class StudentGuardianStaffUnitTests
{
    [Fact]
    public void Student_Initialization_SetsDefaultsCorrectly()
    {
        var student = new Student
        {
            FirstName = "Ayaan",
            LastName = "Malik",
            AdmissionNumber = "ADM-2026-0042",
            CurrentGrade = "Grade 4",
            Gender = Gender.Male,
            DateOfBirth = new DateTime(2016, 4, 15)
        };

        student.FullName.Should().Be("Ayaan Malik");
        student.Status.Should().Be(StudentStatus.Active);
        student.StudentGuardians.Should().NotBeNull();
        student.StudentGuardians.Should().BeEmpty();
        student.IsDeleted.Should().BeFalse();
        student.AdmissionDate.Date.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public void Guardian_Initialization_SetsPropertiesCorrectly()
    {
        var guardian = new Guardian
        {
            FirstName = "Sajid",
            LastName = "Malik",
            Phone = "+92 300 1234567",
            RelationshipType = GuardianRelationshipType.Father,
            NationalId = "61101-1234567-1",
            Email = "sajid.malik@domain.pk",
            Occupation = "Chartered Accountant",
            AnnualIncome = 2500000,
            IsEmergencyContact = true
        };

        guardian.FullName.Should().Be("Sajid Malik");
        guardian.StudentGuardians.Should().NotBeNull();
        guardian.StudentGuardians.Should().BeEmpty();
        guardian.IsEmergencyContact.Should().BeTrue();
        guardian.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void StudentGuardian_AssociatesStudentAndGuardian_WithPickupAuthorization()
    {
        var studentId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();

        var link = new StudentGuardian
        {
            StudentId = studentId,
            GuardianId = guardianId,
            Relationship = "Mother",
            IsPrimaryContact = true,
            IsAuthorizedToPickup = true
        };

        link.StudentId.Should().Be(studentId);
        link.GuardianId.Should().Be(guardianId);
        link.Relationship.Should().Be("Mother");
        link.IsPrimaryContact.Should().BeTrue();
        link.IsAuthorizedToPickup.Should().BeTrue();
        link.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Guardian_WithMultipleStudentLinks_RepresentsSiblingGroup()
    {
        var guardian = new Guardian
        {
            Id = Guid.NewGuid(),
            FirstName = "Imran",
            LastName = "Qureshi",
            Phone = "+92 321 9876543",
            RelationshipType = GuardianRelationshipType.Father
        };

        var student1 = new Student
        {
            Id = Guid.NewGuid(),
            FirstName = "Bilal",
            LastName = "Qureshi",
            AdmissionNumber = "ADM-2026-0001",
            CurrentGrade = "Grade 7",
            Gender = Gender.Male
        };

        var student2 = new Student
        {
            Id = Guid.NewGuid(),
            FirstName = "Hira",
            LastName = "Qureshi",
            AdmissionNumber = "ADM-2026-0002",
            CurrentGrade = "Grade 3",
            Gender = Gender.Female
        };

        var link1 = new StudentGuardian
        {
            StudentId = student1.Id,
            GuardianId = guardian.Id,
            Student = student1,
            Guardian = guardian,
            Relationship = "Father",
            IsPrimaryContact = true
        };

        var link2 = new StudentGuardian
        {
            StudentId = student2.Id,
            GuardianId = guardian.Id,
            Student = student2,
            Guardian = guardian,
            Relationship = "Father",
            IsPrimaryContact = true
        };

        guardian.StudentGuardians.Add(link1);
        guardian.StudentGuardians.Add(link2);

        guardian.StudentGuardians.Should().HaveCount(2);
        guardian.StudentGuardians.Select(sg => sg.Student!.FullName).Should().Contain(new[] { "Bilal Qureshi", "Hira Qureshi" });
        guardian.StudentGuardians.Select(sg => sg.Student!.CurrentGrade).Should().Contain(new[] { "Grade 7", "Grade 3" });
    }

    [Fact]
    public void StaffProfile_InitializationAndUserLink_ReflectsProfileDetails()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Zubair",
            LastName = "Shah",
            Email = "zubair.shah@school.edu",
            PhoneNumber = "+92 345 6789012",
            RoleType = UserRoleType.Teacher,
            IsActive = true
        };

        var staff = new StaffProfile
        {
            UserId = user.Id,
            User = user,
            EmployeeCode = "EMP-2026-0015",
            Designation = "Head of Computer Science",
            Department = "Information Technology",
            Qualification = "MS Computer Science",
            Specialization = "Software Engineering & AI",
            EmploymentType = EmploymentType.FullTime,
            Status = StaffStatus.Active,
            JoiningDate = new DateTime(2024, 1, 10),
            BasicSalary = 120000,
            EmergencyContactName = "Mrs. Zubair",
            EmergencyContactPhone = "+92 345 9990000",
            NationalId = "35202-9876543-1"
        };

        staff.User.Should().NotBeNull();
        staff.User!.FullName.Should().Be("Zubair Shah");
        staff.EmployeeCode.Should().Be("EMP-2026-0015");
        staff.Designation.Should().Be("Head of Computer Science");
        staff.Department.Should().Be("Information Technology");
        staff.EmploymentType.Should().Be(EmploymentType.FullTime);
        staff.Status.Should().Be(StaffStatus.Active);
        staff.BasicSalary.Should().Be(120000);
        staff.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("ADM-2026-0001", true)]
    [InlineData("ADM-2026-9999", true)]
    [InlineData("EMP-2026-0001", true)]
    [InlineData("INVALID-CODE", false)]
    public void EnrollmentAndEmployeeCode_FormatValidation(string code, bool isValidPattern)
    {
        bool matches = System.Text.RegularExpressions.Regex.IsMatch(code, @"^(ADM|EMP)-\d{4}-\d{4}$");
        matches.Should().Be(isValidPattern);
    }
}
