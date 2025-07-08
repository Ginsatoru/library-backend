using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class AdjustmentDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AdjustmentDetailId { get; set; }

        [Required]
        public Guid AdjustmentId { get; set; }

        [Required]
        public Guid CatalogId { get; set; }

        [Required]
        public int QuantityChanged { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [ForeignKey("AdjustmentId")]
        public Adjustment? Adjustment { get; set; }

        [ForeignKey("CatalogId")]
        public Catalog? Catalog { get; set; }
    }
}
