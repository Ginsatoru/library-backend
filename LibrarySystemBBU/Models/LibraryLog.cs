using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class LibraryLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        [Required, StringLength(150)]
        public string StudentName { get; set; } = string.Empty;

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime VisitDate { get; set; } = DateTime.Today;

        [StringLength(200)]
        public string? Purpose { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // ==== NEW: Workflow status ====
        [Required, StringLength(20)]
        public string Status { get; set; } = "Pending";

        // Timestamp when approved (null if still pending)
        public DateTime? ApprovedUtc { get; set; }

        // Timestamp when returned (null if not yet returned)
        public DateTime? ReturnedUtc { get; set; }

        // Many books per log
        public ICollection<LibraryLogItem> Items { get; set; } = new List<LibraryLogItem>();
    }

    public class LibraryLogItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogItemId { get; set; }

        [Required]
        public int LogId { get; set; }

        [Required]
        public int BookId { get; set; }

        [ForeignKey(nameof(LogId))]
        public LibraryLog Log { get; set; } = default!;

        [ForeignKey(nameof(BookId))]
        public Book Book { get; set; } = default!;

        // (Optional) Mark per-item return time if you want that granularity
        public DateTime? ReturnedDate { get; set; }

        // Convenience (not mapped): Book Title from Catalog
        [NotMapped]
        public string? BookTitle => Book?.Catalog?.Title ?? Book?.Title;
    }
}
