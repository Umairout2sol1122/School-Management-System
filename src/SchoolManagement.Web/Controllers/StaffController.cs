using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class StaffController : Controller
{
    [HttpGet("/staff")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Faculty & Staff Management";
        return View();
    }
}
