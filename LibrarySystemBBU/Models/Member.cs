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

        public Guid? UserId { get; set; }
        public Users? Users { get; set; }

        public ICollection<BookLoan> BookLoans { get; set; } = new List<BookLoan>();

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }
}
