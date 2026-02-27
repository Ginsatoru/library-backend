using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml; // EPPlus

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
            var model = new DashboardViewModel
            {
                TotalCatalog = await _context.Catalogs.CountAsync(),
                TotalBooks = await _context.Books.CountAsync(),
                TotalMembers = await _context.Members.CountAsync(),
                TotalBorrows = await _context.BookBorrows.CountAsync(),
                ReturnedBorrows = await _context.BookBorrows.CountAsync(l => l.IsReturned),
                NotReturn = await _context.BookBorrows.CountAsync(l => !l.IsReturned),
                Purchases = await _context.Purchases.CountAsync(),
                // NEW: total adjustments
                Adjustments = await _context.Adjustments.CountAsync()
            };

            model.PendingReturns = await _context.BookBorrows
                .CountAsync(l => !l.IsReturned && l.DueDate >= DateTime.Today);

            model.OverdueBorrows = await _context.BookBorrows
                .CountAsync(l => !l.IsReturned && l.DueDate < DateTime.Today);

            // Monthly (by LoanDate)
            var monthly = await _context.BookBorrows
                .AsNoTracking()
                .GroupBy(l => new { y = l.LoanDate.Year, m = l.LoanDate.Month })
                .Select(g => new { g.Key.y, g.Key.m, Count = g.Count() })
                .OrderBy(x => x.y).ThenBy(x => x.m)
                .ToListAsync();

            model.MonthlyLabels = monthly
                .Select(x => $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(x.m)} {x.y}")
                .ToList();
            model.MonthlyBorrowStats = monthly.Select(x => x.Count).ToList();

            // Last 7 days
            var start7 = DateTime.Today.AddDays(-6);
            var daily = await _context.BookBorrows
                .AsNoTracking()
                .Where(l => l.LoanDate >= start7)
                .GroupBy(l => new { y = l.LoanDate.Year, m = l.LoanDate.Month, d = l.LoanDate.Day })
                .Select(g => new { g.Key.y, g.Key.m, g.Key.d, Count = g.Count() })
                .OrderBy(x => x.y).ThenBy(x => x.m).ThenBy(x => x.d)
                .ToListAsync();

            model.DailyLabels = daily.Select(x => new DateTime(x.y, x.m, x.d).ToString("MMM dd")).ToList();
            model.DailyBorrowStats = daily.Select(x => x.Count).ToList();

            return View(model);
        }

        // ---- Universal Search ----
        [HttpGet]
        public async Task<IActionResult> Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(new { results = new List<object>() });

            q = q.Trim();

            var books = await _context.Books
                .Include(b => b.Catalog)
                .Where(b =>
                    (b.Catalog != null && (
                        EF.Functions.Like(b.Catalog.Title ?? "", $"%{q}%") ||
                        EF.Functions.Like(b.Catalog.Author ?? "", $"%{q}%") ||
                        EF.Functions.Like(b.Catalog.ISBN ?? "", $"%{q}%") ||
                        EF.Functions.Like(b.Catalog.Category ?? "", $"%{q}%")
                    )) ||
                    EF.Functions.Like(b.Barcode ?? "", $"%{q}%")
                )
                .Select(b => new
                {
                    type = "Book",
                    b.BookId,
                    title = b.Catalog != null ? b.Catalog.Title : "(No catalog)",
                    author = b.Catalog != null ? b.Catalog.Author : "",
                    isbn = b.Catalog != null ? b.Catalog.ISBN : "",
                    barcode = b.Barcode
                })
                .ToListAsync();

            var members = await _context.Members
                .Where(m =>
                    EF.Functions.Like(m.FullName ?? "", $"%{q}%") ||
                    EF.Functions.Like(m.Email ?? "", $"%{q}%") ||
                    EF.Functions.Like(m.Phone ?? "", $"%{q}%")
                )
                .Select(m => new
                {
                    type = "Member",
                    m.MemberId,
                    fullName = m.FullName,
                    email = m.Email,
                    phone = m.Phone
                })
                .ToListAsync();

            var borrows = await _context.BookBorrows
                .Include(l => l.LibraryMember)
                .Where(l =>
                    (l.LibraryMember != null && EF.Functions.Like(l.LibraryMember.FullName ?? "", $"%{q}%")) ||
                    EF.Functions.Like(l.LoanId.ToString(), $"%{q}%")
                )
                .Select(l => new
                {
                    type = "BookBorrow",
                    loanId = l.LoanId,
                    member = l.LibraryMember != null ? l.LibraryMember.FullName : "(Unknown)",
                    loanDate = l.LoanDate,
                    dueDate = l.DueDate
                })
                .ToListAsync();

            var results = books.Cast<object>().Concat(members).Concat(borrows).ToList();
            return Json(new { results });
        }

        // ---- Download Excel ----
        // View checkbox keys expected:
        // TotalBorrows, ReturnedBorrows, NotReturn, OverdueBorrows, Purchases, Adjustments, TotalBooks, TotalMembers
        [HttpGet]
        public async Task<IActionResult> DownloadExcel(DateTime start, DateTime end, string[] totals)
        {
            if (end < start) (start, end) = (end, start);

            var data = new Dictionary<string, object>();

            if (totals?.Contains("TotalBorrows") == true)
                data.Add("Total Book Borrows",
                    await _context.BookBorrows.CountAsync(l =>
                        l.LoanDate >= start && l.LoanDate <= end));

            if (totals?.Contains("ReturnedBorrows") == true)
                data.Add("Total Returned",
                    await _context.BookBorrows.CountAsync(l =>
                        l.IsReturned && l.LoanDate >= start && l.LoanDate <= end));

            if (totals?.Contains("NotReturn") == true)
                data.Add("Total Not Return",
                    await _context.BookBorrows.CountAsync(l =>
                        !l.IsReturned && l.LoanDate >= start && l.LoanDate <= end));

            if (totals?.Contains("OverdueBorrows") == true)
                data.Add("Overdue Book Borrows",
                    await _context.BookBorrows.CountAsync(l =>
                        !l.IsReturned &&
                        l.DueDate < DateTime.Today &&
                        l.LoanDate >= start && l.LoanDate <= end));

            if (totals?.Contains("Purchases") == true)
                data.Add("Purchases",
                    await _context.Purchases.CountAsync(p =>
                        p.PurchaseDate >= start && p.PurchaseDate <= end));

            // NEW: Adjustments (filter by date range)
            if (totals?.Contains("Adjustments") == true)
                data.Add("Adjustments",
                    await _context.Adjustments.CountAsync(a =>
                        // CHANGE "AdjustmentDate" to your actual date property if different
                        a.AdjustmentDate >= start && a.AdjustmentDate <= end));

            if (totals?.Contains("TotalBooks") == true)
                data.Add("Total Books", await _context.Books.CountAsync());

            if (totals?.Contains("TotalMembers") == true)
                data.Add("Total Members", await _context.Members.CountAsync());

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Summary");

            int col = 1;
            foreach (var header in data.Keys)
                ws.Cells[1, col++].Value = header;

            col = 1;
            foreach (var value in data.Values)
                ws.Cells[2, col++].Value = value;

            ws.Cells[ws.Dimension.Address].AutoFitColumns();

            var fileName = $"DashboardSummary_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx";
            var fileContents = package.GetAsByteArray();
            return File(
                fileContents,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }
    }
}
