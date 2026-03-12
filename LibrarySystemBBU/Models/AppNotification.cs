using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class AppNotification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }

        [Required]
        [StringLength(50)]
        public string EventType { get; set; } = string.Empty;
        // EventType values: "NewMember", "LibraryLog", "OverdueLoan", "ReminderSent"

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Message { get; set; }

        [StringLength(200)]
        public string? Url { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}