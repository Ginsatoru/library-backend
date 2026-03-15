using System.ComponentModel.DataAnnotations;

namespace LibrarySystemBBU.Models
{
    public class LibrarySettings
    {
        [Key]
        public int Id { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? WeekdayHours { get; set; }

        [StringLength(100)]
        public string? WeekendHours { get; set; }
    }
}