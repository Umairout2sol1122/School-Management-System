using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class SchoolProfileController : Controller
{
    [HttpGet("/school-profile")]
    public IActionResult Index()
    {
        ViewData["Title"] = "School Profile & Campus Settings";
        return View();
    }
}
