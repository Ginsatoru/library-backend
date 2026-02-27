using System.ComponentModel.DataAnnotations;

namespace LibrarySystemBBU.ViewModels
{
    public sealed class ForgotPasswordRequestViewModel
    {
        [Required(ErrorMessage = "Email or username is required.")]
        [StringLength(100)]
        public string EmailOrUserName { get; set; } = "";
    }
}
