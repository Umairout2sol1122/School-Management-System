using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class CertificatesController : Controller
{
    [HttpGet("/certificates")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Certificates & Clearance Management";
        return View();
    }

    [HttpGet("/verify-certificate")]
    public IActionResult Verify([FromQuery] string? code)
    {
        ViewData["Title"] = "Digital Certificate Verification";
        ViewBag.InitialCode = code ?? string.Empty;
        return View();
    }
}
