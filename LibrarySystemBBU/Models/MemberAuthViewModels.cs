using System.ComponentModel.DataAnnotations;

namespace LibrarySystemBBU.Models
{
    public class MemberRegisterViewModel
    {
        [Required, StringLength(255)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required, EmailAddress, StringLength(100)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required, StringLength(20)]
        [Phone(ErrorMessage = "Invalid phone number.")]
        [Display(Name = "Phone")]
        public string Phone { get; set; }

        [StringLength(255)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [StringLength(50)]
        [Display(Name = "Gender")]
        public string? Gender { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Member Type")]
        public string MemberType { get; set; } = "General";

        [Required, StringLength(20, MinimumLength = 5)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }
    }

    public class MemberLoginViewModel
    {
        [Display(Name = "Email / Phone / Full name")]
        [Required(ErrorMessage = "{0} is required.")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "{0} is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; } = false;

        public string? ReturnUrl { get; set; }
    }
}
