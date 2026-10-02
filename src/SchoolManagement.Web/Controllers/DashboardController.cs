using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class DashboardController : Controller
{
    [HttpGet("/")]
    [HttpGet("/dashboard")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Executive Dashboard";
        return View();
    }
}
