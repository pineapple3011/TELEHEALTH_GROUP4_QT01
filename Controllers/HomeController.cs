using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    public class HomeController : Controller
    {
        private readonly TelehealthContext _context;
        public HomeController(TelehealthContext context)
        {
            _context = context;
        }
        private readonly ILogger<HomeController> _logger;
        //=======================================

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
