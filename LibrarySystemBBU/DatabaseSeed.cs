using Microsoft.AspNetCore.Identity;
using LibrarySystemBBU.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

internal class DatabaseSeed
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserDataContext _userDataContext;

    public DatabaseSeed(RoleManager<IdentityRole> roleManager, UserDataContext userDataContext)
    {
        _roleManager = roleManager;
        _userDataContext = userDataContext;
    }

    internal async Task AddRoles()
    {
        var roles = new List<string> { "Admin", "Account", "User", "Manager" };

        foreach (var role in roles)
        {
            if (!(await _roleManager.RoleExistsAsync(role)))
            {
                var appRole = new IdentityRole
                {
                    Name = role
                };

                await _roleManager.CreateAsync(appRole);
            }
        }
    }
}
