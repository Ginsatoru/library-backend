using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace LibrarySystemBBU.Controllers
{
    public class MembersController : Controller
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _env;

        public MembersController(DataContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Members
        public async Task<IActionResult> Index()
        {
            var dataContext = _context.Members.Include(m => m.Users);
            return View(await dataContext.ToListAsync());
        }

        // GET: Members/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m => m.MemberId == id);

            if (member == null) return NotFound();

            return View(member);
        }

        // GET: Members/Create
        public IActionResult Create()
        {
            // Use Username (not Email) for Linked User dropdown
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName");
            ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName"); // For Created By
            return View();
        }

        // POST: Members/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("MemberId,FullName,Gender,Email,Phone,Address,MemberType,JoinDate,Modified,ProfilePicturePath,IsActive,Notes,UserId,CreatedBy")] Member member,
            IFormFile ProfilePicture)
        {
            if (ModelState.IsValid)
            {
                member.MemberId = Guid.NewGuid();
                member.JoinDate = DateTime.UtcNow;
                member.Modified = DateTime.UtcNow;

                try
                {
                    if (ProfilePicture != null && ProfilePicture.Length > 0)
                    {
                        member.ProfilePicturePath = await SaveProfilePicture(ProfilePicture);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("ProfilePicturePath", "Image upload failed: " + ex.Message);
                    ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
                    ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
                    return View(member);
                }

                _context.Add(member);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
            ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
            return View(member);
        }

        // GET: Members/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
            ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
            return View(member);
        }

        // POST: Members/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            Guid id,
            [Bind("MemberId,FullName,Gender,Email,Phone,Address,MemberType,JoinDate,Modified,ProfilePicturePath,IsActive,Notes,UserId,CreatedBy")] Member member,
            IFormFile ProfilePicture)
        {
            if (id != member.MemberId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.MemberId == id);
                    if (existing == null) return NotFound();

                    member.Modified = DateTime.UtcNow;
                    member.JoinDate = existing.JoinDate;

                    try
                    {
                        if (ProfilePicture != null && ProfilePicture.Length > 0)
                        {
                            if (!string.IsNullOrEmpty(existing.ProfilePicturePath))
                                DeleteProfilePicture(existing.ProfilePicturePath);

                            member.ProfilePicturePath = await SaveProfilePicture(ProfilePicture);
                        }
                        else
                        {
                            member.ProfilePicturePath = existing.ProfilePicturePath;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("ProfilePicturePath", "Image upload failed: " + ex.Message);
                        ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
                        ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
                        return View(member);
                    }

                    _context.Update(member);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MemberExists(member.MemberId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
            ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
            return View(member);
        }

        // GET: Members/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m => m.MemberId == id);

            if (member == null) return NotFound();

            return View(member);
        }

        // POST: Members/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var member = await _context.Members.FindAsync(id);
            if (member != null)
            {
                if (!string.IsNullOrEmpty(member.ProfilePicturePath))
                    DeleteProfilePicture(member.ProfilePicturePath);

                _context.Members.Remove(member);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool MemberExists(Guid id)
        {
            return _context.Members.Any(e => e.MemberId == id);
        }

        // ----------- IMAGE UPLOAD HELPERS --------------
        private async Task<string> SaveProfilePicture(IFormFile profilePicture)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "members");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new InvalidOperationException("Only image files (.jpg, .jpeg, .png, .gif, .webp) are allowed.");

            if (profilePicture.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("File size must be less than 10MB.");

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profilePicture.CopyToAsync(stream);
            }

            return $"/uploads/members/{uniqueFileName}";
        }

        private void DeleteProfilePicture(string profilePicturePath)
        {
            if (string.IsNullOrEmpty(profilePicturePath)) return;
            var filePath = Path.Combine(_env.WebRootPath, profilePicturePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { }
            }
        }
    }
}
