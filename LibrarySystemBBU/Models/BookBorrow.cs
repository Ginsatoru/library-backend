using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class BookBorrow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LoanId { get; set; } 

        [Required(ErrorMessage = "Member ID is required.")]
        public Guid MemberId { get; set; } 

        // REMOVED: BookId direct foreign key. Books are now linked via LoanBookDetails.

        [Required(ErrorMessage = "Loan Date is required.")]
        [DataType(DataType.Date)]
        public DateTime LoanDate { get; set; } = DateTime.Now.Date;

        [Required(ErrorMessage = "Due Date is required.")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [Required(ErrorMessage = "Is Returned status is required.")]
        public bool IsReturned { get; set; } = false; 
        // --- Financial Tracking for Borrowing (remains on the header) ---
        [Required(ErrorMessage = "Borrowing fee is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal BorrowingFee { get; set; } = 0.00m; 

        public Book? Book { get; set; } 

        [Required(ErrorMessage = "Payment status is required.")]
        public bool IsPaid { get; set; } = false; 

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? DepositAmount { get; set; } 

        // --- Navigation Properties ---
        [ForeignKey("MemberId")]
        public Member? LibraryMember { get; set; }

        public ICollection<BookBorrowDetail> LoanBookDetails { get; set; } = new List<BookBorrowDetail>();
        public ICollection<BookReturn> BookReturns { get; set; } = new List<BookReturn>();
        public ICollection<LoanReminder> LoanReminders { get; set; } = new List<LoanReminder>();
    }

    public class BookBorrowDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LoanBookDetailId { get; set; }

        [Required(ErrorMessage = "Loan ID is required for loan detail.")]
        public int LoanId { get; set; }

        [Required(ErrorMessage = "Catalog ID is required for loan detail.")]
        public Guid CatalogId { get; set; }

        [Required(ErrorMessage = "Book ID is required for loan detail.")]
        public int BookId { get; set; }  

        [Required(ErrorMessage = "Condition Out is required.")]
        [StringLength(100)]
        public required string ConditionOut { get; set; }

        [StringLength(100)]
        public string? ConditionIn { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? FineDetailAmount { get; set; }

        [StringLength(500)]
        public string? FineDetailReason { get; set; }

        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;

        [ForeignKey("LoanId")]
        public BookBorrow? Loan { get; set; }

        [ForeignKey("CatalogId")]
        public Catalog? Catalog { get; set; }

        [ForeignKey("BookId")]
        public Book? Book { get; set; }
    }

    public class BookReturn
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReturnId { get; set; } 

        // Foreign Key to BookLoans
        [Required(ErrorMessage = "Loan ID is required.")]
        public int LoanId { get; set; }

        [Required(ErrorMessage = "Return Date is required.")]
        [DataType(DataType.Date)]
        public DateTime ReturnDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Late Days is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Late Days must be a non-negative number.")]
        public int LateDays { get; set; }

        [Required(ErrorMessage = "Fine Amount is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        [Range(0.00, double.MaxValue, ErrorMessage = "Fine Amount must be a non-negative number.")]
        public decimal FineAmount { get; set; } = 0.00m;

        // --- Financial Tracking ---
        // Extra charge (e.g. damage, other fees)
        [Column(TypeName = "decimal(10, 2)")]
        [Range(0.00, double.MaxValue, ErrorMessage = "Extra Charge must be a non-negative number.")]
        public decimal ExtraCharge { get; set; } = 0.00m;  

        [Required(ErrorMessage = "Total Amount Paid by member is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal AmountPaid { get; set; } = 0.00m;

        [Column(TypeName = "decimal(10, 2)")] 
        public decimal? RefundAmount { get; set; } 

        [StringLength(100)]
        public string? ConditionOnReturn { get; set; } 

        [StringLength(500)]
        public string? Notes { get; set; } 
        // Navigation property
        [ForeignKey("LoanId")]
        public BookBorrow? Loan { get; set; }
    }
}
