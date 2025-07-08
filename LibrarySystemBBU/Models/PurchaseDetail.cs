using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class PurchaseDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PurchaseDetailId { get; set; } // Primary Key (int, auto-incrementing)

        [Required(ErrorMessage = "Purchase ID is required.")]
        public Guid PurchaseId { get; set; } // Foreign Key to the Purchase Header

        [Required(ErrorMessage = "Book ID is required for purchase detail.")]
        public int BookId { get; set; } // Foreign Key to the specific Book (int)

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Unit Price is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "Line Total is required.")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal LineTotal { get; set; } // Quantity * UnitPrice

        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("PurchaseId")]
        public Purchase? Purchase { get; set; }

        [ForeignKey("BookId")]
        public Book? Book { get; set; }
    }
}
