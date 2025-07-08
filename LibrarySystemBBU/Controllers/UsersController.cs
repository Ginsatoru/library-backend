using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LibrarySystemBBU.Controllers
{
    public class UsersController : Controller
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UsersController(DataContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // --- ROLE DROPDOWN HELPER (HARDCODED) ---
        private void PopulateRolesDropDownList(object selectedRole = null)
        {
            var roles = new List<string> { "Admin", "Librarian", "User", "Guest" };
            ViewBag.Roles = new SelectList(roles, selectedRole);
        }

        // --- GENDER DROPDOWN HELPER (HARDCODED) ---
        private void PopulateGenderDropDownList(object selectedGender = null)
        {
            var genders = new List<string> { "Male", "Female", "Other" };
            ViewBag.Genders = new SelectList(genders, selectedGender);
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            var user = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);
            if (user == null) return NotFound();
            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            PopulateRolesDropDownList();
            PopulateGenderDropDownList();
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Users user, IFormFile ProfilePicture)
        {
            if (ModelState.IsValid)
            {
                user.Id = Guid.NewGuid();
                user.Created = DateTime.UtcNow;
                user.Modified = DateTime.UtcNow;

                if (ProfilePicture != null && ProfilePicture.Length > 0)
                {
                    user.ProfilePicturePath = await SaveProfilePicture(ProfilePicture);
                }

                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PopulateRolesDropDownList(user.RoleName);
            PopulateGenderDropDownList(user.Gender);
            return View(user);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Password = string.Empty;
            PopulateRolesDropDownList(user.RoleName);
            PopulateGenderDropDownList(user.Gender);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Users user, IFormFile ProfilePicture)
        {
            if (id != user.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.FindAsync(id);
                if (existingUser == null) return NotFound();

                // Update all allowed fields
                existingUser.FirstName = user.FirstName;
                existingUser.LastName = user.LastName;
                existingUser.UserName = user.UserName;
                existingUser.Email = user.Email;
                existingUser.Phone = user.Phone;
                existingUser.Address = user.Address;
                existingUser.Gender = user.Gender;
                existingUser.DateOfBirth = user.DateOfBirth;
                existingUser.RoleName = user.RoleName;
                existingUser.Notes = user.Notes;
                existingUser.IsActive = user.IsActive;
                existingUser.Modified = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(user.Password))
                    existingUser.Password = user.Password;

                if (ProfilePicture != null && ProfilePicture.Length > 0)
                {
                    if (!string.IsNullOrEmpty(existingUser.ProfilePicturePath))
                        DeleteProfilePicture(existingUser.ProfilePicturePath);

                    existingUser.ProfilePicturePath = await SaveProfilePicture(ProfilePicture);
                }
                else
                {
                    existingUser.ProfilePicturePath = user.ProfilePicturePath;
                }

                _context.Update(existingUser);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PopulateRolesDropDownList(user.RoleName);
            PopulateGenderDropDownList(user.Gender);
            return View(user);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                if (!string.IsNullOrEmpty(user.ProfilePicturePath))
                    DeleteProfilePicture(user.ProfilePicturePath);

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool UsersExists(Guid id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        // Save profile picture helper
        private async Task<string> SaveProfilePicture(IFormFile profilePicture)
        {
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new InvalidOperationException("Only image files are allowed.");

            if (profilePicture.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("File size must be less than 10MB.");

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profilePicture.CopyToAsync(stream);
            }

            return $"/uploads/profiles/{uniqueFileName}";
        }

        // Delete profile picture helper
        private void DeleteProfilePicture(string profilePicturePath)
        {
            if (string.IsNullOrEmpty(profilePicturePath)) return;
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, profilePicturePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); }
                catch { /* log if needed */ }
            }
        }
    }
}
