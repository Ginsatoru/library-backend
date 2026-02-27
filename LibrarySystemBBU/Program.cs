using System;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IUserService, UserServiceImpl>();
builder.Services.AddScoped<DapperFactory>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IReportService, ReportService>();

// Email sender + options
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// CORS — allow React dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",  // Vite default
                "http://localhost:5174",  // Vite alternate
                "http://localhost:3000"   // fallback
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // required for cookie auth
    });
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ReturnUrlParameter = "ReturnUrl";
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.SlidingExpiration = true;

        // Prevent cookie auth from redirecting API/JSON requests — return 401 instead
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/MemberAuth") &&
                ctx.Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                ctx.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? throw new Exception("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddHttpClient();
builder.Services.Configure<LibrarySystemBBU.Services.TelegramOptions>(
    builder.Configuration.GetSection("Telegram")
);
builder.Services.AddSingleton<ITelegramService, TelegramService>();
builder.Services.AddHostedService<OverdueLoanReminderService>();

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Do NOT redirect to HTTPS in dev — React runs on HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

// CORS must come before Auth
app.UseCors("ReactFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();