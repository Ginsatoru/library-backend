using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class LoanBookDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LoanBookDetailId { get; set; }

        [Required(ErrorMessage = "Loan ID is required for loan detail.")]
        public int LoanId { get; set; }

        [Required(ErrorMessage = "Catalog ID is required for loan detail.")]
        public Guid CatalogId { get; set; }

        [Required(ErrorMessage = "Book ID is required for loan detail.")]
        public int BookId { get; set; }  // <-- Changed from Guid to int

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
        public BookLoan? Loan { get; set; }

        [ForeignKey("CatalogId")]
        public Catalog? Catalog { get; set; }

        [ForeignKey("BookId")]
        public Book? Book { get; set; }
    }
}
