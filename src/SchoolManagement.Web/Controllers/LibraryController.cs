using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class LibraryController : Controller
{
    [HttpGet("/library")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Library Management System";
        return View();
    }
}
