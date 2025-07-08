using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic; // Needed for navigation if required

namespace LibrarySystemBBU.Models
{
    // Represents the record of a book being returned.
    public class BookReturn
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReturnId { get; set; } // Primary Key

        // Foreign Key to BookLoans
        [Required(ErrorMessage = "Loan ID is required.")]
        public int LoanId { get; set; }

        [Required(ErrorMessage = "Return Date is required.")]
        [DataType(DataType.Date)]
        public DateTime ReturnDate { get; set; } = DateTime.Now.Date;

        [Required(ErrorMessage = "Late Days is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Late Days must be a non-negative number.")]
        public int LateDays { get; set; }

        [Required(ErrorMessage = "Fine Amount is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        [Range(0.00, double.MaxValue, ErrorMessage = "Fine Amount must be a non-negative number.")]
        public decimal FineAmount { get; set; } = 0.00m;

        // --- Financial Tracking ---
        [Required(ErrorMessage = "Total Amount Paid by member is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal AmountPaid { get; set; } = 0.00m;

        [Column(TypeName = "decimal(10, 2)")] // Nullable if deposit not always taken
        public decimal? RefundAmount { get; set; } // Amount refunded to the member from deposit

        [StringLength(100)]
        public string? ConditionOnReturn { get; set; } // e.g., "Good", "Slightly Damaged", "Severely Damaged"

        [StringLength(500)]
        public string? Notes { get; set; } // Any specific notes about the return

        // Navigation property
        [ForeignKey("LoanId")]
        public BookLoan? Loan { get; set; }
    }
}
