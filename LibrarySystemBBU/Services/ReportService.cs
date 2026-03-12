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

        public async Task<MonthlyReportViewModel> GetReportAsync(DateTime start, DateTime end)
        {
            // end is inclusive date — shift to next day for < comparison
            var endExclusive = end.Date.AddDays(1);

            var vm = new MonthlyReportViewModel
            {
                StartDate = start.Date,
                EndDate = end.Date
            };

            // -------------------------------------------------
            // 1) Student take-home borrowing
            // -------------------------------------------------
            var studentLoans = _context.BookBorrows
                .AsNoTracking()
                .Include(l => l.LibraryMember)
                .Include(l => l.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(b => b.Catalog)
                .Include(l => l.BookReturns)
                .Where(l => l.LoanDate >= start && l.LoanDate < endExclusive)
                .Where(l => l.LibraryMember != null &&
                            l.LibraryMember.MemberType == "Student");

            vm.StudentTakeHomeLoans = await studentLoans.CountAsync();

            vm.StudentTakeHomeItemsBorrowed = await _context.LoanBookDetails
                .AsNoTracking()
                .Include(d => d.Loan)
                    .ThenInclude(l => l.LibraryMember)
                .Where(d => d.Loan != null &&
                            d.Loan.LoanDate >= start && d.Loan.LoanDate < endExclusive)
                .Where(d => d.Loan.LibraryMember != null &&
                            d.Loan.LibraryMember.MemberType == "Student")
                .CountAsync();

            // -------------------------------------------------
            // 2) Student take-home returns
            // -------------------------------------------------
            var studentReturnsInRange = _context.BookReturns
                .AsNoTracking()
                .Include(r => r.Loan)
                    .ThenInclude(l => l.LibraryMember)
                .Include(r => r.Loan)
                    .ThenInclude(l => l.LoanBookDetails)
                        .ThenInclude(d => d.Book)
                            .ThenInclude(b => b.Catalog)
                .Where(r => r.ReturnDate >= start && r.ReturnDate < endExclusive)
                .Where(r => r.Loan != null &&
                            r.Loan.LibraryMember != null &&
                            r.Loan.LibraryMember.MemberType == "Student");

            vm.StudentTakeHomeReturns = await studentReturnsInRange.CountAsync();

            // -------------------------------------------------
            // Borrow detail rows
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
                            d.Loan.LoanDate >= start && d.Loan.LoanDate < endExclusive)
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
                    BookTitle = d.Book != null ? (d.Book.Title ?? d.Book.Catalog.Title) : "(Unknown)",
                    CatalogTitle = d.Book != null ? d.Book.Catalog.Title : "(Unknown)",
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
            // Return detail rows
            // -------------------------------------------------
            vm.StudentReturnDetails = await _context.BookReturns
                .AsNoTracking()
                .Include(r => r.Loan)
                    .ThenInclude(l => l.LibraryMember)
                .Include(r => r.Loan)
                    .ThenInclude(l => l.LoanBookDetails)
                        .ThenInclude(d => d.Book)
                            .ThenInclude(b => b.Catalog)
                .Where(r => r.ReturnDate >= start && r.ReturnDate < endExclusive)
                .Where(r => r.Loan != null &&
                            r.Loan.LibraryMember != null &&
                            r.Loan.LibraryMember.MemberType == "Student")
                .OrderBy(r => r.ReturnDate)
                .SelectMany(r => r.Loan!.LoanBookDetails.Select(d => new BorrowReportItemDto
                {
                    LoanDate = r.Loan.LoanDate,
                    DueDate = r.Loan.DueDate,
                    ReturnDate = r.ReturnDate,
                    MemberId = r.Loan.MemberId,
                    MemberName = r.Loan.LibraryMember!.FullName,
                    MemberType = r.Loan.LibraryMember.MemberType,
                    BookTitle = d.Book != null ? (d.Book.Title ?? d.Book.Catalog.Title) : "(Unknown)",
                    CatalogTitle = d.Book != null ? d.Book.Catalog.Title : "(Unknown)",
                    Barcode = d.Book != null ? d.Book.Barcode : string.Empty,
                    DepositAmount = r.Loan.DepositAmount ?? 0m,
                    FineAmount = r.FineAmount,
                    ExtraCharge = r.ExtraCharge
                }))
                .ToListAsync();

            // -------------------------------------------------
            // 3) In-library reading
            // -------------------------------------------------
            var logsInRange = _context.LibraryLogs
                .AsNoTracking()
                .Include(l => l.Items)
                    .ThenInclude(i => i.Book)
                        .ThenInclude(b => b.Catalog)
                .Where(l => l.VisitDate >= start && l.VisitDate < endExclusive);

            vm.InLibraryBorrowCount = await logsInRange.CountAsync();

            vm.InLibraryItemsRead = await _context.LibraryLogItems
                .AsNoTracking()
                .Include(i => i.Log)
                .Where(i => i.Log.VisitDate >= start && i.Log.VisitDate < endExclusive)
                .CountAsync();

            vm.InLibraryReturnCount = await logsInRange
                .Where(l => l.Status == "Returned")
                .CountAsync();

            vm.InLibraryDetails = await _context.LibraryLogItems
                .AsNoTracking()
                .Include(i => i.Log)
                .Include(i => i.Book)
                    .ThenInclude(b => b.Catalog)
                .Where(i => i.Log.VisitDate >= start && i.Log.VisitDate < endExclusive)
                .OrderBy(i => i.Log.VisitDate)
                .Select(i => new InLibraryReportItemDto
                {
                    VisitDate = i.Log.VisitDate,
                    StudentName = i.Log.StudentName,
                    Gender = i.Log.Gender,
                    Purpose = i.Log.Purpose,
                    BookTitle = i.Book != null ? (i.Book.Title ?? i.Book.Catalog.Title) : "(Unknown)",
                    CatalogTitle = i.Book != null ? i.Book.Catalog.Title : "(Unknown)",
                    Barcode = i.Book != null ? i.Book.Barcode : string.Empty,
                    Status = i.Log.Status,
                    ReturnedDate = i.ReturnedDate ?? i.Log.ReturnedUtc
                })
                .ToListAsync();

            // -------------------------------------------------
            // 4) Purchases
            // -------------------------------------------------
            var purchasesInRange = _context.Purchases
                .AsNoTracking()
                .Include(p => p.Book)
                    .ThenInclude(b => b.Catalog)
                .Where(p => p.PurchaseDate >= start && p.PurchaseDate < endExclusive);

            vm.PurchaseTransactions = await purchasesInRange.CountAsync();
            vm.PurchaseTotalQuantity = await purchasesInRange.SumAsync(p => (int?)p.Quantity) ?? 0;
            vm.PurchaseTotalCost = await purchasesInRange.SumAsync(p => (decimal?)p.Cost) ?? 0m;

            vm.PurchaseItems = await purchasesInRange
                .OrderBy(p => p.PurchaseDate)
                .Select(p => new PurchaseReportItemDto
                {
                    PurchaseDate = p.PurchaseDate,
                    Supplier = p.Supplier,
                    BookTitle = p.Book != null ? (p.Book.Title ?? p.Book.Catalog.Title) : "(Unknown)",
                    Quantity = p.Quantity,
                    Cost = p.Cost,
                    Notes = p.Notes
                })
                .ToListAsync();

            // -------------------------------------------------
            // 5) Adjustments
            // -------------------------------------------------
            var adjustmentsInRange = _context.Adjustments
                .AsNoTracking()
                .Where(a => a.AdjustmentDate >= start && a.AdjustmentDate < endExclusive);

            vm.TotalAdjustments = await adjustmentsInRange.CountAsync();
            vm.TotalAdjustmentQtyChange = await adjustmentsInRange.SumAsync(a => (int?)a.QuantityChange) ?? 0;

            vm.AdjustmentsByType = await adjustmentsInRange
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
                            d.Adjustment.AdjustmentDate < endExclusive)
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
            // 6) Financial summary
            // -------------------------------------------------
            vm.IncomeBorrowingFees = await studentLoans
                .Where(l => l.IsPaid)
                .SumAsync(l => (decimal?)l.BorrowingFee) ?? 0m;

            vm.IncomeFines = await studentReturnsInRange
                .SumAsync(r => (decimal?)r.FineAmount) ?? 0m;

            vm.IncomeExtraCharges = await studentReturnsInRange
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