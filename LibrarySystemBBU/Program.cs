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
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = ".AdminAuth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ReturnUrlParameter = "ReturnUrl";
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx =>
        {
            var isJson = ctx.Request.Headers["Accept"].ToString().Contains("application/json")
                      || ctx.Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest"
                      || ctx.Request.Path.Value?.EndsWith("Json", StringComparison.OrdinalIgnoreCase) == true;

            if (isJson)
            {
                ctx.Response.StatusCode = 401;
                return Task.CompletedTask;
            }

            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddCookie("MemberCookie", options =>
    {
        options.Cookie.Name = ".MemberAuth";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = 403;
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
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");
app.Run();