using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class StudentsController : Controller
{
    [HttpGet("/students")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Student Information System";
        return View();
    }
}
