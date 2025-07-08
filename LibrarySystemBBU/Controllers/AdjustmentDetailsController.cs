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
    public class AdjustmentDetailsController : Controller
    {
        private readonly DataContext _context;

        public AdjustmentDetailsController(DataContext context)
        {
            _context = context;
        }

        // GET: AdjustmentDetails
        public async Task<IActionResult> Index()
        {
            var dataContext = _context.AdjustmentDetails.Include(a => a.Adjustment).Include(a => a.Catalog);
            return View(await dataContext.ToListAsync());
        }

        // GET: AdjustmentDetails/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var adjustmentDetail = await _context.AdjustmentDetails
                .Include(a => a.Adjustment)
                .Include(a => a.Catalog)
                .FirstOrDefaultAsync(m => m.AdjustmentDetailId == id);
            if (adjustmentDetail == null)
            {
                return NotFound();
            }

            return View(adjustmentDetail);
        }

        // GET: AdjustmentDetails/Create
        public IActionResult Create()
        {
            ViewData["AdjustmentId"] = new SelectList(_context.Adjustments, "AdjustmentId", "AdjustmentType");
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode");
            return View();
        }

        // POST: AdjustmentDetails/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AdjustmentDetailId,AdjustmentId,CatalogId,QuantityChanged,Note")] AdjustmentDetail adjustmentDetail)
        {
            if (ModelState.IsValid)
            {
                _context.Add(adjustmentDetail);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AdjustmentId"] = new SelectList(_context.Adjustments, "AdjustmentId", "AdjustmentType", adjustmentDetail.AdjustmentId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", adjustmentDetail.CatalogId);
            return View(adjustmentDetail);
        }

        // GET: AdjustmentDetails/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var adjustmentDetail = await _context.AdjustmentDetails.FindAsync(id);
            if (adjustmentDetail == null)
            {
                return NotFound();
            }
            ViewData["AdjustmentId"] = new SelectList(_context.Adjustments, "AdjustmentId", "AdjustmentType", adjustmentDetail.AdjustmentId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", adjustmentDetail.CatalogId);
            return View(adjustmentDetail);
        }

        // POST: AdjustmentDetails/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AdjustmentDetailId,AdjustmentId,CatalogId,QuantityChanged,Note")] AdjustmentDetail adjustmentDetail)
        {
            if (id != adjustmentDetail.AdjustmentDetailId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(adjustmentDetail);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AdjustmentDetailExists(adjustmentDetail.AdjustmentDetailId))
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
            ViewData["AdjustmentId"] = new SelectList(_context.Adjustments, "AdjustmentId", "AdjustmentType", adjustmentDetail.AdjustmentId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", adjustmentDetail.CatalogId);
            return View(adjustmentDetail);
        }

        // GET: AdjustmentDetails/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var adjustmentDetail = await _context.AdjustmentDetails
                .Include(a => a.Adjustment)
                .Include(a => a.Catalog)
                .FirstOrDefaultAsync(m => m.AdjustmentDetailId == id);
            if (adjustmentDetail == null)
            {
                return NotFound();
            }

            return View(adjustmentDetail);
        }

        // POST: AdjustmentDetails/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var adjustmentDetail = await _context.AdjustmentDetails.FindAsync(id);
            if (adjustmentDetail != null)
            {
                _context.AdjustmentDetails.Remove(adjustmentDetail);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AdjustmentDetailExists(int id)
        {
            return _context.AdjustmentDetails.Any(e => e.AdjustmentDetailId == id);
        }
    }
}
