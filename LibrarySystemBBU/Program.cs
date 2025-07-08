using LibrarySystemBBU.Data;
using LibrarySystemBBU.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml; // <-- ADD THIS

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Register your custom services
builder.Services.AddScoped<IUserService, UserServiceImpl>();
builder.Services.AddScoped<DapperFactory>();
builder.Services.AddHttpContextAccessor();

// Configure authentication using cookie scheme
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ReturnUrlParameter = "ReturnUrl";
    });

// Authorization services
builder.Services.AddAuthorization();

// Configure the database context with SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? throw new Exception("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Register any background hosted services
builder.Services.AddHostedService<OverdueLoanReminderService>();

// ----------------------------------------------------------
// EPPlus 7.x: Set license context for non-commercial use
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
// ----------------------------------------------------------

var app = builder.Build();

// Middleware pipeline configuration
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Enable authentication middleware
app.UseAuthorization();  // Enable authorization middleware

// Default route mapping to DashboardController by default
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
