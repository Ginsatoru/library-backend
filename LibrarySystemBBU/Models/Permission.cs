using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LibrarySystemBBU.Models
{
    public class Permission
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string ClaimName { get; set; } = null!;

        [Required, StringLength(100)]
        public string ClaimValue { get; set; } = null!;

        // Many-to-many with Roles
        public ICollection<Roles> Roles { get; set; } = new List<Roles>();

        // Many-to-many with Users
        public ICollection<Users> Users { get; set; } = new List<Users>();

        // If you’re using a join table
    }
}
