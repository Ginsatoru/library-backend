using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security; // Not needed for Role model, remove if not used

namespace LibrarySystemBBU.Models
{
    public class Roles // Your custom role model
    {
        public Guid Id { get; set; }
        public required string Name { get; set; } // Role name, make required

        // Collection for custom Many-to-Many relationship with Permissions
        public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
        // public ICollection<Users> Users { get; set; } = new List<Users>(); // If Roles directly track Users

        public Roles()
        {
            Id = Guid.NewGuid(); // Example: Generate GUID in application
        }
        public ICollection<Users> Users { get; set; } = new List<Users>();
      

    }
}