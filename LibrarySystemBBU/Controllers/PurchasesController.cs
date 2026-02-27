using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;      
using System.Text;    
using System.Threading.Tasks;

namespace LibrarySystemBBU.Controllers
{
    public class PurchasesController : Controller
    {
        private readonly DataContext _context;

        public PurchasesController(DataContext context)
        {
            _context = context;
        }

        private SelectList BuildBookSelectList(int? selectedBookId = null)
        {
            // 1) Pull raw fields (project nav props explicitly; no Include needed for projection)
            var raw = _context.Books
                .AsNoTracking()
                .Select(b => new
                {
                    b.BookId,
                    b.Title,
                    b.Barcode,
                    CatalogTitle = b.Catalog != null ? b.Catalog.Title : null,
                    CatalogIsbn = b.Catalog != null ? b.Catalog.ISBN : null
                })
                .ToList(); 

            // 2) Compose friendly text in C#
            var list = raw
                .Select(b =>
                {
                    string baseTitle =
                        !string.IsNullOrWhiteSpace(b.Title)
                            ? b.Title
                            : (!string.IsNullOrWhiteSpace(b.CatalogTitle)
                                ? b.CatalogTitle!
                                : $"Book #{b.BookId}");

                    // Append ISBN if present
                    string text = !string.IsNullOrWhiteSpace(b.CatalogIsbn)
                        ? $"{baseTitle} — {b.CatalogIsbn}"
                        : baseTitle;

                    // Optional: if we had to fall back hard, show barcode for disambiguation
                    if (text.StartsWith("Book #", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(b.Barcode))
                        text = $"{text} — {b.Barcode}";

                    return new { b.BookId, Text = text };
                })
                .OrderBy(x => x.Text)
                .ToList();

            return new SelectList(list, "BookId", "Text", selectedBookId);
        }

        // Kept for compatibility; just delegates to the new builder.
        private void PopulateBookSelectList(int? selectedBookId = null)
        {
            ViewData["BookId"] = BuildBookSelectList(selectedBookId);
        }

        private static void RecalculateFromDetails(Purchase purchase)
        {
            purchase.PurchaseDetails ??= new List<PurchaseDetail>();

            foreach (var d in purchase.PurchaseDetails)
            {
                d.LineTotal = Math.Round(d.UnitPrice * d.Quantity, 2, MidpointRounding.AwayFromZero);
            }

            purchase.Cost = purchase.PurchaseDetails.Sum(d => d.LineTotal);
            purchase.Quantity = purchase.PurchaseDetails.Sum(d => d.Quantity);
        }

        private static void SatisfyHeaderRequirementsFromDetails(Purchase purchase)
        {
            // pick first detail's BookId so Purchase.BookId [Required] passes
            if (purchase.PurchaseDetails.Any())
            {
                purchase.BookId = purchase.PurchaseDetails.First().BookId;
            }
        }

        private static void ClearHeaderValidation(ModelStateDictionary modelState)
        {
            // these are recomputed server-side, so remove their posted-state validation
            modelState.Remove(nameof(Purchase.BookId));
            modelState.Remove(nameof(Purchase.Quantity));
            modelState.Remove(nameof(Purchase.Cost));
        }

        // --------------------------
        // Index
        // --------------------------
        public async Task<IActionResult> Index()
        {
            var data = await _context.Purchases
                .AsNoTracking()
                .Include(p => p.Book).ThenInclude(b => b.Catalog)  
                .Include(p => p.PurchaseDetails)
                .OrderByDescending(p => p.PurchaseDate)
                .ThenByDescending(p => p.Created)
                .ToListAsync();

            return View(data);
        }

        // --------------------------
        // Export (Excel .xls via HTML table)
        // --------------------------
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var data = await _context.Purchases
                .AsNoTracking()
                .Include(p => p.Book).ThenInclude(b => b.Catalog)
                .Include(p => p.PurchaseDetails)
                .OrderByDescending(p => p.PurchaseDate)
                .ThenByDescending(p => p.Created)
                .ToListAsync();

            string H(string? v) => WebUtility.HtmlEncode(v ?? string.Empty);

            string BookLabel(Book? b)
            {
                if (b == null) return string.Empty;

                var baseTitle = !string.IsNullOrWhiteSpace(b.Title)
                    ? b.Title
                    : (!string.IsNullOrWhiteSpace(b.Catalog?.Title)
                        ? b.Catalog!.Title
                        : $"Book #{b.BookId}");

                var text = !string.IsNullOrWhiteSpace(b.Catalog?.ISBN)
                    ? $"{baseTitle} — {b.Catalog!.ISBN}"
                    : baseTitle;

                if (text.StartsWith("Book #") && !string.IsNullOrWhiteSpace(b.Barcode))
                    text = $"{text} — {b.Barcode}";

                return text;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<table border='1'>");

            // Header row (you can add Khmer here if you want bilingual)
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>Purchase Date</th>");
            sb.AppendLine("<th>Book</th>");
            sb.AppendLine("<th>Supplier</th>");
            sb.AppendLine("<th>Quantity</th>");
            sb.AppendLine("<th>Cost</th>");
            sb.AppendLine("<th>Notes</th>");
            sb.AppendLine("</tr></thead>");

            sb.AppendLine("<tbody>");

            foreach (var p in data)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{H(p.PurchaseDate.ToString("yyyy-MM-dd"))}</td>");
                sb.AppendLine($"<td>{H(BookLabel(p.Book))}</td>");
                sb.AppendLine($"<td>{H(p.Supplier)}</td>");
                sb.AppendLine($"<td>{p.Quantity}</td>");
                sb.AppendLine($"<td>{p.Cost.ToString("0.00")}</td>");
                sb.AppendLine($"<td>{H(p.Notes)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); 
            var bytes = encoding.GetBytes(sb.ToString());
            var fileName = "Purchases_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xls";

            return File(bytes, "application/vnd.ms-excel", fileName);
        }

        // --------------------------
        // Details
        // --------------------------
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var purchase = await _context.Purchases
                .Include(p => p.Book).ThenInclude(b => b.Catalog)
                .Include(p => p.PurchaseDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(b => b.Catalog)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase == null) return NotFound();

            return View(purchase);
        }

        // --------------------------
        // Create (GET)
        // --------------------------
        public IActionResult Create()
        {
            ViewData["BookId"] = BuildBookSelectList();

            return View(new Purchase
            {
                PurchaseDate = DateTime.Now.Date,
                PurchaseDetails = new List<PurchaseDetail>()
            });
        }

        // --------------------------
        // Create (POST)
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BookId,Quantity,PurchaseDate,Supplier,Cost,Notes,PurchaseDetails")] Purchase purchase)
        {
            purchase.PurchaseDetails ??= new List<PurchaseDetail>();

            if (!purchase.PurchaseDetails.Any())
                ModelState.AddModelError(string.Empty, "Please add at least one line item.");

            foreach (var d in purchase.PurchaseDetails)
                d.LineTotal = Math.Round(d.UnitPrice * d.Quantity, 2, MidpointRounding.AwayFromZero);

            purchase.Cost = purchase.PurchaseDetails.Sum(d => d.LineTotal);
            purchase.Quantity = purchase.PurchaseDetails.Sum(d => d.Quantity);

            if (purchase.PurchaseDetails.Any())
                purchase.BookId = purchase.PurchaseDetails.First().BookId;

            ModelState.Remove(nameof(Purchase.BookId));
            ModelState.Remove(nameof(Purchase.Quantity));
            ModelState.Remove(nameof(Purchase.Cost));

            if (!ModelState.IsValid)
            {
                ViewData["BookId"] = BuildBookSelectList(purchase.BookId);
                return View(purchase);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                purchase.PurchaseId = Guid.NewGuid();
                purchase.Created = DateTime.UtcNow;
                purchase.Modified = DateTime.UtcNow;

                foreach (var d in purchase.PurchaseDetails)
                {
                    d.PurchaseId = purchase.PurchaseId;
                    d.Created = DateTime.UtcNow;
                    d.Modified = DateTime.UtcNow;
                }

                _context.Purchases.Add(purchase);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Could not save the purchase. Please try again.");
                ViewData["BookId"] = BuildBookSelectList(purchase.BookId);
                return View(purchase);
            }
        }

        // --------------------------
        // Edit (GET)
        // --------------------------
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var purchase = await _context.Purchases
                .Include(p => p.PurchaseDetails)
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase == null) return NotFound();

