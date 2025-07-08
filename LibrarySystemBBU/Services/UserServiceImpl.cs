// Services/UserServiceImpl.cs
using LibrarySystemBBU.Data; // For DataContext
using LibrarySystemBBU.Models; // For your custom models (Users, LoginRequest, RegisterRequest, MsgResponse)

using Microsoft.AspNetCore.Authentication; // For HttpContext.SignInAsync
using Microsoft.AspNetCore.Authentication.Cookies; // For CookieAuthenticationDefaults
using Microsoft.AspNetCore.Http; // For IHttpContextAccessor
using Microsoft.Extensions.Logging; // For ILogger
using System; // For DateTime
using System.Linq; // For LINQ queries
using System.Security.Claims; // For ClaimTypes
using System.Threading.Tasks; // For Task
using BCrypt.Net; // Make sure this using directive is present
using Microsoft.EntityFrameworkCore; // For FirstOrDefaultAsync (ADD THIS USING)


namespace LibrarySystemBBU.Services
{
    public sealed class UserServiceImpl : IUserService
    {
        private readonly DataContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserServiceImpl> _logger;

        public UserServiceImpl(DataContext context, IHttpContextAccessor httpContextAccessor, ILogger<UserServiceImpl> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<MsgResponse> CreateUser(RegisterRequest request)
        {
            // Null check for HttpContext, though it should be available in web context
            if (_httpContextAccessor.HttpContext is null)
            {
                _logger.LogError("HttpContext is null in CreateUser.");
                return new MsgResponse(false, "Internal server error: HttpContext not available.");
            }

            // Ensure your Users DbSet exists in DataContext
            // FIX: Use FirstOrDefaultAsync for asynchronous query
            var existingUser = await _context.Users
                                        .Where(u => u.UserName == request.UserName || u.Email == request.Email)
                                        .FirstOrDefaultAsync();

            if (existingUser != null)
            {
                return new MsgResponse(false, $"{request.UserName} and/or {request.Email} already registered.");
            }

            // Make sure BCrypt.Net-Next is installed
            var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new Users // This is your custom Users model
            {
                Email = request.Email,
                UserName = request.UserName,
                Password = hash,
                RoleName = request.RoleName, // Ensure RoleName is handled in your Users model
                Created = DateTime.UtcNow, // Set creation date
                Modified = DateTime.UtcNow // Set modification date
            };

            await _context.Users.AddAsync(user);

            return await _context.SaveChangesAsync() > 0
                ? new MsgResponse(true, "User created successfully")
                : new MsgResponse(false, "Failed to create user");
        }

        public async Task<MsgResponse> GetAuthenticatedUser(LoginRequest request)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                _logger.LogError("HttpContext is null in GetAuthenticatedUser.");
                return new MsgResponse(false, "Internal server error: HttpContext not available.");
            }

            // FIX: Use FirstOrDefaultAsync for asynchronous query
            var user = await _context.Users.Where(u => u.UserName == request.UserName).FirstOrDefaultAsync();
            if (user is null) return new MsgResponse(false, $"{request.UserName} does not exist");

            // --- FIX: Add try-catch block for SaltParseException ---
            bool isPasswordCorrect = false;
            try
            {
                // Ensure BCrypt.Net-Next is installed
                isPasswordCorrect = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
            }
            catch (SaltParseException ex)
            {
                _logger.LogError(ex, "SaltParseException: Invalid hash format for user {UserName}. Hash: '{StoredHash}'", user.UserName, user.Password);
                // Return a generic "Invalid login attempt" to avoid revealing internal details
                return new MsgResponse(false, "Invalid login attempt.");
            }
            catch (Exception ex) // Catch other unexpected exceptions during verify
            {
                _logger.LogError(ex, "An unexpected error occurred during password verification for user {UserName}.", user.UserName);
                return new MsgResponse(false, "An unexpected error occurred during login.");
            }
            // --- END FIX ---

            if (!isPasswordCorrect) // Now use the boolean result from the try-catch
            {
                return new MsgResponse(false, "Invalid password");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                // Ensure RoleName is correctly handled in your Users model and database
                new Claim(ClaimTypes.Role, user.RoleName?.ToString() ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal, new AuthenticationProperties
                {
                    IsPersistent = request.RememberMe,
                    IssuedUtc = DateTime.UtcNow,
                    ExpiresUtc = DateTime.UtcNow.AddDays(1),
                });

            return new MsgResponse(true, "Login Success");
        }

        public async Task<bool> LogUserOut()
        {
            _logger.LogInformation("Logging out user");
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext is null)
                {
                    _logger.LogError("HttpContext is null in LogUserOut.");
                    return false;
                }
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging out user");
                return false;
            }
        }
    }
}