using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class Purchase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid PurchaseId { get; set; }

        // FOREIGN KEY: This is necessary!
        [Required(ErrorMessage = "Book is required.")]
        public int BookId { get; set; } // Foreign key to Book.BookId

        [NotMapped]
        public string? BookTitle => Book?.Title;

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Purchase Date is required.")]
        [DataType(DataType.Date)]
        public DateTime PurchaseDate { get; set; } = DateTime.Now.Date;

        [Required(ErrorMessage = "Supplier is required.")]
        [StringLength(255)]
        public required string Supplier { get; set; }

        [Required(ErrorMessage = "Cost is required.")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Cost { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Book? Book { get; set; }
        public ICollection<PurchaseDetail> PurchaseDetails { get; set; } = new List<PurchaseDetail>();
    }
}
