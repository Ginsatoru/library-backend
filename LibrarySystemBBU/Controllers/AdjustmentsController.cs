using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystemBBU.Controllers
{
    // ---------- Inline ViewModels ----------
    public class AdjustmentDetailRowVM
    {
        public int? AdjustmentDetailId { get; set; }

        public Guid CatalogId { get; set; }

        [System.ComponentModel.DataAnnotations.Range(-1_000_000, 1_000_000)]
        public int QuantityChanged { get; set; }

        // Only required for Damage/Lost
        public int? BookId { get; set; }

        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? Note { get; set; }
    }

    public class AdjustmentWithDetailsVM
    {
        public Guid? AdjustmentId { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        public Guid CatalogId { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string AdjustmentType { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        public int QuantityChange { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Date)]
        public DateTime AdjustmentDate { get; set; } = DateTime.Today;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        public Guid? AdjustedByUserId { get; set; }

        [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "Add at least one detail.")]
        public List<AdjustmentDetailRowVM> Details { get; set; } = new();

        // Dropdowns
        public SelectList? Catalogs { get; set; }
        public SelectList? Users { get; set; }
        public SelectList? AdjustmentTypes { get; set; }

        // For showing book dropdown in Damage/Lost
        public List<SelectListItem>? Books { get; set; }
    }

    public class AdjustmentsController : Controller
    {
        private readonly DataContext _context;

        public AdjustmentsController(DataContext context)
        {
            _context = context;
        }

        // ---------- Dropdown helpers ----------
        private SelectList Catalogs(Guid? selected = null)
        {
            var raw = _context.Catalogs
                .AsNoTracking()
                .Select(c => new { c.CatalogId, c.Title, c.ISBN })
                .OrderBy(c => c.Title)
                .ThenBy(c => c.ISBN)
                .ToList();

            var items = raw.Select(c => new
            {
                c.CatalogId,
                Display = string.IsNullOrWhiteSpace(c.ISBN)
                    ? c.Title
                    : (c.Title + " (" + c.ISBN + ")")
            }).ToList();

            return new SelectList(items, "CatalogId", "Display", selected);
        }

        private List<SelectListItem> BooksAll()
        {
            var list = _context.Books
                .AsNoTracking()
                .Include(b => b.Catalog)
                .OrderBy(b => b.Catalog.Title)
                .ThenBy(b => b.Barcode)
                .Select(b => new SelectListItem
                {
                    Value = b.BookId.ToString(),
                    Text = $"{(string.IsNullOrWhiteSpace(b.Catalog.Title) ? "Book" : b.Catalog.Title)} — {b.Barcode}"
                })
                .ToList();

            return list;
        }

        private SelectList Users(Guid? selected = null) =>
            new SelectList(_context.Users.AsNoTracking().OrderBy(u => u.UserName), "Id", "UserName", selected);

        private SelectList AdjustmentTypes(string? selected = null) =>
            new SelectList(new[] { "Damage", "Lost" }, selected);

        private void FillLists(AdjustmentWithDetailsVM vm)
        {
            vm.Catalogs = Catalogs(vm.CatalogId);
            vm.Users = Users(vm.AdjustedByUserId);
            vm.AdjustmentTypes = AdjustmentTypes(vm.AdjustmentType);
            vm.Books = BooksAll();
        }

        private AdjustmentWithDetailsVM ToVM(Adjustment a)
        {
            return new AdjustmentWithDetailsVM
            {
                AdjustmentId = a.AdjustmentId,
                CatalogId = a.CatalogId,
                AdjustmentType = a.AdjustmentType,
                QuantityChange = a.QuantityChange,
                AdjustmentDate = a.AdjustmentDate,
                Reason = a.Reason,
                AdjustedByUserId = a.AdjustedByUserId,
                Details = a.AdjustmentDetails
                    .OrderBy(d => d.AdjustmentDetailId)
                    .Select(d => new AdjustmentDetailRowVM
                    {
                        AdjustmentDetailId = d.AdjustmentDetailId,
                        CatalogId = d.CatalogId,
                        QuantityChanged = d.QuantityChanged,
                        BookId = d.BookId,
                        Note = d.Note
                    }).ToList()
            };
        }

        private bool AdjustmentExists(Guid id) =>
            _context.Adjustments.Any(e => e.AdjustmentId == id);

        // ---------- Core rules: auto-sign + auto-total ----------
        private static readonly HashSet<string> NegativeTypes = new(new[] { "Decrease", "Damage", "Lost" });

        private void NormalizeSigns(AdjustmentWithDetailsVM vm)
        {
            if (vm.Details == null) return;

            bool isDamageOrLost =
                string.Equals(vm.AdjustmentType, "Damage", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vm.AdjustmentType, "Lost", StringComparison.OrdinalIgnoreCase);

            foreach (var d in vm.Details)
            {
                if (d == null) continue;

                if (isDamageOrLost)
                {
                    d.QuantityChanged = -1;
                    continue;
                }

                if (NegativeTypes.Contains(vm.AdjustmentType))
                    d.QuantityChanged = -Math.Abs(d.QuantityChanged);
                else if (vm.AdjustmentType == "Increase")
                    d.QuantityChanged = Math.Abs(d.QuantityChanged);
            }
        }

        private void ValidateAndCompute(AdjustmentWithDetailsVM vm)
        {
            bool isDamageOrLost =
                string.Equals(vm.AdjustmentType, "Damage", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vm.AdjustmentType, "Lost", StringComparison.OrdinalIgnoreCase);

            if (isDamageOrLost)
            {
                vm.Details = vm.Details?
                    .Where(d => d != null && d.BookId.HasValue && d.BookId.Value > 0)
                    .ToList() ?? new();

                if (!vm.Details.Any())
                    ModelState.AddModelError("Details", "Add at least one book for Damage/Lost.");

                foreach (var d in vm.Details)
                {
                    if (!d.BookId.HasValue || d.BookId.Value <= 0)
                        ModelState.AddModelError("Details", "Each Damage/Lost row must have a book selected.");
                }
            }
            else
            {
                vm.Details = vm.Details?
                    .Where(d => d != null)
                    .ToList() ?? new();

                if (!vm.Details.Any())
                    ModelState.AddModelError("Details", "Add at least one detail row.");
            }

            NormalizeSigns(vm);
            vm.QuantityChange = vm.Details.Sum(d => d.QuantityChanged);

            switch (vm.AdjustmentType)
            {
                case "Increase":
                    if (vm.QuantityChange < 0)
                        ModelState.AddModelError(nameof(vm.QuantityChange),
                            "Increase requires a non-negative total.");
                    break;
                case "Decrease":
                case "Damage":
                case "Lost":
                    if (vm.QuantityChange > 0)
                        ModelState.AddModelError(nameof(vm.QuantityChange),
                            $"{vm.AdjustmentType} requires a non-positive total.");
                    break;
                case "Correction":
                    break;
                default:
                    ModelState.AddModelError(nameof(vm.AdjustmentType), "Unknown adjustment type.");
                    break;
            }
        }

        // ---------- KEY FIX: Recalculate catalog counts from actual Books ----------
        // Called after ANY operation that changes Book.Status or Book count,
        // so TotalCopies and AvailableCopies always reflect ground truth.
        private async Task RecalculateCatalogCountsAsync(IEnumerable<Guid> catalogIds)
        {
            var ids = (catalogIds ?? Enumerable.Empty<Guid>()).Distinct().ToList();
            if (!ids.Any()) return;

            var catalogs = await _context.Catalogs
                .Include(c => c.Books)
                .Where(c => ids.Contains(c.CatalogId))
                .ToListAsync();

            foreach (var c in catalogs)
            {
                c.TotalCopies = c.Books.Count;
                c.AvailableCopies = c.Books.Count(b =>
                    string.Equals(b.Status, "Available", StringComparison.OrdinalIgnoreCase));
            }
        }

        // ---------- APPLY IMPACT TO CATALOG + BOOKS ----------
        private async Task ApplyImpactOnCreateAsync(AdjustmentWithDetailsVM vm)
        {
            bool isDamageOrLost =
                string.Equals(vm.AdjustmentType, "Damage", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vm.AdjustmentType, "Lost", StringComparison.OrdinalIgnoreCase);

            if (vm.Details == null || !vm.Details.Any())
                return;

            if (!isDamageOrLost)
            {
                // --------- Catalog-level change (Increase/Decrease/Correction) ----------
                var catalog = await _context.Catalogs
                    .Include(c => c.Books)
                    .FirstOrDefaultAsync(c => c.CatalogId == vm.CatalogId);

                if (catalog == null) return;

                int delta = vm.Details.Sum(d => d.QuantityChanged);

                // For these types, TotalCopies changes (actual physical copies added/removed)
                catalog.TotalCopies += delta;
                if (catalog.TotalCopies < 0) catalog.TotalCopies = 0;

                // Fill detail CatalogId
                foreach (var d in vm.Details)
                    d.CatalogId = vm.CatalogId;

                // Recalculate AvailableCopies from actual book records
                await RecalculateCatalogCountsAsync(new[] { vm.CatalogId });
            }
            else
            {
                // --------- Damage / Lost: mark specific books, then recalculate ----------
                var bookIds = vm.Details
                    .Where(d => d.BookId.HasValue && d.BookId.Value > 0)
                    .Select(d => d.BookId!.Value)
                    .Distinct()
                    .ToList();

                if (!bookIds.Any()) return;

                var books = await _context.Books
                    .Include(b => b.Catalog)
                    .Where(b => bookIds.Contains(b.BookId))
                    .ToListAsync();

                var affectedCatalogIds = new HashSet<Guid>();

                foreach (var row in vm.Details)
                {
                    if (!row.BookId.HasValue || row.BookId.Value <= 0) continue;

                    var book = books.FirstOrDefault(b => b.BookId == row.BookId.Value);
                    if (book == null || book.Catalog == null) continue;

                    row.CatalogId = book.Catalog.CatalogId;
                    affectedCatalogIds.Add(book.Catalog.CatalogId);

                    // KEY FIX: Set the book status to "Lost" or "Damage" regardless of current status.
                    // This makes the book immediately unavailable for borrowing.
                    // Only change if not already in that state to avoid double-counting.
                    if (!string.Equals(book.Status, vm.AdjustmentType, StringComparison.OrdinalIgnoreCase))
                    {
                        book.Status = vm.AdjustmentType; // "Damage" or "Lost"
                        book.Modified = DateTime.UtcNow;
                    }
                }

                // KEY FIX: Recalculate TotalCopies + AvailableCopies from actual book statuses.
                // TotalCopies = all books for that catalog (Lost/Damage books still physically exist).
                // AvailableCopies = only Available books.
                await RecalculateCatalogCountsAsync(affectedCatalogIds);
            }
        }

        // ---------- UNDO: Reverse Damage/Lost on delete ----------
        private async Task UndoDamageOrLostOnDeleteAsync(Adjustment adjustment)
        {
            if (adjustment.AdjustmentType != "Damage" && adjustment.AdjustmentType != "Lost")
                return;

            var detailBookIds = adjustment.AdjustmentDetails
                .Where(d => d.BookId.HasValue && d.BookId.Value > 0)
                .Select(d => d.BookId!.Value)
                .Distinct()
                .ToList();

            if (!detailBookIds.Any()) return;

            var books = await _context.Books
                .Include(b => b.Catalog)
                .Where(b => detailBookIds.Contains(b.BookId))
                .ToListAsync();

            var affectedCatalogIds = new HashSet<Guid>();

            foreach (var b in books)
            {
                if (b.Catalog == null) continue;

                affectedCatalogIds.Add(b.CatalogId);

                // Only flip back if still Damage/Lost — don't override if manually changed
                if (string.Equals(b.Status, "Damage", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(b.Status, "Lost", StringComparison.OrdinalIgnoreCase))
                {
                    b.Status = "Available";
                    b.Modified = DateTime.UtcNow;
                }
            }

            // KEY FIX: Recalculate from actual book statuses after restoring them
            await RecalculateCatalogCountsAsync(affectedCatalogIds);
        }

        // ===================== JSON: Books for Catalog =====================
        /// <summary>
        /// Returns only AVAILABLE books for a given catalog (used by Adjustment detail rows).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBooksForCatalog(Guid catalogId)
        {
            var books = await _context.Books
                .AsNoTracking()
                .Where(b => b.CatalogId == catalogId && b.Status == "Available")
                .OrderBy(b => b.Barcode)
                .Select(b => new
                {
                    id = b.BookId,
                    text = b.Barcode
                })
                .ToListAsync();

            return Json(books);
        }

        // ===================== INDEX =====================
        public async Task<IActionResult> Index()
        {
            var list = await _context.Adjustments
                .AsNoTracking()
                .Include(a => a.Catalog)
                .Include(a => a.AdjustedByUser)
                .Include(a => a.AdjustmentDetails)
                .OrderByDescending(a => a.AdjustmentDate)
                .ToListAsync();

            return View(list);
        }

        // ===================== EXPORT =====================
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var list = await _context.Adjustments
                .AsNoTracking()
                .Include(a => a.Catalog)
                .Include(a => a.AdjustedByUser)
                .Include(a => a.AdjustmentDetails)
                .OrderByDescending(a => a.AdjustmentDate)
                .ToListAsync();

            string H(string? v) => WebUtility.HtmlEncode(v ?? "");

            var sb = new StringBuilder();
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<table border='1'>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>Adjustment ID</th>");
            sb.AppendLine("<th>Catalog Title</th>");
            sb.AppendLine("<th>ISBN</th>");
            sb.AppendLine("<th>Adjustment Type</th>");
            sb.AppendLine("<th>Total Qty Change</th>");
            sb.AppendLine("<th>Adjustment Date</th>");
            sb.AppendLine("<th>Reason</th>");
            sb.AppendLine("<th>User</th>");
            sb.AppendLine("<th>Detail Qty Changed</th>");
            sb.AppendLine("<th>Detail Note</th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var a in list)
            {
                var details = a.AdjustmentDetails?
                    .OrderBy(d => d.AdjustmentDetailId)
                    .ToList()
                    ?? new List<AdjustmentDetail>();

                if (details.Any())
                {
                    foreach (var d in details)
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendLine($"<td>{H(a.AdjustmentId.ToString())}</td>");
                        sb.AppendLine($"<td>{H(a.Catalog?.Title)}</td>");
                        sb.AppendLine($"<td>{H(a.Catalog?.ISBN)}</td>");
                        sb.AppendLine($"<td>{H(a.AdjustmentType)}</td>");
                        sb.AppendLine($"<td>{a.QuantityChange}</td>");
                        sb.AppendLine($"<td>{H(a.AdjustmentDate.ToString("yyyy-MM-dd"))}</td>");
                        sb.AppendLine($"<td>{H(a.Reason)}</td>");
                        sb.AppendLine($"<td>{H(a.AdjustedByUser?.UserName)}</td>");
                        sb.AppendLine($"<td>{d.QuantityChanged}</td>");
                        sb.AppendLine($"<td>{H(d.Note)}</td>");
                        sb.AppendLine("</tr>");
                    }
                }
                else
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{H(a.AdjustmentId.ToString())}</td>");
                    sb.AppendLine($"<td>{H(a.Catalog?.Title)}</td>");
                    sb.AppendLine($"<td>{H(a.Catalog?.ISBN)}</td>");
                    sb.AppendLine($"<td>{H(a.AdjustmentType)}</td>");
                    sb.AppendLine($"<td>{a.QuantityChange}</td>");
                    sb.AppendLine($"<td>{H(a.AdjustmentDate.ToString("yyyy-MM-dd"))}</td>");
                    sb.AppendLine($"<td>{H(a.Reason)}</td>");
                    sb.AppendLine($"<td>{H(a.AdjustedByUser?.UserName)}</td>");
                    sb.AppendLine("<td></td>");
                    sb.AppendLine("<td></td>");
                    sb.AppendLine("</tr>");
                }
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var bytes = encoding.GetBytes(sb.ToString());
            var fileName = "Adjustments_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xls";

            return File(bytes, "application/vnd.ms-excel", fileName);
        }

        // ===================== DETAILS =====================
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var adjustment = await _context.Adjustments
                .AsNoTracking()
                .Include(a => a.Catalog)
                .Include(a => a.AdjustedByUser)
                .Include(a => a.AdjustmentDetails).ThenInclude(d => d.Catalog)
                .Include(a => a.AdjustmentDetails).ThenInclude(d => d.Book)
                .FirstOrDefaultAsync(m => m.AdjustmentId == id);

            if (adjustment == null) return NotFound();

            return View(adjustment);
        }

        // ===================== CREATE =====================
        [HttpGet]
        public IActionResult Create()
        {
            var vm = new AdjustmentWithDetailsVM
            {
                AdjustmentDate = DateTime.Today,
                Details = new List<AdjustmentDetailRowVM> { new AdjustmentDetailRowVM() }
            };
            FillLists(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdjustmentWithDetailsVM vm)
        {
            ValidateAndCompute(vm);

            if (!ModelState.IsValid)
            {
                FillLists(vm);
                return View(vm);
            }

            // Apply book status changes + recalculate catalog counts
            await ApplyImpactOnCreateAsync(vm);

            var entity = new Adjustment
            {
                AdjustmentId = Guid.NewGuid(),
                CatalogId = vm.CatalogId,
                AdjustmentType = vm.AdjustmentType,
                QuantityChange = vm.QuantityChange,
                AdjustmentDate = vm.AdjustmentDate,
                Reason = vm.Reason,
                AdjustedByUserId = vm.AdjustedByUserId,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            };

            foreach (var r in vm.Details)
            {
                entity.AdjustmentDetails.Add(new AdjustmentDetail
                {
                    AdjustmentId = entity.AdjustmentId,
                    CatalogId = r.CatalogId,
                    QuantityChanged = r.QuantityChanged,
                    BookId = r.BookId,
                    Note = r.Note
                });
            }

            _context.Adjustments.Add(entity);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { id = entity.AdjustmentId });
        }

        // ===================== EDIT =====================
        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var adjustment = await _context.Adjustments
                .Include(a => a.AdjustmentDetails)
                .FirstOrDefaultAsync(a => a.AdjustmentId == id);

            if (adjustment == null) return NotFound();

            var vm = ToVM(adjustment);
            if (!vm.Details.Any()) vm.Details.Add(new AdjustmentDetailRowVM());
            FillLists(vm);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, AdjustmentWithDetailsVM vm)
        {
            if (id != vm.AdjustmentId) return NotFound();

            ValidateAndCompute(vm);

            if (!ModelState.IsValid)
            {
                FillLists(vm);
                return View(vm);
            }

            var adjustment = await _context.Adjustments
                .Include(a => a.AdjustmentDetails)
                .FirstOrDefaultAsync(a => a.AdjustmentId == id);

            if (adjustment == null) return NotFound();

            adjustment.CatalogId = vm.CatalogId;
            adjustment.AdjustmentType = vm.AdjustmentType;
            adjustment.QuantityChange = vm.QuantityChange;
            adjustment.AdjustmentDate = vm.AdjustmentDate;
            adjustment.Reason = vm.Reason;
            adjustment.AdjustedByUserId = vm.AdjustedByUserId;
            adjustment.Modified = DateTime.UtcNow;

            var postedIds = vm.Details.Where(d => d.AdjustmentDetailId.HasValue)
                                      .Select(d => d.AdjustmentDetailId!.Value)
                                      .ToHashSet();

            foreach (var existing in adjustment.AdjustmentDetails.ToList())
            {
                if (!postedIds.Contains(existing.AdjustmentDetailId))
                    _context.AdjustmentDetails.Remove(existing);
            }

            foreach (var row in vm.Details)
            {
                if (row.AdjustmentDetailId.HasValue)
                {
                    var entity = adjustment.AdjustmentDetails
                        .First(d => d.AdjustmentDetailId == row.AdjustmentDetailId.Value);
                    entity.CatalogId = row.CatalogId;
                    entity.QuantityChanged = row.QuantityChanged;
                    entity.BookId = row.BookId;
                    entity.Note = row.Note;
                }
                else
                {
                    adjustment.AdjustmentDetails.Add(new AdjustmentDetail
                    {
                        AdjustmentId = adjustment.AdjustmentId,
                        CatalogId = row.CatalogId,
                        QuantityChanged = row.QuantityChanged,
                        BookId = row.BookId,
                        Note = row.Note
                    });
                }
            }

            try
            {
                await _context.SaveChangesAsync();

                // KEY FIX: Recalculate catalog counts after edit
                await RecalculateCatalogCountsAsync(new[] { adjustment.CatalogId });
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AdjustmentExists(id)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index), new { id = adjustment.AdjustmentId });
        }

        // ===================== DELETE =====================
        [HttpGet]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var adjustment = await _context.Adjustments
                .AsNoTracking()
                .Include(a => a.Catalog)
                .Include(a => a.AdjustedByUser)
                .Include(a => a.AdjustmentDetails).ThenInclude(d => d.Catalog)
                .Include(a => a.AdjustmentDetails).ThenInclude(d => d.Book)
                .FirstOrDefaultAsync(m => m.AdjustmentId == id);

            if (adjustment == null) return NotFound();

            return View(adjustment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var adjustment = await _context.Adjustments
                .Include(a => a.AdjustmentDetails)
                .FirstOrDefaultAsync(a => a.AdjustmentId == id);

            if (adjustment != null)
            {
                // Collect affected catalog IDs before deletion
                var affectedCatalogIds = adjustment.AdjustmentDetails
                    .Select(d => d.CatalogId)
                    .Distinct()
                    .Append(adjustment.CatalogId)
                    .ToList();

                // Undo Lost/Damage — restore book statuses to Available
                await UndoDamageOrLostOnDeleteAsync(adjustment);

                if (adjustment.AdjustmentDetails.Any())
                    _context.AdjustmentDetails.RemoveRange(adjustment.AdjustmentDetails);

                _context.Adjustments.Remove(adjustment);
                await _context.SaveChangesAsync();

                // KEY FIX: Recalculate catalog counts after books are restored
                await RecalculateCatalogCountsAsync(affectedCatalogIds);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}