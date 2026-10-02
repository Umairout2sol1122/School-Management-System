using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class AttendanceController : Controller
{
    [HttpGet("/attendance")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Daily Student Attendance";
        return View();
    }
}
