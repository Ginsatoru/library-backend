using System;
using System.Collections.Generic;

namespace LibrarySystemBBU.Models
{
    public class BorrowReceiptVm
    {
        public int LoanId { get; set; }

        public string MemberName { get; set; } = "";
        public DateTime LoanDate { get; set; }
        public DateTime DueDate { get; set; }

        public decimal BorrowingFee { get; set; }
        public decimal? DepositAmount { get; set; }
        public bool IsPaid { get; set; }

        public List<string> BookTitles { get; set; } = new();
    }

    public class ReturnReceiptVm
    {
        public int ReturnId { get; set; }
        public int LoanId { get; set; }

        public string MemberName { get; set; } = "";
        public DateTime LoanDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime ReturnDate { get; set; }

        public int LateDays { get; set; }
        public decimal FineAmount { get; set; }
        public decimal ExtraCharge { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal? RefundAmount { get; set; }

        public decimal BorrowingFee { get; set; }
        public decimal? DepositAmount { get; set; }

        public string? ConditionOnReturn { get; set; }
        public string? Notes { get; set; }

        public List<string> BookTitles { get; set; } = new();
    }
}
