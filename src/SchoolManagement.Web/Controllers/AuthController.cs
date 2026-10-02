using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace SchoolManagement.Web.Controllers;

public class AuthController : Controller
{
    [HttpGet("/login")]
    public IActionResult Login()
    {
        ViewData["Title"] = "Institutional Sign In";
        return View();
    }

    [HttpGet("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("SchoolManagement_Web_Session");
        Response.Cookies.Delete("sms_user_name");
        Response.Cookies.Delete("sms_user_role");
        Response.Cookies.Delete("sms_user_org");
        Response.Cookies.Delete("sms_user_branch");
        return Redirect("/login");
    }
}
