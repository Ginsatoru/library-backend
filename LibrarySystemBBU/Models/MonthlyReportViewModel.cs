using System;
using System.Collections.Generic;

namespace LibrarySystemBBU.ViewModels
{
    public class MonthlyReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM");

        // Borrowed in this month (LoanDate in month)
        public int StudentTakeHomeLoans { get; set; }
        public int StudentTakeHomeItemsBorrowed { get; set; }

        // Returned in this month (ReturnDate in month)
        public int StudentTakeHomeReturns { get; set; }

        public List<BorrowReportItemDto> StudentBorrowDetails { get; set; } = new();

        // ✅ NEW: Returned details (ReturnDate in month)
        public List<BorrowReportItemDto> StudentReturnDetails { get; set; } = new();

        public int InLibraryBorrowCount { get; set; }
        public int InLibraryReturnCount { get; set; }
        public int InLibraryItemsRead { get; set; }
        public List<InLibraryReportItemDto> InLibraryDetails { get; set; } = new();

        public int PurchaseTransactions { get; set; }
        public int PurchaseTotalQuantity { get; set; }
        public decimal PurchaseTotalCost { get; set; }
        public List<PurchaseReportItemDto> PurchaseItems { get; set; } = new();

        public int TotalAdjustments { get; set; }
        public int TotalAdjustmentQtyChange { get; set; }
        public List<AdjustmentSummaryDto> AdjustmentsByType { get; set; } = new();
        public List<AdjustmentReportItemDto> AdjustmentItems { get; set; } = new();

        public decimal IncomeBorrowingFees { get; set; }
        public decimal IncomeFines { get; set; }
        public decimal IncomeExtraCharges { get; set; }
        public decimal ExpensePurchases { get; set; }
        public decimal NetCashFlow { get; set; }
    }

    public class BorrowReportItemDto
    {
        public DateTime LoanDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string MemberType { get; set; } = string.Empty;

        public string BookTitle { get; set; } = string.Empty;
        public string CatalogTitle { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        public decimal DepositAmount { get; set; }
        public decimal FineAmount { get; set; }
        public decimal ExtraCharge { get; set; }
    }

    public class InLibraryReportItemDto
    {
        public DateTime VisitDate { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? Purpose { get; set; }

        public string BookTitle { get; set; } = string.Empty;
        public string CatalogTitle { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public DateTime? ReturnedDate { get; set; }
    }

    public class PurchaseReportItemDto
    {
        public DateTime PurchaseDate { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public string BookTitle { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Cost { get; set; }
        public string? Notes { get; set; }
    }

    public class AdjustmentSummaryDto
    {
        public string AdjustmentType { get; set; } = string.Empty;
        public int TotalQuantityChange { get; set; }
        public int Count { get; set; }
    }

    public class AdjustmentReportItemDto
    {
        public DateTime AdjustmentDate { get; set; }
        public string AdjustmentType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string CatalogTitle { get; set; } = string.Empty;
        public int QuantityChanged { get; set; }
    }
}
