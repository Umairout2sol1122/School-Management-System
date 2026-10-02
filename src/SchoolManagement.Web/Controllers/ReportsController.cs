using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class ReportsController : Controller
{
    [HttpGet("/reports")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Institutional Reports & Data Center";
        return View();
    }
}
