using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class AcademicsController : Controller
{
    [HttpGet("/academics")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Academic Architecture & Class Management";
        return View();
    }
}
