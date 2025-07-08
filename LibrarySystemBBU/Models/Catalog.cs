using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class Catalog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid CatalogId { get; set; }

        [Required(ErrorMessage = "Book ID is required for catalog entry.")]
        public int BookId { get; set; } // Foreign Key - matches Book PK type exactly

        [Required(ErrorMessage = "Barcode is required.")]
        [StringLength(50)]
        public required string Barcode { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(50)]
        public required string Status { get; set; }

        [StringLength(255)]
        public string? Location { get; set; }

        [Required(ErrorMessage = "Acquisition Date is required.")]
        [DataType(DataType.Date)]
        public DateTime AcquisitionDate { get; set; } = DateTime.Now.Date;

        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? ImagePath { get; set; }

        [StringLength(500)]
        public string? PdfFilePath { get; set; }

        // Navigation property to Book
        public Book? Book { get; set; }
    }
}
