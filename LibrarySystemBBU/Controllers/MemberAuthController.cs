using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using LibrarySystemBBU.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LibrarySystemBBU.Controllers
{
    public class MemberAuthController : Controller
    {
        private readonly DataContext _context;
        private readonly IConfiguration _config;

        public MemberAuthController(DataContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // ----------------------------------------
        // POST: /MemberAuth/LoginJson
        // ----------------------------------------
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> LoginJson([FromBody] MemberLoginJsonRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Login) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest(new { success = false, message = "Login and password are required." });

            var loginText = model.Login.Trim();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m =>
                    m.Email == loginText ||
                    (!string.IsNullOrEmpty(m.Phone) && m.Phone == loginText) ||
                    m.FullName == loginText ||
                    (m.Users != null && m.Users.UserName == loginText));

            if (member == null)
                return Ok(new { success = false, message = "Member not found." });

            if (!member.IsActive)
                return Ok(new { success = false, message = "This account is inactive. Please contact the library." });

            if (!member.CanMemberLogin)
                return Ok(new { success = false, message = "Your account does not have a portal password set. Please contact the library to activate your portal access." });

            if (!member.VerifyPassword(model.Password))
                return Ok(new { success = false, message = "Invalid password." });

            await SignInMemberAsync(member, model.RememberMe);

            return Ok(new
            {
                success = true,
                member = new
                {
                    memberId       = member.MemberId,
                    fullName       = member.FullName,
                    email          = member.Email,
                    phone          = member.Phone,
                    memberType     = member.MemberType,
                    profilePicture = member.ProfilePicturePath
                }
            });
        }

        // ----------------------------------------
        // POST: /MemberAuth/RegisterJson
        // ----------------------------------------
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterJson([FromBody] MemberRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { success = false, message = string.Join(" ", errors) });
            }

            var existing = await _context.Members
                .FirstOrDefaultAsync(m =>
                    m.Email == model.Email ||
                    (!string.IsNullOrEmpty(model.Phone) && m.Phone == model.Phone));

            if (existing != null)
                return Ok(new { success = false, message = "A member with this email or phone already exists." });

            var member = new Member
            {
                MemberId   = Guid.NewGuid(),
                FullName   = model.FullName,
                Email      = model.Email,
                Phone      = model.Phone,
                Address    = model.Address,
                MemberType = model.MemberType,
                JoinDate   = DateTime.UtcNow.Date,
                Modified   = DateTime.UtcNow,
                IsActive   = true,
                CreatedBy  = "Self-Register"
            };

            if (!member.TrySetPassword(model.Password))
                return Ok(new { success = false, message = "Invalid password. Please use 5–20 characters." });

            member.TelegramPairToken = Guid.NewGuid().ToString("N").Substring(0, 10);

            _context.Members.Add(member);
            await _context.SaveChangesAsync();

            await SignInMemberAsync(member, rememberMe: true);

            return Ok(new
            {
                success = true,
                member = new
                {
                    memberId       = member.MemberId,
                    fullName       = member.FullName,
                    email          = member.Email,
                    phone          = member.Phone,
                    memberType     = member.MemberType,
                    profilePicture = member.ProfilePicturePath
                }
            });
        }

        // ----------------------------------------
        // POST: /MemberAuth/ForgotPasswordJson
        // ----------------------------------------
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPasswordJson([FromBody] ForgotPasswordRequestViewModel model)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.EmailOrUserName))
                return BadRequest(new { success = false, message = "Email or login is required." });

            var input = model.EmailOrUserName.Trim();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m =>
                    m.Email == input ||
                    (!string.IsNullOrEmpty(m.Phone) && m.Phone == input) ||
                    m.FullName == input ||
                    (m.Users != null && m.Users.UserName == input));

            if (member == null || !member.IsActive)
                return Ok(new { success = true, message = "If an account exists, an OTP has been sent to your email." });

            var otp = new Random().Next(100000, 999999).ToString();
            member.PasswordResetOtp = otp;
            member.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);
            member.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var emailOk = await SendEmailAsync(
                to: member.Email,
                subject: "Library Password Reset OTP",
                body: $"Your OTP code is: {otp}\n\nThis code expires in 10 minutes.");

            return Ok(new
            {
                success = true,
                message = emailOk
                    ? "OTP sent to your email."
                    : "OTP generated but email is not configured. Contact admin."
            });
        }

        // ----------------------------------------
        // POST: /MemberAuth/ResetPasswordJson
        // ----------------------------------------
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPasswordJson([FromBody] ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { success = false, message = string.Join(" ", errors) });
            }

            if (model.NewPassword != model.ConfirmNewPassword)
                return Ok(new { success = false, message = "Passwords do not match." });

            var input = (model.EmailOrUserName ?? "").Trim();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m =>
                    m.Email == input ||
                    (!string.IsNullOrEmpty(m.Phone) && m.Phone == input) ||
                    m.FullName == input ||
                    (m.Users != null && m.Users.UserName == input));

            if (member == null)
                return Ok(new { success = false, message = "Member not found." });

            if (string.IsNullOrWhiteSpace(member.PasswordResetOtp) || !member.PasswordResetOtpExpiry.HasValue)
                return Ok(new { success = false, message = "No OTP request found. Please request OTP again." });

            if (member.PasswordResetOtpExpiry.Value < DateTime.UtcNow)
                return Ok(new { success = false, message = "OTP expired. Please request a new one." });

            if (!string.Equals(member.PasswordResetOtp, model.OtpCode?.Trim(), StringComparison.Ordinal))
                return Ok(new { success = false, message = "Invalid OTP code." });

            if (!member.TrySetPassword(model.NewPassword))
                return Ok(new { success = false, message = "Invalid password. Please use 5–20 characters." });

            member.PasswordResetOtp = null;
            member.PasswordResetOtpExpiry = null;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Password reset successfully." });
        }

        // ----------------------------------------
        // POST: /MemberAuth/LogoutJson
        // ----------------------------------------
        [HttpPost]
        public async Task<IActionResult> LogoutJson()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { success = true });
        }

        public class MemberLoginJsonRequest
        {
            public string Login { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool RememberMe { get; set; } = false;
        }

        // ----------------------------------------
        // GET: /MemberAuth/Register
        // ----------------------------------------
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new MemberRegisterViewModel());
        }

        // ----------------------------------------
        // POST: /MemberAuth/Register
        // ----------------------------------------
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(MemberRegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existing = await _context.Members
                .FirstOrDefaultAsync(m =>
                    m.Email == model.Email ||
                    (!string.IsNullOrEmpty(model.Phone) && m.Phone == model.Phone));

            if (existing != null)
            {
                ModelState.AddModelError(string.Empty, "A member with this email or phone already exists.");
                return View(model);
            }

            var member = new Member
            {
                MemberId = Guid.NewGuid(),
                FullName = model.FullName,
                Email    = model.Email,
                Phone    = model.Phone,
                Address  = model.Address,
                MemberType = model.MemberType,
                JoinDate   = DateTime.UtcNow.Date,
                Modified   = DateTime.UtcNow,
                IsActive   = true,
                CreatedBy  = "Self-Register"
            };

            if (!member.TrySetPassword(model.Password))
            {
                ModelState.AddModelError(string.Empty, "Invalid password. Please use 5–20 characters.");
                return View(model);
            }

            member.TelegramPairToken = Guid.NewGuid().ToString("N").Substring(0, 10);

            _context.Members.Add(member);
            await _context.SaveChangesAsync();

            await SignInMemberAsync(member, rememberMe: true);

            return RedirectToAction("ConnectTelegram", "MemberAuth");
        }

        // ----------------------------------------
        // GET: /MemberAuth/ConnectTelegram
        // ----------------------------------------
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ConnectTelegram()
        {
            var memberIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(memberIdClaim, out var memberId))
                return RedirectToAction("Register", "MemberAuth");

            var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberId == memberId);
            if (member == null)
                return RedirectToAction("Register", "MemberAuth");

            if (string.IsNullOrWhiteSpace(member.TelegramPairToken))
            {
                member.TelegramPairToken = Guid.NewGuid().ToString("N").Substring(0, 10);
                await _context.SaveChangesAsync();
            }

            var botUsername = _config["Telegram:BotUsername"] ?? "YourLibraryBot";
            var deepLink = $"https://t.me/{botUsername}?start={member.TelegramPairToken}";
            ViewBag.TelegramDeepLink = deepLink;

            return View("~/Views/MemberPortal/ConnectTelegram.cshtml", member);
        }

        // ----------------------------------------
        // GET: /MemberAuth/Login
        // ----------------------------------------
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new MemberLoginViewModel { ReturnUrl = returnUrl });
        }

        // ----------------------------------------
        // POST: /MemberAuth/Login
        // ----------------------------------------
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(MemberLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var loginText = model.Login?.Trim();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m =>
                    m.Email == loginText ||
                    (!string.IsNullOrEmpty(m.Phone) && m.Phone == loginText) ||
                    m.FullName == loginText ||
                    (m.Users != null && m.Users.UserName == loginText));

            if (member == null)
            {
                ModelState.AddModelError(string.Empty, "Member not found.");
                return View(model);
            }

            if (!member.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This member account is inactive. Please contact the library.");
                return View(model);
            }

            if (!member.VerifyPassword(model.Password))
            {
                ModelState.AddModelError(string.Empty, "Invalid password.");
                return View(model);
            }

            await SignInMemberAsync(member, model.RememberMe);

            if (!string.IsNullOrEmpty(model.ReturnUrl)
                && Url.IsLocalUrl(model.ReturnUrl)
                && model.ReturnUrl != "/"
                && model.ReturnUrl != Url.Content("~/"))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Members");
        }

        // ----------------------------------------
        // GET: /MemberAuth/ForgotPassword
        // ----------------------------------------
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
            if (!ModelState.IsValid)
                return View(model);

            var input = (model.EmailOrUserName ?? "").Trim();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m =>
                    m.Email == input ||
                    (!string.IsNullOrEmpty(m.Phone) && m.Phone == input) ||
                    m.FullName == input ||
                    (m.Users != null && m.Users.UserName == input));

            if (member == null)
            {
                TempData["MemberForgotPwdMsg"] = "Member not found.";
                return View(model);
            }

            if (!member.IsActive)
            {
                TempData["MemberForgotPwdMsg"] = "This member account is inactive. Please contact the library.";
                return View(model);
            }

            var otp = new Random().Next(100000, 999999).ToString();
            member.PasswordResetOtp = otp;
            member.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);
            member.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var emailOk = await SendEmailAsync(
                to: member.Email,
                subject: "Library Password Reset OTP",
                body: $"Your OTP code is: {otp}\n\nThis code expires in 10 minutes.");

            TempData["MemberForgotPwdMsg"] = emailOk
                ? "OTP sent to your email."
                : "OTP generated, but email service is not configured. Please contact admin.";

            return RedirectToAction(nameof(ResetPassword), new { emailOrUserName = input });
        }

        // ----------------------------------------
        // GET/POST: /MemberAuth/ResetPassword
        // ----------------------------------------
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string? emailOrUserName)
        {
            return View(new ResetPasswordViewModel { EmailOrUserName = emailOrUserName ?? "" });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.NewPassword != model.ConfirmNewPassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                return View(model);
            }

            var input = (model.EmailOrUserName ?? "").Trim();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m =>
                    m.Email == input ||
                    (!string.IsNullOrEmpty(m.Phone) && m.Phone == input) ||
                    m.FullName == input ||
                    (m.Users != null && m.Users.UserName == input));

            if (member == null)
            {
                ModelState.AddModelError(string.Empty, "Member not found.");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(member.PasswordResetOtp) || !member.PasswordResetOtpExpiry.HasValue)
            {
                ModelState.AddModelError(string.Empty, "No OTP request found. Please request OTP again.");
                return View(model);
            }

            if (member.PasswordResetOtpExpiry.Value < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "OTP expired. Please request OTP again.");
                return View(model);
            }

            if (!string.Equals(member.PasswordResetOtp, model.OtpCode?.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError(string.Empty, "Invalid OTP code.");
                return View(model);
            }

            if (!member.TrySetPassword(model.NewPassword))
            {
                ModelState.AddModelError(string.Empty, "Invalid password. Please use 5–20 characters.");
                return View(model);
            }

            member.PasswordResetOtp = null;
            member.PasswordResetOtpExpiry = null;

            await _context.SaveChangesAsync();

            TempData["MemberResetPwdSuccess"] = "Password reset successfully. Please login.";

            return RedirectToAction("Login", "MemberAuth");
        }

        // ----------------------------------------
        // POST: /MemberAuth/Logout
        // ----------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "MemberAuth");
        }

        // ----------------------------------------
        // Helper: create cookie auth for member
        // ----------------------------------------
        private async Task SignInMemberAsync(Member member, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, member.MemberId.ToString()),
                new Claim(ClaimTypes.Name, member.FullName),
                new Claim("FullName", member.FullName),
                new Claim(ClaimTypes.Email, member.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, "Member"),
                new Claim("AccountType", "Member")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    IssuedUtc    = DateTime.UtcNow,
                    ExpiresUtc   = DateTime.UtcNow.AddDays(1)
                });
        }

        // ----------------------------------------
        // SMTP helper
        // ----------------------------------------
        private async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var host     = _config["Smtp:Host"];
                var portStr  = _config["Smtp:Port"];
                var username = _config["Smtp:Username"];
                var password = _config["Smtp:Password"];
                var from     = _config["Smtp:From"] ?? username;

                if (string.IsNullOrWhiteSpace(host) ||
                    string.IsNullOrWhiteSpace(portStr) ||
                    string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(password) ||
                    string.IsNullOrWhiteSpace(from))
                {
                    return false;
                }

                if (!int.TryParse(portStr, out var port))
                    port = 587;

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl   = true
                };

                using var message = new MailMessage(from, to, subject, body);
                await Task.Run(() => client.Send(message));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}