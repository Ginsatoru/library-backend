using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class Catalog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid CatalogId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(255)]
        public required string Title { get; set; }

        [Required(ErrorMessage = "Author is required.")]
        [StringLength(255)]
        public required string Author { get; set; }

        [Required(ErrorMessage = "ISBN is required.")]
        [StringLength(50)]
        public required string ISBN { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        [StringLength(100)]
        public required string Category { get; set; }

        [Required(ErrorMessage = "Total copies is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Total copies must be a non-negative number.")]
        public int TotalCopies { get; set; }

        [Required(ErrorMessage = "Available copies is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Available copies must be a non-negative number.")]
        public int AvailableCopies { get; set; }

        [StringLength(500)]
        public string? ImagePath { get; set; }

        [StringLength(500)]
        public string? PdfFilePath { get; set; }
        public int BorrowCount { get; set; } = 0;
        public int InLibraryCount { get; set; } = 0;
        public ICollection<Book> Books { get; set; } = new List<Book>();
    }

    public class Book
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookId { get; set; }

        [Required(ErrorMessage = "Catalog is required for this book.")]
        public Guid CatalogId { get; set; }

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


        public Catalog Catalog { get; set; } = default!;

        public ICollection<PurchaseDetail> PurchaseDetails { get; set; } = new List<PurchaseDetail>();
        public ICollection<BookBorrowDetail> BookBorrowDetail { get; set; } = new List<BookBorrowDetail>();

        public string? Title { get; internal set; }
    }
}
