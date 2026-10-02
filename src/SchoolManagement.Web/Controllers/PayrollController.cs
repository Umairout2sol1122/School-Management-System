using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class PayrollController : Controller
{
    [HttpGet("/payroll")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Staff Payroll & Compensation";
        return View();
    }
}
