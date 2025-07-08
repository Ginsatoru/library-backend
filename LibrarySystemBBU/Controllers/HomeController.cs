using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace LibrarySystemBBU.Controllers
{
    /// <summary>
    /// All actions in this controller require the user to be authenticated.
    /// If not, user will be redirected to /Account/Login automatically.
    /// </summary>
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Main home page for authenticated users.
        /// </summary>
        public IActionResult Index()
        {
            // You can add any logging or business logic here.
            return View();
        }

        /// <summary>
        /// Privacy page, also protected.
        /// </summary>
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Error page (not usually protected so you can see errors even if not logged in).
        /// </summary>
        [AllowAnonymous]
        public IActionResult Error()
        {
            // You can add error view model etc if needed.
            return View();
        }
    }
}
