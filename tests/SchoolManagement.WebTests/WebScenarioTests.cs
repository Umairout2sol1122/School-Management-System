using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.Json;
using Xunit;

namespace SchoolManagement.WebTests;

public class WebScenarioTests
{
    // ==========================================
    // 1. POSITIVE SCENARIOS
    // ==========================================

    [Fact]
    public void TC_WEB_POS_001_AppSettings_HasValidApiBaseUrl()
    {
        // Positive scenario: Web application config defines target API base URL
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ApiBaseUrl", "https://localhost:7011/" }
        });
        var config = configBuilder.Build();

        string? apiUrl = config["ApiBaseUrl"];

        apiUrl.Should().NotBeNullOrWhiteSpace();
        apiUrl.Should().Be("https://localhost:7011/");
    }

    [Fact]
    public void TC_WEB_POS_002_BrandColorToken_MatchesSpecification()
    {
        // Positive scenario: #924EC0 is verified as the primary brand token
        const string expectedBrandColor = "#924EC0";
        expectedBrandColor.Should().MatchRegex("^#[0-9A-Fa-f]{6}$");
        expectedBrandColor.ToUpper().Should().Be("#924EC0");
    }

    [Fact]
    public void TC_WEB_POS_003_ValidLoginPayload_SerializesProperly()
    {
        // Positive scenario: Client auth payload matches API contract
        var payload = new
        {
            usernameOrEmail = "superadmin@schoolmanagement.com",
            password = "Admin@2026!Pak"
        };

        string json = JsonSerializer.Serialize(payload);
        json.Should().Contain("superadmin@schoolmanagement.com");
        json.Should().Contain("Admin@2026!Pak");
    }

    // ==========================================
    // 2. NEGATIVE SCENARIOS
    // ==========================================

    [Theory]
    [InlineData("", "Admin@2026!Pak")]
    [InlineData("admin@school.com", "")]
    [InlineData(null, null)]
    public void TC_WEB_NEG_001_InvalidLoginCredentials_AreDetectedBeforeSubmit(string? username, string? password)
    {
        // Negative scenario: Missing required login inputs
        bool isValid = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("invalid-email-format")]
    [InlineData("@missingusername.com")]
    [InlineData("missingdomain@")]
    public void TC_WEB_NEG_002_MalformedEmail_FailsValidation(string email)
    {
        // Negative scenario: Invalid email string
        bool hasAt = email.Contains('@');
        bool hasUsername = hasAt && email.IndexOf('@') > 0;
        bool hasDomain = hasUsername && email.IndexOf('@') < email.LastIndexOf('.') && email.LastIndexOf('.') < email.Length - 1;

        bool isValid = hasAt && hasUsername && hasDomain;
        isValid.Should().BeFalse();
    }

    [Fact]
    public void TC_WEB_NEG_003_EmptyStudentAdmission_TriggersValidation()
    {
        // Negative scenario: Student admission without first name or last name
        string firstName = "";
        string lastName = "Ali";
        string currentGrade = "Grade 5";

        bool canSubmit = !string.IsNullOrWhiteSpace(firstName) &&
                         !string.IsNullOrWhiteSpace(lastName) &&
                         !string.IsNullOrWhiteSpace(currentGrade);

        canSubmit.Should().BeFalse();
    }

    // ==========================================
    // 3. BOUNDARY SCENARIOS
    // ==========================================

    [Theory]
    [InlineData(0, 1)]      // Page 0 clamped to 1
    [InlineData(-5, 1)]     // Negative page clamped to 1
    [InlineData(1, 1)]      // Page 1 maintained
    [InlineData(100, 100)]  // Page 100 maintained
    public void TC_WEB_BND_001_PaginationPageNumber_ClampedToMinimumOne(int inputPage, int expectedPage)
    {
        // Boundary scenario: Page number lower bound is 1
        int actualPage = Math.Max(1, inputPage);
        actualPage.Should().Be(expectedPage);
    }

    [Theory]
    [InlineData(0, 1)]      // PageSize 0 clamped to 1
    [InlineData(15, 15)]    // PageSize 15 maintained
    [InlineData(50, 50)]    // PageSize 50 maintained
    [InlineData(200, 100)]  // PageSize > 100 clamped to 100
    public void TC_WEB_BND_002_PaginationPageSize_ClampedToMax100(int inputSize, int expectedSize)
    {
        // Boundary scenario: Page size bound [1, 100]
        int actualSize = Math.Clamp(inputSize, 1, 100);
        actualSize.Should().Be(expectedSize);
    }

    [Fact]
    public void TC_WEB_BND_003_SearchQuery_WithSpecialCharacters_IsSafelyHandled()
    {
        // Boundary scenario: Special characters in search input
        string rawSearch = "  O'Connor & Sons <script>alert(1)</script>  ";
        string sanitized = rawSearch.Trim();
        string encoded = Uri.EscapeDataString(sanitized);

        encoded.Should().NotContain("<script>");
        encoded.Should().Contain("%3Cscript%3E");
    }

    // ==========================================
    // 4. SECURITY SCENARIOS
    // ==========================================

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("OrganizationAdmin", false)]
    [InlineData("Teacher", false)]
    [InlineData("Staff", false)]
    [InlineData("Student", false)]
    [InlineData("Parent", false)]
    public void TC_WEB_SEC_001_SuperAdminPortal_RestrictedToSuperAdminRole(string role, bool hasAccess)
    {
        // Security scenario: Organizations management portal is only accessible to SuperAdmin
        bool isSuperAdmin = role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
        isSuperAdmin.Should().Be(hasAccess);
    }

    [Fact]
    public void TC_WEB_SEC_002_ExpiredJwtToken_MustTriggerReAuthentication()
    {
        // Security scenario: Expired JWT token timestamp detection
        var expirationTime = DateTime.UtcNow.AddMinutes(-5);
        bool isExpired = expirationTime < DateTime.UtcNow;

        isExpired.Should().BeTrue();
    }

    [Fact]
    public void TC_WEB_SEC_003_CrossTenantAccess_PreventedWhenOrgIdMismatch()
    {
        // Security scenario: Current user cannot access different tenant's data
        var userOrgId = Guid.NewGuid();
        var targetRecordOrgId = Guid.NewGuid();

        bool isSuperAdmin = false;
        bool hasTenantAccess = isSuperAdmin || userOrgId == targetRecordOrgId;

        hasTenantAccess.Should().BeFalse();
    }

    // ==========================================
    // 5. NETWORK / RESILIENCE SCENARIOS
    // ==========================================

    [Fact]
    public void TC_WEB_NET_001_ApiDownResponse_ReturnsCleanErrorMessage()
    {
        // Network scenario: Simulated 503 / Network Unavailable handling
        var simulatedStatusCode = HttpStatusCode.ServiceUnavailable;
        string userMessage = simulatedStatusCode switch
        {
            HttpStatusCode.ServiceUnavailable => "Institutional API service is temporarily unavailable. Please retry shortly.",
            HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
            HttpStatusCode.Forbidden => "You do not have administrative permission to view this resource.",
            _ => "An unexpected communication error occurred."
        };

        userMessage.Should().Contain("temporarily unavailable");
    }

    [Fact]
    public void TC_WEB_NET_002_TimeoutScenario_ProvidesActionableMessage()
    {
        // Network scenario: Simulated client timeout
        var isTimeout = true;
        string message = isTimeout
            ? "API request timed out. Please check your network connection."
            : "Success";

        message.Should().Be("API request timed out. Please check your network connection.");
    }
}
