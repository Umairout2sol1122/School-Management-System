using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SchoolManagement.Web.Controllers;
using Xunit;

namespace SchoolManagement.WebTests;

public class WebControllerRoutingTests
{
    [Fact]
    public void AuthController_Login_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new AuthController();

        // Act
        var result = controller.Login();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Institutional Sign In");
    }

    [Fact]
    public async Task AuthController_Logout_RedirectsToLogin()
    {
        // Arrange
        var controller = new AuthController();
        var authServiceMock = new Mock<IAuthenticationService>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authServiceMock.Object)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await controller.Logout();

        // Assert
        var redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
        redirectResult.Url.Should().Be("/login");
    }

    [Fact]
    public void DashboardController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new DashboardController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Executive Dashboard");
    }

    [Fact]
    public void SchoolProfileController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new SchoolProfileController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("School Profile & Campus Settings");
    }

    [Fact]
    public void OrganizationsController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new OrganizationsController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Organizations & Institutions");
    }

    [Fact]
    public void StudentsController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new StudentsController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Student Information System");
    }

    [Fact]
    public void StaffController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new StaffController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Faculty & Staff Management");
    }

    [Fact]
    public void FeesController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new FeesController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Fees & Finance Management");
    }

    [Fact]
    public void LibraryController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new LibraryController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Library Management System");
    }

    [Fact]
    public void TransportController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new TransportController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Transport & Fleet Management");
    }

    [Fact]
    public void BranchesController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new BranchesController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Campus & Branch Management");
    }

    [Fact]
    public void AttendanceController_Index_ReturnsView_WithCorrectTitle()
    {
        // Arrange
        var controller = new AttendanceController();

        // Act
        var result = controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ViewData["Title"].Should().Be("Daily Student Attendance");
    }
}
