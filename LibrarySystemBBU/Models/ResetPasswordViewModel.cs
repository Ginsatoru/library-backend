using System.ComponentModel.DataAnnotations;

namespace LibrarySystemBBU.ViewModels
{
    public sealed class ResetPasswordViewModel
    {
        [Required]
        [StringLength(100)]
        public string EmailOrUserName { get; set; } = "";

        [Required]
        [StringLength(10)]
        public string OtpCode { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string ConfirmNewPassword { get; set; } = "";
    }
}
