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
        public required string AdjustmentType { get; set; }

        [Required(ErrorMessage = "Quantity Change is required.")]
        public int QuantityChange { get; set; } = 0;

        [Required(ErrorMessage = "Adjustment Date is required.")]
        [DataType(DataType.Date)]
        public DateTime AdjustmentDate { get; set; } = DateTime.Now.Date;

        [Required(ErrorMessage = "Reason is required.")]
        [StringLength(500)]
        public required string Reason { get; set; }

        public Guid? AdjustedByUserId { get; set; }

        // Allow null for Created/Modified for DB flexibility (if you want).
        public DateTime? Created { get; set; }
        public DateTime? Modified { get; set; }

        // Navigation
        [ForeignKey("CatalogId")]
        public Catalog? Catalog { get; set; }

        [ForeignKey("AdjustedByUserId")]
        public Users? AdjustedByUser { get; set; }

        public ICollection<AdjustmentDetail> AdjustmentDetails { get; set; } = new List<AdjustmentDetail>();
    }
}
