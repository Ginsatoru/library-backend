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

        public int? BookId { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Purchase Date is required.")]
        [DataType(DataType.Date)]
        public DateTime PurchaseDate { get; set; } = DateTime.Now.Date;

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

        [Required]
        public Guid PurchaseId { get; set; }

        [Required(ErrorMessage = "Catalog is required.")]
        public Guid CatalogId { get; set; }

        [StringLength(500)]
        public string? Barcode { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Unit Price is required.")]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal LineTotal { get; set; }

        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;

        public int? BookId { get; set; }

        [ForeignKey(nameof(PurchaseId))]
        public Purchase? Purchase { get; set; }

        [ForeignKey(nameof(CatalogId))]
        public Catalog? Catalog { get; set; }

        public Book? Book { get; set; }
    }
}