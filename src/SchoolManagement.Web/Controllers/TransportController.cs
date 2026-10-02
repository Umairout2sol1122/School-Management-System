using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class TransportController : Controller
{
    [HttpGet("/transport")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Transport & Fleet Management";
        return View();
    }
}
