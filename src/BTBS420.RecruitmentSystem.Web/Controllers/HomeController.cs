using Microsoft.AspNetCore.Mvc;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
