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

        // --------------------------
        // AJAX book search for Select2
        // --------------------------
        [HttpGet]
        public async Task<IActionResult> SearchBooks(string term, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Books
                .AsNoTracking()
                .Include(b => b.Catalog)
                .Where(b =>
                    string.IsNullOrEmpty(term) ||
                    (b.Title != null && b.Title.Contains(term)) ||
                    (b.Catalog != null && b.Catalog.Title != null && b.Catalog.Title.Contains(term)) ||
                    (b.Catalog != null && b.Catalog.ISBN != null && b.Catalog.ISBN.Contains(term)) ||
                    (b.Barcode != null && b.Barcode.Contains(term)));

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(b => b.Title ?? b.Catalog.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    id = b.BookId,
                    text = (b.Title ?? b.Catalog.Title ?? "Book #" + b.BookId)
                           + (b.Catalog.ISBN != null ? " — " + b.Catalog.ISBN : "")
                })
                .ToListAsync();

            return Json(new
            {
                results = items,
                pagination = new { more = (page * pageSize) < total }
            });
        }

        // --------------------------
        // Helpers
        // --------------------------
        private static void RecalculateFromDetails(Purchase purchase)
        {
            purchase.PurchaseDetails ??= new List<PurchaseDetail>();

            foreach (var d in purchase.PurchaseDetails)
                d.LineTotal = Math.Round(d.UnitPrice * d.Quantity, 2, MidpointRounding.AwayFromZero);

            purchase.Cost = purchase.PurchaseDetails.Sum(d => d.LineTotal);
            purchase.Quantity = purchase.PurchaseDetails.Sum(d => d.Quantity);
        }

        private static void SatisfyHeaderRequirementsFromDetails(Purchase purchase)
        {
            if (purchase.PurchaseDetails.Any())
                purchase.BookId = purchase.PurchaseDetails.First().BookId;
        }

        private static void ClearHeaderValidation(ModelStateDictionary modelState)
        {
            modelState.Remove(nameof(Purchase.BookId));
            modelState.Remove(nameof(Purchase.Quantity));
            modelState.Remove(nameof(Purchase.Cost));
        }

        private string BookLabel(Book? b)
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
        // Export
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

            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" /></head><body>");
            sb.AppendLine("<table border='1'><thead><tr>");
            sb.AppendLine("<th>Purchase Date</th><th>Book</th><th>Supplier</th><th>Quantity</th><th>Cost</th><th>Notes</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var p in data)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{H(p.PurchaseDate.ToString("yyyy-MM-dd"))}</td>");
                sb.AppendLine($"<td>{H(BookLabel(p.Book))}</td>");
                sb.AppendLine($"<td>{H(p.Supplier)}</td>");
                sb.AppendLine($"<td>{p.Quantity}</td>");
                sb.AppendLine($"<td>{p.Cost:0.00}</td>");
                sb.AppendLine($"<td>{H(p.Notes)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");

            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
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
                    .ThenInclude(d => d.Book).ThenInclude(b => b.Catalog)
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
        public async Task<IActionResult> Create(
            [Bind("BookId,Quantity,PurchaseDate,Supplier,Cost,Notes,PurchaseDetails")] Purchase purchase)
        {
            purchase.PurchaseDetails ??= new List<PurchaseDetail>();

            if (!purchase.PurchaseDetails.Any())
                ModelState.AddModelError(string.Empty, "Please add at least one line item.");

            RecalculateFromDetails(purchase);
            SatisfyHeaderRequirementsFromDetails(purchase);
            ClearHeaderValidation(ModelState);

            if (!ModelState.IsValid)
                return View(purchase);

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
                    .ThenInclude(d => d.Book).ThenInclude(b => b.Catalog)
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase == null) return NotFound();

            return View(purchase);
        }

        // --------------------------
        // Edit (POST)
        // --------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            Guid id,
            [Bind("PurchaseId,BookId,Quantity,PurchaseDate,Supplier,Cost,Notes,PurchaseDetails")] Purchase incoming)
        {
            if (id != incoming.PurchaseId) return NotFound();

            incoming.PurchaseDetails ??= new List<PurchaseDetail>();

            if (!incoming.PurchaseDetails.Any())
                ModelState.AddModelError(string.Empty, "Please add at least one line item.");

            RecalculateFromDetails(incoming);
            SatisfyHeaderRequirementsFromDetails(incoming);
            ClearHeaderValidation(ModelState);

            if (!ModelState.IsValid)
                return View(incoming);

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var dbPurchase = await _context.Purchases
                    .Include(p => p.PurchaseDetails)
                    .FirstOrDefaultAsync(p => p.PurchaseId == id);

                if (dbPurchase == null) return NotFound();

                dbPurchase.BookId = incoming.BookId;
                dbPurchase.PurchaseDate = incoming.PurchaseDate;
                dbPurchase.Supplier = incoming.Supplier;
                dbPurchase.Notes = incoming.Notes;
                dbPurchase.Cost = incoming.Cost;
                dbPurchase.Quantity = incoming.Quantity;
                dbPurchase.Modified = DateTime.UtcNow;

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
                return View(incoming);
            }
        }

        // --------------------------
        // Delete (GET)
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

        // --------------------------
        // Delete (POST)
        // --------------------------
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