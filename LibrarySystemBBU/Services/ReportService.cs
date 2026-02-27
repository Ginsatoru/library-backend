using System;
using System.Linq;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystemBBU.Services
{
    public class ReportService : IReportService
    {
        private readonly DataContext _context;

        public ReportService(DataContext context)
        {
            _context = context;
        }

        public async Task<MonthlyReportViewModel> GetMonthlyReportAsync(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var vm = new MonthlyReportViewModel
            {
                Year = year,
                Month = month
            };

            // -------------------------------------------------
            // 1) Student take-home borrowing (header + detail)
            //    Borrowed THIS MONTH = LoanDate in month
            // -------------------------------------------------
            var studentLoans = _context.BookBorrows
                .AsNoTracking()
                .Include(l => l.LibraryMember)
                .Include(l => l.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(b => b.Catalog)
                .Include(l => l.BookReturns)
                .Where(l => l.LoanDate >= start && l.LoanDate < end)
                .Where(l => l.LibraryMember != null &&
                            l.LibraryMember.MemberType == "Student");

            // how many loans happened this month
            vm.StudentTakeHomeLoans = await studentLoans.CountAsync();

            // how many items borrowed this month (rows in LoanBookDetails)
            vm.StudentTakeHomeItemsBorrowed = await _context.LoanBookDetails
                .AsNoTracking()
                .Include(d => d.Loan)
                    .ThenInclude(l => l.LibraryMember)
                .Where(d => d.Loan != null &&
                            d.Loan.LoanDate >= start && d.Loan.LoanDate < end)
                .Where(d => d.Loan.LibraryMember != null &&
                            d.Loan.LibraryMember.MemberType == "Student")
                .CountAsync();

            // -------------------------------------------------
            // ✅ Student take-home returns THIS MONTH (header)
            //    Returned THIS MONTH = ReturnDate in month
            //    IMPORTANT: filter by Student also
            // -------------------------------------------------
            var studentReturnsInMonth = _context.BookReturns
                .AsNoTracking()
                .Include(r => r.Loan)
                    .ThenInclude(l => l.LibraryMember)
                .Include(r => r.Loan)
                    .ThenInclude(l => l.LoanBookDetails)
                        .ThenInclude(d => d.Book)
                            .ThenInclude(b => b.Catalog)
                .Where(r => r.ReturnDate >= start && r.ReturnDate < end)
                .Where(r => r.Loan != null &&
                            r.Loan.LibraryMember != null &&
                            r.Loan.LibraryMember.MemberType == "Student");

            vm.StudentTakeHomeReturns = await studentReturnsInMonth.CountAsync();

            // -------------------------------------------------
            // Borrow detail rows (Borrowed THIS MONTH)
            // One row per item (LoanBookDetail)
            // -------------------------------------------------
            vm.StudentBorrowDetails = await _context.LoanBookDetails
                .AsNoTracking()
                .Include(d => d.Loan)
                    .ThenInclude(l => l.LibraryMember)
                .Include(d => d.Book)
                    .ThenInclude(b => b.Catalog)
                .Include(d => d.Loan)
                    .ThenInclude(l => l.BookReturns)
                .Where(d => d.Loan != null &&
                            d.Loan.LoanDate >= start && d.Loan.LoanDate < end)
                .Where(d => d.Loan.LibraryMember != null &&
                            d.Loan.LibraryMember.MemberType == "Student")
                .OrderBy(d => d.Loan!.LoanDate)
                .Select(d => new BorrowReportItemDto
                {
                    LoanDate = d.Loan!.LoanDate,
                    DueDate = d.Loan.DueDate,
                    ReturnDate = d.Loan.BookReturns
                        .OrderBy(r => r.ReturnDate)
                        .Select(r => (DateTime?)r.ReturnDate)
                        .FirstOrDefault(),

                    MemberId = d.Loan.MemberId,
                    MemberName = d.Loan.LibraryMember!.FullName,
                    MemberType = d.Loan.LibraryMember.MemberType,

                    BookTitle = d.Book != null
                        ? (d.Book.Title ?? d.Book.Catalog.Title)
                        : "(Unknown)",

                    // NOTE: your code used d.Catalog (not in Include); safer to use Book.Catalog
                    CatalogTitle = d.Book != null
                        ? d.Book.Catalog.Title
                        : "(Unknown)",

                    Barcode = d.Book != null ? d.Book.Barcode : string.Empty,

                    DepositAmount = d.Loan.DepositAmount ?? 0m,

                    FineAmount = d.Loan.BookReturns
                        .OrderBy(r => r.ReturnDate)
                        .Select(r => (decimal?)r.FineAmount)
                        .FirstOrDefault() ?? 0m,

                    ExtraCharge = d.Loan.BookReturns
                        .OrderBy(r => r.ReturnDate)
                        .Select(r => (decimal?)r.ExtraCharge)
                        .FirstOrDefault() ?? 0m
                })
                .ToListAsync();

            // -------------------------------------------------
            // ✅ Return detail rows (Returned THIS MONTH)
            // One row per returned item
            // - If a loan returns multiple items, you want row per item
            // -------------------------------------------------
            vm.StudentReturnDetails = await _context.BookReturns
                .AsNoTracking()
                .Include(r => r.Loan)
                    .ThenInclude(l => l.LibraryMember)
                .Include(r => r.Loan)
                    .ThenInclude(l => l.LoanBookDetails)
                        .ThenInclude(d => d.Book)
                            .ThenInclude(b => b.Catalog)
                .Where(r => r.ReturnDate >= start && r.ReturnDate < end)
                .Where(r => r.Loan != null &&
                            r.Loan.LibraryMember != null &&
                            r.Loan.LibraryMember.MemberType == "Student")
                .OrderBy(r => r.ReturnDate)
                // Flatten: BookReturn -> LoanBookDetails
                .SelectMany(r => r.Loan!.LoanBookDetails.Select(d => new BorrowReportItemDto
                {
                    // show both return + loan info
                    LoanDate = r.Loan.LoanDate,
                    DueDate = r.Loan.DueDate,
                    ReturnDate = r.ReturnDate,

                    MemberId = r.Loan.MemberId,
                    MemberName = r.Loan.LibraryMember!.FullName,
                    MemberType = r.Loan.LibraryMember.MemberType,

                    BookTitle = d.Book != null
                        ? (d.Book.Title ?? d.Book.Catalog.Title)
                        : "(Unknown)",

                    CatalogTitle = d.Book != null
                        ? d.Book.Catalog.Title
                        : "(Unknown)",

                    Barcode = d.Book != null ? d.Book.Barcode : string.Empty,

                    // usually deposit is from loan; keep for reference
                    DepositAmount = r.Loan.DepositAmount ?? 0m,

                    // ✅ return financial values must come from BookReturn (NOT Loan)
                    FineAmount = r.FineAmount,
                    ExtraCharge = r.ExtraCharge
                }))
                .ToListAsync();

            // -------------------------------------------------
            // 2) In-library reading (LibraryLog header + detail)
            // -------------------------------------------------
            var logsInMonth = _context.LibraryLogs
                .AsNoTracking()
                .Include(l => l.Items)
                    .ThenInclude(i => i.Book)
                        .ThenInclude(b => b.Catalog)
                .Where(l => l.VisitDate >= start && l.VisitDate < end);

            vm.InLibraryBorrowCount = await logsInMonth.CountAsync();

            vm.InLibraryItemsRead = await _context.LibraryLogItems
                .AsNoTracking()
                .Include(i => i.Log)
                .Where(i => i.Log.VisitDate >= start && i.Log.VisitDate < end)
                .CountAsync();

            vm.InLibraryReturnCount = await logsInMonth
                .Where(l => l.Status == "Returned")
                .CountAsync();

            vm.InLibraryDetails = await _context.LibraryLogItems
                .AsNoTracking()
                .Include(i => i.Log)
                .Include(i => i.Book)
                    .ThenInclude(b => b.Catalog)
                .Where(i => i.Log.VisitDate >= start && i.Log.VisitDate < end)
                .OrderBy(i => i.Log.VisitDate)
                .Select(i => new InLibraryReportItemDto
                {
                    VisitDate = i.Log.VisitDate,
                    StudentName = i.Log.StudentName,
                    Gender = i.Log.Gender,
                    Purpose = i.Log.Purpose,

                    BookTitle = i.Book != null
                        ? (i.Book.Title ?? i.Book.Catalog.Title)
                        : "(Unknown)",
                    CatalogTitle = i.Book != null
                        ? i.Book.Catalog.Title
                        : "(Unknown)",
                    Barcode = i.Book != null ? i.Book.Barcode : string.Empty,

                    Status = i.Log.Status,
                    ReturnedDate = i.ReturnedDate ?? i.Log.ReturnedUtc
                })
                .ToListAsync();

            // -------------------------------------------------
            // 3) Purchases (summary + details)
            // -------------------------------------------------
            var purchasesInMonth = _context.Purchases
                .AsNoTracking()
                .Include(p => p.Book)
                    .ThenInclude(b => b.Catalog)
                .Where(p => p.PurchaseDate >= start && p.PurchaseDate < end);

            vm.PurchaseTransactions = await purchasesInMonth.CountAsync();
            vm.PurchaseTotalQuantity = await purchasesInMonth
                .SumAsync(p => (int?)p.Quantity) ?? 0;
            vm.PurchaseTotalCost = await purchasesInMonth
                .SumAsync(p => (decimal?)p.Cost) ?? 0m;

            vm.PurchaseItems = await purchasesInMonth
                .OrderBy(p => p.PurchaseDate)
                .Select(p => new PurchaseReportItemDto
                {
                    PurchaseDate = p.PurchaseDate,
                    Supplier = p.Supplier,
                    BookTitle = p.Book != null
                        ? (p.Book.Title ?? p.Book.Catalog.Title)
                        : "(Unknown)",
                    Quantity = p.Quantity,
                    Cost = p.Cost,
                    Notes = p.Notes
                })
                .ToListAsync();

            // -------------------------------------------------
            // 4) Adjustments (summary + details)
            // -------------------------------------------------
            var adjustmentsInMonth = _context.Adjustments
                .AsNoTracking()
                .Where(a => a.AdjustmentDate >= start && a.AdjustmentDate < end);

            vm.TotalAdjustments = await adjustmentsInMonth.CountAsync();
            vm.TotalAdjustmentQtyChange = await adjustmentsInMonth
                .SumAsync(a => (int?)a.QuantityChange) ?? 0;

            vm.AdjustmentsByType = await adjustmentsInMonth
                .GroupBy(a => a.AdjustmentType)
                .Select(g => new AdjustmentSummaryDto
                {
                    AdjustmentType = g.Key,
                    TotalQuantityChange = g.Sum(a => a.QuantityChange),
                    Count = g.Count()
                })
                .OrderByDescending(x => Math.Abs(x.TotalQuantityChange))
                .ToListAsync();

            vm.AdjustmentItems = await _context.AdjustmentDetails
                .AsNoTracking()
                .Include(d => d.Adjustment)
                .Include(d => d.Catalog)
                .Where(d => d.Adjustment != null &&
                            d.Adjustment.AdjustmentDate >= start &&
                            d.Adjustment.AdjustmentDate < end)
                .OrderBy(d => d.Adjustment!.AdjustmentDate)
                .Select(d => new AdjustmentReportItemDto
                {
                    AdjustmentDate = d.Adjustment!.AdjustmentDate,
                    AdjustmentType = d.Adjustment.AdjustmentType,
                    Reason = d.Adjustment.Reason,
                    CatalogTitle = d.Catalog != null ? d.Catalog.Title : "(Unknown)",
                    QuantityChanged = d.QuantityChanged
                })
                .ToListAsync();

            // -------------------------------------------------
            // 5) Financial summary
            // -------------------------------------------------
            vm.IncomeBorrowingFees = await studentLoans
                .Where(l => l.IsPaid) // remove if you want all loans
                .SumAsync(l => (decimal?)l.BorrowingFee) ?? 0m;

            // ✅ Use Student returns only, to match your report
            vm.IncomeFines = await studentReturnsInMonth
                .SumAsync(r => (decimal?)r.FineAmount) ?? 0m;

            vm.IncomeExtraCharges = await studentReturnsInMonth
                .SumAsync(r => (decimal?)r.ExtraCharge) ?? 0m;

            vm.ExpensePurchases = vm.PurchaseTotalCost;

            vm.NetCashFlow = vm.IncomeBorrowingFees
                             + vm.IncomeFines
                             + vm.IncomeExtraCharges
                             - vm.ExpensePurchases;

            return vm;
        }
    }
}
