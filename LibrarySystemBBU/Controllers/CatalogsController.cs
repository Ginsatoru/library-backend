using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystemBBU.Controllers
{
    public class CatalogsController : Controller
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _env;

        public CatalogsController(DataContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ViewModel (ដដែល បន្ថែម IFormFile)
        public class CatalogFormVM
        {
            // Catalog
            public Guid CatalogId { get; set; }
            public string Title { get; set; } = "";
            public string Author { get; set; } = "";
            public string ISBN { get; set; } = "";
            public string Category { get; set; } = "";
            public int TotalCopies { get; set; }
            public int AvailableCopies { get; set; }
            public string? ImagePath { get; set; }
            public string? PdfFilePath { get; set; }

            // New: files to upload
            public IFormFile? ImageFile { get; set; }
            public IFormFile? PdfFile { get; set; }

            // Books
            public List<BookRow> Books { get; set; } = new();
            public class BookRow
            {
                public int BookId { get; set; }
                public Guid CatalogId { get; set; }
                public string Barcode { get; set; } = "";
                public string Status { get; set; } = "Available";
                public string? Location { get; set; }
                public DateTime AcquisitionDate { get; set; } = DateTime.Now.Date;
            }
        }

        // ---------- Utilities ----------
        private async Task<string?> SaveFileAsync(IFormFile? file, string subFolder, string[] allowedExts)
        {
            if (file == null || file.Length == 0) return null;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext))
            {
                ModelState.AddModelError("", $"File type {ext} is not allowed.");
                return null;
            }

            var uploadsRoot = Path.Combine(_env.WebRootPath ?? "", "uploads", subFolder);
            Directory.CreateDirectory(uploadsRoot);

            var fname = $"{Guid.NewGuid():N}{ext}";
            var full = Path.Combine(uploadsRoot, fname);
            using (var stream = System.IO.File.Create(full))
            {
                await file.CopyToAsync(stream);
            }

            // return relative web path
            var webPath = $"/uploads/{subFolder}/{fname}".Replace("\\", "/");
            return webPath;
        }

        private void Normalize(CatalogFormVM vm)
        {
            vm.Title = vm.Title?.Trim() ?? "";
            vm.Author = vm.Author?.Trim() ?? "";
            vm.ISBN = vm.ISBN?.Trim() ?? "";
            vm.Category = vm.Category?.Trim() ?? "";
            vm.ImagePath = string.IsNullOrWhiteSpace(vm.ImagePath) ? null : vm.ImagePath!.Trim();
            vm.PdfFilePath = string.IsNullOrWhiteSpace(vm.PdfFilePath) ? null : vm.PdfFilePath!.Trim();
            vm.Books ??= new();
            foreach (var b in vm.Books)
            {
                b.Barcode = b.Barcode?.Trim() ?? "";
                b.Status = b.Status?.Trim() ?? "";
                b.Location = string.IsNullOrWhiteSpace(b.Location) ? null : b.Location!.Trim();
            }
        }

        private static bool Filled(CatalogFormVM.BookRow r) =>
            !(string.IsNullOrWhiteSpace(r.Barcode) || string.IsNullOrWhiteSpace(r.Status));

        // 🔐 New: server-side source of truth for TotalCopies & AvailableCopies
        private void RecalculateCopiesFromBooks(CatalogFormVM vm)
        {
            vm.Books ??= new();
            var filled = vm.Books.Where(Filled).ToList();

            vm.TotalCopies = filled.Count;
            vm.AvailableCopies = filled.Count(b =>
                string.Equals(b.Status, "Available", StringComparison.OrdinalIgnoreCase));
        }

        private static CatalogFormVM MapToVM(Catalog c) => new CatalogFormVM
        {
            CatalogId = c.CatalogId,
            Title = c.Title,
            Author = c.Author,
            ISBN = c.ISBN,
            Category = c.Category,
            TotalCopies = c.TotalCopies,
            AvailableCopies = c.AvailableCopies,
            ImagePath = c.ImagePath,
            PdfFilePath = c.PdfFilePath,
            Books = (c.Books ?? new List<Book>()).OrderBy(b => b.BookId).Select(b => new CatalogFormVM.BookRow
            {
                BookId = b.BookId,
                CatalogId = b.CatalogId,
                Barcode = b.Barcode,
                Status = b.Status,
                Location = b.Location,
                AcquisitionDate = b.AcquisitionDate
            }).ToList()
        };

        // small helper for CSV escaping
        private static string CsvEscape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var v = value.Replace("\"", "\"\"");
            return $"\"{v}\"";
        }

        // ---------- Index ----------
        public async Task<IActionResult> Index()
        {
            // Include Books so we can search by Barcode on the UI
            var catalogs = await _context.Catalogs
                .Include(c => c.Books)
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .ToListAsync();

            return View(catalogs);
        }

        // ---------- EXPORT (Catalog + Books, EN+KH, Excel-compatible .xls) ----------
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var catalogs = await _context.Catalogs
                .Include(c => c.Books)
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .ToListAsync();

            // Helper for HTML encoding
            string H(string? value) => WebUtility.HtmlEncode(value ?? "");

            var sb = new StringBuilder();

            // HTML header with UTF-8, Excel will read this correctly (Khmer included)
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<table border='1'>");

            // Header row (bilingual EN / KH)
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>Catalog Title</th>");
            sb.AppendLine("<th>Author</th>");
            sb.AppendLine("<th>ISBN</th>");
            sb.AppendLine("<th>Category</th>");
            sb.AppendLine("<th>Total Copies</th>");
            sb.AppendLine("<th>Available Copies</th>");
            sb.AppendLine("<th>Borrowed</th>");
            sb.AppendLine("<th>Read in Library</th>");
            sb.AppendLine("<th>Copy Barcode</th>");
            sb.AppendLine("<th>Copy Status</th>");
            sb.AppendLine("<th>Location</th>");
            sb.AppendLine("<th>Acquisition Date</th>");
            sb.AppendLine("</tr></thead>");

            sb.AppendLine("<tbody>");

            // One row per book copy; if no books, we still output one row with empty book fields
            foreach (var c in catalogs)
            {
                var books = c.Books?.OrderBy(b => b.BookId).ToList() ?? new List<Book>();

                if (books.Any())
                {
                    foreach (var b in books)
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendLine($"<td>{H(c.Title)}</td>");
                        sb.AppendLine($"<td>{H(c.Author)}</td>");
                        sb.AppendLine($"<td>{H(c.ISBN)}</td>");
                        sb.AppendLine($"<td>{H(c.Category)}</td>");
                        sb.AppendLine($"<td>{c.TotalCopies}</td>");
                        sb.AppendLine($"<td>{c.AvailableCopies}</td>");
                        sb.AppendLine($"<td>{c.BorrowCount}</td>");
                        sb.AppendLine($"<td>{c.InLibraryCount}</td>");
                        sb.AppendLine($"<td>{H(b.Barcode)}</td>");
                        sb.AppendLine($"<td>{H(b.Status)}</td>");
                        sb.AppendLine($"<td>{H(b.Location)}</td>");
                        sb.AppendLine($"<td>{H(b.AcquisitionDate.ToString("yyyy-MM-dd"))}</td>");
                        sb.AppendLine("</tr>");
                    }
                }
                else
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{H(c.Title)}</td>");
                    sb.AppendLine($"<td>{H(c.Author)}</td>");
                    sb.AppendLine($"<td>{H(c.ISBN)}</td>");
                    sb.AppendLine($"<td>{H(c.Category)}</td>");
                    sb.AppendLine($"<td>{c.TotalCopies}</td>");
                    sb.AppendLine($"<td>{c.AvailableCopies}</td>");
                    sb.AppendLine($"<td>{c.BorrowCount}</td>");
                    sb.AppendLine($"<td>{c.InLibraryCount}</td>");
                    sb.AppendLine("<td></td>");
                    sb.AppendLine("<td></td>");
                    sb.AppendLine("<td></td>");
                    sb.AppendLine("<td></td>");
                    sb.AppendLine("</tr>");
                }
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            // UTF-8 with BOM for safety with Khmer
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var bytes = encoding.GetBytes(sb.ToString());

            var fileName = "CatalogsWithBooks_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xls";

            // Excel will open this HTML file as a real spreadsheet
            return File(bytes, "application/vnd.ms-excel", fileName);
        }

        // ---------- Details ----------
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            var catalog = await _context.Catalogs
                .Include(c => c.Books)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound();
            return View(MapToVM(catalog));
        }

        // ---------- Create GET ----------
        public IActionResult Create() => View(new CatalogFormVM());

        // ---------- Create POST (with file upload & validations) ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CatalogFormVM vm)
        {
            Normalize(vm);

            // 🔐 Always recalc on server (ignore posted Total/Available)
            RecalculateCopiesFromBooks(vm);

            // ---- Numeric business rules for copies ----
            if (vm.TotalCopies < 0)
            {
                ModelState.AddModelError(nameof(vm.TotalCopies), "Total copies cannot be negative.");
            }

            if (vm.AvailableCopies < 0)
            {
                ModelState.AddModelError(nameof(vm.AvailableCopies), "Available copies cannot be negative.");
            }

            if (vm.AvailableCopies > vm.TotalCopies)
            {
                ModelState.AddModelError(nameof(vm.AvailableCopies), "Available copies cannot be greater than total copies.");
            }

            if (!ModelState.IsValid) return View(vm);

            // Save files (optional)
            if (vm.ImageFile != null)
                vm.ImagePath = await SaveFileAsync(vm.ImageFile, "images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" });
            if (vm.PdfFile != null)
                vm.PdfFilePath = await SaveFileAsync(vm.PdfFile, "pdfs", new[] { ".pdf" });

            if (!ModelState.IsValid) return View(vm); // in case file type invalid

            // ---- Professional conditions: ISBN and Barcode uniqueness ----

            // 1) ISBN must be unique among Catalogs
            if (!string.IsNullOrWhiteSpace(vm.ISBN))
            {
                bool isbnExists = await _context.Catalogs
                    .AsNoTracking()
                    .AnyAsync(c => c.ISBN == vm.ISBN);

                if (isbnExists)
                {
                    ModelState.AddModelError(nameof(vm.ISBN), "This ISBN already exists in another catalog.");
                }
            }

            // 2) Barcodes must be unique (within the posted list and in the DB)
            var filledRows = vm.Books.Where(Filled).ToList();
            if (filledRows.Any())
            {
                var barcodes = filledRows.Select(b => b.Barcode).ToList();

                // Check duplicate barcodes in the same form submission
                var duplicateLocal = barcodes
                    .GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateLocal.Any())
                {
                    ModelState.AddModelError("",
                        "Barcodes must be unique. Duplicate(s) in this catalog: " +
                        string.Join(", ", duplicateLocal));
                }

                // Check duplicate barcodes in the database
                var existingBarcodes = await _context.Books
                    .AsNoTracking()
                    .Where(b => barcodes.Contains(b.Barcode))
                    .Select(b => b.Barcode)
                    .Distinct()
                    .ToListAsync();

                if (existingBarcodes.Any())
                {
                    ModelState.AddModelError("",
                        "These barcodes already exist in the system: " +
                        string.Join(", ", existingBarcodes));
                }
            }

            if (!ModelState.IsValid) return View(vm);

            var cat = new Catalog
            {
                CatalogId = Guid.NewGuid(),
                Title = vm.Title,
                Author = vm.Author,
                ISBN = vm.ISBN,
                Category = vm.Category,
                TotalCopies = vm.TotalCopies,          // ✅ server-calculated
                AvailableCopies = vm.AvailableCopies,  // ✅ server-calculated
                ImagePath = vm.ImagePath,
                PdfFilePath = vm.PdfFilePath
            };
            _context.Catalogs.Add(cat);

            foreach (var r in vm.Books.Where(Filled))
            {
                _context.Books.Add(new Book
                {
                    CatalogId = cat.CatalogId,
                    Barcode = r.Barcode,
                    Status = r.Status,
                    Location = r.Location,
                    AcquisitionDate = r.AcquisitionDate,
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ---------- Edit GET ----------
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var catalog = await _context.Catalogs
                .Include(c => c.Books)
                .FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound();
            return View(MapToVM(catalog));
        }

        // ---------- Edit POST (file replace if uploaded & validations) ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, CatalogFormVM vm)
        {
            if (id != vm.CatalogId) return NotFound();
            Normalize(vm);

            // 🔐 Always recalc on server (ignore posted Total/Available)
            RecalculateCopiesFromBooks(vm);

            // ---- Numeric business rules for copies ----
            if (vm.TotalCopies < 0)
            {
                ModelState.AddModelError(nameof(vm.TotalCopies), "Total copies cannot be negative.");
            }

            if (vm.AvailableCopies < 0)
            {
                ModelState.AddModelError(nameof(vm.AvailableCopies), "Available copies cannot be negative.");
            }

            if (vm.AvailableCopies > vm.TotalCopies)
            {
                ModelState.AddModelError(nameof(vm.AvailableCopies), "Available copies cannot be greater than total copies.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var catalog = await _context.Catalogs
                .Include(c => c.Books)
                .FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound();

            // ---- Professional conditions: ISBN and Barcode uniqueness ----

            // 1) ISBN unique (exclude current catalog)
            if (!string.IsNullOrWhiteSpace(vm.ISBN))
            {
                bool isbnInUse = await _context.Catalogs
                    .AsNoTracking()
                    .AnyAsync(c => c.CatalogId != id && c.ISBN == vm.ISBN);

                if (isbnInUse)
                {
                    ModelState.AddModelError(nameof(vm.ISBN),
                        "This ISBN is already used by another catalog.");
                }
            }

            // 2) Barcodes unique
            var posted = vm.Books.Where(Filled).ToList();
            if (posted.Any())
            {
                var barcodes = posted.Select(b => b.Barcode).ToList();

                // Local duplicates in the form
                var duplicateLocal = barcodes
                    .GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateLocal.Any())
                {
                    ModelState.AddModelError("",
                        "Barcodes must be unique. Duplicate(s) in this catalog: " +
                        string.Join(", ", duplicateLocal));
                }

                // Duplicates in DB (exclude same BookId)
                var existing = await _context.Books
                    .AsNoTracking()
                    .Where(b => barcodes.Contains(b.Barcode))
                    .Select(b => new { b.BookId, b.Barcode })
                    .ToListAsync();

                var duplicateDbBarcodes = existing
                    .Where(e => posted.Any(p => p.Barcode == e.Barcode && p.BookId != e.BookId))
                    .Select(e => e.Barcode)
                    .Distinct()
                    .ToList();

                if (duplicateDbBarcodes.Any())
                {
                    ModelState.AddModelError("",
                        "These barcodes already exist in another copy: " +
                        string.Join(", ", duplicateDbBarcodes));
                }
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            // upload new files if provided
            if (vm.ImageFile != null)
            {
                var newPath = await SaveFileAsync(vm.ImageFile, "images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" });
                if (!string.IsNullOrWhiteSpace(newPath)) catalog.ImagePath = newPath;
            }
            if (vm.PdfFile != null)
            {
                var newPdf = await SaveFileAsync(vm.PdfFile, "pdfs", new[] { ".pdf" });
                if (!string.IsNullOrWhiteSpace(newPdf)) catalog.PdfFilePath = newPdf;
            }
            if (!ModelState.IsValid) return View(MapToVM(catalog));

            // update fields
            catalog.Title = vm.Title;
            catalog.Author = vm.Author;
            catalog.ISBN = vm.ISBN;
            catalog.Category = vm.Category;
            catalog.TotalCopies = vm.TotalCopies;          // ✅ server-calculated
            catalog.AvailableCopies = vm.AvailableCopies;  // ✅ server-calculated

            // sync books
            var postedFilled = vm.Books.Where(Filled).ToList();
            var dbBooks = catalog.Books.ToList();

            // update existing
            foreach (var row in postedFilled.Where(p => p.BookId > 0))
            {
                var db = dbBooks.FirstOrDefault(b => b.BookId == row.BookId);
                if (db != null)
                {
                    db.Barcode = row.Barcode;
                    db.Status = row.Status;
                    db.Location = row.Location;
                    db.AcquisitionDate = row.AcquisitionDate;
                    db.Modified = DateTime.UtcNow;
                }
            }

            // add new
            foreach (var row in postedFilled.Where(p => p.BookId == 0))
            {
                _context.Books.Add(new Book
                {
                    CatalogId = catalog.CatalogId,
                    Barcode = row.Barcode,
                    Status = row.Status,
                    Location = row.Location,
                    AcquisitionDate = row.AcquisitionDate,
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow
                });
            }

            // remove deleted
            var postedIds = postedFilled.Where(p => p.BookId > 0).Select(p => p.BookId).ToHashSet();
            var toRemove = dbBooks.Where(b => !postedIds.Contains(b.BookId)).ToList();
            if (toRemove.Any()) _context.Books.RemoveRange(toRemove);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ---------- API: List ----------
[HttpGet]
public async Task<IActionResult> IndexJson()
{
    var catalogs = await _context.Catalogs
        .Include(c => c.Books)
        .AsNoTracking()
        .OrderBy(c => c.Title)
        .Select(c => new
        {
            catalogId       = c.CatalogId,
            title           = c.Title,
            author          = c.Author,
            isbn            = c.ISBN,
            category        = c.Category,
            totalCopies     = c.TotalCopies,
            availableCopies = c.AvailableCopies,
            borrowCount     = c.BorrowCount,
            inLibraryCount  = c.InLibraryCount,
            imagePath       = c.ImagePath,
            hasPdf          = !string.IsNullOrEmpty(c.PdfFilePath)
        })
        .ToListAsync();

    return Json(catalogs);
}

// ---------- API: Single ----------
[HttpGet]
public async Task<IActionResult> DetailsJson(Guid id)
{
    var c = await _context.Catalogs
        .Include(c => c.Books)
        .AsNoTracking()
        .FirstOrDefaultAsync(cat => cat.CatalogId == id);

    if (c == null) return NotFound(new { success = false, message = "Catalog not found." });

    return Json(new
    {
        catalogId       = c.CatalogId,
        title           = c.Title,
        author          = c.Author,
        isbn            = c.ISBN,
        category        = c.Category,
        totalCopies     = c.TotalCopies,
        availableCopies = c.AvailableCopies,
        borrowCount     = c.BorrowCount,
        inLibraryCount  = c.InLibraryCount,
        imagePath       = c.ImagePath,
        pdfFilePath     = c.PdfFilePath,
        hasPdf          = !string.IsNullOrEmpty(c.PdfFilePath),
        books           = c.Books.Select(b => new
        {
            bookId          = b.BookId,
            barcode         = b.Barcode,
            status          = b.Status,
            location        = b.Location,
            acquisitionDate = b.AcquisitionDate
        })
    });
}

        // ---------- Delete ----------
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var catalog = await _context.Catalogs
                .Include(c => c.Books)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound();
            return View(MapToVM(catalog));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var catalog = await _context.Catalogs
                .Include(c => c.Books)
                .FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog != null)
            {
                if (catalog.Books.Any()) _context.Books.RemoveRange(catalog.Books);
                _context.Catalogs.Remove(catalog);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
