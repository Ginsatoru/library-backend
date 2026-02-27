using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LibrarySystemBBU.ViewModels
{
    public class UserProfileViewModel
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? RoleName { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public string? ProfilePicturePath { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Modified { get; set; }

        public IFormFile? Picture { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmNewPassword { get; set; }

    }
}
