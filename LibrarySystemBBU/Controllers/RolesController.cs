using System;
using System.Linq;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystemBBU.Controllers
{
    public class RolesController : Controller
    {
        private readonly DataContext _context;

        public RolesController(DataContext context)
        {
            _context = context;
        }

        private string CurrentRole() =>
            HttpContext?.User?.Claims?.FirstOrDefault(c => c.Type.EndsWith("role", StringComparison.OrdinalIgnoreCase))?.Value
            ?? HttpContext?.Session?.GetString("RoleName");

        private bool IsAdmin() =>
            HttpContext?.User?.IsInRole("Admin") == true ||
            string.Equals(CurrentRole(), "Admin", StringComparison.OrdinalIgnoreCase);

        private bool IsLibrarian() =>
            HttpContext?.User?.IsInRole("Librarian") == true ||
            string.Equals(CurrentRole(), "Librarian", StringComparison.OrdinalIgnoreCase);

        private void PassRoleToView()
        {
            ViewBag.IsAdmin = IsAdmin();
            ViewBag.IsLibrarian = IsLibrarian();
            ViewBag.IsReadOnly = !IsAdmin();
        }

        // GET: Roles
        public async Task<IActionResult> Index()
        {
            if (!(IsAdmin() || IsLibrarian())) return RedirectToAction("AccessDenied", "Users");
            PassRoleToView();
            return View(await _context.Roles.ToListAsync());
        }

        // GET: Roles/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (!(IsAdmin() || IsLibrarian())) return RedirectToAction("AccessDenied", "Users");
            if (id == null) return NotFound();

            var roles = await _context.Roles.FirstOrDefaultAsync(m => m.Id == id);
            if (roles == null) return NotFound();

            PassRoleToView();
            return View(roles);
        }

        // GET: Roles/Create
        public IActionResult Create()
        {
            if (!(IsAdmin() || IsLibrarian())) return RedirectToAction("AccessDenied", "Users");
            PassRoleToView();
            return View();
        }

        // POST: Roles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] Roles roles)
        {
            if (!IsAdmin())
            {
                TempData["Error"] = "Only Admin can create roles.";
                PassRoleToView();
                return View(roles);
            }

            if (ModelState.IsValid)
            {
                roles.Id = Guid.NewGuid();
                _context.Add(roles);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PassRoleToView();
            return View(roles);
        }

        // GET: Roles/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (!(IsAdmin() || IsLibrarian())) return RedirectToAction("AccessDenied", "Users");
            if (id == null) return NotFound();

            var roles = await _context.Roles.FindAsync(id);
            if (roles == null) return NotFound();

            PassRoleToView();
            return View(roles);
        }

        // POST: Roles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name")] Roles roles)
        {
            if (!IsAdmin())
            {
                TempData["Error"] = "Only Admin can update roles.";
                PassRoleToView();
                return View(roles);
            }

            if (id != roles.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(roles);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Roles.Any(e => e.Id == roles.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            PassRoleToView();
            return View(roles);
        }

        // GET: Roles/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (!(IsAdmin() || IsLibrarian())) return RedirectToAction("AccessDenied", "Users");
            if (id == null) return NotFound();

            var roles = await _context.Roles.FirstOrDefaultAsync(m => m.Id == id);
            if (roles == null) return NotFound();

            PassRoleToView();
            return View(roles);
        }

        // POST: Roles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            if (!IsAdmin())
            {
                TempData["Error"] = "Only Admin can delete roles.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            var roles = await _context.Roles.FindAsync(id);
            if (roles != null) _context.Roles.Remove(roles);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
