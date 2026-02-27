using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public sealed class Users
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required, StringLength(50)]
        public string FirstName { get; set; }

        [Required, StringLength(50)]
        public string LastName { get; set; }

        [Required, StringLength(100)]
        public string UserName { get; set; }

        [Required, StringLength(100), EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(200)]
        public string Password { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? Gender { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public DateTime? Created { get; set; }
        public DateTime? Modified { get; set; }

        [StringLength(100)]
        public string? RoleName { get; set; }

        [StringLength(500)]
        public string? ProfilePicturePath { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        // =========================
        // Forgot Password (OTP)
        // =========================
        [StringLength(200)]
        public string? PasswordResetOtpHash { get; set; }

        public DateTime? PasswordResetOtpExpiresUtc { get; set; }

        // --- Navigation ---
        public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
        public ICollection<Member> LibraryMembers { get; set; } = new List<Member>();

        public Users()
        {
            Id = Guid.NewGuid();
            Created = DateTime.UtcNow;
            Modified = DateTime.UtcNow;
        }
    }
}
