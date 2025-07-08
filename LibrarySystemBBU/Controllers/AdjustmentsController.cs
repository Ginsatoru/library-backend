using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;

namespace LibrarySystemBBU.Controllers
{
    public class AdjustmentsController : Controller
    {
        private readonly DataContext _context;

        public AdjustmentsController(DataContext context)
        {
            _context = context;
        }

        // GET: Adjustments
        public async Task<IActionResult> Index()
        {
            var dataContext = _context.Adjustments.Include(a => a.AdjustedByUser).Include(a => a.Catalog);
            return View(await dataContext.ToListAsync());
        }

        // GET: Adjustments/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var adjustment = await _context.Adjustments
                .Include(a => a.AdjustedByUser)
                .Include(a => a.Catalog)
                .FirstOrDefaultAsync(m => m.AdjustmentId == id);
            if (adjustment == null)
            {
                return NotFound();
            }

            return View(adjustment);
        }

        // GET: Adjustments/Create
        public IActionResult Create()
        {
            ViewData["AdjustedByUserId"] = new SelectList(_context.Users, "Id", "UserName");
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode");
            return View();
        }

        // POST: Adjustments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AdjustmentId,CatalogId,AdjustmentType,QuantityChange,AdjustmentDate,Reason,AdjustedByUserId,Created,Modified")] Adjustment adjustment)
        {
            if (ModelState.IsValid)
            {
                adjustment.AdjustmentId = Guid.NewGuid();
                _context.Add(adjustment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AdjustedByUserId"] = new SelectList(_context.Users, "Id", "UserName", adjustment.AdjustedByUserId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", adjustment.CatalogId);
            return View(adjustment);
        }

        // GET: Adjustments/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var adjustment = await _context.Adjustments.FindAsync(id);
            if (adjustment == null)
            {
                return NotFound();
            }
            ViewData["AdjustedByUserId"] = new SelectList(_context.Users, "Id", "UserName", adjustment.AdjustedByUserId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", adjustment.CatalogId);
            return View(adjustment);
        }

        // POST: Adjustments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("AdjustmentId,CatalogId,AdjustmentType,QuantityChange,AdjustmentDate,Reason,AdjustedByUserId,Created,Modified")] Adjustment adjustment)
        {
            if (id != adjustment.AdjustmentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(adjustment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AdjustmentExists(adjustment.AdjustmentId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["AdjustedByUserId"] = new SelectList(_context.Users, "Id", "UserName", adjustment.AdjustedByUserId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", adjustment.CatalogId);
            return View(adjustment);
        }

        // GET: Adjustments/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var adjustment = await _context.Adjustments
                .Include(a => a.AdjustedByUser)
                .Include(a => a.Catalog)
                .FirstOrDefaultAsync(m => m.AdjustmentId == id);
            if (adjustment == null)
            {
                return NotFound();
            }

            return View(adjustment);
        }

        // POST: Adjustments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var adjustment = await _context.Adjustments.FindAsync(id);
            if (adjustment != null)
            {
                _context.Adjustments.Remove(adjustment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AdjustmentExists(Guid id)
        {
            return _context.Adjustments.Any(e => e.AdjustmentId == id);
        }
    }
}
