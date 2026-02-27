using System.ComponentModel.DataAnnotations;

namespace LibrarySystemBBU.Models
{
    public sealed class RegisterRequest
    {
        [Required(ErrorMessage = "{0} is needed.")]
        public required string UserName { get; set; }

        [Required(ErrorMessage = "{0} is needed.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public required string Email { get; set; }

        // NEW: Phone (required)
        [Required(ErrorMessage = "{0} is needed.")]
        [Phone(ErrorMessage = "Invalid phone number.")]
        [StringLength(20, ErrorMessage = "{0} must be at most {1} characters.")]
        public required string Phone { get; set; }

        [Required(ErrorMessage = "{0} is needed.")]
        public required string FirstName { get; set; }

        [Required(ErrorMessage = "{0} is needed.")]
        public required string LastName { get; set; }

        // Optional, defaults to "User"
        public string? RoleName { get; set; }

        [Required(ErrorMessage = "{0} is needed.")]
        [StringLength(20, MinimumLength = 5)]
        [DataType(DataType.Password)]
        public required string Password { get; set; }

        [Required(ErrorMessage = "{0} is needed.")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        [StringLength(20, MinimumLength = 5)]
        public required string ConfirmPassword { get; set; }
    }
}
