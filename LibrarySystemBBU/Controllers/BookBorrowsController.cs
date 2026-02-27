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
    public class BookBorrowsController : Controller
    {
        private readonly DataContext _context;
        public BookBorrowsController(DataContext context) { _context = context; }

        // =========================
        // Helpers
        // =========================
        private async Task AddHistoryAsync(History history)
        {
            history.OccurredUtc = DateTime.UtcNow;
            _context.Histories.Add(history);
            await _context.SaveChangesAsync();
        }

        // Check by BookId (barcode)
        private async Task<(bool ok, string? error)> TryReserveBooksAsync(IEnumerable<int> bookIds)
        {
            var ids = (bookIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0) return (true, null);

            bool conflict = await _context.BookBorrows
                .AsNoTracking()
                .Where(l => !l.IsReturned)
                .AnyAsync(l => l.LoanBookDetails.Any(d => ids.Contains(d.BookId)));

            if (conflict)
                return (false, "One or more selected books are already on an active loan.");

            return (true, null);
        }

        // BOOK STATUS
        private async Task SetBooksStatusAsync(IEnumerable<int> bookIds, string status)
        {
            var ids = (bookIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (!ids.Any()) return;

            var books = await _context.Books
                .Where(b => ids.Contains(b.BookId))
                .ToListAsync();

            foreach (var b in books)
            {
                b.Status = status;
                b.Modified = DateTime.UtcNow;
            }
        }

        private Task MarkBooksAsBorrowedAsync(IEnumerable<int> bookIds) => SetBooksStatusAsync(bookIds, "Borrowed");
        private Task MarkBooksAsAvailableAsync(IEnumerable<int> bookIds) => SetBooksStatusAsync(bookIds, "Available");

        // LOOKUPS
        private async Task PopulateLookupsAsync()
        {
            ViewBag.MemberId = await _context.Members
                .AsNoTracking()
                .OrderBy(m => m.FullName)
                .Select(m => new SelectListItem
                {
                    Value = m.MemberId.ToString(),
                    Text = m.FullName
                })
                .ToListAsync();

            var bookLookupRaw = await _context.Books
                .AsNoTracking()
                .Include(b => b.Catalog)
                .Select(b => new
                {
                    b.BookId,
                    b.CatalogId,
                    b.Barcode,
                    BookTitle = b.Title,
                    CatalogTitle = b.Catalog != null ? b.Catalog.Title : null,
                    CatalogIsbn = b.Catalog != null ? b.Catalog.ISBN : null
                })
                .ToListAsync();

            ViewBag.BookId = bookLookupRaw
                .OrderBy(b => (b.BookTitle ?? b.CatalogTitle) ?? string.Empty)
                .ThenBy(b => b.Barcode)
                .Select(b => new SelectListItem
                {
                    Value = b.BookId.ToString(),
                    Text = $"{(string.IsNullOrWhiteSpace(b.BookTitle) ? b.CatalogTitle : b.BookTitle) ?? "Book"} — {b.Barcode}"
                })
                .ToList();

            var rawCatalogs = await _context.Catalogs
                .AsNoTracking()
                .Select(c => new { c.CatalogId, c.Title, c.ISBN })
                .OrderBy(c => c.Title)
                .ThenBy(c => c.ISBN)
                .ToListAsync();

            ViewBag.CatalogId = rawCatalogs
                .Select(c => new SelectListItem
                {
                    Value = c.CatalogId.ToString(),
                    Text = string.IsNullOrWhiteSpace(c.ISBN) ? c.Title : $"{c.Title} ({c.ISBN})"
                })
                .ToList();

            var booksByCatalog = bookLookupRaw
                .Select(b => new
                {
                    b.BookId,
                    b.CatalogId,
                    Text = $"{(string.IsNullOrWhiteSpace(b.BookTitle)
                                ? (string.IsNullOrWhiteSpace(b.CatalogIsbn)
                                    ? b.CatalogTitle
                                    : $"{b.CatalogTitle} ({b.CatalogIsbn})")
                                : b.BookTitle) ?? "Book"} — {b.Barcode}"
                })
                .OrderBy(x => x.Text)
                .ToList();

            ViewBag.BooksByCatalog = booksByCatalog;
        }

        // AVAILABILITY (per Catalog copy)
        private static Dictionary<Guid, int> GetItemCountsByCatalog(IEnumerable<BookBorrowDetail> items)
            => (items ?? Enumerable.Empty<BookBorrowDetail>())
                .GroupBy(x => x.CatalogId)
                .ToDictionary(g => g.Key, g => g.Count());

        private async Task UpdateAvailableCopiesAsync(Dictionary<Guid, int> deltas, bool clamp = true)
        {
            if (deltas == null || deltas.Count == 0) return;

            var ids = deltas.Keys.ToList();
            var catalogs = await _context.Catalogs
                .Where(c => ids.Contains(c.CatalogId))
                .ToListAsync();

            foreach (var c in catalogs)
            {
                if (!deltas.TryGetValue(c.CatalogId, out var delta)) continue;
                c.AvailableCopies += delta;

                if (clamp)
                {
                    if (c.AvailableCopies < 0) c.AvailableCopies = 0;
                    if (c.AvailableCopies > c.TotalCopies) c.AvailableCopies = c.TotalCopies;
                }
            }
        }

        private async Task UpdateBorrowCountsAsync(Dictionary<Guid, int>? adds, Dictionary<Guid, int>? subs = null)
        {
            var keys = new HashSet<Guid>();
            if (adds != null) foreach (var k in adds.Keys) keys.Add(k);
            if (subs != null) foreach (var k in subs.Keys) keys.Add(k);
            if (keys.Count == 0) return;

            var catalogs = await _context.Catalogs
                .Where(c => keys.Contains(c.CatalogId))
                .ToListAsync();

            foreach (var c in catalogs)
            {
                if (adds != null && adds.TryGetValue(c.CatalogId, out var add))
                    c.BorrowCount += add;

                if (subs != null && subs.TryGetValue(c.CatalogId, out var sub))
                    c.BorrowCount -= sub;

                if (c.BorrowCount < 0) c.BorrowCount = 0;
            }
        }

        private static (Dictionary<Guid, int> deltaAdd, Dictionary<Guid, int> deltaRelease)
            ComputeDeltaByCatalog(IEnumerable<BookBorrowDetail> oldItems, IEnumerable<BookBorrowDetail> newItems)
        {
            var oldMap = GetItemCountsByCatalog(oldItems);
            var newMap = GetItemCountsByCatalog(newItems);

            var allIds = oldMap.Keys.Union(newMap.Keys);
            var add = new Dictionary<Guid, int>();
            var rel = new Dictionary<Guid, int>();

            foreach (var id in allIds)
            {
                var oldQty = oldMap.TryGetValue(id, out var oq) ? oq : 0;
                var newQty = newMap.TryGetValue(id, out var nq) ? nq : 0;
                if (nq > oq) add[id] = nq - oq;
                if (oq > nq) rel[id] = oq - nq;
            }
            return (add, rel);
        }

        // =========================
        // Pages
        // =========================
        [HttpGet("BookBorrows")]
        [HttpGet("BookBorrows/Index")]
        public async Task<IActionResult> Index()
        {
            var loans = await _context.BookBorrows
                .Include(b => b.LibraryMember)
                .Include(b => b.LoanBookDetails)
                .Include(b => b.BookReturns)
                .AsNoTracking()
                .OrderByDescending(x => x.LoanDate)
                .ToListAsync();

            await PopulateLookupsAsync();
            return View(loans);
        }

        // EXPORT -> Excel (.xls)
        [HttpGet("BookBorrows/Export")]
        public async Task<IActionResult> Export()
        {
            var loans = await _context.BookBorrows
                .Include(b => b.LibraryMember)
                .Include(b => b.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(bk => bk.Catalog)
                .Include(b => b.BookReturns)
                .AsNoTracking()
                .OrderByDescending(x => x.LoanDate)
                .ToListAsync();

            string H(string? v) => WebUtility.HtmlEncode(v ?? string.Empty);
            var today = DateTime.Today;

            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" /></head><body>");
            sb.AppendLine("<table border='1'>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>Loan ID</th><th>Member</th><th>Loan Date</th><th>Due Date</th><th>Status</th><th>Borrowing Fee</th><th>Is Paid</th><th>Deposit</th><th>Items Count</th><th>Returns Count</th><th>Book Titles</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var loan in loans)
            {
                var member = loan.LibraryMember?.FullName ?? "—";

                string status =
                    loan.IsReturned ? "Returned"
                    : (loan.DueDate.Date <= today ? "Overdue" : "In Progress");

                var titles = loan.LoanBookDetails
                    .Select(d => d.Book?.Catalog?.Title ?? d.Book?.Title)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();

                var titlesJoined = titles.Count > 0 ? string.Join(", ", titles) : string.Empty;

                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{loan.LoanId}</td>");
                sb.AppendLine($"<td>{H(member)}</td>");
                sb.AppendLine($"<td>{H(loan.LoanDate.ToString("yyyy-MM-dd"))}</td>");
                sb.AppendLine($"<td>{H(loan.DueDate.ToString("yyyy-MM-dd"))}</td>");
                sb.AppendLine($"<td>{H(status)}</td>");
                sb.AppendLine($"<td>{loan.BorrowingFee:0.00}</td>");
                sb.AppendLine($"<td>{H(loan.IsPaid ? "Paid" : "Unpaid")}</td>");
                sb.AppendLine($"<td>{(loan.DepositAmount ?? 0m):0.00}</td>");
                sb.AppendLine($"<td>{(loan.LoanBookDetails?.Count ?? 0)}</td>");
                sb.AppendLine($"<td>{(loan.BookReturns?.Count ?? 0)}</td>");
                sb.AppendLine($"<td>{H(titlesJoined)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var bytes = encoding.GetBytes(sb.ToString());
            var fileName = "BookBorrows_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xls";

            return File(bytes, "application/vnd.ms-excel", fileName);
        }

        // CREATE (GET)
        [HttpGet("BookBorrows/Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateLookupsAsync();
            var model = new BookBorrow
            {
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(7),
                LoanBookDetails = new List<BookBorrowDetail>()
            };
            return View(model);
        }

        // CREATE (POST) - UPDATED DATE RULES:
        // ✅ LoanDate: allow any date (past/future)
        // ✅ DueDate: must be >= LoanDate
        [HttpPost("BookBorrows/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MemberId,LoanDate,DueDate,IsReturned,BorrowingFee,IsPaid,DepositAmount,LoanBookDetails")] BookBorrow model)
        {
            model.IsReturned = false;

            model.LoanDate = model.LoanDate == default ? DateTime.Today : model.LoanDate.Date;
            model.DueDate = model.DueDate == default ? model.LoanDate.AddDays(7) : model.DueDate.Date;

            // ✅ NEW: allow any LoanDate (remove "cannot be earlier than today")
            // ✅ Only validate DueDate >= LoanDate
            if (model.DueDate.Date < model.LoanDate.Date)
                ModelState.AddModelError(nameof(model.DueDate), "Due date cannot be before loan date.");

            var validItems = (model.LoanBookDetails ?? new List<BookBorrowDetail>())
                .Where(x => x.BookId > 0 && x.CatalogId != Guid.Empty && !string.IsNullOrWhiteSpace(x.ConditionOut))
                .ToList();

            if (model.MemberId == Guid.Empty)
                ModelState.AddModelError(nameof(model.MemberId), "Member is required.");

            if (model.MemberId != Guid.Empty)
            {
                bool hasActive = await _context.BookBorrows
                    .AnyAsync(x => x.MemberId == model.MemberId && !x.IsReturned);

                if (hasActive)
                    ModelState.AddModelError(string.Empty, "This member already has an active loan. Please return it first.");
            }

            if (validItems.Count == 0)
                ModelState.AddModelError("LoanBookDetails", "At least one valid borrow item is required.");

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync();
                return View(model);
            }

            foreach (var d in validItems)
            {
                d.Created = DateTime.UtcNow;
                d.Modified = DateTime.UtcNow;
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var (ok, error) = await TryReserveBooksAsync(validItems.Select(i => i.BookId));
                if (!ok)
                {
                    await tx.RollbackAsync();
                    ModelState.AddModelError(string.Empty, error ?? "Selected books are not available.");
                    await PopulateLookupsAsync();
                    return View(model);
                }

                var needMap = GetItemCountsByCatalog(validItems);
                var take = needMap.ToDictionary(kv => kv.Key, kv => -kv.Value);
                await UpdateAvailableCopiesAsync(take);

                await UpdateBorrowCountsAsync(needMap);
                await MarkBooksAsBorrowedAsync(validItems.Select(i => i.BookId));

                model.LoanBookDetails = validItems;
                model.BookReturns = new List<BookReturn>();

                _context.BookBorrows.Add(model);
                await _context.SaveChangesAsync();

                await _context.Entry(model).Reference(b => b.LibraryMember).LoadAsync();
                await _context.Entry(model)
                    .Collection(b => b.LoanBookDetails)
                    .Query()
                    .Include(d => d.Book)
                        .ThenInclude(b => b.Catalog)
                    .LoadAsync();

                var titles = model.LoanBookDetails
                    .Select(d => d.Book?.Catalog?.Title ?? d.Book?.Title)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();

                var history = new History
                {
                    EntityType = "Loan",
                    ActionType = "BorrowHome",
                    LoanId = model.LoanId,
                    MemberName = model.LibraryMember?.FullName,
                    LoanDate = model.LoanDate,
                    DueDate = model.DueDate,
                    BorrowingFee = model.BorrowingFee,
                    DepositAmount = model.DepositAmount,
                    Quantity = model.LoanBookDetails.Count,
                    BookTitle = titles.Count > 0 ? string.Join(", ", titles) : null,
                    LocationType = "Home",
                    Notes = "Created home loan (MVC form)."
                };
                await AddHistoryAsync(history);

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            TempData["ok"] = "Loan created.";
            return RedirectToAction(nameof(Index));
        }

        // EDIT (GET) - explicit route helps prevent 404 issues
        [HttpGet("BookBorrows/Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var loan = await _context.BookBorrows
                .Include(b => b.LoanBookDetails)
                .Include(b => b.BookReturns)
                .Include(b => b.LibraryMember)
                .FirstOrDefaultAsync(x => x.LoanId == id);

            if (loan == null) return NotFound();

            await PopulateLookupsAsync();
            return View(loan);
        }

        // EDIT (POST) - UPDATED DATE RULES:
        // ✅ LoanDate: allow any date (past/future)
        // ✅ DueDate: must be >= LoanDate
        [HttpPost("BookBorrows/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("LoanId,MemberId,LoanDate,DueDate,IsReturned,BorrowingFee,IsPaid,DepositAmount,LoanBookDetails")] BookBorrow model)
        {
            if (id != model.LoanId) return BadRequest();

            var existing = await _context.BookBorrows
                .Include(b => b.LoanBookDetails)
                .FirstOrDefaultAsync(x => x.LoanId == id);

            if (existing == null) return NotFound();

            model.LoanDate = model.LoanDate == default ? existing.LoanDate.Date : model.LoanDate.Date;
            model.DueDate = model.DueDate == default ? existing.DueDate.Date : model.DueDate.Date;

            // ✅ Only validate DueDate >= LoanDate
            if (model.DueDate.Date < model.LoanDate.Date)
                ModelState.AddModelError(nameof(model.DueDate), "Due date cannot be before loan date.");

            var validItems = (model.LoanBookDetails ?? new List<BookBorrowDetail>())
                .Where(x => x.BookId > 0 && x.CatalogId != Guid.Empty && !string.IsNullOrWhiteSpace(x.ConditionOut))
                .ToList();

            if (model.MemberId == Guid.Empty)
                ModelState.AddModelError(nameof(model.MemberId), "Member is required.");

            if (model.MemberId != Guid.Empty && !model.IsReturned)
            {
                bool hasAnotherActive = await _context.BookBorrows
                    .AnyAsync(x => x.MemberId == model.MemberId && !x.IsReturned && x.LoanId != id);

                if (hasAnotherActive)
                    ModelState.AddModelError(string.Empty, "This member already has another active loan. Please return it first.");
            }

            if (validItems.Count == 0)
                ModelState.AddModelError("LoanBookDetails", "At least one valid borrow item is required.");

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync();
                return View(model);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var wasReturned = existing.IsReturned;
                var willReturn = model.IsReturned;

                if (wasReturned && !willReturn)
                {
                    var allBookIds = validItems.Select(i => i.BookId);
                    var (ok, error) = await TryReserveBooksAsync(allBookIds);
                    if (!ok)
                    {
                        await tx.RollbackAsync();
                        ModelState.AddModelError(string.Empty, error ?? "Selected books are not available.");
                        await PopulateLookupsAsync();
                        return View(model);
                    }
                }
                else if (!wasReturned && !willReturn)
                {
                    var oldIdsSet = existing.LoanBookDetails.Select(d => d.BookId).ToHashSet();
                    var addIds = validItems
                        .Select(d => d.BookId)
                        .Where(id2 => !oldIdsSet.Contains(id2))
                        .Distinct()
                        .ToList();

                    if (addIds.Count > 0)
                    {
                        var (ok, error) = await TryReserveBooksAsync(addIds);
                        if (!ok)
                        {
                            await tx.RollbackAsync();
                            ModelState.AddModelError(string.Empty, error ?? "Selected books are not available.");
                            await PopulateLookupsAsync();
                            return View(model);
                        }
                    }
                }

                existing.MemberId = model.MemberId;
                existing.LoanDate = model.LoanDate.Date;
                existing.DueDate = model.DueDate.Date;
                existing.IsReturned = model.IsReturned;
                existing.BorrowingFee = model.BorrowingFee;
                existing.IsPaid = model.IsPaid;
                existing.DepositAmount = model.DepositAmount;

                var (deltaAdd, deltaRelease) = ComputeDeltaByCatalog(existing.LoanBookDetails, validItems);
                await UpdateBorrowCountsAsync(deltaAdd, deltaRelease);

                // Status logic
                var oldBookIds = existing.LoanBookDetails.Select(d => d.BookId).ToHashSet();
                var newBookIds = validItems.Select(d => d.BookId).Where(x => x > 0).ToHashSet();
                var removedIds = oldBookIds.Except(newBookIds).ToList();
                var addedIds = newBookIds.Except(oldBookIds).ToList();

if (willReturn)
{
    await MarkBooksAsAvailableAsync(oldBookIds.Union(newBookIds));
    
    var give = GetItemCountsByCatalog(existing.LoanBookDetails);
    await UpdateAvailableCopiesAsync(give);

    var alreadyHasReturn = await _context.BookReturns.AnyAsync(r => r.LoanId == existing.LoanId);
    if (!alreadyHasReturn)
    {
        _context.BookReturns.Add(new BookReturn
        {
            LoanId      = existing.LoanId,
            ReturnDate  = DateTime.Now,
            LateDays    = 0,
            FineAmount  = 0,
            ExtraCharge = 0,
            AmountPaid  = 0,
            Notes       = "Returned via admin edit.",
        });
    }
}
                else
                {
if (wasReturned && !willReturn)
{
    await MarkBooksAsBorrowedAsync(newBookIds);

    var take = GetItemCountsByCatalog(validItems)
        .ToDictionary(kv => kv.Key, kv => -kv.Value);
    await UpdateAvailableCopiesAsync(take);
}
                    else
                    {
                        await MarkBooksAsBorrowedAsync(addedIds);
                        await MarkBooksAsAvailableAsync(removedIds);
                    }
                }

                _context.RemoveRange(existing.LoanBookDetails);
                existing.LoanBookDetails = new List<BookBorrowDetail>();

                foreach (var it in validItems)
                {
                    existing.LoanBookDetails.Add(new BookBorrowDetail
                    {
                        LoanId = existing.LoanId,
                        BookId = it.BookId,
                        CatalogId = it.CatalogId,
                        ConditionOut = it.ConditionOut?.Trim() ?? "",
                        ConditionIn = string.IsNullOrWhiteSpace(it.ConditionIn) ? null : it.ConditionIn.Trim(),
                        FineDetailAmount = it.FineDetailAmount,
                        FineDetailReason = string.IsNullOrWhiteSpace(it.FineDetailReason) ? null : it.FineDetailReason.Trim(),
                        Created = DateTime.UtcNow,
                        Modified = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["ok"] = "Loan updated.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // DETAILS (explicit route fixes your "/BookBorrows/Details/6" 404)
        [HttpGet("BookBorrows/Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var loan = await _context.BookBorrows
                .Include(b => b.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(bk => bk.Catalog)
                .Include(b => b.BookReturns)
                .Include(b => b.LibraryMember)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.LoanId == id);

            if (loan == null) return NotFound();
            return View(loan);
        }

        // DELETE (make route match your JS: fetch('/BookBorrows/Delete/{id}'))
        [HttpPost("BookBorrows/Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var loan = await _context.BookBorrows
                .Include(b => b.LoanBookDetails)
                .Include(b => b.BookReturns)
                .Include(b => b.LibraryMember)
                .FirstOrDefaultAsync(b => b.LoanId == id);

            if (loan == null)
                return NotFound("Loan not found.");

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var histories = await _context.Histories
                    .Where(h => h.LoanId == loan.LoanId)
                    .ToListAsync();

                if (histories.Any())
                    _context.Histories.RemoveRange(histories);

                if (!loan.IsReturned && loan.LoanBookDetails.Any())
                {
                    var give = GetItemCountsByCatalog(loan.LoanBookDetails);
                    await UpdateAvailableCopiesAsync(give);

                    await MarkBooksAsAvailableAsync(loan.LoanBookDetails.Select(d => d.BookId));
                }

                if (loan.LoanBookDetails.Any())
                {
                    var usage = GetItemCountsByCatalog(loan.LoanBookDetails);
                    await UpdateBorrowCountsAsync(null, usage);
                }

                _context.BookBorrows.Remove(loan);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
                return Ok(new { ok = true, message = "Loan deleted." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }


        // =========================
        // LOOKUPS (Ajax)
        // =========================

        [HttpGet("BookBorrows/GetBooksByCatalog")]
        public async Task<IActionResult> GetBooksByCatalog(Guid catalogId)
        {
            var books = await _context.Books
                .AsNoTracking()
                .Where(b => b.CatalogId == catalogId)
                .Select(b => new
                {
                    bookId   = b.BookId,
                    barcode  = b.Barcode,
                    status   = b.Status,
                    location = b.Location
                })
                .OrderBy(b => b.barcode)
                .ToListAsync();

            return Json(books);
        }

        [HttpGet("BookBorrows/GetLoan")]
        public async Task<IActionResult> GetLoan(int id)
        {
            var loan = await _context.BookBorrows
                .Include(b => b.LibraryMember)
                .Include(b => b.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(bk => bk.Catalog)
                .Include(b => b.BookReturns)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.LoanId == id);

            if (loan == null) return NotFound(new { success = false, message = "Loan not found." });

            return Json(new
            {
                loanId        = loan.LoanId,
                memberId      = loan.MemberId,
                memberName    = loan.LibraryMember?.FullName,
                loanDate      = loan.LoanDate,
                dueDate       = loan.DueDate,
                isReturned    = loan.IsReturned,
                borrowingFee  = loan.BorrowingFee,
                isPaid        = loan.IsPaid,
                depositAmount = loan.DepositAmount,
                items         = loan.LoanBookDetails.Select(d => new
                {
                    loanBookDetailId = d.LoanBookDetailId,
                    bookId           = d.BookId,
                    catalogId        = d.CatalogId,
                    barcode          = d.Book?.Barcode,
                    catalogTitle     = d.Book?.Catalog?.Title ?? d.Book?.Title,
                    conditionOut     = d.ConditionOut,
                    conditionIn      = d.ConditionIn,
                    fineDetailAmount = d.FineDetailAmount,
                    fineDetailReason = d.FineDetailReason,
                }),
                returns = loan.BookReturns.Select(r => new
                {
                    returnId          = r.ReturnId,
                    returnDate        = r.ReturnDate,
                    lateDays          = r.LateDays,
                    fineAmount        = r.FineAmount,
                    extraCharge       = r.ExtraCharge,
                    amountPaid        = r.AmountPaid,
                    refundAmount      = r.RefundAmount,
                    conditionOnReturn = r.ConditionOnReturn,
                    notes             = r.Notes,
                })
            });
        }

        // =========================
        // RETURN (POST)
        // Restores available copies, marks books Available,
        // records BookReturn with timestamp, logs History.
        // =========================

        [HttpPost("BookBorrows/ReturnJson")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnJson([FromBody] ReturnRequest model)
        {
            if (model == null || model.LoanId <= 0)
                return BadRequest(new { success = false, message = "Invalid request." });

            var loan = await _context.BookBorrows
                .Include(b => b.LibraryMember)
                .Include(b => b.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(bk => bk.Catalog)
                .Include(b => b.BookReturns)
                .FirstOrDefaultAsync(x => x.LoanId == model.LoanId);

            if (loan == null)
                return NotFound(new { success = false, message = "Loan not found." });

            if (loan.IsReturned)
                return Ok(new { success = false, message = "This loan has already been returned." });

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Mark loan as returned
                loan.IsReturned = true;

                // 2. Update condition-in on each detail if provided
                if (model.ConditionItems != null)
                {
                    foreach (var ci in model.ConditionItems)
                    {
                        var detail = loan.LoanBookDetails.FirstOrDefault(d => d.LoanBookDetailId == ci.LoanBookDetailId);
                        if (detail == null) continue;
                        detail.ConditionIn      = ci.ConditionIn?.Trim();
                        detail.FineDetailAmount = ci.FineDetailAmount;
                        detail.FineDetailReason = ci.FineDetailReason?.Trim();
                        detail.Modified         = DateTime.UtcNow;
                    }
                }

                // 3. Record the BookReturn with full timestamp (not .Date)
                var bookReturn = new BookReturn
                {
                    LoanId            = loan.LoanId,
                    ReturnDate        = DateTime.Now,          // ← full timestamp
                    LateDays          = model.LateDays,
                    FineAmount        = model.FineAmount,
                    ExtraCharge       = model.ExtraCharge,
                    AmountPaid        = model.AmountPaid,
                    RefundAmount      = model.RefundAmount,
                    ConditionOnReturn = model.ConditionOnReturn?.Trim(),
                    Notes             = model.Notes?.Trim(),
                };
                _context.BookReturns.Add(bookReturn);

                // 4. Restore available copies on each catalog
                var give = GetItemCountsByCatalog(loan.LoanBookDetails);
                await UpdateAvailableCopiesAsync(give);

                // 5. Mark individual books as Available
                await MarkBooksAsAvailableAsync(loan.LoanBookDetails.Select(d => d.BookId));

                await _context.SaveChangesAsync();

                // 6. Log history
                var titles = loan.LoanBookDetails
                    .Select(d => d.Book?.Catalog?.Title ?? d.Book?.Title)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();

                var history = new History
                {
                    EntityType    = "Loan",
                    ActionType    = "ReturnHome",
                    LoanId        = loan.LoanId,
                    MemberName    = loan.LibraryMember?.FullName,
                    LoanDate      = loan.LoanDate,
                    DueDate       = loan.DueDate,
                    ReturnDate    = bookReturn.ReturnDate,
                    BorrowingFee  = loan.BorrowingFee,
                    FineAmount    = model.FineAmount,
                    AmountPaid    = model.AmountPaid,
                    DepositAmount = loan.DepositAmount,
                    Quantity      = loan.LoanBookDetails.Count,
                    BookTitle     = titles.Count > 0 ? string.Join(", ", titles) : null,
                    LocationType  = "Home",
                    Notes         = model.Notes?.Trim() ?? "Returned via portal."
                };
                await AddHistoryAsync(history);

                await tx.CommitAsync();

                return Ok(new
                {
                    success    = true,
                    message    = "Book returned successfully.",
                    returnDate = bookReturn.ReturnDate,
                    returnId   = bookReturn.ReturnId
                });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // =========================
        // UN-RETURN (POST)
        // Reverses a return: removes BookReturn record,
        // marks loan active again, decrements available copies.
        // =========================

        [HttpPost("BookBorrows/UnReturnJson")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnReturnJson([FromBody] UnReturnRequest model)
        {
            if (model == null || model.LoanId <= 0)
                return BadRequest(new { success = false, message = "Invalid request." });

            var loan = await _context.BookBorrows
                .Include(b => b.LibraryMember)
                .Include(b => b.LoanBookDetails)
                .Include(b => b.BookReturns)
                .FirstOrDefaultAsync(x => x.LoanId == model.LoanId);

            if (loan == null)
                return NotFound(new { success = false, message = "Loan not found." });

            if (!loan.IsReturned)
                return Ok(new { success = false, message = "This loan is not marked as returned." });

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Remove the most recent BookReturn (or specific one if ReturnId provided)
                BookReturn? ret = model.ReturnId.HasValue
                    ? loan.BookReturns.FirstOrDefault(r => r.ReturnId == model.ReturnId.Value)
                    : loan.BookReturns.OrderByDescending(r => r.ReturnDate).FirstOrDefault();

                if (ret != null)
                    _context.BookReturns.Remove(ret);

                // 2. Mark loan as active again
                loan.IsReturned = false;

                // 3. Decrement available copies (re-borrow)
                var take = GetItemCountsByCatalog(loan.LoanBookDetails)
                    .ToDictionary(kv => kv.Key, kv => -kv.Value);
                await UpdateAvailableCopiesAsync(take);

                // 4. Mark books as Borrowed again
                await MarkBooksAsBorrowedAsync(loan.LoanBookDetails.Select(d => d.BookId));

                await _context.SaveChangesAsync();

                // 5. Log history
                var history = new History
                {
                    EntityType   = "Loan",
                    ActionType   = "UnReturn",
                    LoanId       = loan.LoanId,
                    MemberName   = loan.LibraryMember?.FullName,
                    LoanDate     = loan.LoanDate,
                    DueDate      = loan.DueDate,
                    LocationType = "Home",
                    Notes        = "Return reversed (un-returned)."
                };
                await AddHistoryAsync(history);

                await tx.CommitAsync();

                return Ok(new { success = true, message = "Return reversed successfully." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // =========================
        // RECEIPT (GET)
        // =========================

        [HttpGet("BookBorrows/ReceiptJson/{id:int}")]
        public async Task<IActionResult> ReceiptJson(int id)
        {
            var loan = await _context.BookBorrows
                .Include(b => b.LibraryMember)
                .Include(b => b.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(bk => bk.Catalog)
                .Include(b => b.BookReturns)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.LoanId == id);

            if (loan == null)
                return NotFound(new { success = false, message = "Loan not found." });

            var latestReturn = loan.BookReturns
                .OrderByDescending(r => r.ReturnDate)
                .FirstOrDefault();

            return Json(new
            {
                loanId        = loan.LoanId,
                memberName    = loan.LibraryMember?.FullName,
                loanDate      = loan.LoanDate,
                dueDate       = loan.DueDate,
                isReturned    = loan.IsReturned,
                borrowingFee  = loan.BorrowingFee,
                isPaid        = loan.IsPaid,
                depositAmount = loan.DepositAmount,
                returnDate    = latestReturn?.ReturnDate,
                fineAmount    = latestReturn?.FineAmount,
                extraCharge   = latestReturn?.ExtraCharge,
                amountPaid    = latestReturn?.AmountPaid,
                refundAmount  = latestReturn?.RefundAmount,
                books         = loan.LoanBookDetails.Select(d => new
                {
                    title        = d.Book?.Catalog?.Title ?? d.Book?.Title ?? "Unknown",
                    barcode      = d.Book?.Barcode,
                    conditionOut = d.ConditionOut,
                    conditionIn  = d.ConditionIn,
                    fineAmount   = d.FineDetailAmount,
                    fineReason   = d.FineDetailReason,
                })
            });
        }

        // =========================
        // REQUEST MODELS
        // =========================

        public class ReturnRequest
        {
            public int LoanId { get; set; }
            public int LateDays { get; set; }
            public decimal FineAmount { get; set; }
            public decimal ExtraCharge { get; set; }
            public decimal AmountPaid { get; set; }
            public decimal? RefundAmount { get; set; }
            public string? ConditionOnReturn { get; set; }
            public string? Notes { get; set; }
            public List<ConditionItem>? ConditionItems { get; set; }
        }

        public class ConditionItem
        {
            public int LoanBookDetailId { get; set; }
            public string? ConditionIn { get; set; }
            public decimal? FineDetailAmount { get; set; }
            public string? FineDetailReason { get; set; }
        }

        public class UnReturnRequest
        {
            public int LoanId { get; set; }
            public int? ReturnId { get; set; }
        }

        // ── Route aliases for MVC admin frontend ──
[HttpPost("BookBorrows/Return/{id:int}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Return(int id)
{
    var loan = await _context.BookBorrows
        .Include(b => b.LibraryMember)
        .Include(b => b.LoanBookDetails)
            .ThenInclude(d => d.Book)
                .ThenInclude(bk => bk.Catalog)
        .Include(b => b.BookReturns)
        .FirstOrDefaultAsync(x => x.LoanId == id);

    if (loan == null) return NotFound(new { success = false, message = "Loan not found." });
    if (loan.IsReturned) return Ok(new { success = false, message = "Already returned." });

    using var tx = await _context.Database.BeginTransactionAsync();
    try
    {
        loan.IsReturned = true;

        var alreadyHasReturn = await _context.BookReturns.AnyAsync(r => r.LoanId == id);
        if (!alreadyHasReturn)
        {
            _context.BookReturns.Add(new BookReturn
            {
                LoanId      = loan.LoanId,
                ReturnDate  = DateTime.Now,
                LateDays    = 0,
                FineAmount  = 0,
                ExtraCharge = 0,
                AmountPaid  = 0,
                Notes       = "Returned via admin panel.",
            });
        }

        var give = GetItemCountsByCatalog(loan.LoanBookDetails);
        await UpdateAvailableCopiesAsync(give);
        await MarkBooksAsAvailableAsync(loan.LoanBookDetails.Select(d => d.BookId));

        var titles = loan.LoanBookDetails
            .Select(d => d.Book?.Catalog?.Title ?? d.Book?.Title)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct().ToList();

        await AddHistoryAsync(new History
        {
            EntityType   = "Loan",
            ActionType   = "ReturnHome",
            LoanId       = loan.LoanId,
            MemberName   = loan.LibraryMember?.FullName,
            LoanDate     = loan.LoanDate,
            DueDate      = loan.DueDate,
            ReturnDate   = DateTime.Now,
            BorrowingFee = loan.BorrowingFee,
            Quantity     = loan.LoanBookDetails.Count,
            BookTitle    = titles.Count > 0 ? string.Join(", ", titles) : null,
            LocationType = "Home",
            Notes        = "Returned via admin panel.",
        });

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { success = true, message = "Returned successfully.", returnDate = DateTime.Now });
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
}

[HttpPost("BookBorrows/UnReturn/{id:int}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UnReturn(int id)
{
    var loan = await _context.BookBorrows
        .Include(b => b.LibraryMember)
        .Include(b => b.LoanBookDetails)
        .Include(b => b.BookReturns)
        .FirstOrDefaultAsync(x => x.LoanId == id);

    if (loan == null) return NotFound(new { success = false, message = "Loan not found." });
    if (!loan.IsReturned) return Ok(new { success = false, message = "Loan is not returned." });

    using var tx = await _context.Database.BeginTransactionAsync();
    try
    {
        var ret = loan.BookReturns.OrderByDescending(r => r.ReturnDate).FirstOrDefault();
        if (ret != null) _context.BookReturns.Remove(ret);

        loan.IsReturned = false;

        var take = GetItemCountsByCatalog(loan.LoanBookDetails)
            .ToDictionary(kv => kv.Key, kv => -kv.Value);
        await UpdateAvailableCopiesAsync(take);
        await MarkBooksAsBorrowedAsync(loan.LoanBookDetails.Select(d => d.BookId));

        await AddHistoryAsync(new History
        {
            EntityType   = "Loan",
            ActionType   = "UnReturn",
            LoanId       = loan.LoanId,
            MemberName   = loan.LibraryMember?.FullName,
            LoanDate     = loan.LoanDate,
            DueDate      = loan.DueDate,
            LocationType = "Home",
            Notes        = "Return reversed via admin panel.",
        });

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { success = true, message = "Un-returned successfully." });
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
}
    }
}
