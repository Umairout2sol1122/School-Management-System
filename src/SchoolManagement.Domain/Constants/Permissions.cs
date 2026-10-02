namespace SchoolManagement.Domain.Constants;

public static class Permissions
{
    // Organization & Platform
    public const string OrganizationsManage = "Organizations.Manage";
    public const string OrganizationsView = "Organizations.View";
    public const string BranchesManage = "Branches.Manage";
    public const string BranchesView = "Branches.View";

    // Users & Roles
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersEdit = "Users.Edit";
    public const string UsersDisable = "Users.Disable";
    public const string UsersResetPassword = "Users.ResetPassword";
    public const string RolesView = "Roles.View";
    public const string RolesManage = "Roles.Manage";

    // Students
    public const string StudentsView = "Students.View";
    public const string StudentsCreate = "Students.Create";
    public const string StudentsEdit = "Students.Edit";
    public const string StudentsDelete = "Students.Delete";
    public const string StudentsExport = "Students.Export";

    // Parents
    public const string ParentsView = "Parents.View";
    public const string ParentsCreate = "Parents.Create";
    public const string ParentsEdit = "Parents.Edit";
    public const string ParentsDelete = "Parents.Delete";

    // Teachers & Staff
    public const string TeachersView = "Teachers.View";
    public const string TeachersCreate = "Teachers.Create";
    public const string TeachersEdit = "Teachers.Edit";
    public const string TeachersDelete = "Teachers.Delete";
    public const string TeachersAssign = "Teachers.Assign";

    // Academics
    public const string AcademicsView = "Academics.View";
    public const string AcademicsManage = "Academics.Manage";

    // Attendance
    public const string AttendanceView = "Attendance.View";
    public const string AttendanceMark = "Attendance.Mark";
    public const string AttendanceEdit = "Attendance.Edit";
    public const string AttendanceApprove = "Attendance.Approve";

    // Fees
    public const string FeesView = "Fees.View";
    public const string FeesCreateStructure = "Fees.CreateStructure";
    public const string FeesGenerateInvoices = "Fees.GenerateInvoices";
    public const string FeesCollectPayment = "Fees.CollectPayment";
    public const string FeesRefund = "Fees.Refund";
    public const string FeesExport = "Fees.Export";

    // Examinations
    public const string ExamsView = "Exams.View";
    public const string ExamsSchedule = "Exams.Schedule";
    public const string ExamsEnterMarks = "Exams.EnterMarks";
    public const string ExamsPublishResults = "Exams.PublishResults";

    // Timetable
    public const string TimetableView = "Timetable.View";
    public const string TimetableManage = "Timetable.Manage";

    // Homework
    public const string HomeworkView = "Homework.View";
    public const string HomeworkCreate = "Homework.Create";
    public const string HomeworkReview = "Homework.Review";

    // Communication
    public const string CommunicationView = "Communication.View";
    public const string CommunicationSend = "Communication.Send";

    // Reports & Audit
    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";
    public const string AuditView = "Audit.View";

    public static IReadOnlyList<string> All => new[]
    {
        OrganizationsManage, OrganizationsView, BranchesManage, BranchesView,
        UsersView, UsersCreate, UsersEdit, UsersDisable, UsersResetPassword,
        RolesView, RolesManage,
        StudentsView, StudentsCreate, StudentsEdit, StudentsDelete, StudentsExport,
        ParentsView, ParentsCreate, ParentsEdit, ParentsDelete,
        TeachersView, TeachersCreate, TeachersEdit, TeachersDelete, TeachersAssign,
        AcademicsView, AcademicsManage,
        AttendanceView, AttendanceMark, AttendanceEdit, AttendanceApprove,
        FeesView, FeesCreateStructure, FeesGenerateInvoices, FeesCollectPayment, FeesRefund, FeesExport,
        ExamsView, ExamsSchedule, ExamsEnterMarks, ExamsPublishResults,
        TimetableView, TimetableManage,
        HomeworkView, HomeworkCreate, HomeworkReview,
        CommunicationView, CommunicationSend,
        ReportsView, ReportsExport, AuditView
    };
}
