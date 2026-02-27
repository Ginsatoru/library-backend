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

        
        [Required(ErrorMessage = "Book is required.")]
        public int BookId { get; set; }

        [NotMapped]
        public string? BookTitle => Book?.Title;

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Purchase Date is required.")]
        [DataType(DataType.Date)]
        public DateTime PurchaseDate { get; set; } = DateTime.Now.Date;

        // Use data annotation validation instead of C# 11 'required'
        [Required(ErrorMessage = "Supplier is required.")]
        [StringLength(255)]
        public string Supplier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Cost is required.")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Cost { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;

        public Book? Book { get; set; }

        public ICollection<PurchaseDetail> PurchaseDetails { get; set; } = new List<PurchaseDetail>();
    }

    public class PurchaseDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PurchaseDetailId { get; set; } 

        // FK to Purchase (required)
        [Required(ErrorMessage = "Purchase ID is required.")]
        public Guid PurchaseId { get; set; }

        // FK to Book (required)
        [Required(ErrorMessage = "Book ID is required for purchase detail.")]
        public int BookId { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Unit Price is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "Line Total is required.")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal LineTotal { get; set; }

        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;

        // Navigation properties (nullable; EF sets them if included)
        [ForeignKey(nameof(PurchaseId))]
        public Purchase? Purchase { get; set; }

        [ForeignKey(nameof(BookId))]
        public Book? Book { get; set; }
    }
}
