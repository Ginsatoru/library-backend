using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystemBBU.Controllers
{
    public class LibraryLogsController : Controller
    {
        private readonly DataContext _context;

        public LibraryLogsController(DataContext context)
        {
            _context = context;
        }

        private const string STATUS_PENDING = "Pending";
        private const string STATUS_APPROVED = "Approved";
        private const string STATUS_RETURNED = "Returned";

        private bool IsAjax =>
            string.Equals(Request?.Headers["X-Requested-With"].ToString(),
                          "XMLHttpRequest",
                          StringComparison.OrdinalIgnoreCase);

        // ------------------------- Shadow property helpers -------------------------
        private string GetStatus(LibraryLog e)
        {
            return _context.Entry(e).Property<string>("Status").CurrentValue ?? STATUS_PENDING;
        }

        private DateTime? GetApprovedUtc(LibraryLog e)
        {
            return _context.Entry(e).Property<DateTime?>("ApprovedUtc").CurrentValue;
        }

        private DateTime? GetReturnedUtc(LibraryLog e)
        {
            return _context.Entry(e).Property<DateTime?>("ReturnedUtc").CurrentValue;
        }


        private async Task UpsertLogHistoryAsync(
            LibraryLog log,
            string actionType,
            string? notes = null,
            int? quantityOverride = null,
            DateTime? returnDate = null)
        {
            // Make sure Items + Books + Catalogs are loaded
            await _context.Entry(log)
                .Collection(l => l.Items)
                .Query()
                .Include(i => i.Book)
                    .ThenInclude(b => b.Catalog)
                .LoadAsync();

            var titles = (log.Items ?? new List<LibraryLogItem>())
                .Select(SafeTitle)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            int qty = quantityOverride ?? (log.Items?.Count ?? 0);

            var history = await _context.Histories
                .FirstOrDefaultAsync(h =>
                    h.EntityType == "LibraryLog" &&
                    h.LogId == log.LogId);

            if (history == null)
            {
                history = new History
                {
                    EntityType = "LibraryLog",
                    LogId = log.LogId,
                    LocationType = "Library"
                };
                _context.Histories.Add(history);
            }

            history.ActionType = actionType;
            history.MemberName = log.StudentName;
            history.CatalogTitle = titles.Count > 0 ? string.Join(", ", titles) : null;
            history.Quantity = qty;
            history.LoanDate = log.VisitDate;
            history.ReturnDate = returnDate;
            history.LocationType = "Library";
            history.Notes = notes;
            history.OccurredUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        // ------------------------- helpers -------------------------
        private static string CsvEscape(string? s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";
        private static string CsvText(string? s) => $"=\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";
        private static string CsvDateText(DateTime? dt, string fmt) => dt.HasValue ? CsvText(dt.Value.ToString(fmt)) : "\"\"";
        private static string HtmlEncode(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

        private static string SafeTitle(LibraryLogItem? it)
        {
            var title = it?.Book?.Catalog?.Title ?? it?.Book?.Title ?? string.Empty;
            var code = it?.Book?.Barcode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(code)) return string.Empty;
            return string.IsNullOrWhiteSpace(code) ? title : $"{title} ({code})";
        }

        private async Task<HashSet<int>> GetUnavailableBookIdsAsync()
        {
            var ids = await _context.LibraryLogItems
                .Where(i => EF.Property<DateTime?>(i, "ReturnedDate") == null)
                .Select(i => i.BookId)
                .Distinct()
                .ToListAsync();

            return ids.ToHashSet();
        }

        // ------------------------- VM -------------------------
        public class LibraryLogFormVM
        {
            public int? LogId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public string? Gender { get; set; }
            public DateTime VisitDate { get; set; } = DateTime.Today;
            public string? Purpose { get; set; }
            public string? Notes { get; set; }
            public string Status { get; set; } = STATUS_PENDING;
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
            public DateTime? ApprovedUtc { get; set; }
            public DateTime? ReturnedUtc { get; set; }
            public List<int> BookIds { get; set; } = new();
        }

        private LibraryLogFormVM MapToVM(LibraryLog e) => new LibraryLogFormVM
        {
            LogId = e.LogId,
            StudentName = e.StudentName,
            PhoneNumber = e.PhoneNumber,
            Gender = e.Gender,
            VisitDate = e.VisitDate,
            Purpose = e.Purpose,
            Notes = e.Notes,
            CreatedUtc = e.CreatedUtc,
            Status = GetStatus(e),
            ApprovedUtc = GetApprovedUtc(e),
            ReturnedUtc = GetReturnedUtc(e),
            BookIds = e.Items?.Select(i => i.BookId).ToList() ?? new List<int>()
        };


        private async Task PopulateBookSelectAsync(IEnumerable<int>? selectedIds = null, bool onlyAvailable = true)
        {
            selectedIds ??= Array.Empty<int>();
            var selectedSet = selectedIds.ToHashSet();

            HashSet<int> unavailable = onlyAvailable ? await GetUnavailableBookIdsAsync() : new();

            var books = await _context.Books
                .AsNoTracking()
                .Include(b => b.Catalog)
                .OrderBy(b => b.Catalog.Title).ThenBy(b => b.Barcode)
                .Select(b => new
                {
                    b.BookId,
                    DisplayName = b.Catalog.Title + " (" + b.Barcode + ")",
                    IsUnavailable = unavailable.Contains(b.BookId)
                })
                .Where(x => !onlyAvailable || !x.IsUnavailable || selectedSet.Contains(x.BookId))
                .ToListAsync();

            ViewBag.BookList = new SelectList(books, "BookId", "DisplayName", selectedIds);
        }

        // ------------------------- Filters/Index/Details -------------------------
        private IQueryable<LibraryLog> ApplyFilters(string? status, string? q, DateTime? from, DateTime? to)
        {
            status = string.IsNullOrWhiteSpace(status) ? "All" : status;

            var query = _context.LibraryLogs
                .AsNoTracking()
                .Include(l => l.Items).ThenInclude(i => i.Book).ThenInclude(b => b.Catalog)
                .AsQueryable();

            if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(l =>
                    EF.Property<string>(l, "Status") == status ||
                    (status == STATUS_PENDING && EF.Property<string>(l, "Status") == null));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim();
                query = query.Where(l =>
                    l.StudentName.Contains(kw) ||
                    (l.PhoneNumber != null && l.PhoneNumber.Contains(kw)) ||
                    (l.Purpose != null && l.Purpose.Contains(kw)) ||
                    (l.Notes != null && l.Notes.Contains(kw)) ||
                    l.Items.Any(i =>
                        (i.Book != null && i.Book.Barcode.Contains(kw)) ||
                        (i.Book != null && i.Book.Catalog != null && i.Book.Catalog.Title.Contains(kw))));
            }

            if (from.HasValue) query = query.Where(l => l.VisitDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(l => l.VisitDate <= to.Value.Date);

            return query;
        }

        public async Task<IActionResult> Index(string? status = "All", string? q = null, DateTime? from = null, DateTime? to = null)
        {
            var logs = await ApplyFilters(status, q, from, to)
                .OrderByDescending(l => l.VisitDate).ThenByDescending(l => l.LogId)
                .ToListAsync();

            ViewData["status"] = string.IsNullOrWhiteSpace(status) ? "All" : status;
            ViewData["q"] = q;
            ViewData["from"] = from?.ToString("yyyy-MM-dd");
            ViewData["to"] = to?.ToString("yyyy-MM-dd");
            return View(logs);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var e = await _context.LibraryLogs
                .AsNoTracking()
                .Include(l => l.Items).ThenInclude(i => i.Book).ThenInclude(b => b.Catalog)
                .FirstOrDefaultAsync(x => x.LogId == id);
            if (e == null) return NotFound();

            return View(e);
        }

        // ------------------------- Create -------------------------
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateBookSelectAsync(null, onlyAvailable: true);
            return View(new LibraryLogFormVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LibraryLogFormVM vm)
        {
            // Always force today's date for in-library log
            vm.VisitDate = DateTime.Today;

            var unavailable = await GetUnavailableBookIdsAsync();
            var requested = (vm.BookIds ?? new List<int>()).Distinct().ToList();
            var blocked = requested.Where(id => unavailable.Contains(id)).ToList();
            if (blocked.Any())
            {
                ModelState.AddModelError(nameof(vm.BookIds),
                    "Some selected books are currently borrowed and cannot be chosen until returned.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateBookSelectAsync(vm.BookIds, onlyAvailable: true);
                return View(vm);
            }

            var log = new LibraryLog
            {
                StudentName = vm.StudentName?.Trim() ?? string.Empty,
                PhoneNumber = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim(),
                Gender = string.IsNullOrWhiteSpace(vm.Gender) ? null : vm.Gender.Trim(),
                VisitDate = DateTime.Today,
                Purpose = string.IsNullOrWhiteSpace(vm.Purpose) ? null : vm.Purpose.Trim(),
                Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
                CreatedUtc = DateTime.UtcNow,
                Items = new List<LibraryLogItem>()
            };

            _context.Entry(log).Property("Status").CurrentValue = STATUS_PENDING;
            _context.Entry(log).Property("ApprovedUtc").CurrentValue = null;
            _context.Entry(log).Property("ReturnedUtc").CurrentValue = null;

            foreach (var bid in requested)
                log.Items.Add(new LibraryLogItem { BookId = bid });

            _context.LibraryLogs.Add(log);
            await _context.SaveChangesAsync();

            // HISTORY: create / pending
            await UpsertLogHistoryAsync(
                log,
                actionType: "BorrowInLibrary",
                notes: "Library log created (Pending).",
                quantityOverride: log.Items?.Count
            );

            TempData["ok"] = "Library log created (Pending).";
            return RedirectToAction(nameof(Index), new { status = STATUS_PENDING });
        }

        // ------------------------- Edit (visit date not editable) -------------------------
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var e = await _context.LibraryLogs
                .Include(l => l.Items)
                .FirstOrDefaultAsync(x => x.LogId == id);
            if (e == null) return NotFound();

            var vm = MapToVM(e);
            await PopulateBookSelectAsync(vm.BookIds, onlyAvailable: true);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LibraryLogFormVM vm)
        {
            if (id != vm.LogId) return NotFound();

            var e = await _context.LibraryLogs
                .Include(l => l.Items)
                .FirstOrDefaultAsync(x => x.LogId == id);
            if (e == null) return NotFound();

            // Do NOT allow changing VisitDate; keep e.VisitDate as is.

            var unavailable = await GetUnavailableBookIdsAsync();
            var requested = (vm.BookIds ?? new List<int>()).Distinct().ToList();
            var oldSet = e.Items.Select(i => i.BookId).ToHashSet();
            var newlyAdded = requested.Where(b => !oldSet.Contains(b)).ToList();
            var blocked = newlyAdded.Where(id2 => unavailable.Contains(id2)).ToList();
            if (blocked.Any())
                ModelState.AddModelError(nameof(vm.BookIds), "Some selected books are currently borrowed and cannot be added.");

            if (!ModelState.IsValid)
            {
                await PopulateBookSelectAsync(vm.BookIds, onlyAvailable: true);
                return View(vm);
            }

            e.StudentName = vm.StudentName?.Trim() ?? string.Empty;
            e.PhoneNumber = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim();
            e.Gender = string.IsNullOrWhiteSpace(vm.Gender) ? null : vm.Gender.Trim();
            // e.VisitDate remains original
            e.Purpose = string.IsNullOrWhiteSpace(vm.Purpose) ? null : vm.Purpose.Trim();
            e.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();

            var newSet = requested.ToHashSet();

            var toRemove = e.Items.Where(i => !newSet.Contains(i.BookId)).ToList();
            if (toRemove.Any()) _context.LibraryLogItems.RemoveRange(toRemove);

            var toAdd = newSet.Except(oldSet).ToList();
            foreach (var bid in toAdd)
                e.Items.Add(new LibraryLogItem { BookId = bid });

            await _context.SaveChangesAsync();

            await UpsertLogHistoryAsync(
                e,
                actionType: "BorrowInLibrary",
                notes: "Library log updated.",
                quantityOverride: e.Items?.Count
            );

            TempData["ok"] = "Library log updated.";
            return RedirectToAction(nameof(Index));
        }

        // ------------------------- Workflow -------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var e = await _context.LibraryLogs
                .Include(l => l.Items)
                    .ThenInclude(i => i.Book)
                        .ThenInclude(b => b.Catalog)
                .FirstOrDefaultAsync(x => x.LogId == id);
            if (e == null) return NotFound();

            var statusProp = _context.Entry(e).Property<string>("Status");
            var status = statusProp.CurrentValue ?? STATUS_PENDING;

            if (status == STATUS_APPROVED || status == STATUS_RETURNED)
            {
                var info = "This log has already been approved/returned.";
                if (IsAjax) return Json(new { ok = false, message = info, status });
                TempData["Info"] = info;
                return RedirectToAction(nameof(Index));
            }

            statusProp.CurrentValue = STATUS_APPROVED;
            var now = DateTime.UtcNow;
            _context.Entry(e).Property("ApprovedUtc").CurrentValue = now;
            await _context.SaveChangesAsync();

            // increase InLibraryCount per Catalog for this log
            var byCatalog = e.Items
                .Where(it => it.Book != null)
                .GroupBy(it => it.Book.CatalogId)
                .ToDictionary(g => g.Key, g => g.Count());

            if (byCatalog.Count > 0)
            {
                var ids = byCatalog.Keys.ToList();
                var catalogs = await _context.Catalogs
                    .Where(c => ids.Contains(c.CatalogId))
                    .ToListAsync();

                foreach (var c in catalogs)
                {
                    if (byCatalog.TryGetValue(c.CatalogId, out var cnt))
                    {
                        c.InLibraryCount += cnt;
                    }
                }

                await _context.SaveChangesAsync();
            }

            await UpsertLogHistoryAsync(
                e,
                actionType: "Approve",
                notes: "Library log approved.",
                quantityOverride: e.Items?.Count
            );

            if (IsAjax)
                return Json(new
                {
                    ok = true,
                    message = "Log approved.",
                    status = STATUS_APPROVED,
                    approvedUtc = now.ToString("yyyy-MM-dd HH:mm")
                });

            TempData["ok"] = "Log approved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id, int[]? itemIds = null)
        {
            var e = await _context.LibraryLogs
                .Include(l => l.Items).ThenInclude(i => i.Book).ThenInclude(b => b.Catalog)
                .FirstOrDefaultAsync(x => x.LogId == id);
            if (e == null) return NotFound();

            var statusProp = _context.Entry(e).Property<string>("Status");
            var status = statusProp.CurrentValue ?? STATUS_PENDING;

            if (status == STATUS_PENDING)
            {
                var msg = "You must approve the log before returning.";
                if (IsAjax) return Json(new { ok = false, message = msg, status });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(Index));
            }
            if (status == STATUS_RETURNED)
            {
                var info = "This log is already marked as Returned.";
                if (IsAjax) return Json(new { ok = false, message = info, status });
                TempData["Info"] = info;
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;
            var targetItems = (itemIds == null || itemIds.Length == 0)
                ? e.Items.ToList()
                : e.Items.Where(i => itemIds.Contains(i.LogItemId)).ToList();

            foreach (var item in targetItems)
                _context.Entry(item).Property<DateTime?>("ReturnedDate").CurrentValue = now;

            var allReturned = e.Items.All(i =>
                _context.Entry(i).Property<DateTime?>("ReturnedDate").CurrentValue != null);

            DateTime? returnedUtc = null;
            if (allReturned)
            {
                statusProp.CurrentValue = STATUS_RETURNED;
                _context.Entry(e).Property("ReturnedUtc").CurrentValue = now;
                returnedUtc = now;
            }

            await _context.SaveChangesAsync();

            await UpsertLogHistoryAsync(
                e,
                actionType: "ReturnLibrary",
                notes: allReturned
                    ? "All items returned. Log marked as Returned."
                    : "Some items returned (partial).",
                quantityOverride: targetItems.Count,
                returnDate: returnedUtc
            );

            var msgOk = allReturned ? "All items returned. Log is marked as Returned." : "Selected items marked as returned.";

            if (IsAjax)
                return Json(new
                {
                    ok = true,
                    message = msgOk,
                    status = statusProp.CurrentValue,
                    returnedUtc = returnedUtc?.ToString("yyyy-MM-dd HH:mm")
                });

            TempData["ok"] = msgOk;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unreturn(int id, int[]? itemIds = null)
        {
            var e = await _context.LibraryLogs.Include(l => l.Items).FirstOrDefaultAsync(x => x.LogId == id);
            if (e == null) return NotFound();

            var statusProp = _context.Entry(e).Property<string>("Status");
            var status = statusProp.CurrentValue ?? STATUS_PENDING;

            if (status == STATUS_PENDING)
            {
                var info = "This log is still Pending and has nothing to unreturn.";
                if (IsAjax) return Json(new { ok = false, message = info, status });
                TempData["Info"] = info;
                return RedirectToAction(nameof(Index));
            }

            var targetItems = (itemIds == null || itemIds.Length == 0)
                ? e.Items.ToList()
                : e.Items.Where(i => itemIds.Contains(i.LogItemId)).ToList();

            if (!targetItems.Any())
            {
                var info = "No matching items to unreturn.";
                if (IsAjax) return Json(new { ok = false, message = info, status });
                TempData["Info"] = info;
                return RedirectToAction(nameof(Index));
            }

            foreach (var item in targetItems)
                _context.Entry(item).Property<DateTime?>("ReturnedDate").CurrentValue = null;

            var allReturned = e.Items.All(i =>
                _context.Entry(i).Property<DateTime?>("ReturnedDate").CurrentValue != null);

            if (!allReturned)
            {
                statusProp.CurrentValue = STATUS_APPROVED;
                _context.Entry(e).Property<DateTime?>("ReturnedUtc").CurrentValue = null;
            }

            await _context.SaveChangesAsync();

            await UpsertLogHistoryAsync(
                e,
                actionType: "Unreturn",
                notes: "Library items unreturned; log moved back to Approved.",
                quantityOverride: targetItems.Count,
                returnDate: null
            );

            var msgOk = allReturned ? "Items were unchanged (all still returned)." : "Items unreturned. Log moved back to Approved.";

            if (IsAjax)
                return Json(new
                {
                    ok = true,
                    message = msgOk,
                    status = statusProp.CurrentValue,
                    returnedUtc = (string?)null
                });

            TempData["ok"] = msgOk;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToPending(int id)
        {
            var e = await _context.LibraryLogs.Include(l => l.Items).FirstOrDefaultAsync(x => x.LogId == id);
            if (e == null) return NotFound();

            _context.Entry(e).Property("Status").CurrentValue = STATUS_PENDING;
            _context.Entry(e).Property<DateTime?>("ApprovedUtc").CurrentValue = null;
            _context.Entry(e).Property<DateTime?>("ReturnedUtc").CurrentValue = null;
            foreach (var item in e.Items)
                _context.Entry(item).Property<DateTime?>("ReturnedDate").CurrentValue = null;

            await _context.SaveChangesAsync();

            await UpsertLogHistoryAsync(
                e,
                actionType: "ToPending",
                notes: "Log moved back to Pending.",
                quantityOverride: e.Items?.Count,
                returnDate: null
            );

            if (IsAjax)
                return Json(new
                {
                    ok = true,
                    message = "Moved to Pending.",
                    status = STATUS_PENDING,
                    approvedUtc = (string?)null,
                    returnedUtc = (string?)null
                });

            TempData["ok"] = "Moved to Pending.";
            return RedirectToAction(nameof(Index));
        }

        // ------------------------- Export -------------------------
        [HttpGet]
        public async Task<IActionResult> Export(string status = "All", string? q = null, DateTime? from = null, DateTime? to = null, string format = "csv")
        {
            var data = await ApplyFilters(status, q, from, to)
                .OrderByDescending(l => l.VisitDate).ThenByDescending(l => l.LogId)
                .ToListAsync();

            if (string.Equals(format, "print", StringComparison.OrdinalIgnoreCase))
                return View("ExportPrint", data);

            // UPDATED: use "xls" as format key
            if (string.Equals(format, "xls", StringComparison.OrdinalIgnoreCase))
            {
                var html = BuildExcelHtml(data);
                var bytes = AddUtf8Bom(html);
                return File(bytes, "application/vnd.ms-excel; charset=utf-8", "LibraryLogs.xls");
            }

            var csv = BuildCsv(data);
            var csvBytes = AddUtf8Bom(csv);
            return File(csvBytes, "text/csv; charset=utf-8", "LibraryLogs.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportOne(int id, string format = "csv")
        {
            var l = await _context.LibraryLogs
                .AsNoTracking()
                .Include(x => x.Items).ThenInclude(i => i.Book).ThenInclude(b => b.Catalog)
                .FirstOrDefaultAsync(x => x.LogId == id);
            if (l == null) return NotFound();

            if (string.Equals(format, "print", StringComparison.OrdinalIgnoreCase))
                return View("ExportOnePrint", l);

            // UPDATED: use "xls" as format key
            if (string.Equals(format, "xls", StringComparison.OrdinalIgnoreCase))
            {
                var html = BuildExcelHtml(new List<LibraryLog> { l });
                var bytes = AddUtf8Bom(html);
                return File(bytes, "application/vnd.ms-excel; charset=utf-8", $"LibraryLog_{l.LogId}.xls");
            }

            var csv = BuildCsv(new List<LibraryLog> { l });
            var csvBytes = AddUtf8Bom(csv);
            return File(csvBytes, "text/csv; charset=utf-8", $"LibraryLog_{l.LogId}.csv");
        }

        // ------------------------- DELETE (ONLY Pending) -------------------------
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var e = await _context.LibraryLogs
                .AsNoTracking()
                .Include(l => l.Items).ThenInclude(i => i.Book).ThenInclude(b => b.Catalog)
                .FirstOrDefaultAsync(x => x.LogId == id);
            if (e == null) return NotFound();

            var status = GetStatus(e);
            if (!string.Equals(status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only Pending logs can be deleted.";
                return RedirectToAction(nameof(Details), new { id = e.LogId });
            }

            return View(e);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var e = await _context.LibraryLogs
                .Include(l => l.Items)
                .FirstOrDefaultAsync(x => x.LogId == id);

            if (e == null)
            {
                if (IsAjax) return Json(new { ok = false, message = "Record not found." });
                return RedirectToAction(nameof(Index));
            }

            var status = GetStatus(e);
            if (!string.Equals(status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase))
            {
                var msg = "Cannot delete. Only Pending logs can be deleted.";
                if (IsAjax) return Json(new { ok = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(Details), new { id = e.LogId });
            }

            _context.LibraryLogs.Remove(e);

            // delete related History row(s) for this log
            var histories = await _context.Histories
                .Where(h => h.EntityType == "LibraryLog" && h.LogId == id)
                .ToListAsync();
            if (histories.Any())
            {
                _context.Histories.RemoveRange(histories);
            }

            await _context.SaveChangesAsync();

            if (IsAjax) return Json(new { ok = true, message = "Library log deleted.", id });

            TempData["ok"] = "Library log deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ------------------------- CSV/HTML helpers -------------------------
        private string BuildCsv(IEnumerable<LibraryLog> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LogId,Status,Student,Phone,Gender,VisitDate,Books,CreatedUtc,ApprovedUtc,ReturnedUtc");
            foreach (var l in rows)
            {
                var titles = string.Join(" | ",
                    (l.Items ?? new List<LibraryLogItem>()).Select(SafeTitle).Where(s => !string.IsNullOrWhiteSpace(s)));

                var statusVal = GetStatus(l);
                DateTime? appr = GetApprovedUtc(l);
                DateTime? ret = GetReturnedUtc(l);

                sb.Append(string.Join(",", new[]
                {
                    l.LogId.ToString(),
                    CsvEscape(statusVal),
                    CsvEscape(l.StudentName),
                    CsvText(l.PhoneNumber),
                    CsvEscape(l.Gender),
                    CsvDateText(l.VisitDate, "yyyy-MM-dd"),
                    CsvEscape(titles),
                    CsvDateText(l.CreatedUtc, "yyyy-MM-dd HH:mm"),
                    CsvDateText(appr,        "yyyy-MM-dd HH:mm"),
                    CsvDateText(ret,         "yyyy-MM-dd HH:mm")
                }));
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        private string BuildExcelHtml(IEnumerable<LibraryLog> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta charset=\"UTF-8\"></head><body>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr><th>LogId</th><th>Status</th><th>Student</th><th>Phone</th><th>Gender</th><th>VisitDate</th><th>Books</th><th>CreatedUtc</th><th>ApprovedUtc</th><th>ReturnedUtc</th></tr>");

            foreach (var l in rows)
            {
                var statusVal = GetStatus(l);
                DateTime? appr = GetApprovedUtc(l);
                DateTime? ret = GetReturnedUtc(l);

                var books = string.Join(" | ",
                    (l.Items ?? new List<LibraryLogItem>()).Select(SafeTitle).Where(s => !string.IsNullOrWhiteSpace(s)));

                string tdText(string? v) => $"<td style='mso-number-format:\\@'>{HtmlEncode(v)}</td>";
                string tdDate(DateTime? d, string f) => d.HasValue
                    ? $"<td style='mso-number-format:\\@'>{HtmlEncode(d.Value.ToString(f))}</td>"
                    : "<td></td>";

                sb.Append("<tr>");
                sb.Append($"<td>{l.LogId}</td>");
                sb.Append(tdText(statusVal));
                sb.Append(tdText(l.StudentName));
                sb.Append(tdText(l.PhoneNumber));
                sb.Append(tdText(l.Gender));
                sb.Append(tdDate(l.VisitDate, "yyyy-MM-dd"));
                sb.Append(tdText(books));
                sb.Append(tdDate(l.CreatedUtc, "yyyy-MM-dd HH:mm"));
                sb.Append(tdDate(appr, "yyyy-MM-dd HH:mm"));
                sb.Append(tdDate(ret, "yyyy-MM-dd HH:mm"));
                sb.Append("</tr>");
            }

            sb.AppendLine("</table></body></html>");
            return sb.ToString();
        }

        private static byte[] AddUtf8Bom(string s)
        {
            var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return utf8Bom.GetBytes(s);
        }

        // GET /LibraryLogs/GetAvailableBooksJson
        [HttpGet]
        public async Task<IActionResult> GetAvailableBooksJson()
        {
            var unavailable = await GetUnavailableBookIdsAsync();

            var books = await _context.Books
                .AsNoTracking()
                .Include(b => b.Catalog)
                .Where(b => !unavailable.Contains(b.BookId))
                .OrderBy(b => b.Catalog.Title)
                .ThenBy(b => b.Barcode)
                .Select(b => new
                {
                    bookId    = b.BookId,
                    title     = b.Catalog != null ? b.Catalog.Title : b.Title,
                    barcode   = b.Barcode,
                    category  = b.Catalog != null ? b.Catalog.Category : null,
                    imagePath = b.Catalog != null ? b.Catalog.ImagePath : null,
                })
                .ToListAsync();

            return Json(books);
        }

        // POST /LibraryLogs/CreateJson
        [HttpPost]
        public async Task<IActionResult> CreateJson([FromBody] LibraryLogJsonRequest model)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            if (string.IsNullOrWhiteSpace(model.StudentName))
                return Ok(new { success = false, message = "Name is required." });

            if (string.IsNullOrWhiteSpace(model.PhoneNumber))
                return Ok(new { success = false, message = "Phone is required." });

            if (string.IsNullOrWhiteSpace(model.Gender))
                return Ok(new { success = false, message = "Gender is required." });

            if (string.IsNullOrWhiteSpace(model.Purpose))
                return Ok(new { success = false, message = "Purpose is required." });

            if (model.BookIds == null || model.BookIds.Count == 0)
                return Ok(new { success = false, message = "Please select at least one book." });

            var requested = model.BookIds.Distinct().ToList();
            var unavailable = await GetUnavailableBookIdsAsync();
            var blocked = requested.Where(id => unavailable.Contains(id)).ToList();
            if (blocked.Any())
                return Ok(new { success = false, message = "One or more selected books are currently unavailable." });

            var log = new LibraryLog
            {
                StudentName = model.StudentName.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                Gender      = model.Gender?.Trim(),
                VisitDate   = DateTime.Today,
                Purpose     = model.Purpose?.Trim(),
                Notes       = model.Notes?.Trim(),
                CreatedUtc  = DateTime.UtcNow,
                Items       = new List<LibraryLogItem>()
            };

            _context.Entry(log).Property("Status").CurrentValue      = STATUS_PENDING;
            _context.Entry(log).Property("ApprovedUtc").CurrentValue = null;
            _context.Entry(log).Property("ReturnedUtc").CurrentValue = null;

            foreach (var bid in requested)
                log.Items.Add(new LibraryLogItem { BookId = bid });

            _context.LibraryLogs.Add(log);
            await _context.SaveChangesAsync();

            await UpsertLogHistoryAsync(
                log,
                actionType: "BorrowInLibrary",
                notes: "Library log created via member portal (Pending).",
                quantityOverride: log.Items?.Count
            );

            return Ok(new
            {
                success = true,
                message = "Your request has been submitted. Please wait for staff approval.",
                logId   = log.LogId,
            });
        }

        public class LibraryLogJsonRequest
        {
            public string StudentName { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public string? Gender { get; set; }
            public string? Purpose { get; set; }
            public string? Notes { get; set; }
            public List<int> BookIds { get; set; } = new();
        }
    }
}
