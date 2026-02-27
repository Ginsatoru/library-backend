using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace LibrarySystemBBU.Services
{
    public sealed class UserServiceImpl : IUserService
    {
        private readonly DataContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserServiceImpl> _logger;
        private readonly IEmailSender _emailSender;

        public UserServiceImpl(
            DataContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<UserServiceImpl> logger,
            IEmailSender emailSender)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _emailSender = emailSender;
        }

        // ---------------- CREATE USER ----------------
        public async Task<MsgResponse> CreateUser(RegisterRequest request)
        {
            if (_httpContextAccessor.HttpContext == null)
            {
                _logger.LogError("HttpContext is null in CreateUser.");
                return new MsgResponse(false, "Internal server error: HttpContext not available.");
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName || u.Email == request.Email);

            if (existingUser != null)
                return new MsgResponse(false, $"{request.UserName} and/or {request.Email} already registered.");

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new Users
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                UserName = request.UserName,
                Email = request.Email,
                Phone = request.Phone,
                Password = hashedPassword,
                RoleName = request.RoleName ?? "User",
                ProfilePicturePath = "/admin-lte/img/avatar2.png",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                IsActive = true
            };

            await _context.Users.AddAsync(user);
            var saved = await _context.SaveChangesAsync() > 0;

            return saved
                ? new MsgResponse(true, "User created successfully.")
                : new MsgResponse(false, "Failed to create user.");
        }

        // ---------------- HELPER: password check for Users (hash OR plain) ----------------
        private bool CheckUserPassword(Users user, string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(user.Password))
                return false;

            // BCrypt hash patterns
            if (user.Password.StartsWith("$2a$") || user.Password.StartsWith("$2b$") || user.Password.StartsWith("$2y$"))
            {
                return BCrypt.Net.BCrypt.Verify(plainPassword, user.Password);
            }

            // Legacy plain-text mode
            return string.Equals(user.Password, plainPassword);
        }

        // ---------------- HELPER: build auth props based on RememberMe ----------------
        private AuthenticationProperties BuildAuthProperties(bool rememberMe)
        {
            var props = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                IssuedUtc = DateTimeOffset.UtcNow
            };

            props.ExpiresUtc = rememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(1);

            return props;
        }

        // ---------------- MAIN LOGIN: ONLY Users ----------------
        public async Task<MsgResponse> GetAuthenticatedUser(LoginRequest request)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogError("HttpContext is null in GetAuthenticatedUser.");
                return new MsgResponse(false, "Internal server error: HttpContext not available.");
            }

            var key = request.UserName?.Trim() ?? "";
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == key || u.Email == key);

            if (user == null)
                return new MsgResponse(false, $"{request.UserName} does not exist.");

            var isPasswordCorrect = CheckUserPassword(user, request.Password);

            if (!isPasswordCorrect)
                return new MsgResponse(false, "Invalid password.");

            if (!user.IsActive)
                return new MsgResponse(false, "Account is inactive. Please contact admin.");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim("FullName", $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.RoleName ?? "User"),
                new Claim("ProfilePicturePath", user.ProfilePicturePath ?? "/admin-lte/img/avatar2.png"),
                new Claim("AccountType", "User")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            var authProps = BuildAuthProperties(request.RememberMe);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProps);

            return new MsgResponse(true, "Login success (user).");
        }

        // ---------------- GET USER BY ID ----------------
        public async Task<Users?> GetByIdAsync(Guid id)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        // ---------------- LOGOUT ----------------
        public async Task<bool> LogUserOut()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext == null)
                {
                    _logger.LogError("HttpContext is null in LogUserOut.");
                    return false;
                }

                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during logout.");
                return false;
            }
        }

        // ==========================================================
        // FORGOT PASSWORD - Email OTP
        // ==========================================================

        private static string GenerateOtp6Digits()
        {
            // Cryptographically strong OTP
            var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return value.ToString("D6");
        }

        private async Task<Users?> FindUserByEmailOrUsernameAsync(string emailOrUsername)
        {
            if (string.IsNullOrWhiteSpace(emailOrUsername))
                return null;

            var key = emailOrUsername.Trim();
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == key || u.UserName == key);
        }

        public async Task<MsgResponse> RequestPasswordResetOtpAsync(string emailOrUsername)
        {
            var user = await FindUserByEmailOrUsernameAsync(emailOrUsername);

            // Security: do not reveal if email exists
            if (user == null)
            {
                return new MsgResponse(true, "If the account exists, an OTP has been sent to its email.");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return new MsgResponse(false, "This account does not have an email.");
            }

            if (!user.IsActive)
            {
                return new MsgResponse(false, "Account is inactive. Please contact admin.");
            }

            var otp = GenerateOtp6Digits();

            // Store HASH only (never store raw OTP)
            user.PasswordResetOtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
            user.PasswordResetOtpExpiresUtc = DateTime.UtcNow.AddMinutes(10);
            user.Modified = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            var subject = "Password Reset OTP";
            var body = $@"
        <div style='font-family:Arial,sans-serif;'>
            <h3>Password Reset</h3>
            <p>Your OTP code is:</p>
            <h2 style='letter-spacing:4px'>{otp}</h2>
            <p>This code expires in <b>10 minutes</b>.</p>
            <p>If you did not request this, please ignore this email.</p>
        </div>";

            // ✅ Use EmailSendResult
            var sendResult = await _emailSender.SendAsync(user.Email, subject, body);

            if (!sendResult.Success)
            {
                _logger.LogError("Failed sending OTP email to {Email}. Reason: {Reason}", user.Email, sendResult.Error);

                var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

                return new MsgResponse(false, isDev
                    ? $"Failed to send OTP email: {sendResult.Error}"
                    : "Failed to send OTP email. Please try again later.");
            }

            return new MsgResponse(true, "OTP sent to your email. Please check your inbox.");
        }


        public async Task<MsgResponse> ResetPasswordWithOtpAsync(string emailOrUsername, string otpCode, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(emailOrUsername))
                return new MsgResponse(false, "Invalid request.");

            if (string.IsNullOrWhiteSpace(otpCode))
                return new MsgResponse(false, "OTP is required.");

            if (string.IsNullOrWhiteSpace(newPassword))
                return new MsgResponse(false, "New password is required.");

            var user = await FindUserByEmailOrUsernameAsync(emailOrUsername);
            if (user == null)
                return new MsgResponse(false, "Invalid request.");

            if (!user.IsActive)
                return new MsgResponse(false, "Account is inactive. Please contact admin.");

            if (string.IsNullOrWhiteSpace(user.PasswordResetOtpHash) || user.PasswordResetOtpExpiresUtc == null)
                return new MsgResponse(false, "No OTP request found. Please request OTP again.");

            if (user.PasswordResetOtpExpiresUtc.Value < DateTime.UtcNow)
                return new MsgResponse(false, "OTP expired. Please request a new OTP.");

            var ok = BCrypt.Net.BCrypt.Verify(otpCode.Trim(), user.PasswordResetOtpHash);
            if (!ok)
                return new MsgResponse(false, "Invalid OTP code.");

            // Update password (hash)
            user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // Clear OTP fields
            user.PasswordResetOtpHash = null;
            user.PasswordResetOtpExpiresUtc = null;
            user.Modified = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return new MsgResponse(true, "Password has been reset successfully. You can login now.");
        }
    }
}
