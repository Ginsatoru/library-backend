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
    public class CatalogsController : Controller
    {
        private readonly DataContext _context;

        public CatalogsController(DataContext context)
        {
            _context = context;
        }

        // GET: Catalogs
        public async Task<IActionResult> Index()
        {
            var dataContext = _context.Catalogs.Include(c => c.Book);
            return View(await dataContext.ToListAsync());
        }

        // GET: Catalogs/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var catalog = await _context.Catalogs
                .Include(c => c.Book)
                .FirstOrDefaultAsync(m => m.CatalogId == id);
            if (catalog == null) return NotFound();

            return View(catalog);
        }

        // GET: Catalogs/Create
        public IActionResult Create()
        {
            // Show Book title in dropdown (or combine Title & Author if you want)
            ViewData["BookId"] = new SelectList(_context.Books, "BookId", "Title");
            return View();
        }

        // POST: Catalogs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
     [Bind("CatalogId,BookId,Barcode,Status,Location,AcquisitionDate,ImagePath,PdfFilePath")] Catalog catalog,
     IFormFile? ImageFile,
     IFormFile? PdfFile)
        {
            if (ModelState.IsValid)
            {
                catalog.CatalogId = Guid.NewGuid();
                catalog.Created = DateTime.UtcNow;
                catalog.Modified = DateTime.UtcNow;

                // Image Upload
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                    var imgFile = $"{Guid.NewGuid()}_{Path.GetFileName(ImageFile.FileName)}";
                    var imgPath = Path.Combine(uploads, imgFile);
                    using (var stream = new FileStream(imgPath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }
                    catalog.ImagePath = "/uploads/" + imgFile;
                }

                // PDF Upload
                if (PdfFile != null && PdfFile.Length > 0)
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                    var pdfFile = $"{Guid.NewGuid()}_{Path.GetFileName(PdfFile.FileName)}";
                    var pdfPath = Path.Combine(uploads, pdfFile);
                    using (var stream = new FileStream(pdfPath, FileMode.Create))
                    {
                        await PdfFile.CopyToAsync(stream);
                    }
                    catalog.PdfFilePath = "/uploads/" + pdfFile;
                }

                _context.Add(catalog);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BookId"] = new SelectList(_context.Books, "BookId", "Title", catalog.BookId);
            return View(catalog);
        }


        // GET: Catalogs/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var catalog = await _context.Catalogs.FindAsync(id);
            if (catalog == null) return NotFound();

            ViewData["BookId"] = new SelectList(_context.Books, "BookId", "Title", catalog.BookId);
            return View(catalog);
        }

        // POST: Catalogs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            Guid id,
            [Bind("CatalogId,BookId,Barcode,Status,Location,AcquisitionDate,Created,ImagePath,PdfFilePath")] Catalog catalog,
            IFormFile? ImageFile,
            IFormFile? PdfFile)
        {
            if (id != catalog.CatalogId)
                return NotFound();

            // Get the existing row from DB for original paths!
            var existingCatalog = await _context.Catalogs.AsNoTracking().FirstOrDefaultAsync(c => c.CatalogId == id);
            if (existingCatalog == null)
                return NotFound();

            if (ModelState.IsValid)
            {
                // Only update the Modified timestamp, keep original Created
                catalog.Modified = DateTime.UtcNow;
                catalog.Created = existingCatalog.Created;

                // --- Image Upload logic ---
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                    var imgFile = $"{Guid.NewGuid()}_{Path.GetFileName(ImageFile.FileName)}";
                    var imgPath = Path.Combine(uploads, imgFile);
                    using (var stream = new FileStream(imgPath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }
                    // Optional: Delete old image file
                    if (!string.IsNullOrEmpty(existingCatalog.ImagePath))
                    {
                        var oldImage = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingCatalog.ImagePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                        if (System.IO.File.Exists(oldImage)) System.IO.File.Delete(oldImage);
                    }
                    catalog.ImagePath = "/uploads/" + imgFile;
                }
                else
                {
                    // Keep old image path if not uploading new
                    catalog.ImagePath = existingCatalog.ImagePath;
                }

                // --- PDF Upload logic ---
                if (PdfFile != null && PdfFile.Length > 0)
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                    var pdfFile = $"{Guid.NewGuid()}_{Path.GetFileName(PdfFile.FileName)}";
                    var pdfPath = Path.Combine(uploads, pdfFile);
                    using (var stream = new FileStream(pdfPath, FileMode.Create))
                    {
                        await PdfFile.CopyToAsync(stream);
                    }
                    // Optional: Delete old PDF file
                    if (!string.IsNullOrEmpty(existingCatalog.PdfFilePath))
                    {
                        var oldPdf = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingCatalog.PdfFilePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                        if (System.IO.File.Exists(oldPdf)) System.IO.File.Delete(oldPdf);
                    }
                    catalog.PdfFilePath = "/uploads/" + pdfFile;
                }
                else
                {
                    // Keep old PDF path if not uploading new
                    catalog.PdfFilePath = existingCatalog.PdfFilePath;
                }

                try
                {
                    _context.Update(catalog);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CatalogExists(catalog.CatalogId))
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
            ViewData["BookId"] = new SelectList(_context.Books, "BookId", "Title", catalog.BookId);
            return View(catalog);
        }


        // GET: Catalogs/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var catalog = await _context.Catalogs
                .Include(c => c.Book)
                .FirstOrDefaultAsync(m => m.CatalogId == id);
            if (catalog == null) return NotFound();

            return View(catalog);
        }

        // POST: Catalogs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var catalog = await _context.Catalogs.FindAsync(id);
            if (catalog != null)
            {
                _context.Catalogs.Remove(catalog);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CatalogExists(Guid id)
        {
            return _context.Catalogs.Any(e => e.CatalogId == id);
        }
    }
}
