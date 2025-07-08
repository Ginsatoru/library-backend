using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class BookLoan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LoanId { get; set; } // Primary Key (int, auto-incrementing)

        [Required(ErrorMessage = "Member ID is required.")]
        public Guid MemberId { get; set; } // Foreign key to LibraryMember

        // REMOVED: BookId direct foreign key. Books are now linked via LoanBookDetails.

        [Required(ErrorMessage = "Loan Date is required.")]
        [DataType(DataType.Date)]
        public DateTime LoanDate { get; set; } = DateTime.Now.Date;

        [Required(ErrorMessage = "Due Date is required.")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [Required(ErrorMessage = "Is Returned status is required.")]
        public bool IsReturned { get; set; } = false; // True if all items on this loan have been returned

        // --- Financial Tracking for Borrowing (remains on the header) ---
        [Required(ErrorMessage = "Borrowing fee is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal BorrowingFee { get; set; } = 0.00m; // The total fee to borrow items on this loan

        public Book? Book { get; set; } // <-- This navigation was removed when you moved BookId to LoanBookDetail


        [Required(ErrorMessage = "Payment status is required.")]
        public bool IsPaid { get; set; } = false; // True if the borrowing fee has been paid

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? DepositAmount { get; set; } // Optional: Total refundable deposit taken for this loan

        // --- Navigation Properties ---
        [ForeignKey("MemberId")]
        public Member? LibraryMember { get; set; }

        public ICollection<LoanBookDetail> LoanBookDetails { get; set; } = new List<LoanBookDetail>(); // NEW: One-to-Many to loan details
        public ICollection<BookReturn> BookReturns { get; set; } = new List<BookReturn>(); // Historical returns for this loan
        public ICollection<LoanReminder> LoanReminders { get; set; } = new List<LoanReminder>();


    }
}