using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Tenant;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ThemePrimaryColor { get; set; } = "#6C5CE7"; // Pak2Source purple
    public string ThemeSecondaryColor { get; set; } = "#5840BA";
    public bool ThemeDarkMode { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int MaxBranches { get; set; } = 5;
    public string? Website { get; set; }
    public string? PrincipalName { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? OrganizationType { get; set; } = "School"; // School, Institute, College, Academy
    public string? Description { get; set; }
    public string? WorkingDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";
    public string? WorkingHours { get; set; } = "08:00 - 14:00";
    public string? TimeZone { get; set; } = "Pakistan Standard Time";
    public string? Currency { get; set; } = "PKR";
    public string? DateFormat { get; set; } = "yyyy-MM-dd";
    public string? Language { get; set; } = "en";

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<OrganizationModule> Modules { get; set; } = new List<OrganizationModule>();
    public ICollection<AcademicSession> AcademicSessions { get; set; } = new List<AcademicSession>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<ClassGrade> ClassGrades { get; set; } = new List<ClassGrade>();
    public ICollection<SubjectCourse> SubjectCourses { get; set; } = new List<SubjectCourse>();
    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
}
