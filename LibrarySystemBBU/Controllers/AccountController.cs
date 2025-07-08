// Controllers/AccountController.cs
using LibrarySystemBBU.Services;
using LibrarySystemBBU.Data; // Ensure this is not conflicting if you have a DataContext AND ApplicationDbContext
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization; // Make sure this is present
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication; // Needed for Url.IsLocalUrl and RedirectToLocal helper

namespace LibrarySystemBBU.Controllers
{
    // Do NOT put [Authorize] on the AccountController class level.
    // We need its actions to be accessible by unauthenticated users.
    public class AccountController : Controller
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        // GET: /Account/Login (your custom login path)
        [HttpGet] // Explicitly state GET
        [ActionName("Login")] // Maps /Account/Login to this action
        [AllowAnonymous] // CRUCIAL: Allows unauthenticated users to access the login page
        public IActionResult Authentication(string? ReturnUrl = null)
        {
            ViewBag.ReturnUrl = ReturnUrl ?? Url.Content("~/");
            return View(); // Points to Views/Account/Login.cshtml
        }

        // POST: /Account/Login (submitting login credentials)
        [HttpPost]
        [ActionName("Login")] // Maps /Account/Login to this action
        [AllowAnonymous] // CRUCIAL: Allows unauthenticated users to post to the login action
        [ValidateAntiForgeryToken] // Protects against Cross-Site Request Forgery
        public async Task<IActionResult> Authentication(LoginRequest request, string? ReturnUrl = null)
        {
            ViewBag.ReturnUrl = ReturnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var loginResult = await _userService.GetAuthenticatedUser(request);
            if (!loginResult.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, loginResult.Message);
                return View(request);
            }

            // Redirect to the original URL or to the home page if no return URL
            return RedirectToLocal(ReturnUrl);
        }

        // GET: /Account/Register
        [HttpGet] // Explicitly state GET
        [AllowAnonymous] // CRUCIAL: Allows unauthenticated users to access the register page
        public IActionResult Register()
        {
            return View(); // Points to Views/Account/Register.cshtml
        }

        // POST: /Account/Register (submitting registration data)
        [HttpPost]
        [AllowAnonymous] // CRUCIAL: Allows unauthenticated users to post to the register action
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var result = await _userService.CreateUser(request);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(request);
            }

            // Optionally sign in the user immediately after registration
            var loginRequest = new LoginRequest
            {
                UserName = request.UserName,
                Password = request.Password // Re-using password from registration for immediate sign-in
            };

            var loginResult = await _userService.GetAuthenticatedUser(loginRequest);
            if (loginResult.IsSuccess)
            {
                return RedirectToAction("Index", "Home"); // Redirect to home on successful registration and login
            }
            // If sign-in after registration fails (shouldn't often if CreateUser was successful)
            ModelState.AddModelError(string.Empty, loginResult.Message);
            return View(request); // Stay on register page with error
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (await _userService.LogUserOut())
            {
                return RedirectToAction("Login", "Account"); // Redirect to login page after logout
            }
            return BadRequest(); // Something went wrong with logout
        }

        // GET: /Account/AccessDenied (your custom access denied page)
        [AllowAnonymous] // CRUCIAL: Allows unauthenticated users to see the access denied page
        public IActionResult AccessDenied()
        {
            return View(); // Points to Views/Account/AccessDenied.cshtml
        }

        // Helper method for safe local redirects
        private IActionResult RedirectToLocal(string? returnUrl) // Make returnUrl nullable
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }
    }
}