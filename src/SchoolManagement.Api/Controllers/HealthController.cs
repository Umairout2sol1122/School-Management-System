using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : BaseApiController
{
    [HttpGet]
    public IActionResult Get()
    {
        return ApiSuccess(new
        {
            status = "Healthy",
            service = "School Management System API",
            version = "1.0.0",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            timestampUtc = DateTime.UtcNow
        });
    }
}
