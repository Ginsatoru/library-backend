using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
using ExcelDataReader;

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

        // ── View Models ──────────────────────────────────────────────────────
        public class CatalogFormVM
        {
            public Guid CatalogId { get; set; }
            public string Title { get; set; } = "";
            public string Author { get; set; } = "";
            public string ISBN { get; set; } = "";
            public string Category { get; set; } = "";
            public string? Faculty { get; set; }
            public int TotalCopies { get; set; }
            public int AvailableCopies { get; set; }
            public string? ImagePath { get; set; }
            public string? PdfFilePath { get; set; }
            public IFormFile? ImageFile { get; set; }
            public IFormFile? PdfFile { get; set; }
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

        // ── Import DTOs ──────────────────────────────────────────────────────
        public class ImportRowDto
        {
            public int RowNumber { get; set; }
            public string? Title { get; set; }
            public string? Author { get; set; }
            public string? ISBN { get; set; }
            public string? Faculty { get; set; }
            public string? Category { get; set; }
            public string? Barcode { get; set; }
            public string? Status { get; set; }
            public string? Location { get; set; }
            public string? AcquisitionDate { get; set; }
            public string? Error { get; set; }
            public bool HasError => !string.IsNullOrEmpty(Error);
            public string? MatchedCatalogId { get; set; }
            public bool IsNewCatalog { get; set; }
            public bool IsNewBook { get; set; }
        }

        public class ImportConfirmRequest
        {
            public List<string> Fields { get; set; } = new();
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private async Task<string?> SaveFileAsync(IFormFile? file, string subFolder, string[] allowedExts)
        {
            if (file == null || file.Length == 0) return null;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext)) { ModelState.AddModelError("", $"File type {ext} is not allowed."); return null; }
            var uploadsRoot = Path.Combine(_env.WebRootPath ?? "", "uploads", subFolder);
            Directory.CreateDirectory(uploadsRoot);
            var fname = $"{Guid.NewGuid():N}{ext}";
            var full = Path.Combine(uploadsRoot, fname);
            using (var stream = System.IO.File.Create(full)) { await file.CopyToAsync(stream); }
            return $"/uploads/{subFolder}/{fname}".Replace("\\", "/");
        }

        private void Normalize(CatalogFormVM vm)
        {
            vm.Title = vm.Title?.Trim() ?? "";
            vm.Author = vm.Author?.Trim() ?? "";
            vm.ISBN = vm.ISBN?.Trim() ?? "";
            vm.Category = vm.Category?.Trim() ?? "";
            vm.Faculty = string.IsNullOrWhiteSpace(vm.Faculty) ? null : vm.Faculty!.Trim();
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
            Faculty = c.Faculty,
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

        private static string CsvEscape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var v = value.Replace("\"", "\"\"");
            return $"\"{v}\"";
        }

        /// <summary>
        /// Parse .xlsx / .xls (HTML export) / .csv / .tsv into rows.
        /// </summary>
        private static List<string[]> ParseExcelFile(Stream stream, string fileName)
        {
            var rows = new List<string[]>();
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (ext == ".xlsx")
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                using var excelReader = ExcelDataReader.ExcelReaderFactory.CreateOpenXmlReader(stream);
                var dataSet = excelReader.AsDataSet(new ExcelDataReader.ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataReader.ExcelDataTableConfiguration { UseHeaderRow = false }
                });
                var table = dataSet.Tables[0];
                foreach (System.Data.DataRow row in table.Rows)
                {
                    var cells = row.ItemArray
                        .Select(c => c == null || c == DBNull.Value ? "" : c.ToString()!.Trim())
                        .ToArray();
                    if (cells.Any(c => !string.IsNullOrWhiteSpace(c))) rows.Add(cells);
                }
            }
            else if (ext == ".xls")
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var content = reader.ReadToEnd();
                var trMatches = System.Text.RegularExpressions.Regex.Matches(
                    content, @"<tr[^>]*>(.*?)</tr>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                foreach (System.Text.RegularExpressions.Match tr in trMatches)
                {
                    var cellMatches = System.Text.RegularExpressions.Regex.Matches(
                        tr.Groups[1].Value, @"<t[dh][^>]*>(.*?)</t[dh]>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                        System.Text.RegularExpressions.RegexOptions.Singleline);
                    var cells = cellMatches
                        .Cast<System.Text.RegularExpressions.Match>()
                        .Select(m => WebUtility.HtmlDecode(
                            System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value, "<[^>]+>", "").Trim()))
                        .ToArray();
                    if (cells.Length > 0) rows.Add(cells);
                }
            }
            else
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var content = reader.ReadToEnd();
                char delim = content.Contains('\t') ? '\t' : ',';
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    rows.Add(trimmed.Split(delim).Select(c => c.Trim('"').Trim()).ToArray());
                }
            }

            return rows;
        }

        // ---------- Index ----------
        public IActionResult Index()
        {
            return View();
        }

        // ---------- EXPORT ----------
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var catalogs = await _context.Catalogs
                .Include(c => c.Books)
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .ToListAsync();

            string H(string? value) => WebUtility.HtmlEncode(value ?? "");
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" /></head><body>");
            sb.AppendLine("<table border='1'><thead><tr>");
            sb.AppendLine("<th>Catalog Title</th><th>Author</th><th>ISBN</th><th>Category</th><th>Faculty</th>");
            sb.AppendLine("<th>Total Copies</th><th>Available Copies</th><th>Borrowed</th><th>Read in Library</th><th>PDF Viewers</th>");
            sb.AppendLine("<th>Copy Barcode</th><th>Copy Status</th><th>Location</th><th>Acquisition Date</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var c in catalogs)
            {
                var books = c.Books?.OrderBy(b => b.BookId).ToList() ?? new List<Book>();
                if (books.Any())
                {
                    foreach (var b in books)
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendLine($"<td>{H(c.Title)}</td><td>{H(c.Author)}</td><td>{H(c.ISBN)}</td><td>{H(c.Category)}</td><td>{H(c.Faculty)}</td>");
                        sb.AppendLine($"<td>{c.TotalCopies}</td><td>{c.AvailableCopies}</td><td>{c.BorrowCount}</td><td>{c.InLibraryCount}</td><td>{c.PdfViewerCount}</td>");
                        sb.AppendLine($"<td>{H(b.Barcode)}</td><td>{H(b.Status)}</td><td>{H(b.Location)}</td><td>{H(b.AcquisitionDate.ToString("yyyy-MM-dd"))}</td>");
                        sb.AppendLine("</tr>");
                    }
                }
                else
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{H(c.Title)}</td><td>{H(c.Author)}</td><td>{H(c.ISBN)}</td><td>{H(c.Category)}</td><td>{H(c.Faculty)}</td>");
                    sb.AppendLine($"<td>{c.TotalCopies}</td><td>{c.AvailableCopies}</td><td>{c.BorrowCount}</td><td>{c.InLibraryCount}</td><td>{c.PdfViewerCount}</td>");
                    sb.AppendLine("<td></td><td></td><td></td><td></td>");
                    sb.AppendLine("</tr>");
                }
            }
            sb.AppendLine("</tbody></table></body></html>");

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var bytes = encoding.GetBytes(sb.ToString());
            return File(bytes, "application/vnd.ms-excel",
                "CatalogsWithBooks_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xls");
        }

        // ---------- IMPORT: Preview ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportPreview(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file uploaded." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xls" && ext != ".xlsx" && ext != ".csv" && ext != ".tsv")
                return Json(new { success = false, message = "Unsupported file type. Please upload .xls, .xlsx, .csv, or .tsv." });

            List<string[]> rawRows;
            try
            {
                using var stream = file.OpenReadStream();
                rawRows = ParseExcelFile(stream, file.FileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Failed to parse file: {ex.Message}" });
            }

            if (rawRows.Count < 2)
                return Json(new { success = false, message = "File has no data rows." });

            var header = rawRows[0].Select(h => h.ToLowerInvariant().Trim()).ToArray();

            int Col(params string[] names)
            {
                foreach (var n in names)
                {
                    var idx = Array.IndexOf(header, n);
                    if (idx >= 0) return idx;
                }
                return -1;
            }

            int iTitle = Col("catalog title", "title");
            int iAuthor = Col("author");
            int iISBN = Col("isbn");
            int iCat = Col("category");
            int iBarcode = Col("copy barcode", "barcode");
            int iStatus = Col("copy status", "status");
            int iLoc = Col("location");
            int iAcq = Col("acquisition date", "acquisitiondate");
            int iFaculty = Col("faculty");

            var allCatalogs = await _context.Catalogs
                .Include(c => c.Books)
                .AsNoTracking()
                .ToListAsync();

            var catalogByISBN = allCatalogs
                .Where(c => !string.IsNullOrWhiteSpace(c.ISBN))
                .GroupBy(c => c.ISBN!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var catalogByTitle = allCatalogs
                .Where(c => !string.IsNullOrWhiteSpace(c.Title))
                .GroupBy(c => c.Title!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var allBarcodes = allCatalogs
                .SelectMany(c => c.Books)
                .Select(b => b.Barcode?.Trim() ?? "")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var previewRows = new List<ImportRowDto>();

            for (int i = 1; i < rawRows.Count; i++)
            {
                var cells = rawRows[i];
                string Cell(int idx) => idx >= 0 && idx < cells.Length ? cells[idx].Trim() : "";

                var row = new ImportRowDto { RowNumber = i + 1 };
                var errs = new List<string>();

                row.Title = Cell(iTitle);
                row.Author = Cell(iAuthor);
                row.ISBN = Cell(iISBN);
                row.Category = Cell(iCat);
                row.Barcode = Cell(iBarcode);
                row.Status = Cell(iStatus);
                row.Location = Cell(iLoc);
                row.AcquisitionDate = Cell(iAcq);
                row.Faculty = Cell(iFaculty);

                if (string.IsNullOrWhiteSpace(row.Title))
                    errs.Add("Title is required");

                Catalog? matched = null;
                if (!string.IsNullOrWhiteSpace(row.ISBN) && catalogByISBN.TryGetValue(row.ISBN, out var byISBN))
                    matched = byISBN;
                else if (!string.IsNullOrWhiteSpace(row.Title) && catalogByTitle.TryGetValue(row.Title, out var byTitle))
                    matched = byTitle;

                row.MatchedCatalogId = matched?.CatalogId.ToString();
                row.IsNewCatalog = matched == null && !string.IsNullOrWhiteSpace(row.Title);

                if (!string.IsNullOrWhiteSpace(row.Barcode))
                {
                    var existsInOtherCatalog = matched == null
                        ? allBarcodes.Contains(row.Barcode)
                        : allBarcodes.Contains(row.Barcode) &&
                          !matched.Books.Any(b => b.Barcode.Trim().Equals(row.Barcode, StringComparison.OrdinalIgnoreCase));

                    row.IsNewBook = matched == null
                        ? true
                        : !matched.Books.Any(b => b.Barcode.Trim().Equals(row.Barcode, StringComparison.OrdinalIgnoreCase));

                    if (existsInOtherCatalog && row.IsNewBook)
                        errs.Add($"Barcode '{row.Barcode}' already exists in another catalog");

                    if (!string.IsNullOrWhiteSpace(row.Status) &&
                        !new[] { "available", "borrowed", "maintenance" }.Contains(row.Status.ToLowerInvariant()))
                        errs.Add($"Status '{row.Status}' is not valid (use Available/Borrowed/Maintenance)");

                    if (!string.IsNullOrWhiteSpace(row.AcquisitionDate) &&
                        !DateTime.TryParse(row.AcquisitionDate, out _))
                        errs.Add($"Acquisition date '{row.AcquisitionDate}' is not a valid date");
                }

                if (errs.Any()) row.Error = string.Join("; ", errs);
                previewRows.Add(row);
            }

            var goodRows = previewRows.Where(r => !r.HasError).ToList();
            var errorRows = previewRows.Where(r => r.HasError).ToList();

            var goodCount = goodRows.Count;
            var errorCount = errorRows.Count;
            var newCats = goodRows.Count(r => r.IsNewCatalog);
            var updCats = goodRows.Count(r => !r.IsNewCatalog && r.MatchedCatalogId != null);

            HttpContext.Session.SetString("ImportRows", JsonSerializer.Serialize(goodRows));

            const int previewLimit = 50;
            var previewSample = errorRows.Take(previewLimit)
                .Concat(goodRows.Take(previewLimit))
                .OrderBy(r => r.RowNumber)
                .ToList();

            return Json(new
            {
                success = true,
                totalRows = previewRows.Count,
                goodRows = goodCount,
                errorRows = errorCount,
                newCatalogs = newCats,
                updatedCatalogs = updCats,
                rows = previewSample
            });
        }

        // ---------- IMPORT: Confirm ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportConfirm([FromBody] ImportConfirmRequest request)
        {
            var json = HttpContext.Session.GetString("ImportRows");
            if (string.IsNullOrEmpty(json))
                return Json(new { success = false, message = "Session expired or no preview data found. Please re-upload the file." });

            List<ImportRowDto> rows;
            try
            {
                rows = JsonSerializer.Deserialize<List<ImportRowDto>>(json) ?? new();
            }
            catch
            {
                return Json(new { success = false, message = "Failed to read import session data. Please re-upload the file." });
            }

            if (!rows.Any())
                return Json(new { success = false, message = "No valid rows to import." });

            var fields = request?.Fields ?? new List<string>();
            bool upd(string f) => fields.Contains(f, StringComparer.OrdinalIgnoreCase);

            var allCatalogs = await _context.Catalogs
                .Include(c => c.Books)
                .ToListAsync();

            var catalogByISBN = allCatalogs
                .Where(c => !string.IsNullOrWhiteSpace(c.ISBN))
                .GroupBy(c => c.ISBN!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var catalogByTitle = allCatalogs
                .Where(c => !string.IsNullOrWhiteSpace(c.Title))
                .GroupBy(c => c.Title!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int created = 0, updated = 0, booksAdded = 0, booksUpdated = 0;

            var grouped = rows
                .GroupBy(r => (r.ISBN?.Trim().ToLowerInvariant() ?? "") + "|" + (r.Title?.Trim().ToLowerInvariant() ?? ""));

            int operationCount = 0;
            const int batchSize = 500;

            foreach (var group in grouped)
            {
                var first = group.First();

                Catalog? catalog = null;
                if (!string.IsNullOrWhiteSpace(first.ISBN) && catalogByISBN.TryGetValue(first.ISBN, out var byISBN))
                    catalog = byISBN;
                else if (!string.IsNullOrWhiteSpace(first.Title) && catalogByTitle.TryGetValue(first.Title, out var byTitle))
                    catalog = byTitle;

                if (catalog == null)
                {
                    if (string.IsNullOrWhiteSpace(first.Title)) continue;

                    catalog = new Catalog
                    {
                        CatalogId = Guid.NewGuid(),
                        Title = first.Title!,
                        Author = first.Author ?? "",
                        ISBN = first.ISBN ?? "",
                        Faculty = first.Faculty ?? "",
                        Category = first.Category ?? "",
                        TotalCopies = 0,
                        AvailableCopies = 0,
                    };
                    _context.Catalogs.Add(catalog);

                    if (!string.IsNullOrWhiteSpace(catalog.ISBN))
                        catalogByISBN[catalog.ISBN] = catalog;
                    catalogByTitle[catalog.Title] = catalog;
                    created++;
                }
                else
                {
                    if (upd("title") && !string.IsNullOrWhiteSpace(first.Title)) catalog.Title = first.Title!;
                    if (upd("author") && !string.IsNullOrWhiteSpace(first.Author)) catalog.Author = first.Author!;
                    if (upd("isbn") && !string.IsNullOrWhiteSpace(first.ISBN)) catalog.ISBN = first.ISBN!;
                    if (upd("faculty") && !string.IsNullOrWhiteSpace(first.Faculty)) catalog.Faculty = first.Faculty!;
                    if (upd("category") && !string.IsNullOrWhiteSpace(first.Category)) catalog.Category = first.Category!;
                    updated++;
                }

                foreach (var row in group)
                {
                    if (string.IsNullOrWhiteSpace(row.Barcode)) continue;

                    var existingBook = catalog.Books?
                        .FirstOrDefault(b => b.Barcode.Trim().Equals(row.Barcode, StringComparison.OrdinalIgnoreCase));

                    if (existingBook == null)
                    {
                        var newBook = new Book
                        {
                            CatalogId = catalog.CatalogId,
                            Barcode = row.Barcode,
                            Status = row.Status ?? "Available",
                            Location = string.IsNullOrWhiteSpace(row.Location) ? null : row.Location,
                            AcquisitionDate = DateTime.TryParse(row.AcquisitionDate, out var ad) ? ad : DateTime.Now.Date,
                            Created = DateTime.UtcNow,
                            Modified = DateTime.UtcNow,
                        };
                        _context.Books.Add(newBook);
                        booksAdded++;
                    }
                    else
                    {
                        if (upd("status") && !string.IsNullOrWhiteSpace(row.Status))
                            existingBook.Status = row.Status!;
                        if (upd("location"))
                            existingBook.Location = string.IsNullOrWhiteSpace(row.Location) ? null : row.Location;
                        if (upd("acquisitiondate") && DateTime.TryParse(row.AcquisitionDate, out var ud))
                            existingBook.AcquisitionDate = ud;
                        existingBook.Modified = DateTime.UtcNow;
                        booksUpdated++;
                    }

                    operationCount++;
                    if (operationCount % batchSize == 0)
                        await _context.SaveChangesAsync();
                }
            }

            try
            {
                await _context.SaveChangesAsync();

                var touchedIds = rows
                    .Where(r => r.MatchedCatalogId != null)
                    .Select(r => Guid.Parse(r.MatchedCatalogId!))
                    .Distinct()
                    .ToList();

                var touchedCatalogs = await _context.Catalogs
                    .Include(c => c.Books)
                    .Where(c => touchedIds.Contains(c.CatalogId))
                    .ToListAsync();

                var newlyCreatedCatalogs = await _context.Catalogs
                    .Include(c => c.Books)
                    .OrderByDescending(c => c.CatalogId)
                    .Take(created + 100)
                    .ToListAsync();

                foreach (var c in touchedCatalogs.Concat(newlyCreatedCatalogs).DistinctBy(c => c.CatalogId))
                {
                    c.TotalCopies = c.Books.Count;
                    c.AvailableCopies = c.Books.Count(b =>
                        string.Equals(b.Status, "Available", StringComparison.OrdinalIgnoreCase));
                }

                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("ImportRows");
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Database error: {ex.Message}" });
            }

            return Json(new
            {
                success = true,
                created,
                updated,
                booksAdded,
                booksUpdated,
                message = $"Import complete: {created} catalog(s) created, {updated} updated, {booksAdded} book copies added, {booksUpdated} updated."
            });
        }

        // ---------- Details ----------
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            var catalog = await _context.Catalogs.Include(c => c.Books).AsNoTracking().FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound();
            return View(MapToVM(catalog));
        }

        // ---------- Create GET ----------
        public IActionResult Create() => View(new CatalogFormVM());

        // ---------- Create POST ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CatalogFormVM vm)
        {
            Normalize(vm);
            RecalculateCopiesFromBooks(vm);

            if (vm.TotalCopies < 0) ModelState.AddModelError(nameof(vm.TotalCopies), "Total copies cannot be negative.");
            if (vm.AvailableCopies < 0) ModelState.AddModelError(nameof(vm.AvailableCopies), "Available copies cannot be negative.");
            if (vm.AvailableCopies > vm.TotalCopies) ModelState.AddModelError(nameof(vm.AvailableCopies), "Available copies cannot be greater than total copies.");
            if (!ModelState.IsValid) return View(vm);

            if (vm.ImageFile != null) vm.ImagePath = await SaveFileAsync(vm.ImageFile, "images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" });
            if (vm.PdfFile != null) vm.PdfFilePath = await SaveFileAsync(vm.PdfFile, "pdfs", new[] { ".pdf" });
            if (!ModelState.IsValid) return View(vm);

            if (!string.IsNullOrWhiteSpace(vm.ISBN))
                if (await _context.Catalogs.AsNoTracking().AnyAsync(c => c.ISBN == vm.ISBN))
                    ModelState.AddModelError(nameof(vm.ISBN), "This ISBN already exists in another catalog.");

            var filledRows = vm.Books.Where(Filled).ToList();
            if (filledRows.Any())
            {
                var barcodes = filledRows.Select(b => b.Barcode).ToList();
                var dupLocal = barcodes.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (dupLocal.Any()) ModelState.AddModelError("", "Barcodes must be unique. Duplicate(s): " + string.Join(", ", dupLocal));
                var existing = await _context.Books.AsNoTracking().Where(b => barcodes.Contains(b.Barcode)).Select(b => b.Barcode).Distinct().ToListAsync();
                if (existing.Any()) ModelState.AddModelError("", "These barcodes already exist: " + string.Join(", ", existing));
            }
            if (!ModelState.IsValid) return View(vm);

            var cat = new Catalog
            {
                CatalogId = Guid.NewGuid(),
                Title = vm.Title,
                Author = vm.Author,
                ISBN = vm.ISBN,
                Category = vm.Category,
                Faculty = vm.Faculty,
                TotalCopies = vm.TotalCopies,
                AvailableCopies = vm.AvailableCopies,
                ImagePath = vm.ImagePath,
                PdfFilePath = vm.PdfFilePath
            };
            _context.Catalogs.Add(cat);
            foreach (var r in vm.Books.Where(Filled))
                _context.Books.Add(new Book { CatalogId = cat.CatalogId, Barcode = r.Barcode, Status = r.Status, Location = r.Location, AcquisitionDate = r.AcquisitionDate, Created = DateTime.UtcNow, Modified = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ---------- TrackPdfViewer ----------
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> TrackPdfViewer(Guid id)
        {
            var catalog = await _context.Catalogs.FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound(new { success = false, message = "Catalog not found." });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ua = Request.Headers["User-Agent"].ToString();
            var raw = $"{ip}|{ua}|{id}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var viewerKey = Convert.ToHexString(hash).ToLowerInvariant();

            if (!await _context.CatalogPdfViews.AnyAsync(v => v.CatalogId == id && v.ViewerKey == viewerKey))
            {
                _context.CatalogPdfViews.Add(new CatalogPdfView { CatalogId = id, ViewerKey = viewerKey, ViewedAt = DateTime.UtcNow });
                catalog.PdfViewerCount++;
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true, pdfViewerCount = catalog.PdfViewerCount });
        }

        // ---------- Edit GET ----------
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var catalog = await _context.Catalogs.Include(c => c.Books).FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound();
            return View(MapToVM(catalog));
        }

        // ---------- Edit POST ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, CatalogFormVM vm)
        {
            if (id != vm.CatalogId) return NotFound();
            Normalize(vm);
            RecalculateCopiesFromBooks(vm);

            if (vm.TotalCopies < 0) ModelState.AddModelError(nameof(vm.TotalCopies), "Total copies cannot be negative.");
            if (vm.AvailableCopies < 0) ModelState.AddModelError(nameof(vm.AvailableCopies), "Available copies cannot be negative.");
            if (vm.AvailableCopies > vm.TotalCopies) ModelState.AddModelError(nameof(vm.AvailableCopies), "Available copies cannot be greater than total copies.");
            if (!ModelState.IsValid) return View(vm);

            var catalog = await _context.Catalogs.Include(c => c.Books).FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(vm.ISBN))
                if (await _context.Catalogs.AsNoTracking().AnyAsync(c => c.CatalogId != id && c.ISBN == vm.ISBN))
                    ModelState.AddModelError(nameof(vm.ISBN), "This ISBN is already used by another catalog.");

            var posted = vm.Books.Where(Filled).ToList();
            if (posted.Any())
            {
                var barcodes = posted.Select(b => b.Barcode).ToList();
                var dupLocal = barcodes.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (dupLocal.Any()) ModelState.AddModelError("", "Barcodes must be unique. Duplicate(s): " + string.Join(", ", dupLocal));
                var existing = await _context.Books.AsNoTracking().Where(b => barcodes.Contains(b.Barcode)).Select(b => new { b.BookId, b.Barcode }).ToListAsync();
                var dupDb = existing.Where(e => posted.Any(p => p.Barcode == e.Barcode && p.BookId != e.BookId)).Select(e => e.Barcode).Distinct().ToList();
                if (dupDb.Any()) ModelState.AddModelError("", "These barcodes already exist in another copy: " + string.Join(", ", dupDb));
            }
            if (!ModelState.IsValid) return View(vm);

            if (vm.ImageFile != null) { var p = await SaveFileAsync(vm.ImageFile, "images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }); if (!string.IsNullOrWhiteSpace(p)) catalog.ImagePath = p; }
            if (vm.PdfFile != null) { var p = await SaveFileAsync(vm.PdfFile, "pdfs", new[] { ".pdf" }); if (!string.IsNullOrWhiteSpace(p)) catalog.PdfFilePath = p; }
            if (!ModelState.IsValid) return View(MapToVM(catalog));

            catalog.Title = vm.Title;
            catalog.Author = vm.Author;
            catalog.ISBN = vm.ISBN;
            catalog.Category = vm.Category;
            catalog.Faculty = vm.Faculty;
            catalog.TotalCopies = vm.TotalCopies;
            catalog.AvailableCopies = vm.AvailableCopies;

            var pFilled = vm.Books.Where(Filled).ToList();
            var dbBooks = catalog.Books.ToList();

            foreach (var row in pFilled.Where(p => p.BookId > 0))
            {
                var db = dbBooks.FirstOrDefault(b => b.BookId == row.BookId);
                if (db != null) { db.Barcode = row.Barcode; db.Status = row.Status; db.Location = row.Location; db.AcquisitionDate = row.AcquisitionDate; db.Modified = DateTime.UtcNow; }
            }
            foreach (var row in pFilled.Where(p => p.BookId == 0))
                _context.Books.Add(new Book { CatalogId = catalog.CatalogId, Barcode = row.Barcode, Status = row.Status, Location = row.Location, AcquisitionDate = row.AcquisitionDate, Created = DateTime.UtcNow, Modified = DateTime.UtcNow });

            var pIds = pFilled.Where(p => p.BookId > 0).Select(p => p.BookId).ToHashSet();
            var toRemove = dbBooks.Where(b => !pIds.Contains(b.BookId)).ToList();
            if (toRemove.Any()) _context.Books.RemoveRange(toRemove);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ---------- API: List ----------
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> IndexJson()
        {
            var catalogs = await _context.Catalogs.AsNoTracking().OrderBy(c => c.Title)
                .Select(c => new
                {
                    catalogId = c.CatalogId,
                    title = c.Title,
                    author = c.Author,
                    isbn = c.ISBN,
                    category = c.Category,
                    faculty = c.Faculty,
                    totalCopies = c.TotalCopies,
                    availableCopies = c.AvailableCopies,
                    borrowCount = c.BorrowCount,
                    inLibraryCount = c.InLibraryCount,
                    imagePath = c.ImagePath,
                    hasPdf = !string.IsNullOrEmpty(c.PdfFilePath),
                    pdfViewerCount = c.PdfViewerCount
                }).ToListAsync();
            return Json(catalogs);
        }

        // ---------- API: Single ----------
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> DetailsJson(Guid id)
        {
            var c = await _context.Catalogs.Include(c => c.Books).AsNoTracking().FirstOrDefaultAsync(cat => cat.CatalogId == id);
            if (c == null) return NotFound(new { success = false, message = "Catalog not found." });
            return Json(new
            {
                catalogId = c.CatalogId,
                title = c.Title,
                author = c.Author,
                isbn = c.ISBN,
                category = c.Category,
                faculty = c.Faculty,
                totalCopies = c.TotalCopies,
                availableCopies = c.AvailableCopies,
                borrowCount = c.BorrowCount,
                inLibraryCount = c.InLibraryCount,
                imagePath = c.ImagePath,
                pdfFilePath = c.PdfFilePath,
                pdfViewerCount = c.PdfViewerCount,
                hasPdf = !string.IsNullOrEmpty(c.PdfFilePath),
                books = c.Books.Select(b => new { b.BookId, b.Barcode, b.Status, b.Location, b.AcquisitionDate })
            });
        }

        // ---------- Delete ----------
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var catalog = await _context.Catalogs.Include(c => c.Books).AsNoTracking().FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog == null) return NotFound();
            return View(MapToVM(catalog));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var catalog = await _context.Catalogs.Include(c => c.Books).FirstOrDefaultAsync(c => c.CatalogId == id);
            if (catalog != null)
            {
                if (catalog.Books.Any()) _context.Books.RemoveRange(catalog.Books);
                _context.Catalogs.Remove(catalog);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ---------- Download Template ----------
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var path = Path.Combine(_env.WebRootPath, "templates", "CatalogImportTemplate.xlsx");
            if (!System.IO.File.Exists(path))
                return NotFound("Template file not found.");
            var bytes = System.IO.File.ReadAllBytes(path);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "CatalogImportTemplate.xlsx");
        }
    }
}