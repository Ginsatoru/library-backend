using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    public class Member
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid MemberId { get; set; }

        // ---------------- Profile ----------------
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(255)]
        public required string FullName { get; set; }

        [StringLength(50)]
        public string? Gender { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [StringLength(100)]
        [EmailAddress(ErrorMessage = "Invalid Email Address.")]
        public required string Email { get; set; }

        [StringLength(20)]
        [Phone(ErrorMessage = "Invalid Phone Number.")]
        public string? Phone { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Member Type is required.")]
        [StringLength(50)]
        public required string MemberType { get; set; }

        [Required(ErrorMessage = "Join Date is required.")]
        [DataType(DataType.Date)]
        public DateTime JoinDate { get; set; } = DateTime.Now.Date;

        public DateTime Modified { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? ProfilePicturePath { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        // ---------- Extra IDs & Telegram ----------
        [StringLength(100)]
        public string? DICardNumber { get; set; }

        [StringLength(100)]
        public string? TelegramChatId { get; set; }
        [StringLength(100)]
        public string? TelegramUsername { get; set; }
        public long? TelegramUserId { get; set; }

        /// e.g. https://t.me/YourBot?start=TelegramPairToken
        [StringLength(100)]
        public string? TelegramPairToken { get; set; }

        // ---------------- Links ----------------
        public Guid? UserId { get; set; }
        public Users? Users { get; set; }

        public ICollection<BookBorrow> BookLoans { get; set; } = new List<BookBorrow>();

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [StringLength(200)]
        public string? PasswordHash { get; set; }
        public Guid? PasswordResetToken { get; set; }

        public DateTime? PasswordResetExpiry { get; set; }

        public DateTime? LastPasswordResetAt { get; set; }
        public Guid? LastPasswordResetByUserId { get; set; }
        [ForeignKey(nameof(LastPasswordResetByUserId))]
        public Users? LastPasswordResetByUser { get; set; }

        // --------------- Reset Policy Flags ---------------
        public bool AllowSelfPasswordReset { get; set; } = false;
        public bool StaffOnlyPasswordReset { get; set; } = true;


        // -------- Member OTP Reset (6-digit OTP) --------
        [StringLength(10)]
        public string? PasswordResetOtp { get; set; }

        public DateTime? PasswordResetOtpExpiry { get; set; }


        // --------------- View-only / Form Fields (not mapped) ---------------
        [NotMapped]
        [StringLength(20, MinimumLength = 5)]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [NotMapped]
        [StringLength(20, MinimumLength = 5)]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string? ConfirmPassword { get; set; }

        [NotMapped]
        public bool HasLinkedUser => UserId.HasValue;

        [NotMapped]
        public bool CanMemberLogin => !string.IsNullOrWhiteSpace(PasswordHash);

        // --------------- Helper Methods ---------------

        public bool TrySetPassword(string? plain)
        {
            if (string.IsNullOrWhiteSpace(plain)) return false;
            if (plain.Length < 5 || plain.Length > 20) return false;

            PasswordHash = BCrypt.Net.BCrypt.HashPassword(plain);
            Modified = DateTime.UtcNow;
            LastPasswordResetAt = DateTime.UtcNow;
            return true;
        }

        public bool VerifyPassword(string? plain)
        {
            if (string.IsNullOrWhiteSpace(plain) || string.IsNullOrWhiteSpace(PasswordHash)) return false;
            return BCrypt.Net.BCrypt.Verify(plain, PasswordHash);
        }

        public Guid CreatePasswordResetToken(int validMinutes = 30)
        {
            var token = Guid.NewGuid();
            PasswordResetToken = token;
            PasswordResetExpiry = DateTime.UtcNow.AddMinutes(validMinutes);
            Modified = DateTime.UtcNow;
            return token;
        }

        public void ClearPasswordResetToken()
        {
            PasswordResetToken = null;
            PasswordResetExpiry = null;
            Modified = DateTime.UtcNow;
        }

        public bool IsResetTokenValid(Guid token)
        {
            return PasswordResetToken.HasValue
                   && PasswordResetToken.Value == token
                   && PasswordResetExpiry.HasValue
                   && PasswordResetExpiry.Value >= DateTime.UtcNow;
        }

        public static bool CanResetPassword(Users? actingUser, Member target)
        {
            if (actingUser == null) return false;

            var role = (actingUser.RoleName ?? "").Trim();
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Librarian", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (target.AllowSelfPasswordReset && target.UserId.HasValue && actingUser.Id == target.UserId.Value)
            {
                return true;
            }

            return false;
        }

        public void MarkPasswordResetBy(Users? actingUser)
        {
            LastPasswordResetAt = DateTime.UtcNow;
            LastPasswordResetByUserId = actingUser?.Id;
            Modified = DateTime.UtcNow;
        }
    }
}
