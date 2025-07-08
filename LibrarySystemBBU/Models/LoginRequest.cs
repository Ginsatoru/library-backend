using System.ComponentModel.DataAnnotations;

namespace LibrarySystemBBU.Models
{
    public class LoginRequest
    {
        [Display(Name = "User Name")]
        [Required(ErrorMessage = "{0} is needed.")]
        public required string UserName { get; set; } // FIX: Used required

        [Required(ErrorMessage = "{0} is needed.")]
        [DataType(DataType.Password)]
        public required string Password { get; set; } // FIX: Used required

        [Display(Name = "RememberMe")]
        public bool RememberMe { get; set; } = false;
    }
}