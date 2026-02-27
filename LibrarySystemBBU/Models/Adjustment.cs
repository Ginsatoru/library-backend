using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class Adjustment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid AdjustmentId { get; set; }

        [Required(ErrorMessage = "Catalog ID is required for adjustment.")]
        public Guid CatalogId { get; set; }

        [Required(ErrorMessage = "Adjustment Type is required.")]
        [StringLength(50)]
        public string AdjustmentType { get; set; } = string.Empty;

        /// <summary>
        /// Header-level summary (auto computed).
        /// Increase   => total should be >= 0
        /// Decrease   => total <= 0
        /// Damage/Lost => total <= 0 (usually -number of damaged/lost)
        /// </summary>
        [Required]
        public int QuantityChange { get; set; } = 0;

        [Required]
        [DataType(DataType.Date)]
        public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow.Date;

        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        public Guid? AdjustedByUserId { get; set; }

        public DateTime? Created { get; set; }
        public DateTime? Modified { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // Navigation
        [ForeignKey(nameof(CatalogId))]
        public Catalog? Catalog { get; set; }

        [ForeignKey(nameof(AdjustedByUserId))]
        public Users? AdjustedByUser { get; set; }

        public ICollection<AdjustmentDetail> AdjustmentDetails { get; set; }
            = new List<AdjustmentDetail>();
    }

    public class AdjustmentDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AdjustmentDetailId { get; set; }

        [Required]
        public Guid AdjustmentId { get; set; }

        [Required]
        public Guid CatalogId { get; set; }

        /// <summary>
        /// For Increase/Decrease/Correction:
        ///   - Used as quantity delta (signed).
        ///
        /// For Damage/Lost:
        ///   - We will override to -1 per row (one damaged/lost copy).
        /// </summary>
        [Required]
        public int QuantityChanged { get; set; }

        /// <summary>
        /// Only required for Damage/Lost.
        ///   - Points to the physical Book (Barcode).
        ///   - Nullable for Increase/Decrease/Correction.
        /// </summary>
        public int? BookId { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // Navigation
        [ForeignKey(nameof(AdjustmentId))]
        public Adjustment? Adjustment { get; set; }

        [ForeignKey(nameof(CatalogId))]
        public Catalog? Catalog { get; set; }

        [ForeignKey(nameof(BookId))]
        public Book? Book { get; set; }
    }
}
