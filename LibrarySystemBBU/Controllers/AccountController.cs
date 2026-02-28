using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using LibrarySystemBBU.Services;
using LibrarySystemBBU.Models;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.ViewModels;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LibrarySystemBBU.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _env;

        public AccountController(IUserService userService, DataContext context, IWebHostEnvironment env)
        {
            _userService = userService;
            _context = context;
            _env = env;
        }

        // ==========================
        // MVC: LOGIN
        // ==========================
        [HttpGet]
        [ActionName("Login")]
        [AllowAnonymous]
        public IActionResult Authentication(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "Dashboard");
            return View();
        }

        [HttpPost]
        [ActionName("Login")]
        [AllowAnonymous]
        public async Task<IActionResult> Authentication(LoginRequest request, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
                return View(request);

            var loginResult = await _userService.GetAuthenticatedUser(request);

            if (!loginResult.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, loginResult.Message);
                return View(request);
            }

            return RedirectToLocal(returnUrl);
        }

        // ==========================
        // MVC: REGISTER — DISABLED
        // Admin accounts are created by existing admins only via the Users panel.
        // ==========================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return NotFound();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Register(RegisterRequest request, string? returnUrl = null)
        {
            return NotFound();
        }

        // ==========================
        // MVC: LOGOUT
        // ==========================
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var result = await _userService.LogUserOut();

            if (result)
                return RedirectToAction("Login", "Account");

            return BadRequest("Logout failed.");
        }

        // ==========================
        // MVC: FORGOT PASSWORD (OTP)
        // ==========================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordRequestViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestViewModel model)
        {
            Console.WriteLine($"FORGOT PASSWORD POST HIT: {model.EmailOrUserName}");

            if (!ModelState.IsValid)
                return View(model);

            var result = await _userService.RequestPasswordResetOtpAsync(model.EmailOrUserName);

            Console.WriteLine($"FORGOT PASSWORD RESULT: success={result.IsSuccess}, msg={result.Message}");

            TempData["ForgotPwdMsg"] = result.Message;

            if (result.IsSuccess)
            {
                return RedirectToAction(nameof(ResetPassword), new { emailOrUserName = model.EmailOrUserName });
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string? emailOrUserName)
        {
            return View(new ResetPasswordViewModel
            {
                EmailOrUserName = emailOrUserName ?? ""
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            Console.WriteLine($"RESET PASSWORD POST HIT: {model.EmailOrUserName}");

            TempData.Keep("ResetEmail");

            if (!ModelState.IsValid)
                return View(model);

            if (model.NewPassword != model.ConfirmNewPassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                return View(model);
            }

            var result = await _userService.ResetPasswordWithOtpAsync(
                model.EmailOrUserName,
                model.OtpCode,
                model.NewPassword);

            if (result.IsSuccess)
            {
                TempData["ResetPwdSuccess"] = result.Message;
                return RedirectToAction("Login");
            }

            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ==========================
        // PROFILE
        // ==========================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login");

            var vm = new UserProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                RoleName = user.RoleName,
                IsActive = user.IsActive,
                Notes = user.Notes,
                ProfilePicturePath = user.ProfilePicturePath,
                Created = user.Created,
                Modified = user.Modified
            };

            return View(vm);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized();

            if (!ModelState.IsValid)
            {
                TempData["ProfileError"] = "Please correct the highlighted fields.";
                TempData["OpenEditModal"] = true;
                return RedirectToAction(nameof(Profile));
            }

            if (string.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                TempData["ProfileError"] = "Current password is required.";
                TempData["OpenEditModal"] = true;
                return RedirectToAction(nameof(Profile));
            }

            var verifyRequest = new LoginRequest
            {
                UserName = user.UserName,
                Password = model.CurrentPassword
            };

            var verifyResult = await _userService.GetAuthenticatedUser(verifyRequest);
            if (!verifyResult.IsSuccess)
            {
                TempData["ProfileError"] = "Current password is incorrect. Your changes were not saved.";
                TempData["OpenEditModal"] = true;
                return RedirectToAction(nameof(Profile));
            }

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;
            user.Phone = model.Phone;
            user.Address = model.Address;
            user.Gender = model.Gender;
            user.DateOfBirth = model.DateOfBirth;
            user.Notes = model.Notes;

            if (User.IsInRole("Admin"))
                user.RoleName = model.RoleName;

            user.Modified = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
                user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            if (model.Picture != null && model.Picture.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(model.Picture.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await model.Picture.CopyToAsync(stream);

                user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            await RefreshSignInAsync(user);

            TempData["ProfileSuccess"] = string.IsNullOrWhiteSpace(model.NewPassword)
                ? "Your profile has been updated successfully."
                : "Your profile and password have been updated successfully.";

            return RedirectToAction(nameof(Profile));
        }

        private async Task<Users?> GetCurrentUserAsync()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
        }

        private async Task RefreshSignInAsync(Users user)
        {
            var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var currentProps = authResult?.Properties ?? new AuthenticationProperties();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.RoleName ?? string.Empty),
                new Claim("FullName", $"{user.FirstName} {user.LastName}".Trim()),
                new Claim("ProfilePicturePath", user.ProfilePicturePath ?? "/admin-lte/img/avatar2.png")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, currentProps);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}