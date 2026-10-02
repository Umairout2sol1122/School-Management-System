using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class OrganizationsController : Controller
{
    [HttpGet("/organizations")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Organizations & Institutions";
        return View();
    }
}
