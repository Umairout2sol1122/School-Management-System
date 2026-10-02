using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class FeesController : Controller
{
    [HttpGet("/fees")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Fees & Finance Management";
        return View();
    }
}
