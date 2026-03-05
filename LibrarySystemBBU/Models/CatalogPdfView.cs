using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class CatalogPdfView
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public Guid CatalogId { get; set; }

        [Required]
        [StringLength(64)]
        public required string ViewerKey { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        public Catalog Catalog { get; set; } = default!;
    }
}
