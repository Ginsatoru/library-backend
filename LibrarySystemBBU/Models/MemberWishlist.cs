using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class MemberWishlist
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WishlistId { get; set; }

        [Required]
        public Guid MemberId { get; set; }

        [ForeignKey(nameof(MemberId))]
        public Member Member { get; set; } = default!;

        [Required]
        public Guid CatalogId { get; set; }

        [ForeignKey(nameof(CatalogId))]
        public Catalog Catalog { get; set; } = default!;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}