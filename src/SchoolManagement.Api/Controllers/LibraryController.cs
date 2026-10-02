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
public class LibraryController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public LibraryController(IApplicationDbContext context, IAuditService auditService)
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

        var booksQuery = _context.Books.Where(b => b.OrganizationId == targetOrgId && !b.IsDeleted);
        var issuesQuery = _context.BookIssues.Where(bi => bi.OrganizationId == targetOrgId && !bi.IsDeleted);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            booksQuery = booksQuery.Where(b => b.BranchId == null || b.BranchId == CurrentUserService.BranchId.Value);
            issuesQuery = issuesQuery.Where(bi => bi.BranchId == CurrentUserService.BranchId.Value);
        }

        int totalTitles = await booksQuery.CountAsync(cancellationToken);
        int totalCopies = await booksQuery.SumAsync(b => (int?)b.TotalCopies, cancellationToken) ?? 0;
        int availableCopies = await booksQuery.SumAsync(b => (int?)b.AvailableCopies, cancellationToken) ?? 0;
        int issuedCount = await issuesQuery.CountAsync(bi => bi.Status == BookIssueStatus.Issued, cancellationToken);
        int overdueCount = await issuesQuery.CountAsync(bi => bi.Status == BookIssueStatus.Issued && bi.DueDate < DateTime.UtcNow, cancellationToken);
        decimal totalFines = await issuesQuery.SumAsync(bi => (decimal?)bi.FineAmount, cancellationToken) ?? 0m;

        return ApiSuccess(new
        {
            totalTitles,
            totalCopies,
            availableCopies,
            issuedCount,
            overdueCount,
            totalFines
        });
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] Guid? branchId,
        [FromQuery] bool? availableOnly,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Books
            .Include(b => b.Branch)
            .Where(b => b.OrganizationId == targetOrgId && !b.IsDeleted);

        // Branch Isolation: Principal can only see their own branch books
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(b => b.BranchId == null || b.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(b => b.BranchId == null || b.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "All")
            query = query.Where(b => b.Category.ToLower() == category.Trim().ToLower());

        if (availableOnly == true)
            query = query.Where(b => b.AvailableCopies > 0 && b.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(b =>
                b.Title.ToLower().Contains(term) ||
                b.Author.ToLower().Contains(term) ||
                (b.Isbn != null && b.Isbn.ToLower().Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var books = await query
            .OrderBy(b => b.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BookDto(
                b.Id,
                b.OrganizationId,
                b.BranchId,
                b.Branch != null ? b.Branch.Name : "All Campuses",
                b.Title,
                b.Author,
                b.Isbn,
                b.Publisher,
                b.Category,
                b.TotalCopies,
                b.AvailableCopies,
                b.RackNumber,
                b.IsActive
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(new
        {
            items = books,
            pageNumber,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("books/{id:guid}")]
    public async Task<IActionResult> GetBookById(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var b = await _context.Books
            .Include(b => b.Branch)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == targetOrgId && !b.IsDeleted, cancellationToken);

        if (b == null) return ApiNotFound("Book not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (b.BranchId.HasValue && b.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot access books outside your assigned branch.");
        }

        var dto = new BookDto(
            b.Id,
            b.OrganizationId,
            b.BranchId,
            b.Branch?.Name ?? "All Campuses",
            b.Title,
            b.Author,
            b.Isbn,
            b.Publisher,
            b.Category,
            b.TotalCopies,
            b.AvailableCopies,
            b.RackNumber,
            b.IsActive
        );

        return ApiSuccess(dto);
    }

    [HttpPost("books")]
    public async Task<IActionResult> CreateBook([FromBody] CreateBookRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Author))
            return ApiError("Book title and author are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        Guid? targetBranchId = request.BranchId ?? CurrentUserService.BranchId;
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            targetBranchId = CurrentUserService.BranchId.Value;
        }

        int copies = Math.Max(1, request.TotalCopies);

        var book = new Book
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Isbn = request.Isbn?.Trim(),
            Publisher = request.Publisher?.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            TotalCopies = copies,
            AvailableCopies = copies,
            RackNumber = request.RackNumber?.Trim(),
            IsActive = true
        };

        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Library", "Book", book.Id.ToString(),
            $"Added book '{book.Title}' by '{book.Author}' ({book.TotalCopies} copies)",
            organizationId: targetOrgId,
            branchId: book.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = book.Id, title = book.Title }, "Book added successfully.");
    }

    [HttpPut("books/{id:guid}")]
    public async Task<IActionResult> UpdateBook(Guid id, [FromBody] UpdateBookRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var book = await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == targetOrgId && !b.IsDeleted, cancellationToken);

        if (book == null) return ApiNotFound("Book not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (book.BranchId.HasValue && book.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot edit books outside your assigned branch.");
        }

        int diff = request.TotalCopies - book.TotalCopies;
        book.Title = request.Title.Trim();
        book.Author = request.Author.Trim();
        book.Isbn = request.Isbn?.Trim();
        book.Publisher = request.Publisher?.Trim();
        book.Category = request.Category.Trim();
        book.TotalCopies = Math.Max(1, request.TotalCopies);
        book.AvailableCopies = Math.Max(0, book.AvailableCopies + diff);
        book.RackNumber = request.RackNumber?.Trim();
        book.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Library", "Book", book.Id.ToString(),
            $"Updated book '{book.Title}'",
            organizationId: targetOrgId,
            branchId: book.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Book updated successfully.");
    }

    [HttpDelete("books/{id:guid}")]
    public async Task<IActionResult> DeleteBook(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var book = await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == targetOrgId && !b.IsDeleted, cancellationToken);

        if (book == null) return ApiNotFound("Book not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (book.BranchId.HasValue && book.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot delete books outside your assigned branch.");
        }

        bool hasActiveIssues = await _context.BookIssues
            .AnyAsync(bi => bi.BookId == id && bi.Status == BookIssueStatus.Issued && !bi.IsDeleted, cancellationToken);

        if (hasActiveIssues)
            return ApiError("Cannot delete book while there are copies currently checked out.");

        book.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Library", "Book", book.Id.ToString(),
            $"Deleted book '{book.Title}'",
            organizationId: targetOrgId,
            branchId: book.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Book deleted successfully.");
    }

    [HttpGet("issues")]
    public async Task<IActionResult> GetIssues(
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? staffProfileId,
        [FromQuery] BookIssueStatus? status,
        [FromQuery] bool? overdueOnly,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.BookIssues
            .Include(bi => bi.Book)
            .Include(bi => bi.Student)
            .Include(bi => bi.StaffProfile)
                .ThenInclude(sp => sp!.User)
            .Where(bi => bi.OrganizationId == targetOrgId && !bi.IsDeleted);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(bi => bi.BranchId == CurrentUserService.BranchId.Value);
        }

        if (studentId.HasValue)
            query = query.Where(bi => bi.StudentId == studentId.Value);

        if (staffProfileId.HasValue)
            query = query.Where(bi => bi.StaffProfileId == staffProfileId.Value);

        if (status.HasValue)
            query = query.Where(bi => bi.Status == status.Value);

        if (overdueOnly == true)
            query = query.Where(bi => bi.Status == BookIssueStatus.Issued && bi.DueDate < DateTime.UtcNow);

        var issues = await query
            .OrderByDescending(bi => bi.IssueDate)
            .Select(bi => new BookIssueDto(
                bi.Id,
                bi.BookId,
                bi.Book != null ? bi.Book.Title : "Unknown",
                bi.Book != null ? bi.Book.Author : "Unknown",
                bi.StudentId,
                bi.Student != null ? bi.Student.FullName : null,
                bi.Student != null ? bi.Student.AdmissionNumber : null,
                bi.StaffProfileId,
                bi.StaffProfile != null && bi.StaffProfile.User != null ? bi.StaffProfile.User.FullName : null,
                bi.StaffProfile != null ? bi.StaffProfile.EmployeeCode : null,
                bi.IssueDate,
                bi.DueDate,
                bi.ReturnDate,
                bi.FineAmount,
                bi.DueDate < DateTime.UtcNow && bi.Status == BookIssueStatus.Issued ? BookIssueStatus.Overdue : bi.Status,
                bi.Remarks
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(issues);
    }

    [HttpPost("issue")]
    public async Task<IActionResult> IssueBook([FromBody] IssueBookRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (!request.StudentId.HasValue && !request.StaffProfileId.HasValue)
            return ApiError("Either a student or staff member must be specified to issue a book.");

        var book = await _context.Books
            .FirstOrDefaultAsync(b => b.Id == request.BookId && b.OrganizationId == targetOrgId && !b.IsDeleted, cancellationToken);

        if (book == null) return ApiNotFound("Book not found.");

        if (book.AvailableCopies <= 0)
            return ApiError("No copies available for checkout. All copies are currently issued.");

        Guid? branchId = book.BranchId ?? CurrentUserService.BranchId;

        string recipientName = "Member";
        if (request.StudentId.HasValue)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == request.StudentId.Value && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);
            if (student == null) return ApiNotFound("Student not found.");
            recipientName = student.FullName;
            branchId = student.BranchId;
        }
        else if (request.StaffProfileId.HasValue)
        {
            var staff = await _context.StaffProfiles
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.Id == request.StaffProfileId.Value && sp.OrganizationId == targetOrgId && !sp.IsDeleted, cancellationToken);
            if (staff == null) return ApiNotFound("Staff member not found.");
            recipientName = staff.User?.FullName ?? staff.EmployeeCode;
            branchId = staff.BranchId;
        }

        book.AvailableCopies--;

        var issue = new BookIssue
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = branchId,
            BookId = book.Id,
            StudentId = request.StudentId,
            StaffProfileId = request.StaffProfileId,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(Math.Clamp(request.DueDays, 1, 60)),
            Status = BookIssueStatus.Issued,
            Remarks = request.Remarks?.Trim()
        };

        _context.BookIssues.Add(issue);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Library", "BookIssue", issue.Id.ToString(),
            $"Issued book '{book.Title}' to '{recipientName}'. Due date: {issue.DueDate:yyyy-MM-dd}",
            organizationId: targetOrgId,
            branchId: issue.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            issueId = issue.Id,
            bookTitle = book.Title,
            recipientName,
            dueDate = issue.DueDate,
            remainingCopies = book.AvailableCopies
        }, "Book issued successfully.");
    }

    [HttpPost("return/{issueId:guid}")]
    public async Task<IActionResult> ReturnBook(Guid issueId, [FromBody] ReturnBookRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var issue = await _context.BookIssues
            .Include(bi => bi.Book)
            .FirstOrDefaultAsync(bi => bi.Id == issueId && bi.OrganizationId == targetOrgId && !bi.IsDeleted, cancellationToken);

        if (issue == null) return ApiNotFound("Book checkout record not found.");

        if (issue.Status == BookIssueStatus.Returned)
            return ApiError("This book has already been marked as returned.");

        issue.ReturnDate = DateTime.UtcNow;
        issue.Status = request.Status;
        issue.FineAmount = Math.Max(0m, request.FineAmount ?? 0m);
        if (!string.IsNullOrWhiteSpace(request.Remarks))
        {
            issue.Remarks = string.IsNullOrWhiteSpace(issue.Remarks)
                ? request.Remarks.Trim()
                : $"{issue.Remarks}; {request.Remarks.Trim()}";
        }

        // Return copy to available pool if returned (not lost)
        if (request.Status == BookIssueStatus.Returned && issue.Book != null)
        {
            issue.Book.AvailableCopies = Math.Min(issue.Book.TotalCopies, issue.Book.AvailableCopies + 1);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Library", "BookIssue", issue.Id.ToString(),
            $"Processed return for book '{issue.Book?.Title}'. Status: {issue.Status}, Fine: {issue.FineAmount:C}",
            organizationId: targetOrgId,
            branchId: issue.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            issueId = issue.Id,
            status = issue.Status.ToString(),
            fineAmount = issue.FineAmount,
            returnDate = issue.ReturnDate
        }, "Book return processed successfully.");
    }
}
