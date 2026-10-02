using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class BranchesController : Controller
{
    [HttpGet("/branches")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Campus & Branch Management";
        return View();
    }
}