            ViewData["BookId"] = BuildBookSelectList(purchase.BookId);
            return View(purchase);
        }

        // --------------------------
        // Edit (POST)
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("PurchaseId,BookId,Quantity,PurchaseDate,Supplier,Cost,Notes,PurchaseDetails")] Purchase incoming)
        {
            if (id != incoming.PurchaseId) return NotFound();
            incoming.PurchaseDetails ??= new List<PurchaseDetail>();

            if (!incoming.PurchaseDetails.Any())
            {
                ModelState.AddModelError(string.Empty, "Please add at least one line item.");
            }

            RecalculateFromDetails(incoming);
            SatisfyHeaderRequirementsFromDetails(incoming);
            ClearHeaderValidation(ModelState);

            if (!ModelState.IsValid)
            {
                ViewData["BookId"] = BuildBookSelectList(incoming.BookId);
                return View(incoming);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var dbPurchase = await _context.Purchases
                    .Include(p => p.PurchaseDetails)
                    .FirstOrDefaultAsync(p => p.PurchaseId == id);

                if (dbPurchase == null) return NotFound();

                // Update header
                dbPurchase.BookId = incoming.BookId;
                dbPurchase.PurchaseDate = incoming.PurchaseDate;
                dbPurchase.Supplier = incoming.Supplier;
                dbPurchase.Notes = incoming.Notes;
                dbPurchase.Cost = incoming.Cost;          
                dbPurchase.Quantity = incoming.Quantity; 
                dbPurchase.Modified = DateTime.UtcNow;

                // Replace all details (simple strategy)
                _context.PurchaseDetails.RemoveRange(dbPurchase.PurchaseDetails);

                foreach (var d in incoming.PurchaseDetails)
                {
                    _context.PurchaseDetails.Add(new PurchaseDetail
                    {
                        PurchaseId = dbPurchase.PurchaseId,
                        BookId = d.BookId,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        LineTotal = Math.Round(d.UnitPrice * d.Quantity, 2, MidpointRounding.AwayFromZero),
                        Created = DateTime.UtcNow,
                        Modified = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                if (!_context.Purchases.Any(e => e.PurchaseId == incoming.PurchaseId)) return NotFound();
                throw;
            }
            catch
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Could not update the purchase. Please try again.");
                ViewData["BookId"] = BuildBookSelectList(incoming.BookId);
                return View(incoming);
            }
        }

        // --------------------------
        // Delete (GET/POST)
        // --------------------------
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var purchase = await _context.Purchases
                .Include(p => p.Book)
                .Include(p => p.PurchaseDetails).ThenInclude(d => d.Book)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase == null) return NotFound();

            return View(purchase);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var purchase = await _context.Purchases
                .Include(p => p.PurchaseDetails)
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase != null)
            {
                _context.PurchaseDetails.RemoveRange(purchase.PurchaseDetails);
                _context.Purchases.Remove(purchase);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
