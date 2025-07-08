using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystemBBU.Data
{
    public sealed class UserDataContext : IdentityDbContext
    {
        public UserDataContext(DbContextOptions<UserDataContext> options) : base(options) { }
    }
}
