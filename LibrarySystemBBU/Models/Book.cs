using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{

    public class Book
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookId { get; set; } 

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

        public ICollection<BookLoan> BookLoans { get; set; } = new List<BookLoan>();
        public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
        public ICollection<Catalog> Catalogs { get; set; } = new List<Catalog>();

        public ICollection<LoanBookDetail> LoanBookDetails { get; set; } = new List<LoanBookDetail>();
    }
}
