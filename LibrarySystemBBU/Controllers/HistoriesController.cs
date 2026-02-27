using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;

namespace LibrarySystemBBU.Controllers
{
    public class HistoriesController : Controller
    {
        private readonly DataContext _context;

        // Local timezone for Cambodia/Thailand (UTC+7, no DST)
        private static readonly TimeZoneInfo LocalTimeZone = GetLocalTimeZone();

        public HistoriesController(DataContext context)
        {
            _context = context;
        }

        // --------- Time zone helpers (Bangkok / Cambodia) ---------
        private static TimeZoneInfo GetLocalTimeZone()
        {
            // Windows: "SE Asia Standard Time"
            // Linux: "Asia/Bangkok"
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
                }
                catch
                {
                    // Fallback: fixed UTC+7
                    return TimeZoneInfo.CreateCustomTimeZone(
                        "UTC+7",
                        TimeSpan.FromHours(7),
                        "UTC+7 Local Time",
                        "UTC+7 Local Time"
                    );
                }
            }
        }

        /// <summary>
        /// Convert UTC datetime (from DB) to local (Bangkok/Cambodia) time.
        /// </summary>
        private static DateTime ToLocalTime(DateTime utc)
        {
            if (utc.Kind == DateTimeKind.Unspecified)
            {
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            }

            return TimeZoneInfo.ConvertTimeFromUtc(utc, LocalTimeZone);
        }

        /// <summary>
        /// Convert local (Bangkok/Cambodia) date/time to UTC for querying OccurredUtc.
        /// </summary>
        private static DateTime LocalToUtc(DateTime local)
        {
            if (local.Kind == DateTimeKind.Unspecified)
            {
                local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            }

            return TimeZoneInfo.ConvertTime(local, LocalTimeZone, TimeZoneInfo.Utc);
        }

        // --------------- shared filter logic ----------------
        private IQueryable<History> ApplyFilters(
            string? entityType,
            string? actionType,
            string? location,
            string? q,
            string? from,
            string? to)
        {
            var query = _context.Histories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityType) &&
                !string.Equals(entityType, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(h => h.EntityType == entityType);
            }

            if (!string.IsNullOrWhiteSpace(actionType) &&
                !string.Equals(actionType, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(h => h.ActionType == actionType);
            }

            if (!string.IsNullOrWhiteSpace(location) &&
                !string.Equals(location, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(h => h.LocationType == location);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(h =>
                    (h.MemberName != null && h.MemberName.ToLower().Contains(term)) ||
                    (h.BookTitle != null && h.BookTitle.ToLower().Contains(term)) ||
                    (h.CatalogTitle != null && h.CatalogTitle.ToLower().Contains(term)) ||
                    (h.Notes != null && h.Notes.ToLower().Contains(term)));
            }

            // Treat 'from' and 'to' as LOCAL (Bangkok/Cambodia) dates, then convert to UTC
            if (DateTime.TryParse(from, out var fromLocal))
            {
                var fromLocalDate = fromLocal.Date; // start of local day
                var fromUtc = LocalToUtc(fromLocalDate);
                query = query.Where(h => h.OccurredUtc >= fromUtc);
            }

            if (DateTime.TryParse(to, out var toLocal))
            {
                var endLocalExclusive = toLocal.Date.AddDays(1); // next day local midnight, exclusive
                var endUtc = LocalToUtc(endLocalExclusive);
                query = query.Where(h => h.OccurredUtc < endUtc);
            }

            return query;
        }

        // GET: Histories
        public async Task<IActionResult> Index(
            string? entityType,
            string? actionType,
            string? location,
            string? q,
            string? from,
            string? to)
        {
            var query = ApplyFilters(entityType, actionType, location, q, from, to)
                .OrderByDescending(h => h.OccurredUtc);

            var items = await query.ToListAsync();

            ViewData["entityType"] = entityType ?? "All";
            ViewData["actionType"] = actionType ?? "All";
            ViewData["location"] = location ?? "All";
            ViewData["q"] = q ?? "";
            ViewData["from"] = from ?? "";
            ViewData["to"] = to ?? "";

            ViewData["totalCount"] = items.Count;
            ViewData["homeCount"] = items.Count(h => h.LocationType == "Home");
            ViewData["libraryCount"] = items.Count(h => h.LocationType == "Library");
            ViewData["loanCount"] = items.Count(h => h.EntityType == "Loan");
            ViewData["logCount"] = items.Count(h => h.EntityType == "LibraryLog");
            ViewData["returnCount"] = items.Count(h => h.EntityType == "Return");

            return View(items);
        }

        // GET: Histories/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null) return NotFound();

            var history = await _context.Histories
                .FirstOrDefaultAsync(m => m.Id == id);
            if (history == null) return NotFound();

            return View(history);
        }

        // --------------- EXPORT (CSV / XLS) ----------------
        [HttpGet]
        public async Task<IActionResult> Export(
            string? entityType,
            string? actionType,
            string? location,
            string? q,
            string? from,
            string? to,
            string format = "csv")
        {
            var data = await ApplyFilters(entityType, actionType, location, q, from, to)
                .OrderByDescending(h => h.OccurredUtc)
                .ToListAsync();

            if (string.Equals(format, "xls", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
            {
                var html = BuildExcelHtml(data);
                var bytes = AddUtf8Bom(html);
                return File(bytes, "application/vnd.ms-excel; charset=utf-8", "History.xls");
            }

            // default: CSV
            var csv = BuildCsv(data);
            var csvBytes = AddUtf8Bom(csv);
            return File(csvBytes, "text/csv; charset=utf-8", "History.csv");
        }

        // --------------- CSV / HTML helpers ----------------
        private static string CsvEscape(string? s) =>
            "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private static string CsvText(string? s) =>
            $"=\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";

        private static string CsvDateText(DateTime? dt, string fmt) =>
            dt.HasValue ? CsvText(dt.Value.ToString(fmt)) : "\"\"";

        // Local (Bangkok/Cambodia) datetime for OccurredUtc
        private static string CsvLocalDateTime(DateTime? utc, string fmt) =>
            utc.HasValue ? CsvText(ToLocalTime(utc.Value).ToString(fmt)) : "\"\"";

        private static string HtmlEncode(string? s) =>
            System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

        private string BuildCsv(IEnumerable<History> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("OccurredLocal,EntityType,ActionType,LocationType,MemberName,BookTitle,CatalogTitle,Quantity,LoanDate,ReturnDate,BorrowingFee,FineAmount,AmountPaid,DepositAmount,Notes");

            foreach (var h in rows)
            {
                sb.Append(string.Join(",", new[]
                {
                    // Local time (Bangkok/Cambodia) for OccurredUtc
                    CsvLocalDateTime(h.OccurredUtc, "yyyy-MM-dd HH:mm"),
                    CsvEscape(h.EntityType),
                    CsvEscape(h.ActionType),
                    CsvEscape(h.LocationType),
                    CsvEscape(h.MemberName),
                    CsvEscape(h.BookTitle),
                    CsvEscape(h.CatalogTitle),
                    CsvEscape(h.Quantity?.ToString() ?? ""),
                    CsvDateText(h.LoanDate, "yyyy-MM-dd"),
                    CsvDateText(h.ReturnDate, "yyyy-MM-dd"),
                    CsvEscape(h.BorrowingFee?.ToString("0.00") ?? ""),
                    CsvEscape(h.FineAmount?.ToString("0.00") ?? ""),
                    CsvEscape(h.AmountPaid?.ToString("0.00") ?? ""),
                    CsvEscape(h.DepositAmount?.ToString("0.00") ?? ""),
                    CsvEscape(h.Notes)
                }));
                sb.Append("\r\n");
            }

            return sb.ToString();
        }

        private string BuildExcelHtml(IEnumerable<History> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta charset=\"UTF-8\"></head><body>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr>" +
                          "<th>OccurredLocal</th>" +
                          "<th>EntityType</th>" +
                          "<th>ActionType</th>" +
                          "<th>LocationType</th>" +
                          "<th>MemberName</th>" +
                          "<th>BookTitle</th>" +
                          "<th>CatalogTitle</th>" +
                          "<th>Quantity</th>" +
                          "<th>LoanDate</th>" +
                          "<th>ReturnDate</th>" +
                          "<th>BorrowingFee</th>" +
                          "<th>FineAmount</th>" +
                          "<th>AmountPaid</th>" +
                          "<th>DepositAmount</th>" +
                          "<th>Notes</th>" +
                          "</tr>");

            string tdText(string? v) =>
                $"<td style='mso-number-format:\\@'>{HtmlEncode(v)}</td>";

            string tdDate(DateTime? d, string f) =>
                d.HasValue
                    ? $"<td style='mso-number-format:\\@'>{HtmlEncode(d.Value.ToString(f))}</td>"
                    : "<td></td>";

            string tdLocalDateTime(DateTime? utc, string f) =>
                utc.HasValue
                    ? $"<td style='mso-number-format:\\@'>{HtmlEncode(ToLocalTime(utc.Value).ToString(f))}</td>"
                    : "<td></td>";

            foreach (var h in rows)
            {
                sb.Append("<tr>");
                // Local (Bangkok/Cambodia) time for OccurredUtc
                sb.Append(tdLocalDateTime(h.OccurredUtc, "yyyy-MM-dd HH:mm"));
                sb.Append(tdText(h.EntityType));
                sb.Append(tdText(h.ActionType));
                sb.Append(tdText(h.LocationType));
                sb.Append(tdText(h.MemberName));
                sb.Append(tdText(h.BookTitle));
                sb.Append(tdText(h.CatalogTitle));
                sb.Append(tdText(h.Quantity?.ToString() ?? ""));
                sb.Append(tdDate(h.LoanDate, "yyyy-MM-dd"));
                sb.Append(tdDate(h.ReturnDate, "yyyy-MM-dd"));
                sb.Append(tdText(h.BorrowingFee?.ToString("0.00") ?? ""));
                sb.Append(tdText(h.FineAmount?.ToString("0.00") ?? ""));
                sb.Append(tdText(h.AmountPaid?.ToString("0.00") ?? ""));
                sb.Append(tdText(h.DepositAmount?.ToString("0.00") ?? ""));
                sb.Append(tdText(h.Notes));
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
    }
}
