// Services/IUserService.cs
using LibrarySystemBBU.Models;
using System.Threading.Tasks;

namespace LibrarySystemBBU.Services
{
    public interface IUserService
    {
        Task<MsgResponse> CreateUser(RegisterRequest request);
        Task<MsgResponse> GetAuthenticatedUser(LoginRequest request);
        Task<bool> LogUserOut();
    }
}