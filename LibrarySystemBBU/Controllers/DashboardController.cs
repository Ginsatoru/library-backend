using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml; // <<-- Add for EPPlus

namespace LibrarySystemBBU.Controllers
{
    public class DashboardController : Controller
    {
        private readonly DataContext _context;

        public DashboardController(DataContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel();

            // Total numbers
            model.TotalBooks = await _context.Books.CountAsync();
            model.TotalLoans = await _context.BookLoans.CountAsync();
            model.TotalMembers = await _context.Members.CountAsync();
            model.PendingReturns = await _context.BookLoans
                .CountAsync(l => !l.IsReturned && l.DueDate >= DateTime.Today);
            model.OverdueLoans = await _context.BookLoans
                .CountAsync(l => !l.IsReturned && l.DueDate < DateTime.Today);
            model.ReturnedBooks = await _context.BookLoans
                .CountAsync(l => l.IsReturned);

            // Total Purchases
            model.Purchases = await _context.Purchases.CountAsync();

            // Total Not Returned
            model.NotReturn = await _context.BookLoans
                .CountAsync(l => !l.IsReturned);

            // Monthly stats (by LoanDate)
            var loans = await _context.BookLoans
                .Where(l => l.LoanDate != null)
                .ToListAsync();

            var monthlyGroups = loans
                .GroupBy(l => new { l.LoanDate.Year, l.LoanDate.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .ToList();

            model.MonthlyLabels = monthlyGroups
                .Select(g => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month) + " " + g.Key.Year)
                .ToList();
            model.MonthlyLoanStats = monthlyGroups.Select(g => g.Count()).ToList();
            model.MonthlyMemberStats = monthlyGroups.Select(_ => 0).ToList(); // optional

            // Last 7 days daily loan stats
            var last7Days = DateTime.Today.AddDays(-6);
            var dailyGroups = loans
                .Where(l => l.LoanDate.Date >= last7Days)
                .GroupBy(l => l.LoanDate.Date)
                .OrderBy(g => g.Key)
                .ToList();
            model.DailyLabels = dailyGroups.Select(g => g.Key.ToString("MMM dd")).ToList();
            model.DailyLoanStats = dailyGroups.Select(g => g.Count()).ToList();

            return View(model);
        }

        // --- Universal Search Action for AJAX ---
        [HttpGet]
        public async Task<IActionResult> Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Json(new { results = new List<object>() });

            q = q.ToLowerInvariant();

            var books = await _context.Books
                .Where(b => b.Title.ToLower().Contains(q) || b.Author.ToLower().Contains(q) || b.ISBN.Contains(q))
                .Select(b => new { type = "Book", b.BookId, b.Title, b.Author })
                .ToListAsync();

            var members = await _context.Members
                .Where(m => m.FullName.ToLower().Contains(q) || m.Email.ToLower().Contains(q) || m.Phone.Contains(q))
                .Select(m => new { type = "Member", m.MemberId, m.FullName, m.Email, m.Phone })
                .ToListAsync();

            var loans = await _context.BookLoans
                .Include(l => l.LibraryMember)
                .Where(l => l.LibraryMember.FullName.ToLower().Contains(q))
                .Select(l => new { type = "Loan", l.LoanId, Member = l.LibraryMember.FullName, l.LoanDate, l.DueDate })
                .ToListAsync();

            var results = books.Cast<object>().Concat(members).Concat(loans).ToList();

            return Json(new { results });
        }

        // --- Download Excel Action ---
        [HttpGet]
        public async Task<IActionResult> DownloadExcel(DateTime start, DateTime end, string[] totals)
        {
            // Ensure dates are ordered
            if (end < start)
                (start, end) = (end, start);

            // Aggregate data according to selected totals
            var data = new Dictionary<string, object>();

            if (totals.Contains("TotalLoans"))
                data.Add("Total Loans", await _context.BookLoans.CountAsync(l => l.LoanDate >= start && l.LoanDate <= end));
            if (totals.Contains("ReturnedBooks"))
                data.Add("Total Returned", await _context.BookLoans.CountAsync(l => l.IsReturned && l.LoanDate >= start && l.LoanDate <= end));
            if (totals.Contains("NotReturn"))
                data.Add("Total Not Return", await _context.BookLoans.CountAsync(l => !l.IsReturned && l.LoanDate >= start && l.LoanDate <= end));
            if (totals.Contains("OverdueLoans"))
                data.Add("Overdue Loans", await _context.BookLoans.CountAsync(l => !l.IsReturned && l.DueDate < DateTime.Today && l.LoanDate >= start && l.LoanDate <= end));
            if (totals.Contains("Purchases"))
                data.Add("Purchases", await _context.Purchases.CountAsync(p => p.PurchaseDate >= start && p.PurchaseDate <= end));
            if (totals.Contains("TotalBooks"))
                data.Add("Total Books", await _context.Books.CountAsync());
            if (totals.Contains("TotalMembers"))
                data.Add("Total Members", await _context.Members.CountAsync());

            // Generate Excel
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Summary");

            int col = 1;
            foreach (var header in data.Keys)
            {
                ws.Cells[1, col].Value = header;
                col++;
            }
            col = 1;
            foreach (var value in data.Values)
            {
                ws.Cells[2, col].Value = value;
                col++;
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();

            var fileName = $"DashboardSummary_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx";
            var fileContents = package.GetAsByteArray();
            return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
