using System;
using System.Collections.Generic;

namespace LibrarySystemBBU.Models
{
    public sealed class MemberHistoryViewModel
    {
        // Header
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool IsActive { get; set; }

        // Summary
        public int TotalLoans { get; set; }
        public int ActiveLoans { get; set; }
        public int ReturnedLoans { get; set; }
        public int TotalItemsBorrowed { get; set; }
        public int DistinctBooksBorrowed { get; set; }
        public DateTime? LastBorrowedOn { get; set; }

        public int TotalLateDays { get; set; }
        public decimal TotalFineAmount { get; set; }
        public decimal TotalAmountPaid { get; set; }
        public decimal OutstandingFine => TotalFineAmount - TotalAmountPaid;

        // Timeline
        public List<LoanHistoryVM> Loans { get; set; } = new();
    }

    public sealed class LoanHistoryVM
    {
        public int LoanId { get; set; }
        public DateTime LoanDate { get; set; }
        public DateTime DueDate { get; set; }
        public bool IsReturned { get; set; }
        public decimal BorrowingFee { get; set; }
        public bool IsPaid { get; set; }
        public decimal? DepositAmount { get; set; }

        public List<LoanItemVM> Items { get; set; } = new();
        public List<ReturnVM> Returns { get; set; } = new();
    }

    public sealed class LoanItemVM
    {
        public int LoanBookDetailId { get; set; }
        public int BookId { get; set; }
        public string BookTitle { get; set; } = "";
        public Guid CatalogId { get; set; }
        public string? Barcode { get; set; }
        public string ConditionOut { get; set; } = "";
        public string? ConditionIn { get; set; }
        public decimal? FineDetailAmount { get; set; }
        public string? FineDetailReason { get; set; }
    }

    public sealed class ReturnVM
    {
        public int ReturnId { get; set; }
        public DateTime ReturnDate { get; set; }
        public int LateDays { get; set; }
        public decimal FineAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? ConditionOnReturn { get; set; }
        public string? Notes { get; set; }
    }
}
