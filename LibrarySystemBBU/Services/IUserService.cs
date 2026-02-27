using LibrarySystemBBU.Models;
using System;
using System.Threading.Tasks;

namespace LibrarySystemBBU.Services
{
    public interface IUserService
    {
        Task<MsgResponse> CreateUser(RegisterRequest request);
        Task<MsgResponse> GetAuthenticatedUser(LoginRequest request);
        Task<bool> LogUserOut();

        Task<Users?> GetByIdAsync(Guid id);

        // Forgot password (Email OTP)
        Task<MsgResponse> RequestPasswordResetOtpAsync(string emailOrUsername);
        Task<MsgResponse> ResetPasswordWithOtpAsync(string emailOrUsername, string otpCode, string newPassword);
    }
}
