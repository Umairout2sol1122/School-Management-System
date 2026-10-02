using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    private ITenantContext? _tenantContext;
    private ICurrentUserService? _currentUserService;

    protected ITenantContext TenantContext =>
        _tenantContext ??= HttpContext.RequestServices.GetRequiredService<ITenantContext>();

    protected ICurrentUserService CurrentUserService =>
        _currentUserService ??= HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

    protected IActionResult ApiSuccess<T>(T data, string? message = null)
    {
        return Ok(new
        {
            success = true,
            message,
            data,
            timestampUtc = DateTime.UtcNow
        });
    }

    protected IActionResult ApiSuccess(string? message = null)
    {
        return Ok(new
        {
            success = true,
            message,
            timestampUtc = DateTime.UtcNow
        });
    }

    protected IActionResult ApiError(string message, int statusCode = StatusCodes.Status400BadRequest, object? errors = null)
    {
        return StatusCode(statusCode, new
        {
            success = false,
            message,
            errors,
            timestampUtc = DateTime.UtcNow
        });
    }

    protected IActionResult ApiNotFound(string message = "Resource not found.")
    {
        return ApiError(message, StatusCodes.Status404NotFound);
    }

    protected IActionResult ApiForbidden(string message = "Access denied.")
    {
        return ApiError(message, StatusCodes.Status403Forbidden);
    }

    protected IActionResult ApiUnauthorized(string message = "Unauthorized.")
    {
        return ApiError(message, StatusCodes.Status401Unauthorized);
    }
}
